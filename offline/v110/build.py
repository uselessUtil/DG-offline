"""Build a data-gated 1.1.0 offline diagnostic, not a playable release."""
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
CODE = ROOT/'offline/v110'
sys.path.insert(0, str(ROOT/'offline'))
from manifest import patch as patch_manifest
from v110.audit import SHA, SOURCE, CECIL

MANAGED = 'assets/bin/Data/Managed/'
PACKAGE = 'jp.gungho.dg.offline110diagnostic'
LABEL = 'Divine Gate 1.1.0 Offline Diagnostic'


def digest(data):
    return hashlib.sha256(data).hexdigest()


def run(*args, **kwargs):
    return subprocess.run(list(map(str,args)), check=True, **kwargs)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output', type=Path, default=ROOT/'artifacts/1.1.0-offline-diagnostic1-verified')
    args = parser.parse_args()
    assert digest(SOURCE.read_bytes()) == SHA, 'Exact 1.1.0 source required'
    audit = ROOT/'artifacts/1.1.0-security-audit'
    preflight = json.loads((audit/'preflight.json').read_text())
    assert preflight['source_sha256'] == SHA and preflight['scan_returncode'] == 0
    assert digest((audit/'clamav.txt').read_bytes()) == preflight['scan_report_sha256']
    assert (CODE/'SECURITY_REVIEW.md').exists(), 'Review static evidence before building'
    output = args.output.resolve()
    output.mkdir(parents=True,exist_ok=False)
    target = output/'ディバゲ_1.1.0.apk'
    work = Path(tempfile.mkdtemp(prefix='dg110-build-',dir='/tmp'))
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
    assert len(resources) == len(set(resources)) == 36
    environment = UnityPy.load(str(original/'assets/bin/Data'))
    text_assets = [o.read().m_Name for o in environment.objects if o.type.name == 'TextAsset']
    assert len(text_assets) == 16
    assert not any(n.startswith('MasterData') for n in text_assets), 'Reassess strategy if master data exists'
    requirements = {'source_sha256':SHA,'required_resource_count':36,'required_resources':resources,
        'embedded_master_tables':0,'text_assets':text_assets,'schemas':schemas,'local_handlers':handlers,
        'missing_asset_count':None,'coverage_reason':'No version-matched character, area or bundle catalog rows',
        'cdn_prefix_from_code':'http://d3btkj3txnv5zz.cloudfront.net/900_Release/v1_1_0_kh9onea2habnfla4v3g14dast8ausprob/Android/',
        'cdn_contacted':False}
    (output/'missing-data.json').write_text(json.dumps(requirements,ensure_ascii=False,indent=2)+'\n')
    (output/'MISSING_DATA.md').write_text('# 1.1.0 缺失的 36 份主資料\n\n原 APK 內建 0 份。精確欄位見 missing-data.json；補齊後仍需資料關聯與素材驗證。\n\n'+
        '\n'.join('- `'+r+'`' for r in resources)+'\n')
    (work/'Requirements.cs').write_text('public static class DG110Requirements { public static readonly string[] Names = new string[] { '+
        ','.join(json.dumps(r.rsplit('/',1)[1]) for r in resources)+' }; }')
    adapter, patched = work/'DG110Diagnostic.dll', work/'Assembly-CSharp.dll'
    refs = ['-r:'+str(managed/name) for name in ['mscorlib.dll','System.dll','UnityEngine.dll']]
    run('mcs','-nostdlib','-target:library','-out:'+str(adapter),*refs,CODE/'Diagnostic.cs',work/'Requirements.cs')
    run('mcs','-out:'+str(work/'Patch.exe'),'-r:'+CECIL,CODE/'Patch.cs')
    run('mono',work/'Patch.exe',managed/'Assembly-CSharp.dll',adapter,patched,output/'modified-methods.txt')
    env = UnityPy.load(str(original/'assets/bin/Data/mainData'))
    manager = next(o for o in env.objects if o.type.name == 'MonoManager')
    before = {o.path_id:o.get_raw_data() for o in manager.assets_file.objects.values()}
    tree = manager.read_typetree()
    assert 'DG110Diagnostic.dll' not in tree['m_AssemblyNames']
    tree['m_AssemblyNames'].append('DG110Diagnostic.dll')
    manager.save_typetree(tree)
    main_data = manager.assets_file.save()
    after = {o.path_id:o.get_raw_data() for o in UnityPy.load(main_data).objects}
    assert set(before) == set(after)
    assert all(after[k] == v for k,v in before.items() if k != manager.path_id)
    new_manifest, removed = patch_manifest(manifest,package=PACKAGE,label=LABEL,
        extra_permissions=['android.permission.WRITE_EXTERNAL_STORAGE','android.permission.READ_EXTERNAL_STORAGE',
            'android.permission.READ_PHONE_STATE'],
        extra_components=['jp.ART.android.InstallReceiver','com.CFM.ELAN.ELANAlarmCreator','com.CFM.ELAN.TimedAlarm'])
    replacements = {'AndroidManifest.xml':new_manifest,'assets/bin/Data/mainData':main_data,
        MANAGED+'Assembly-CSharp.dll':patched.read_bytes(),MANAGED+'DG110Diagnostic.dll':adapter.read_bytes()}
    # Use this exact APK's real engines in place of its obsolete external-storage proxy loaders.
    native = {}
    for name in ['libmono.so','libunity.so']:
        data = (original/'assets/libs/armeabi-v7a'/name).read_bytes()
        assert data[:4] == b'\x7fELF' and len(data) > 1000000
        replacements['lib/armeabi-v7a/'+name] = data
        native[name] = digest(data)
    expected_changes = set(replacements)-{MANAGED+'DG110Diagnostic.dll'}
    unsigned = work/'unsigned.apk'
    with zipfile.ZipFile(SOURCE) as src, zipfile.ZipFile(unsigned,'w') as dst:
        for info in src.infolist():
            if info.filename.upper().startswith('META-INF/'): continue
            dst.writestr(info,replacements.pop(info.filename,src.read(info.filename)))
        for name,data in replacements.items(): dst.writestr(name,data,compress_type=zipfile.ZIP_DEFLATED)
    run('zipalign','4',unsigned,work/'aligned.apk')
    key = Path('/var/lib/divine-gate-offline/diagnostic110.p12')
    key.parent.mkdir(parents=True,exist_ok=True)
    if not key.exists():
        run('keytool','-genkeypair','-keystore',key,'-storetype','PKCS12','-storepass','android',
            '-keypass','android','-alias','diagnostic110','-keyalg','RSA','-keysize','2048',
            '-validity','10000','-dname','CN=Divine Gate Offline 110 Diagnostic')
        key.chmod(0o600)
    run('apksigner','sign','--ks',key,'--ks-key-alias','diagnostic110','--ks-pass','pass:android',
        '--min-sdk-version','9','--v1-signing-enabled','true','--v2-signing-enabled','true',
        '--v4-signing-enabled','false','--out',target,work/'aligned.apk')
    with zipfile.ZipFile(target) as modified,zipfile.ZipFile(SOURCE) as source:
        assert modified.testzip() is None
        changed = [n for n in source.namelist() if not n.startswith('META-INF/') and source.read(n) != modified.read(n)]
        assert set(changed) == expected_changes
        assert {n for n in set(modified.namelist())-set(source.namelist()) if not n.startswith('META-INF/')} == {MANAGED+'DG110Diagnostic.dll'}
    with (output/'signature.txt').open('w') as handle:
        run('apksigner','verify','--verbose','--print-certs',target,stdout=handle)
    run('zipalign','-c','4',target,stdout=subprocess.DEVNULL)
    badging = subprocess.check_output(['aapt','dump','badging',str(target)],text=True)
    (output/'manifest-summary.txt').write_text(badging)
    assert "package: name='"+PACKAGE+"' versionCode='14' versionName='1.1.0'" in badging
    for name in ['INTERNET','ACCESS_NETWORK_STATE','WRITE_EXTERNAL_STORAGE','READ_EXTERNAL_STORAGE','READ_PHONE_STATE']:
        assert 'android.permission.'+name not in badging
    assert 'com.android.vending.BILLING' not in badging
    assert digest(SOURCE.read_bytes()) == SHA
    report = {'status':'OFFLINE DIAGNOSTIC ONLY; not playable; Android launch untested',
        'source':'User-supplied sczm/184044_sczm.apk; external provenance unknown; no new APK downloaded',
        'source_sha256':SHA,'source_size':SOURCE.stat().st_size,'sha256':digest(target.read_bytes()),
        'size':target.stat().st_size,'package':PACKAGE,'version':'1.1.0 diagnostic prototype 1',
        'modified_original_entries':changed,'added_entry':MANAGED+'DG110Diagnostic.dll',
        'native_direct_source_sha256':native,'removed_manifest_items':removed,'work_directory':str(work),
        'gameplay_test':'not performed; missing data; no approved Android device identified'}
    (output/'build-report.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n')
    (output/'SHA256SUMS').write_text(report['sha256']+'  '+target.name+'\n')
    (output/'SECURITY_REVIEW.md').write_text((CODE/'SECURITY_REVIEW.md').read_text())
    cdn_link = Path(os.path.relpath(ROOT/'offline/CDN_FINDINGS.md',output)).as_posix()
    (output/'README.md').write_text((CODE/'README.md').read_text().replace('(../CDN_FINDINGS.md)','('+cdn_link+')'))
    print(json.dumps(report,ensure_ascii=False,indent=2))


if __name__ == '__main__': main()
