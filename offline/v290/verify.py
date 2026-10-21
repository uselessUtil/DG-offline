"""Inspect the actual delivered APK; this does not execute the Android player."""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess
import tempfile
import zipfile

ROOT = Path('/workspace')
CODE = ROOT/'offline/v290'
CECIL = '/usr/lib/mono/gac/Mono.Cecil/0.11.1.0__0738eb9f132ed756/Mono.Cecil.dll'
MANAGED = 'assets/bin/Data/Managed/'


def run(*args, **kwargs):
    return subprocess.check_output(list(map(str,args)),text=True,**kwargs)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output',type=Path,default=ROOT/'artifacts/2.9.0-offline-diagnostic1')
    args = parser.parse_args()
    output = args.output.resolve()
    report = json.loads((output/'build-report.json').read_text())
    target = output/'ディバゲ_2.9.0.apk'
    source = ROOT/'jp.gungho.dg-v2.9.0.apk'
    assert hashlib.sha256(target.read_bytes()).hexdigest() == report['sha256']
    assert hashlib.sha256(source.read_bytes()).hexdigest() == report['source_sha256']
    work = Path(tempfile.mkdtemp(prefix='dg290-verify-',dir='/tmp'))
    with zipfile.ZipFile(target) as modified,zipfile.ZipFile(source) as original:
        assert modified.testzip() is None
        original.extract(MANAGED+'Assembly-CSharp.dll',work/'original')
        for name in modified.namelist():
            if name.startswith(MANAGED): modified.extract(name,work/'delivered')
        changed = {n for n in original.namelist() if not n.startswith('META-INF/') and original.read(n) != modified.read(n)}
        assert changed == set(report['modified_original_entries'])
        added = {n for n in set(modified.namelist())-set(original.namelist()) if not n.startswith('META-INF/')}
        assert added == {MANAGED+'DG290Diagnostic.dll'}
        for prefix in ['lib/', 'classes.dex', MANAGED+'UnityEngine.dll']:
            assert all(original.read(n) == modified.read(n) for n in original.namelist() if n.startswith(prefix))
    delivered = work/'delivered'/MANAGED
    run('mcs','-out:'+str(work/'Verify.exe'),'-r:'+CECIL,CODE/'Verify.cs')
    results = run('mono',work/'Verify.exe',work/'original'/MANAGED/'Assembly-CSharp.dll',
        delivered/'Assembly-CSharp.dll',delivered/'DG290Diagnostic.dll',output/'modified-methods.txt')
    run('mcs','-out:'+str(delivered/'VerifyReport.exe'),'-r:'+str(delivered/'DG290Diagnostic.dll'),
        '-r:'+str(delivered/'UnityEngine.dll'),CODE/'VerifyReport.cs')
    results += run('mono',delivered/'VerifyReport.exe')
    results += run('apksigner','verify','--verbose','--print-certs',target)
    run('zipalign','-c','4',target)
    badging = run('aapt','dump','badging',target)
    assert "package: name='jp.gungho.dg.offline290diagnostic'" in badging
    assert "versionCode='35' versionName='2.9.0'" in badging
    assert "application-label:'Divine Gate 2.9.0 Offline Diagnostic'" in badging
    for name in ['INTERNET','ACCESS_NETWORK_STATE','WRITE_EXTERNAL_STORAGE','READ_EXTERNAL_STORAGE']:
        assert 'android.permission.'+name not in badging
    assert 'com.android.vending.BILLING' not in badging
    tree = run('aapt','dump','xmltree',target,'AndroidManifest.xml')
    for name in report['removed_manifest_items']:
        # Removed SDK names should not survive as another XML attribute.
        assert '="'+name+'"' not in tree, name
    missing = json.loads((output/'missing-data.json').read_text())
    assert missing['source_sha256'] == report['source_sha256']
    assert len(set(missing['required_resources'])) == missing['required_resource_count'] == 42
    assert missing['embedded_master_tables'] == 0
    # Recheck preserved default manifest behavior for the existing 1.0.6 builder.
    import sys
    sys.path.insert(0,str(ROOT/'offline'))
    import manifest
    with zipfile.ZipFile(next(ROOT.glob('*1.0.6.apk'))) as old:
        data = old.read('AndroidManifest.xml')
    default, _ = manifest.patch(data)
    explicit, _ = manifest.patch(data,package='jp.gungho.dg.offline106',label='Divine Gate Offline 1.0.6')
    assert default == explicit
    results += 'PASS APK archive, declared changes, permissions, SDK removals, signature, alignment and source hash\n'
    results += 'PASS existing 1.0.6 manifest defaults preserved\n'
    results += 'NOT TESTED: Android launch/render/touch/native behavior, gameplay and save/reload\n'
    (output/'verification.txt').write_text(results)
    print(results)


if __name__ == '__main__': main()
