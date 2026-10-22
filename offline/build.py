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
from recovered_images import create as create_recovered_images, disabled as disable_recovered_images
from event_catalog import create as create_event_catalog
from native_bundles import create as create_native_bundles, disabled as disable_native_bundles

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
    parser.add_argument('--recovered-assets', type=Path, help='Verified DGDB investigation directory containing its image manifest and downloads')
    parser.add_argument('--backup-assets', type=Path, help='Native jp.gungho.dg UnityCache; overrides every matching recovered image')
    parser.add_argument('--restore-native', action='store_true', help='Restore original map/character prefabs, shaders and animations from the backup; missing/failed resources retain the simplified fallback')
    args = parser.parse_args()
    if args.backup_assets and not args.recovered_assets:
        parser.error('--backup-assets requires the existing --recovered-assets fallback dataset')
    if args.restore_native and not args.backup_assets:
        parser.error('--restore-native requires --backup-assets')
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
    event_catalog = create_event_catalog(tables, output)
    create_images(build/'Images.cs', tables)
    recovered = None
    if args.recovered_assets:
        recovered = create_recovered_images(build, tables, args.recovered_assets, args.backup_assets)
        shutil.copy2(build/'recovered-images.json', output/'recovered-images.json')
        shutil.copytree(build/'area-icons', output/'area-icons')
        shutil.copy2(build/'area-icon-manifest.json', output/'area-icon-manifest.json')
        if args.backup_assets:
            shutil.copy2(build/'backup-image-import.json', output/'backup-image-import.json')
            for name in ['chara_icon_empty','chara_icon_question','chara_icon_reject','lock1']:
                shutil.copy2(build/(name+'.png'), output/(name+'.png'))
    else:
        disable_recovered_images(build)
    native = None
    if args.restore_native:
        native = create_native_bundles(args.backup_assets,build,tables)
        shutil.copy2(build/'native-bundle-manifest.json',output/'native-bundle-manifest.json')
    else:
        disable_native_bundles(build)
    refs = ['-r:'+str(managed/name) for name in ['mscorlib.dll','System.dll','System.Core.dll','UnityEngine.dll','Assembly-CSharp.dll']]
    run('mcs','-nostdlib','-target:library','-out:'+str(build/'DivineGateOffline.dll'),*refs,
        ROOT/'offline/Runtime.cs',ROOT/'offline/NativeAssets.cs',ROOT/'offline/FreeChipShop.cs',ROOT/'offline/CollabGacha.cs',build/'Images.cs',build/'RecoveredImages.cs',build/'NativeAssetIndex.cs')
    if recovered:
        run('mcs','-out:'+str(build/'EmbedImages.exe'),'-r:'+CECIL,ROOT/'offline/EmbedImages.cs')
        run('mono',build/'EmbedImages.exe',build/'DivineGateOffline.dll',managed/'mscorlib.dll',
            build/'RecoveredImages.bin',build/'image-offsets.tsv',build/'WithImages.dll')
        shutil.move(build/'WithImages.dll',build/'DivineGateOffline.dll')
    run('mcs','-out:'+str(build/'Patch.exe'),'-r:'+CECIL,ROOT/'offline/Patch.cs')
    run('mono',build/'Patch.exe',managed/'Assembly-CSharp.dll',build/'DivineGateOffline.dll',build/'Assembly-CSharp.dll')
    run('mcs','-out:'+str(build/'VerifyCollabHooks.exe'),'-r:'+CECIL,ROOT/'offline/tests/VerifyCollabHooks.cs')
    run('mono',build/'VerifyCollabHooks.exe',managed/'Assembly-CSharp.dll',build/'Assembly-CSharp.dll',build/'DivineGateOffline.dll')
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
    if native:
        for row in native['bundles']:
            replacements['assets/dg-native/'+row['file']] = (build/'native-bundles'/row['file']).read_bytes()
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
              'version':('1.0.6 offline prototype 20 native models and animations' if native else '1.0.6 offline prototype 20'),
              'collab_gacha':{'pool_ids':[300100,300200,300300,300400],'price_per_draw':5,'maximum_multi_draw':9,'selector':'original 3 cards plus one clone; clear hover and freeze scale tween at cached neutral size before snapshot, after setup and restore; normal-page fourth-card/Back cleanup retained'},
              'free_chip_shop':{'amounts':[1,6,12,25,52,85],'repeatable':True,'balance_limit':999,'paid':False},
              'local_events':event_catalog,
              'package':'jp.gungho.dg.offline106','tables':counts,
              'modified_original_entries':changed,'removed_manifest_items':removed,'work_directory':str(work),
              'original_assets_and_native_libraries':('assets and engine bytes unchanged; install-directory proxy libraries replaced by bundled engine binaries' if args.native_direct else 'unchanged'),
              'native_direct_loader':args.native_direct,
              'missing_downloaded_assets':('native cache images prioritized; existing simplified meshes retained; backup geometry/animations/effects not imported' if args.backup_assets else 'original map meshes/animations absent; recovered portraits/icons/panels, simplified textured floors and upper scenery planes' if recovered else 'numbered colored substitutes, not recovered artwork'),
              'recovered_artwork':({key: recovered[key] for key in ['source','commit','portraits','character_icons','shadow_masks','maps','panel_and_trap_images','area_icons','area_icon_sources','ui_icon_sources','image_pack_bytes','image_pack_sha256','limitations']} if recovered else None)}
    if args.backup_assets:
        report['backup_image_import'] = {k:recovered['backup_import'][k] for k in [
            'source','portraits','character_icons','area_icons','native_maps','area_texture_coverage',
            'fallback_maps','fallback_sprites','source_hashes_unchanged','master_data_imported',
            'original_geometry_imported','limitations']}
    if native:
        report['native_restoration']={k:native[k] for k in ['source','container','characters','areas','effects','native_bytes','source_hashes_unchanged','cached_master_data_imported','fallback_policy','android_load_and_render_verified']}
        report['native_restoration']['packages']=len(native['bundles'])
        report['missing_downloaded_assets']='99 areas and 522 characters routed to original native prefabs; two missing maps, ef_stage_0012 and native loading failures use the existing simplified fallback. Android verification pending.'
        report['recovered_artwork']['limitations']='PNG UI/fallback image pack; see native_restoration for native character/map geometry, shaders and animation packages. Actual Android rendering remains unverified.'
        report['backup_image_import']['scope']='PNG import phase only; native geometry is packaged separately under native_restoration.'
    (output/'build-report.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n')
    (output/'SHA256SUMS').write_text(report['sha256']+'  '+target.name+'\n')
    print(json.dumps(report,ensure_ascii=False,indent=2))


if __name__ == '__main__':
    main()
