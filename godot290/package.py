"""Validate and package a development milestone, never label it a full remake."""
import hashlib,json,pathlib,re,shutil,struct,subprocess,zipfile
from prepare import ROOT,PROJECT,WORK,SOURCE,EXPECTED
OUT=ROOT/'artifacts/godot290-port1'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def main():
    assert sha(SOURCE)==EXPECTED,'Reference source changed'
    for file in (ROOT/'godot290/project').rglob('*'):
        if file.is_file():assert (PROJECT/file.relative_to(ROOT/'godot290/project')).read_bytes()==file.read_bytes(),'Build source is stale: '+str(file)
    apk=WORK/'DivineGate-Godot-arm64-dev.apk'
    libs=[]
    with zipfile.ZipFile(apk) as z:
        assert z.testzip() is None
        assert 'assets/Reference/index.json' in z.namelist()
        for name in z.namelist():
            if not name.startswith('lib/') or not name.endswith('.so'):continue
            data=z.read(name)
            assert data[:4]==b'\x7fELF' and data[4]==2 and struct.unpack_from('<H',data,18)[0]==183,name
            offset=struct.unpack_from('<Q',data,32)[0];size,count=struct.unpack_from('<HH',data,54)
            aligns=[struct.unpack_from('<Q',data,offset+i*size+48)[0] for i in range(count) if struct.unpack_from('<I',data,offset+i*size)[0]==1]
            assert aligns and min(aligns)>=16384,(name,aligns)
            libs.append({'name':name,'sha256':hashlib.sha256(data).hexdigest(),'minimum_load_alignment':min(aligns)})
    assert len(libs)==11 and not any('libunity.so' in x['name'] for x in libs)
    subprocess.run(['/opt/dg-android-sdk/build-tools/35.0.1/apksigner','verify',str(apk)],check=True)
    subprocess.run(['/opt/dg-android-sdk/build-tools/35.0.1/zipalign','-c','-P','16','4',str(apk)],check=True)
    badging=subprocess.run(['aapt','dump','badging',str(apk)],check=True,capture_output=True,text=True).stdout
    assert "name='org.divinegate.godot290'" in badging
    capture=(WORK/'capture.log').read_text()
    assert 'ERROR:' not in capture and 'PASS real Godot mouse events' in capture
    assert 'PASS Godot starter, free chips, scratch and persisted account' in capture
    OUT.mkdir(exist_ok=True)
    target=OUT/'DivineGate-Godot-arm64-dev.apk'
    if target.exists() and sha(target)!=sha(apk):raise FileExistsError('Preserve the existing milestone; choose a new output folder')
    shutil.copy2(apk,target)
    archive=OUT/'DivineGate-Godot-project.zip'
    with zipfile.ZipFile(archive,'w',zipfile.ZIP_DEFLATED,compresslevel=6) as z:
        for f in sorted(PROJECT.rglob('*')):
            if not f.is_file() or '.godot' in f.relative_to(PROJECT).parts or f.suffix=='.import':continue
            z.write(f,'DivineGateGodot/'+f.relative_to(PROJECT).as_posix())
        z.write(ROOT/'godot290/README.md','DivineGateGodot/PORT_STATUS.md')
    for name in ['asset-conversion.json','capture.log','build.log','import.log','android-export.log','rules-fresh.log','rules-reload.log','preview.png']:
        if (WORK/name).exists():shutil.copy2(WORK/name,OUT/name)
    shutil.copy2(ROOT/'godot290/README.md',OUT/'README.md')
    manifest={'status':'development milestone; not a complete 1:1 remake; no playable battles',
              'reference':{'path':str(SOURCE.relative_to(ROOT)),'sha256':EXPECTED},
              'apk':{'file':target.name,'bytes':target.stat().st_size,'sha256':sha(target),'package':'org.divinegate.godot290','version':'0.1.0-port-dev','min_sdk':int(re.search(r"sdkVersion:'(\d+)'",badging)[1]),'target_sdk':int(re.search(r"targetSdkVersion:'(\d+)'",badging)[1])},
              'project':{'file':archive.name,'bytes':archive.stat().st_size,'sha256':sha(archive)},
              'native_libraries':libs,'checks':{'zip_crc':True,'apk_signature':True,'elf_arm64':True,'elf_16k_alignment':True,'apk_16k_alignment':True,'godot_core_smoke':True,'godot_button_input':True,'android_device_test':False},
              'device_test_note':'Host ADB devices was empty. Installation and runtime on Android 16 are not verified.'}
    (OUT/'manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2))
    print(json.dumps(manifest['apk'],ensure_ascii=False));print(OUT)
if __name__=='__main__':main()
