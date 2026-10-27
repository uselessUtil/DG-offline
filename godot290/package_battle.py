"""Package port3 only after the verified build and device evidence exist."""
import json,zipfile
from prepare import ROOT,PROJECT,WORK,SOURCE,EXPECTED
from package_quest import sha,copy_new,check_apk
OUT=ROOT/'artifacts/godot290-port3-battle'
def main():
    required=['device-verification.json','device-final.log']
    missing=[name for name in required if not (OUT/name).is_file()]
    if missing:raise SystemExit('Checkpoint only: finish device verification before packaging. Missing: '+', '.join(missing))
    assert sha(SOURCE)==EXPECTED
    sources={}
    for f in (ROOT/'godot290/project').rglob('*'):
        if not f.is_file():continue
        name=f.relative_to(ROOT/'godot290/project')
        assert sha(PROJECT/name)==sha(f),'Stale source: '+str(f)
        sources[str(name)]=sha(f)
    tests=(WORK/'quest-tests.log').read_text()
    for marker in ['PASS quest integration completed','248 activated / 0 context blocked','identical damage, panels, statuses and RNG state']:assert marker in tests
    assert 'Unhandled exception' not in tests
    capture=(WORK/'capture.log').read_text();assert 'ERROR:' not in capture and 'PASS Godot active cutin / damage / source particle presentation' in capture
    device=json.loads((OUT/'device-verification.json').read_text())
    log=(OUT/'device-final.log').read_text(encoding='utf-8-sig');assert 'ERROR:' not in log and 'FATAL EXCEPTION' not in log
    apks=[]
    for arch in ['arm64','x86_64']:
        file=WORK/f'DivineGate-Godot-{arch}-dev.apk';info=check_apk(file,arch,290003,'0.3.0-battle-dev')
        with zipfile.ZipFile(file) as z:
            for name in ['effects','motions']:assert f'assets/Reference/battle/{name}.json' in z.namelist()
        copy_new(file,OUT/file.name);apks.append(info)
    assert device['tested_apk_sha256']==next(a['sha256'] for a in apks if a['architecture']=='x86_64')
    archive=OUT/'DivineGate-Godot-project.zip'
    if archive.exists():raise FileExistsError('Preserve previous project package')
    with zipfile.ZipFile(archive,'w',zipfile.ZIP_DEFLATED,compresslevel=6) as z:
        for f in sorted(PROJECT.rglob('*')):
            if f.is_file() and '.godot' not in f.relative_to(PROJECT).parts and f.suffix!='.import':z.write(f,'DivineGateGodot/'+f.relative_to(PROJECT).as_posix())
        for name in ['godot290/README.md','LICENSE','THIRD_PARTY_NOTICES.md']:z.write(ROOT/name,'DivineGateGodot/'+('PORT_STATUS.md' if name.endswith('README.md') else name))
    for name in ['quest-tests.log','skill-coverage.json','capture.log','build.log','android-export-arm64.log','android-export-x86_64.log','battle-presentation-conversion.json']:
        copy_new(WORK/name,OUT/name)
    copy_new(ROOT/'godot290/README.md',OUT/'README.md')
    result={'status':'active skills and battle presentation development milestone; NOT complete 1:1',
      'reference':{'path':str(SOURCE.relative_to(ROOT)),'sha256':EXPECTED},'apks':apks,'source_hashes':sources,
      'project':{'file':archive.name,'bytes':archive.stat().st_size,'sha256':sha(archive)},
      'checks':{'zip_crc':True,'signature':True,'arm64_16k_elf':True,'apk_16k_alignment':True,'quest_tests':[1,83],'active_skill_fixtures':248,'deterministic_restore':True,'godot_battle_render':True},
      'device':device,'limitations':['temporary battle layout and portrait pawn','not all 90 curves are bound to original objects/timelines','2D particle projection does not reproduce all mesh, stretched, 3D and sub-emitter behavior','AS displacement and special field movement/pitfalls still incomplete','no source audio found','full cross-product of enemy/passive/status combinations not compared with running r17','native ARM64 and Android 16 runtime untested']}
    (OUT/'manifest.json').write_text(json.dumps(result,ensure_ascii=False,indent=2))
    print(json.dumps([{k:v for k,v in a.items() if k!='native_libraries'} for a in apks],indent=2))
if __name__=='__main__':main()
