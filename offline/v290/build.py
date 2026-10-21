"""Build the 2.9.0 OFFLINE DIAGNOSTIC, deliberately blocked before gameplay."""
import argparse
import hashlib
import json
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile
import zipfile

import UnityPy

ROOT = Path('/workspace')
sys.path.insert(0, str(ROOT/'offline'))
from manifest import patch as patch_manifest

SOURCE = ROOT/'jp.gungho.dg-v2.9.0.apk'
SHA = '3c6ce05ca09837b07faaf99aa80716cc2754eb22887ca580d17e8c86559f2cc4'
CECIL = '/usr/lib/mono/gac/Mono.Cecil/0.11.1.0__0738eb9f132ed756/Mono.Cecil.dll'
MANAGED = 'assets/bin/Data/Managed/'
PACKAGE = 'jp.gungho.dg.offline290diagnostic'
LABEL = 'Divine Gate 2.9.0 Offline Diagnostic'
CODE = ROOT/'offline/v290'


def digest(data):
    return hashlib.sha256(data).hexdigest()


def run(*args, **kwargs):
    return subprocess.run(list(map(str,args)), check=True, **kwargs)


def write_missing(output, resources):
    missing_text = '# 2.9.0 缺失主資料\n\n原 APK 未包含以下 42 份 Resources 主資料。完整欄位 schema 見同目錄 missing-data.json。\n\n'
    missing_text += '\n'.join('- `'+name+'`' for name in resources)+'\n'
    (output/'MISSING_DATA.md').write_text(missing_text)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output', type=Path, default=ROOT/'artifacts/2.9.0-offline-diagnostic1')
    args = parser.parse_args()
    assert digest(SOURCE.read_bytes()) == SHA, 'Version-specific source hash mismatch'
    security = json.loads((ROOT/'artifacts/2.9.0-security-audit/security-verdict.json').read_text())
    assert security['source_sha256'] == SHA and security['clamav_returncode'] == 0
    output = args.output.resolve()
    output.mkdir(parents=True,exist_ok=False)
    target = output/'ディバゲ_2.9.0.apk'
    work = Path(tempfile.mkdtemp(prefix='dg290-build-',dir='/tmp'))
    original = work/'original'
    with zipfile.ZipFile(SOURCE) as archive:
        assert archive.testzip() is None
        archive.extractall(original)
        manifest = archive.read('AndroidManifest.xml')
    managed = original/MANAGED
    run('mcs','-out:'+str(work/'Inspect.exe'),'-r:'+CECIL,CODE/'Inspect.cs')
    inventory = subprocess.check_output(['mono',str(work/'Inspect.exe'),str(managed/'Assembly-CSharp.dll')],text=True)
    resources, tables, handlers = [], {}, []
    for line in inventory.splitlines():
        parts = line.split('\t')
        if parts[0] == 'resource': resources.append(parts[1])
        elif parts[0] == 'table': tables.setdefault(parts[2],{'server_field':parts[1],'columns':{}})
        elif parts[0] == 'column': tables[parts[1]]['columns'][parts[2]] = parts[3]
        elif parts[0] == 'handler': handlers.append({'method':parts[1],'il_instructions':int(parts[2])})
    assert len(resources) == len(set(resources)) == 42
    # Re-inventory this exact input instead of trusting stale analysis output.
    environment = UnityPy.load(str(original/'assets/bin/Data'))
    text_assets = [o.read().m_Name for o in environment.objects if o.type.name == 'TextAsset']
    assert not any(n.startswith('MasterData') for n in text_assets), 'This diagnostic targets the data-less archive only'
    requirements = {'source_sha256':SHA,'required_resource_count':42,'required_resources':resources,
        'embedded_master_tables':0,'tables':tables,'local_backend_methods':handlers,
        'asset_coverage':'Not computable without the 2.9.0 catalog and character/map master data',
        'blocked_features':['account creation','login','quests and battles','gacha','progress persistence'],
        'download_categories':{'1':'MasterData0001','2':'ResMap0001','3':'ResUnit0001/QUALITY_1_*',
                               '4,5,6':'ResIcon0001','7,8':'ResSound0001','9':'ResScratch0001'},
        'cdn_prefix_from_code':'http://d3btkj3txnv5zz.cloudfront.net/900_Release/v2_9_0_f2rwxf2qya5nuo41u9vcnlxbjy8ua9fwc/Android/'}
    (output/'missing-data.json').write_text(json.dumps(requirements,ensure_ascii=False,indent=2)+'\n')
    write_missing(output, resources)
    (work/'Requirements.cs').write_text('public static class DG290Requirements { public static readonly string[] Names = new string[] { '+
        ','.join(json.dumps(r.rsplit('/',1)[1]) for r in resources)+' }; }')
    refs = ['-r:'+str(managed/name) for name in ['mscorlib.dll','System.dll','UnityEngine.dll']]
    adapter = work/'DG290Diagnostic.dll'
    patched = work/'Assembly-CSharp.dll'
    run('mcs','-nostdlib','-target:library','-out:'+str(adapter),*refs,CODE/'Diagnostic.cs',work/'Requirements.cs')
    run('mcs','-out:'+str(work/'Patch.exe'),'-r:'+CECIL,CODE/'Patch.cs')
    run('mono',work/'Patch.exe',managed/'Assembly-CSharp.dll',adapter,patched,output/'modified-methods.txt')
    # Only register the added assembly, preserving all unrelated Unity object bytes.
    env = UnityPy.load(str(original/'assets/bin/Data/mainData'))
    manager = next(o for o in env.objects if o.type.name == 'MonoManager')
    before = {o.path_id:o.get_raw_data() for o in manager.assets_file.objects.values()}
    tree = manager.read_typetree()
    assert 'DG290Diagnostic.dll' not in tree['m_AssemblyNames']
    tree['m_AssemblyNames'].append('DG290Diagnostic.dll')
    manager.save_typetree(tree)
    main_data = manager.assets_file.save()
    after = {o.path_id:o.get_raw_data() for o in UnityPy.load(main_data).objects}
    assert set(before) == set(after)
    assert all(after[k] == v for k,v in before.items() if k != manager.path_id)
    new_manifest, removed = patch_manifest(manifest,package=PACKAGE,label=LABEL,
        extra_permissions=['android.permission.WRITE_EXTERNAL_STORAGE','android.permission.READ_EXTERNAL_STORAGE'],
        extra_components=['it.partytrack.sdk.ReferrerReceiver','com.CFM.ELAN.ELANAlarmCreator',
                          'com.CFM.ELAN.TimedAlarm','com.google.games.bridge.NativeBridgeActivity'])
    replacements = {'AndroidManifest.xml':new_manifest,'assets/bin/Data/mainData':main_data,
        MANAGED+'Assembly-CSharp.dll':patched.read_bytes(), MANAGED+'DG290Diagnostic.dll':adapter.read_bytes()}
    unsigned = work/'unsigned.apk'
    with zipfile.ZipFile(SOURCE) as src, zipfile.ZipFile(unsigned,'w') as dst:
        for info in src.infolist():
            if info.filename.upper().startswith('META-INF/'): continue
            dst.writestr(info,replacements.pop(info.filename,src.read(info.filename)))
        for name,data in replacements.items(): dst.writestr(name,data,compress_type=zipfile.ZIP_DEFLATED)
    run('zipalign','4',unsigned,work/'aligned.apk')
    key = Path('/var/lib/divine-gate-offline/diagnostic290.p12')
    key.parent.mkdir(parents=True,exist_ok=True)
    if not key.exists():
        run('keytool','-genkeypair','-keystore',key,'-storetype','PKCS12','-storepass','android',
            '-keypass','android','-alias','diagnostic290','-keyalg','RSA','-keysize','2048',
            '-validity','10000','-dname','CN=Divine Gate Offline 290 Diagnostic')
        key.chmod(0o600)
    run('apksigner','sign','--ks',key,'--ks-key-alias','diagnostic290','--ks-pass','pass:android',
        '--min-sdk-version','9','--v1-signing-enabled','true','--v2-signing-enabled','true',
        '--out',target,work/'aligned.apk')
    with zipfile.ZipFile(target) as modified,zipfile.ZipFile(SOURCE) as source:
        assert modified.testzip() is None
        changed = [n for n in source.namelist() if not n.startswith('META-INF/') and source.read(n) != modified.read(n)]
        assert set(changed) == {'AndroidManifest.xml','assets/bin/Data/mainData',MANAGED+'Assembly-CSharp.dll'}
        added = set(modified.namelist())-set(source.namelist())
        assert {n for n in added if not n.startswith('META-INF/')} == {MANAGED+'DG290Diagnostic.dll'}
    with (output/'signature.txt').open('w') as handle:
        run('apksigner','verify','--verbose','--print-certs',target,stdout=handle)
    run('zipalign','-c','4',target,stdout=subprocess.DEVNULL)
    badging = subprocess.check_output(['aapt','dump','badging',str(target)],text=True)
    (output/'manifest-summary.txt').write_text(badging)
    assert "package: name='"+PACKAGE+"'" in badging
    for permission in ['INTERNET','ACCESS_NETWORK_STATE','WRITE_EXTERNAL_STORAGE','READ_EXTERNAL_STORAGE']:
        assert 'android.permission.'+permission not in badging
    assert 'com.android.vending.BILLING' not in badging
    assert digest(SOURCE.read_bytes()) == SHA
    report = {'status':'OFFLINE DIAGNOSTIC ONLY; gameplay blocked; Android launch not tested',
        'source_sha256':SHA,'source_size':SOURCE.stat().st_size,'sha256':digest(target.read_bytes()),
        'size':target.stat().st_size,'package':PACKAGE,'version':'2.9.0 diagnostic prototype 1',
        'modified_original_entries':changed,'added_entry':MANAGED+'DG290Diagnostic.dll',
        'removed_manifest_items':removed,'security_verdict':security,'work_directory':str(work),
        'unmodified':'DEX, all native libraries, UnityEngine.dll, all original gameplay assets',
        'gameplay_test':'not performed; no approved Android device identified'}
    (output/'build-report.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n')
    (output/'SHA256SUMS').write_text(report['sha256']+'  '+target.name+'\n')
    shutil.copyfile(CODE/'README.md',output/'README.md')
    print(json.dumps(report,ensure_ascii=False,indent=2))


if __name__ == '__main__': main()
