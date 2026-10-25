"""Additional signed-profile invariants and real r10 -> expanded save tests."""
import argparse,collections,hashlib,io,json,os,re,shutil,subprocess,tempfile,zipfile
from pathlib import Path
from PIL import Image
ROOT=Path('/workspace');CODE=ROOT/'offline/v291/restored';BASE=ROOT/'offline/v291/playable'
CECIL='/usr/lib/mono/gac/Mono.Cecil/0.11.1.0__0738eb9f132ed756/Mono.Cecil.dll'
def run(*a,**kw):
    p=subprocess.run(list(map(str,a)),stdout=subprocess.PIPE,stderr=subprocess.STDOUT,text=True,timeout=180,**kw)
    if p.returncode:raise RuntimeError(p.stdout)
    return p.stdout
def main():
    p=argparse.ArgumentParser();p.add_argument('report',type=Path);a=p.parse_args();out=a.report.parent
    r=json.loads(a.report.read_text());stable_dir=ROOT/'artifacts/2.9.1-offline-dgdb1-r10-drop-level'
    old=json.loads((stable_dir/'build-report.json').read_text())
    for b in [r,old]:assert hashlib.sha256(Path(b['output']).read_bytes()).hexdigest()==b['sha256']
    tested=json.loads((out/'test-report.json').read_text());w=Path(tested['work'])
    temp=Path(tempfile.mkdtemp(prefix='dg291-expanded-extra-',dir='/tmp'))
    class Log(list):
        def append(self,value):super().append(value);print(value,flush=True)
    logs=Log()
    # All native routes and converted model/animation packages stay exactly r10.
    n=json.loads((out/'native-bundle-manifest.json').read_text());b=json.loads((stable_dir/'native-bundle-manifest.json').read_text())
    assert n['routes']==b['routes'] and n['path_ids']==b['path_ids']
    assert n['bundles']==b['bundles'] and n['characters']==b['characters']==595
    with zipfile.ZipFile(r['output']) as z,zipfile.ZipFile(old['output']) as oldz:
        for name in oldz.namelist():
            if name.startswith('assets/dg-native/'):assert z.read(name)==oldz.read(name)
    logs.append('PASS all original595 character routes and converted native model/animation packages are byte-identical to r10.')
    # Validate all fallback texture pixels against their recovered source, plus
    # original portraits and repacked icon pixels against stable payloads.
    current=json.loads((out/'images.json').read_text());before=json.loads((stable_dir/'images.json').read_text())
    newbin=(Path(r['work_directory'])/'build/RecoveredImages.bin').open('rb')
    oldbin=(Path(old['work_directory'])/'build/RecoveredImages.bin').open('rb')
    def raw(f,meta,key):
        v=meta['payloads'][key];f.seek(v['offset']);data=f.read(v['size'])
        assert hashlib.sha256(data).hexdigest()==v['sha256'];return data
    same=0
    for key,v in before['payloads'].items():
        if key.startswith(('portrait/','shadow/')):
            assert raw(newbin,current,key)==raw(oldbin,before,key);same+=1
    sheets={};oldicons=0
    for key,rect in before['sprites'].items():
        atlas,sprite=key.split('/',1);newrect=current['sprites'][key]
        for f,meta,label in [(oldbin,before,'old'),(newbin,current,'new')]:
            k=(label,atlas)
            if k not in sheets:sheets[k]=Image.open(io.BytesIO(raw(f,meta,'atlas/'+atlas))).convert('RGBA')
        def crop(im,r):x,y,w,h=r;return im.crop((x,y,x+w,y+h))
        assert crop(sheets['old',atlas],rect).tobytes()==crop(sheets['new',atlas],newrect).tobytes(),key
        oldicons+=1
    tables=Path(r['work_directory'])/'tables'
    units=json.loads((tables/'MasterDataParamChara.json').read_text())
    base_ids=set(json.loads((out/'restoration.json').read_text())['original_characters'])
    added=[c for c in units if c['fix_id'] and c['fix_id'] not in base_ids]
    assert len(added)==1153
    for c in added:
        f=ROOT/'external/dgdata/large'/(str(c['draw_id']).zfill(3)+'.png')
        if not f.exists():f=ROOT/'external/dgdb/public/img/large'/f.name
        im=Image.open(f).convert('RGBA')
        target=Image.open(io.BytesIO(raw(newbin,current,'portrait/'+str(int(c['res_chara_id_str']))))).convert('RGBA')
        assert im.size==target.size and im.tobytes()==target.tobytes(),c['fix_id']
    # Every map's normal/boss crop is exact source pixels and separate from the
    # fixed frame. map0012 has distinct normal/boss panels.
    sources=[s for s in current['restored_sources'] if 'resource' in s]
    split=flat=0
    for source in sources:
        name=source['resource'].removesuffix('_tex');prefix='restored-map/'+name+'/'
        image=Image.open(io.BytesIO(raw(newbin,current,prefix+'atlas'))).convert('RGBA')
        assert image.size==(1024,1024)
        # Source format is separately verified, including a known flattened
        # and a known separable atlas; never identify padding by alpha alone.
        separated=sum(a>0 and max(r,g,b)>8 for r,g,b,a in image.crop((580,320,1024,544)).getdata())>1000
        if name=='map_0001':assert not separated
        if name=='map_0012':assert separated
        if source['origin']!='original-native-cache':
            original=Image.open(ROOT/source['origin']).convert('RGBA').transpose(Image.Transpose.FLIP_TOP_BOTTOM)
            assert original.tobytes()==image.tobytes(),('Atlas orientation',name)
        for part,box in ([('normal',(592,320,776,544)),('boss',(812,320,1000,544))] if separated
                         else [('normal',(420,104,604,316)),('boss',(420,104,604,316))]):
            expected=image.crop(box)
            expected.putdata([(r,g,b,0 if r==g==b==0 else a) for r,g,b,a in expected.getdata()])
            actual=Image.open(io.BytesIO(raw(newbin,current,prefix+part))).convert('RGBA')
            assert actual.tobytes()==expected.tobytes()
        if separated:split+=1
        else:flat+=1
    assert current['payloads']['restored-map/map_0012/normal']['sha256']!=current['payloads']['restored-map/map_0012/boss']['sha256']
    newbin.close();oldbin.close()
    logs.append(f'PASS {same} original portrait/shadow payloads, {oldicons} original sprite pixels preserved; all1153 added portraits match exact DGDB draw_id source. Maps: {split} separate-panel atlases, {flat} flattened atlases split only in fallback.')
    # Native game IL remains unchanged from r10; no new skill algorithm override.
    oldgame=temp/'r10-game.dll'
    with zipfile.ZipFile(old['output']) as z:oldgame.write_bytes(z.read('assets/bin/Data/Managed/Assembly-CSharp.dll'))
    logs.append(run('mono',w/'tools/VerifyChanges.exe',oldgame,w/'Assembly-CSharp.dll',temp/'method-changes.txt'))
    assert (temp/'method-changes.txt').read_text()==''
    # r10 exporter runs its own signed DLLs and own disposable prefs.
    src=temp/'r10';src.mkdir()
    with zipfile.ZipFile(old['output']) as z:
        for name in z.namelist():
            if name.startswith('assets/bin/Data/Managed/') and name.endswith('.dll'):(src/Path(name).name).write_bytes(z.read(name))
    run('mcs','-target:library','-out:'+str(src/'EngineSupport.dll'),'-r:'+str(src/'UnityEngine.dll'),ROOT/'offline/tests/EngineSupport.cs')
    run('mono',w/'tools/TestEngine.exe',src/'UnityEngine.dll',src/'EngineSupport.dll',src/'engine-test.dll')
    (src/'engine-test.dll').replace(src/'UnityEngine.dll')
    text=(BASE/'ExportLegacy106Fixture.cs').read_text().replace('Singleton<MasterDataManager>.Instance=new MasterDataManager();','Singleton<TimeManager>.Instance=new TimeManager();Singleton<MasterDataManager>.Instance=new MasterDataManager();')
    (src/'Export.cs').write_text(text)
    def refs(d,extra=[]):return ['-r:'+str(d/f) for f in ['Assembly-CSharp.dll','Assembly-CSharp-firstpass.dll','UnityEngine.dll','DivineGateOffline.dll']+extra]
    run('mcs','-out:'+str(src/'Export.exe'),*refs(src),src/'Export.cs')
    oldenv=dict(os.environ,DG_TEST_PREFS=str(src/'prefs'))
    logs.append(run('mono',src/'Export.exe',src/'backup',cwd=src,env=oldenv))
    incoming=src/'backup/legacy106.dgbackup.json'
    run('mcs','-out:'+str(w/'ExpandedSaveTests.exe'),*refs(w),CODE/'ExpandedSaveTests.cs')
    env=dict(os.environ,DG_TEST_PREFS=str(temp/'expanded.prefs'));saved=temp/'expanded'
    for mode in [[],['reload']]:logs.append(run('mono',w/'ExpandedSaveTests.exe',incoming,saved,*mode,cwd=w,env=env))
    reject=(BASE/'RejectNewBackup.cs').read_text().replace('actual 2.9.0 r17 rejects new 2.9.1 schema2','actual r10 rejects expanded2.9.1 schema3')
    (src/'Reject.cs').write_text(reject);run('mcs','-out:'+str(src/'Reject.exe'),*refs(src),src/'Reject.cs')
    logs.append(run('mono',src/'Reject.exe',saved/'expanded.dgbackup.json',cwd=src,env=dict(oldenv,DG_TEST_PREFS=str(src/'reject-prefs'))))
    # Instrument ONLY temporary engine curve I/O to observe the signed fallback
    # method's arguments. The real APK UnityEngine and adapter remain unchanged.
    probe=temp/'door';probe.mkdir()
    for f in w.glob('*.dll'):
        if f.name=='UnityEngine.dll':shutil.copy2(f,probe/f.name)
        else:(probe/f.name).symlink_to(f)
    run('mcs','-target:library','-out:'+str(probe/'DoorCurveProbe.dll'),'-r:'+str(probe/'UnityEngine.dll'),CODE/'DoorCurveProbe.cs')
    run('mcs','-r:'+CECIL,'-out:'+str(temp/'PrepareDoorProbe.exe'),CODE/'PrepareDoorProbe.cs')
    run('mono',temp/'PrepareDoorProbe.exe',probe/'UnityEngine.dll',probe/'DoorCurveProbe.dll',probe/'engine-new.dll')
    (probe/'engine-new.dll').replace(probe/'UnityEngine.dll')
    run('mcs','-out:'+str(probe/'PresentationTests.exe'),*refs(probe),'-r:'+str(probe/'DoorCurveProbe.dll'),CODE/'PresentationTests.cs')
    logs.append(run('mono',probe/'PresentationTests.exe',cwd=probe))
    report={'apk_sha256':r['sha256'],'work':str(temp),'tests':logs,'android_tested':False}
    (out/'expanded-extra-tests.json').write_text(json.dumps(report,ensure_ascii=False,indent=2))
    (out/'expanded-extra-tests.txt').write_text('\n'.join(logs));print('\n'.join(logs))
if __name__=='__main__':main()
