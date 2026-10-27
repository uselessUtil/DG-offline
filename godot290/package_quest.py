"""Package the quest milestone separately from the immutable port1 release."""
import hashlib,json,re,shutil,struct,subprocess,zipfile
from prepare import ROOT,PROJECT,WORK,SOURCE,EXPECTED

OUT=ROOT/'artifacts/godot290-port2-quests-r2'
def sha(path):
    with path.open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def copy_new(source,target):
    if target.exists() and sha(source)!=sha(target):raise FileExistsError('Preserve existing release: '+str(target))
    shutil.copy2(source,target)
def check_apk(path,arch,version_code=290002,version_name="0.2.0-quest-dev"):
    libs=[]
    with zipfile.ZipFile(path) as z:
        assert z.testzip() is None
        assert 'assets/Reference/battle/maps.json' in z.namelist()
        for name in z.namelist():
            if not name.startswith('lib/') or not name.endswith('.so'):continue
            data=z.read(name)
            assert data[:4]==b'\x7fELF' and data[4]==2
            assert struct.unpack_from('<H',data,18)[0]==(183 if arch=='arm64' else 62)
            offset=struct.unpack_from('<Q',data,32)[0];size,count=struct.unpack_from('<HH',data,54)
            aligns=[struct.unpack_from('<Q',data,offset+i*size+48)[0] for i in range(count) if struct.unpack_from('<I',data,offset+i*size)[0]==1]
            assert aligns
            if arch=='arm64':assert min(aligns)>=16384,(name,aligns)
            libs.append({'name':name,'sha256':hashlib.sha256(data).hexdigest(),'minimum_load_alignment':min(aligns)})
    assert len(libs)==11 and not any('libunity.so' in x['name'] for x in libs)
    for command in [['apksigner','verify'],['zipalign','-c','-P','16','4']]:
        subprocess.run(['/opt/dg-android-sdk/build-tools/35.0.1/'+command[0],*command[1:],str(path)],check=True)
    badging=subprocess.run(['aapt','dump','badging',str(path)],check=True,capture_output=True,text=True).stdout
    assert "name='org.divinegate.godot290'" in badging and f"versionCode='{version_code}'" in badging
    return {'file':path.name,'bytes':path.stat().st_size,'sha256':sha(path),'architecture':arch,'version':version_name,'version_code':version_code,
            'min_sdk':int(re.search(r"sdkVersion:'(\d+)'",badging)[1]),'target_sdk':int(re.search(r"targetSdkVersion:'(\d+)'",badging)[1]),'native_libraries':libs}
def main():
    assert sha(SOURCE)==EXPECTED
    sources={}
    for file in (ROOT/'godot290/project').rglob('*'):
        if not file.is_file():continue
        relative=file.relative_to(ROOT/'godot290/project')
        assert sha(PROJECT/relative)==sha(file),'Stale build source: '+str(file)
        sources[str(relative)]=sha(file)
    tests=(WORK/'quest-tests.log').read_text()
    assert 'PASS quest integration completed' in tests and 'Unhandled exception' not in tests
    capture=(WORK/'capture.log').read_text()
    assert 'ERROR:' not in capture
    OUT.mkdir(exist_ok=True)
    apks=[]
    for arch in ['arm64','x86_64']:
        source=WORK/f'DivineGate-Godot-{arch}-dev.apk'
        info=check_apk(source,arch);target=OUT/source.name;copy_new(source,target);apks.append(info)
    archive=OUT/'DivineGate-Godot-project.zip'
    if archive.exists():raise FileExistsError('Project package already exists')
    with zipfile.ZipFile(archive,'w',zipfile.ZIP_DEFLATED,compresslevel=6) as z:
        for f in sorted(PROJECT.rglob('*')):
            if not f.is_file() or '.godot' in f.relative_to(PROJECT).parts or f.suffix=='.import':continue
            z.write(f,'DivineGateGodot/'+f.relative_to(PROJECT).as_posix())
        for name in ['godot290/README.md','LICENSE','THIRD_PARTY_NOTICES.md']:
            z.write(ROOT/name,'DivineGateGodot/'+('PORT_STATUS.md' if name.endswith('README.md') else name))
    for name in ['quest-tests.log','capture.log','build.log','android-export-arm64.log','android-export-x86_64.log','preview.png']:
        if (WORK/name).exists():copy_new(WORK/name,OUT/name)
    for name in ['godot-quest-clear-final.png','godot-normal-clear.png','godot-normal-map.png','godot-battle-resume.png']:
        copy_new(ROOT/'artifacts'/name,OUT/name)
    device_log=ROOT/'artifacts/godot-r2-device.log'
    assert 'ERROR:' not in device_log.read_text(encoding='utf-8-sig')
    copy_new(device_log,OUT/device_log.name)
    copy_new(ROOT/'godot290/README.md',OUT/'README.md')
    report={'status':'playable quest-loop development milestone; not a complete 1:1 remake',
            'reference':{'path':str(SOURCE.relative_to(ROOT)),'sha256':EXPECTED},'package':'org.divinegate.godot290','apks':apks,
            'project':{'file':archive.name,'bytes':archive.stat().st_size,'sha256':sha(archive)},'source_hashes':sources,
            'checks':{'zip_crc':True,'apk_signature':True,'arm64_elf_16k':True,'apk_16k_alignment':True,'quest_model_integration':True,'godot_render':True},
            'device':{'name':'MuMu 12','android':'12','resolution':'1080x1920','native_abi':'x86_64','connection':'host ADB',
                      'tested_quests':[1,83],'battle_input':'ADB touch drags and taps; no injected HP, damage, drops or wins',
                      'tested_flows':['key and boss door','panel countdown and attack','quest clear and rewards','force-stop and battle restore','result restore after update'],
                      'final_r2_retest':'Restored completed quest and returned through quest, party and area menus; no ERROR in this process log. Quest rules unchanged from the full-clear candidate.',
                      'arm64_note':'MuMu ARM64 translation crashed in /system/lib64/arm64/libimg_utils.so before managed startup; use x86_64 APK on MuMu. Native ARM64 phone / Android 16 runtime not tested.'},
            'limitations':['temporary quest layout and portrait pawn','active/LB skill button and some status/boost/passive effects not yet ported','not all quests or multi-floor paths verified','original animation/audio not yet restored']}
    (OUT/'manifest.json').write_text(json.dumps(report,ensure_ascii=False,indent=2))
    print(json.dumps([{k:v for k,v in a.items() if k!='native_libraries'} for a in apks],indent=2));print(OUT)
if __name__=='__main__':main()
