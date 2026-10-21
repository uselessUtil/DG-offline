"""Build a version-specific offline APK using isolated project tooling."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess
import tempfile
import zipfile
import UnityPy
from manifest import patch as patch_manifest
from generate_images import create as create_images

SOURCE_HASH = 'ba3ccd42c28e4d9362cb6bda2b4d21483b98085441f9513ca1b825bc71be81c2'
CECIL = '/usr/lib/mono/gac/Mono.Cecil/0.11.1.0__0738eb9f132ed756/Mono.Cecil.dll'
MANAGED = 'assets/bin/Data/Managed/'
ROOT = Path('/workspace')


def run(*args, **kwargs):
    print('+', ' '.join(map(str, args)), flush=True)
    return subprocess.run(list(map(str, args)), check=True, **kwargs)


def digest(data):
    return hashlib.sha256(data).hexdigest()


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--output', default='/workspace/artifacts/1.0.6-offline-prototype2')
    parser.add_argument('--work', help='New temporary build directory outside the repository')
    parser.add_argument('--native-direct', action='store_true', help='Install the original engine binaries directly instead of obsolete proxy loaders')
    args = parser.parse_args()
    source = ROOT / 'ディバゲ_1.0.6.apk'
    if digest(source.read_bytes()) != SOURCE_HASH:
        raise ValueError('Source APK hash mismatch; refusing a patch for another build')
    output = Path(args.output).resolve()
    output.mkdir(parents=True, exist_ok=True)
    target = output / source.name
    if target.exists():
        raise FileExistsError('Use a new output directory; never overwrite a release')
    work = Path(args.work).resolve() if args.work else Path(tempfile.mkdtemp(prefix='dg106-', dir='/tmp'))
    if work.is_relative_to(ROOT):
        raise ValueError('Extraction must stay outside the repository')
    work.mkdir(parents=True, exist_ok=True)
    original, build, tables = work/'original', work/'build', work/'tables'
    build.mkdir(); tables.mkdir()
    with zipfile.ZipFile(source) as archive:
        if archive.testzip():
            raise ValueError('Source archive integrity check failed')
        for info in archive.infolist():
            if info.filename.startswith('assets/bin/Data/'):
                archive.extract(info, original)
        manifest = archive.read('AndroidManifest.xml')
    managed = original / MANAGED
    env = UnityPy.load(str(original/'assets/bin/Data'))
    counts = {}
    for obj in env.objects:
        if obj.type.name == 'TextAsset':
            value = obj.read()
            if value.m_Name.startswith('MasterData'):
                raw = value.m_Script.encode('utf8', 'surrogateescape') if isinstance(value.m_Script,str) else value.m_Script
                (tables/(value.m_Name+'.json')).write_bytes(raw)
                counts[value.m_Name] = len(json.loads(raw, strict=False))
    if counts.get('MasterDataParamChara') != 523 or counts.get('MasterDataQuest') != 371:
        raise ValueError('Unexpected embedded master data')
    create_images(build/'Images.cs', tables)
    refs = ['-r:'+str(managed/name) for name in ['mscorlib.dll','System.dll','System.Core.dll','UnityEngine.dll','Assembly-CSharp.dll']]
    run('mcs','-nostdlib','-target:library','-out:'+str(build/'DivineGateOffline.dll'),*refs,
        ROOT/'offline/Runtime.cs',build/'Images.cs')
    run('mcs','-out:'+str(build/'Patch.exe'),'-r:'+CECIL,ROOT/'offline/Patch.cs')
    run('mono',build/'Patch.exe',managed/'Assembly-CSharp.dll',build/'DivineGateOffline.dll',build/'Assembly-CSharp.dll')
    # Explicitly register the added adapter assembly with the old Unity player.
    main_env = UnityPy.load(str(original/'assets/bin/Data/mainData'))
    manager = next(obj for obj in main_env.objects if obj.type.name == 'MonoManager')
    saved_objects = {obj.path_id: obj.get_raw_data() for obj in manager.assets_file.objects.values()}
    tree = manager.read_typetree()
    if 'DivineGateOffline.dll' in tree['m_AssemblyNames']:
        raise ValueError('Adapter is already registered')
    tree['m_AssemblyNames'].append('DivineGateOffline.dll')
    manager.save_typetree(tree)
    main_data = manager.assets_file.save()
    verify_main = UnityPy.load(main_data)
    after = {obj.path_id: obj.get_raw_data() for obj in verify_main.objects}
    if set(after) != set(saved_objects):
        raise ValueError('Unity object list changed')
    for path_id, raw in saved_objects.items():
        if path_id != manager.path_id and after[path_id] != raw:
            raise ValueError('Unrelated Unity object changed')
    new_manifest, removed = patch_manifest(manifest)
    replacements = {'AndroidManifest.xml':new_manifest, 'assets/bin/Data/mainData':main_data,
                    MANAGED+'Assembly-CSharp.dll':(build/'Assembly-CSharp.dll').read_bytes(),
                    MANAGED+'DivineGateOffline.dll':(build/'DivineGateOffline.dll').read_bytes()}
    if args.native_direct:
        with zipfile.ZipFile(source) as archive:
            for name in ['libmono.so','libunity.so']:
                replacements['lib/armeabi-v7a/'+name] = archive.read('assets/libs/armeabi-v7a/'+name)
    unsigned = build/'unsigned.apk'
    with zipfile.ZipFile(source) as src, zipfile.ZipFile(unsigned,'w') as dst:
        for info in src.infolist():
            if info.filename.upper().startswith('META-INF/'):
                continue
            dst.writestr(info, replacements.pop(info.filename, src.read(info.filename)))
        for name, data in replacements.items():
            dst.writestr(name,data,compress_type=zipfile.ZIP_DEFLATED)
    run('zipalign','-f','4',unsigned,build/'aligned.apk')
    key = Path('/var/lib/divine-gate-offline/prototype.p12')
    key.parent.mkdir(parents=True,exist_ok=True)
    if not key.exists():
        run('keytool','-genkeypair','-keystore',key,'-storetype','PKCS12','-storepass','android',
            '-keypass','android','-alias','offline106','-keyalg','RSA','-keysize','2048',
            '-validity','10000','-dname','CN=Divine Gate Local Offline Prototype')
        key.chmod(0o600)
    run('apksigner','sign','--ks',key,'--ks-key-alias','offline106','--ks-pass','pass:android',
        '--key-pass','pass:android','--min-sdk-version','9','--v1-signing-enabled','true',
        '--v2-signing-enabled','true','--out',target,build/'aligned.apk')
    with zipfile.ZipFile(target) as modified, zipfile.ZipFile(source) as archived:
        assert modified.testzip() is None
        changed=[]
        for name in archived.namelist():
            if not name.upper().startswith('META-INF/') and archived.read(name) != modified.read(name):
                changed.append(name)
        expected = {'AndroidManifest.xml','assets/bin/Data/mainData',MANAGED+'Assembly-CSharp.dll'}
        if args.native_direct:
            expected.update('lib/armeabi-v7a/'+name for name in ['libmono.so','libunity.so'])
            for name in ['libmono.so','libunity.so']:
                assert modified.read('lib/armeabi-v7a/'+name) == archived.read('assets/libs/armeabi-v7a/'+name)
        assert set(changed) == expected
        assert modified.read(MANAGED+'UnityEngine.dll') == archived.read(MANAGED+'UnityEngine.dll')
        assert not any('EngineSupport' in name or 'LocalApiTests' in name for name in modified.namelist())
    run('apksigner','verify','--verbose','--print-certs',target,stdout=(output/'signature.txt').open('w'))
    run('zipalign','-c','4',target)
    badging = subprocess.check_output(['aapt','dump','badging',str(target)],text=True)
    (output/'manifest-summary.txt').write_text(badging)
    assert "package: name='jp.gungho.dg.offline106'" in badging
    assert "application-label:'Divine Gate Offline 1.0.6'" in badging
    for permission in ['android.permission.INTERNET','com.android.vending.BILLING','android.permission.ACCESS_NETWORK_STATE']:
        assert "uses-permission: name='"+permission+"'" not in badging
    assert digest(source.read_bytes()) == SOURCE_HASH
    report = {'status':'Android gameplay testing pending','source':str(source),'source_sha256':SOURCE_HASH,
              'output':str(target),'sha256':digest(target.read_bytes()),'size':target.stat().st_size,
              'version':'1.0.6 offline prototype 2',
              'package':'jp.gungho.dg.offline106','tables':counts,
              'modified_original_entries':changed,'removed_manifest_items':removed,'work_directory':str(work),
              'original_assets_and_native_libraries':('assets and engine bytes unchanged; install-directory proxy libraries replaced by bundled engine binaries' if args.native_direct else 'unchanged'),
              'native_direct_loader':args.native_direct,
              'missing_downloaded_assets':'numbered colored substitutes',
              'recovered_artwork':None}
    (output/'build-report.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n')
    (output/'SHA256SUMS').write_text(report['sha256']+'  '+target.name+'\n')
    print(json.dumps(report,ensure_ascii=False,indent=2))


if __name__ == '__main__':
    main()
