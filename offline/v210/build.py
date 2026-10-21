"""Build a data-gated 2.1.0 offline diagnostic, not a playable release."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import subprocess
import sys
import tempfile
import zipfile

import UnityPy

ROOT = Path('/workspace')
CODE = ROOT/'offline/v210'
sys.path.insert(0, str(ROOT/'offline'))
from manifest import patch as patch_manifest
from v210.audit import SHA, SOURCE, CECIL

MANAGED = 'assets/bin/Data/Managed/'
PACKAGE = 'jp.gungho.dg.offline210diagnostic'
LABEL = 'Divine Gate 2.1.0 Offline Diagnostic'


def digest(data):
    return hashlib.sha256(data).hexdigest()


def run(*args, **kwargs):
    return subprocess.run(list(map(str,args)), check=True, **kwargs)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output', type=Path, default=ROOT/'artifacts/2.1.0-offline-diagnostic1-verified')
    args = parser.parse_args()
    assert digest(SOURCE.read_bytes()) == SHA, 'Exact 2.1.0 source required'
    output = args.output.resolve()
    output.mkdir(parents=True,exist_ok=False)
    target = output/'ディバゲ_2.1.0.apk'
    work = Path(tempfile.mkdtemp(prefix='dg210-build-',dir='/tmp'))
    original = work/'original'
    with zipfile.ZipFile(SOURCE) as archive:
        assert archive.testzip() is None
        assert len(archive.namelist()) == len(set(archive.namelist()))
        assert not any(n.startswith('/') or '..' in Path(n).parts for n in archive.namelist())
        archive.extractall(original)
        manifest = archive.read('AndroidManifest.xml')
    managed = original/MANAGED
    # Recompute the input inventory; never use stale audit output as a data gate.
    run('mcs','-out:'+str(work/'Inspect.exe'),'-r:'+CECIL,CODE/'Inspect.cs')
    inventory = subprocess.check_output(['mono',str(work/'Inspect.exe'),str(managed/'Assembly-CSharp.dll')],text=True)
    (output/'backend-inventory.tsv').write_text(inventory)
    resources, schemas, handlers = [], {}, []
    for line in inventory.splitlines():
        p = line.split('\t')
        if p[0] == 'resource': resources.append(p[1])
        elif p[0] == 'table': schemas[p[2]] = {'server_field':p[1],'columns':{}}
        elif p[0] == 'column': schemas[p[1]]['columns'][p[2]] = p[3]
        elif p[0] == 'handler': handlers.append({'method':p[1],'il_instructions':int(p[2])})
    assert len(resources) == len(set(resources)) == 37
    environment = UnityPy.load(str(original/'assets/bin/Data'))
    text_assets = [o.read().m_Name for o in environment.objects if o.type.name == 'TextAsset']
    assert len(text_assets) == 15
    assert not any(n.startswith('MasterData') for n in text_assets), 'Reassess strategy if master data exists'
    requirements = {'source_sha256':SHA,'required_resource_count':37,'required_resources':resources,
        'embedded_master_tables':0,'text_assets':text_assets,'schemas':schemas,'local_handlers':handlers,
        'missing_asset_count':None,'coverage_reason':'No version-matched character, area or bundle catalog rows',
        'download_categories':{'1':'Android/MasterData0001','2':'Android/ResMap0001',
            '3':'Android/ResUnit0001/QUALITY_1_{1,2,4}','4,5,6':'Android/ResIcon0001',
            '7,8':'Android/ResSound0001','9':'Android/ResScratch0001'},
        'cdn_prefix_from_code':'http://d3btkj3txnv5zz.cloudfront.net/900_Release/v2_1_0_9aujw0ejg0aj0w3j0jq30jg0jq09dskcq/'}
    (output/'missing-data.json').write_text(json.dumps(requirements,ensure_ascii=False,indent=2)+'\n')
    (output/'MISSING_DATA.md').write_text('# 2.1.0 缺失的 37 份主資料\n\n原 APK 內建 0 份。精確欄位見 missing-data.json；補齊後仍需資料關聯與素材驗證。\n\n'+
        '\n'.join('- `'+r+'`' for r in resources)+'\n')
    (work/'Requirements.cs').write_text('public static class DG210Requirements { public static readonly string[] Names = new string[] { '+
        ','.join(json.dumps(r.rsplit('/',1)[1]) for r in resources)+' }; }')
    adapter, patched = work/'DG210Diagnostic.dll', work/'Assembly-CSharp.dll'
    refs = ['-r:'+str(managed/name) for name in ['mscorlib.dll','System.dll','UnityEngine.dll']]
    run('mcs','-nostdlib','-target:library','-out:'+str(adapter),*refs,CODE/'Diagnostic.cs',work/'Requirements.cs')
    run('mcs','-out:'+str(work/'Patch.exe'),'-r:'+CECIL,CODE/'Patch.cs')
    run('mono',work/'Patch.exe',managed/'Assembly-CSharp.dll',adapter,patched,output/'modified-methods.txt')
    env = UnityPy.load(str(original/'assets/bin/Data/mainData'))
    manager = next(o for o in env.objects if o.type.name == 'MonoManager')
    before = {o.path_id:o.get_raw_data() for o in manager.assets_file.objects.values()}
    tree = manager.read_typetree()
    assert 'DG210Diagnostic.dll' not in tree['m_AssemblyNames']
    tree['m_AssemblyNames'].append('DG210Diagnostic.dll')
    manager.save_typetree(tree)
    main_data = manager.assets_file.save()
    after = {o.path_id:o.get_raw_data() for o in UnityPy.load(main_data).objects}
    assert set(before) == set(after)
    assert all(after[k] == v for k,v in before.items() if k != manager.path_id)
    new_manifest, removed = patch_manifest(manifest,package=PACKAGE,label=LABEL,
        extra_permissions=['android.permission.WRITE_EXTERNAL_STORAGE','android.permission.READ_EXTERNAL_STORAGE'],
        extra_components=['jp.ART.android.InstallReceiver','com.CFM.ELAN.ELANAlarmCreator','com.CFM.ELAN.TimedAlarm'])
    replacements = {'AndroidManifest.xml':new_manifest,'assets/bin/Data/mainData':main_data,
        MANAGED+'Assembly-CSharp.dll':patched.read_bytes(),MANAGED+'DG210Diagnostic.dll':adapter.read_bytes()}
    # Use this exact APK's real engines in place of its obsolete external-storage proxy loaders.
    native = {}
    for name in ['libmono.so','libunity.so']:
        data = (original/'assets/libs/armeabi-v7a'/name).read_bytes()
        assert data[:4] == b'\x7fELF' and len(data) > 1000000
        replacements['lib/armeabi-v7a/'+name] = data
        native[name] = digest(data)
    expected_changes = set(replacements)-{MANAGED+'DG210Diagnostic.dll'}
    unsigned = work/'unsigned.apk'
    with zipfile.ZipFile(SOURCE) as src, zipfile.ZipFile(unsigned,'w') as dst:
        for info in src.infolist():
            if info.filename.upper().startswith('META-INF/'): continue
            dst.writestr(info,replacements.pop(info.filename,src.read(info.filename)))
        for name,data in replacements.items(): dst.writestr(name,data,compress_type=zipfile.ZIP_DEFLATED)
    run('zipalign','4',unsigned,work/'aligned.apk')
    key = Path('/var/lib/divine-gate-offline/diagnostic210.p12')
    key.parent.mkdir(parents=True,exist_ok=True)
    if not key.exists():
        run('keytool','-genkeypair','-keystore',key,'-storetype','PKCS12','-storepass','android',
            '-keypass','android','-alias','diagnostic210','-keyalg','RSA','-keysize','2048',
            '-validity','10000','-dname','CN=Divine Gate Offline 210 Diagnostic')
        key.chmod(0o600)
    run('apksigner','sign','--ks',key,'--ks-key-alias','diagnostic210','--ks-pass','pass:android',
        '--min-sdk-version','9','--v1-signing-enabled','true','--v2-signing-enabled','true',
        '--v4-signing-enabled','false','--out',target,work/'aligned.apk')
    with zipfile.ZipFile(target) as modified,zipfile.ZipFile(SOURCE) as source:
        assert modified.testzip() is None
        changed = [n for n in source.namelist() if not n.startswith('META-INF/') and source.read(n) != modified.read(n)]
        assert set(changed) == expected_changes
        assert {n for n in set(modified.namelist())-set(source.namelist()) if not n.startswith('META-INF/')} == {MANAGED+'DG210Diagnostic.dll'}
    with (output/'signature.txt').open('w') as handle:
        run('apksigner','verify','--verbose','--print-certs',target,stdout=handle)
    run('zipalign','-c','4',target,stdout=subprocess.DEVNULL)
    badging = subprocess.check_output(['aapt','dump','badging',str(target)],text=True)
    (output/'manifest-summary.txt').write_text(badging)
    assert "package: name='"+PACKAGE+"' versionCode='18' versionName='2.1.0'" in badging
    for name in ['INTERNET','ACCESS_NETWORK_STATE','WRITE_EXTERNAL_STORAGE','READ_EXTERNAL_STORAGE']:
        assert 'android.permission.'+name not in badging
    assert 'com.android.vending.BILLING' not in badging
    assert digest(SOURCE.read_bytes()) == SHA
    report = {'status':'OFFLINE DIAGNOSTIC ONLY; not playable; Android launch untested',
        'source':'User-supplied archived ディバゲ_2.1.0.apk; no new APK downloaded',
        'source_sha256':SHA,'source_size':SOURCE.stat().st_size,'sha256':digest(target.read_bytes()),
        'size':target.stat().st_size,'package':PACKAGE,'version':'2.1.0 diagnostic prototype 1',
        'modified_original_entries':changed,'added_entry':MANAGED+'DG210Diagnostic.dll',
        'native_direct_source_sha256':native,'removed_manifest_items':removed,'work_directory':str(work),
        'gameplay_test':'not performed; missing data; no approved Android device identified'}
    (output/'build-report.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n')
    (output/'SHA256SUMS').write_text(report['sha256']+'  '+target.name+'\n')
    cdn_link = Path(os.path.relpath(ROOT/'offline/CDN_FINDINGS.md',output)).as_posix()
    (output/'README.md').write_text((CODE/'README.md').read_text().replace('(../CDN_FINDINGS.md)','('+cdn_link+')'))
    print(json.dumps(report,ensure_ascii=False,indent=2))


if __name__ == '__main__': main()
