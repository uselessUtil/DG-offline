"""Verify the delivered 1.0.9 diagnostic APK and reproduce its missing-data barrier."""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess
import tempfile
import zipfile

import UnityPy

ROOT = Path('/workspace')
CODE = ROOT/'offline/v109'
CECIL = '/usr/lib/mono/gac/Mono.Cecil/0.11.1.0__0738eb9f132ed756/Mono.Cecil.dll'
MANAGED = 'assets/bin/Data/Managed/'


def run(*args):
    return subprocess.check_output(list(map(str,args)),text=True)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output',type=Path,default=ROOT/'artifacts/1.0.9-offline-diagnostic1-verified')
    output = parser.parse_args().output.resolve()
    report = json.loads((output/'build-report.json').read_text())
    source, target = ROOT/'sczm/179978_sczm.apk', output/'ディバゲ_1.0.9.apk'
    assert hashlib.sha256(source.read_bytes()).hexdigest() == report['source_sha256']
    assert hashlib.sha256(target.read_bytes()).hexdigest() == report['sha256']
    work = Path(tempfile.mkdtemp(prefix='dg109-verify-',dir='/tmp'))
    with zipfile.ZipFile(target) as modified, zipfile.ZipFile(source) as original:
        assert modified.testzip() is None
        assert len(modified.namelist()) == len(set(modified.namelist()))
        original.extract(MANAGED+'Assembly-CSharp.dll',work/'original')
        for name in modified.namelist():
            if name.startswith(MANAGED): modified.extract(name,work/'delivered')
        changed = {n for n in original.namelist() if not n.startswith('META-INF/') and original.read(n) != modified.read(n)}
        assert changed == set(report['modified_original_entries']) == {
            'AndroidManifest.xml','assets/bin/Data/mainData',MANAGED+'Assembly-CSharp.dll',
            'lib/armeabi-v7a/libmono.so','lib/armeabi-v7a/libunity.so'}
        added = {n for n in set(modified.namelist())-set(original.namelist()) if not n.startswith('META-INF/')}
        assert added == {MANAGED+'DG109Diagnostic.dll'}
        assert {n for n in set(original.namelist())-set(modified.namelist()) if not n.startswith('META-INF/')} == set()
        assert original.read('classes.dex') == modified.read('classes.dex')
        for name in ['libmono.so','libunity.so']:
            real = original.read('assets/libs/armeabi-v7a/'+name)
            assert real == modified.read('lib/armeabi-v7a/'+name)
            assert hashlib.sha256(real).hexdigest() == report['native_direct_source_sha256'][name]
        # Independently verify the single MonoManager change in the delivered file.
        before = UnityPy.load(original.read('assets/bin/Data/mainData'))
        after = UnityPy.load(modified.read('assets/bin/Data/mainData'))
        old = {o.path_id:o for o in before.objects}
        new = {o.path_id:o for o in after.objects}
        assert set(old) == set(new)
        for key,obj in old.items():
            if obj.type.name != 'MonoManager':
                assert obj.get_raw_data() == new[key].get_raw_data()
            else:
                a,b = obj.read_typetree(),new[key].read_typetree()
                a['m_AssemblyNames'].append('DG109Diagnostic.dll')
                assert a == b
    delivered = work/'delivered'/MANAGED
    run('mcs','-out:'+str(work/'Verify.exe'),'-r:'+CECIL,CODE/'Verify.cs')
    results = run('mono',work/'Verify.exe',work/'original'/MANAGED/'Assembly-CSharp.dll',
        delivered/'Assembly-CSharp.dll',delivered/'DG109Diagnostic.dll',output/'modified-methods.txt')
    run('mcs','-out:'+str(delivered/'VerifyReport.exe'),'-r:'+str(delivered/'DG109Diagnostic.dll'),
        '-r:'+str(delivered/'UnityEngine.dll'),CODE/'VerifyReport.cs')
    results += run('mono',delivered/'VerifyReport.exe')
    # Stub only a disposable test copy; do not package the stub or execute Android code.
    probe = work/'probe'
    probe.mkdir()
    for name in ['Assembly-CSharp.dll','Assembly-CSharp-firstpass.dll','DG109Diagnostic.dll']:
        (probe/name).write_bytes((delivered/name).read_bytes())
    run('mcs','-out:'+str(work/'Prepare.exe'),'-r:'+CECIL,CODE/'PrepareTestEngine.cs')
    run('mono',work/'Prepare.exe',delivered/'UnityEngine.dll',probe/'UnityEngine.dll')
    run('mcs','-out:'+str(probe/'Probe.exe'),'-r:'+str(probe/'Assembly-CSharp.dll'),
        '-r:'+str(probe/'UnityEngine.dll'),CODE/'ProbeMissingData.cs')
    probe_results = run('mono',probe/'Probe.exe')
    (output/'missing-data-probe.txt').write_text(probe_results)
    results += probe_results
    results += run('apksigner','verify','--verbose','--print-certs',target)
    run('zipalign','-c','4',target)
    badging = run('aapt','dump','badging',target)
    assert "package: name='jp.gungho.dg.offline109diagnostic' versionCode='13' versionName='1.0.9'" in badging
    assert "application-label:'Divine Gate 1.0.9 Offline Diagnostic'" in badging
    for name in ['INTERNET','ACCESS_NETWORK_STATE','WRITE_EXTERNAL_STORAGE','READ_EXTERNAL_STORAGE','READ_PHONE_STATE']:
        assert 'android.permission.'+name not in badging
    assert 'com.android.vending.BILLING' not in badging
    tree = run('aapt','dump','xmltree',target,'AndroidManifest.xml')
    for name in report['removed_manifest_items']:
        assert '="'+name+'"' not in tree
    missing = json.loads((output/'missing-data.json').read_text())
    assert missing['source_sha256'] == report['source_sha256']
    assert len(set(missing['required_resources'])) == missing['required_resource_count'] == 34
    assert missing['embedded_master_tables'] == 0
    run('mcs','-out:'+str(work/'Inspect.exe'),'-r:'+CECIL,CODE/'Inspect.cs')
    inventory = run('mono',work/'Inspect.exe',work/'original'/MANAGED/'Assembly-CSharp.dll')
    assert inventory == (output/'backend-inventory.tsv').read_text()
    resources, handlers = [], []
    for line in inventory.splitlines():
        p = line.split('\t')
        if p[0] == 'resource': resources.append(p[1])
        elif p[0] == 'handler': handlers.append({'method':p[1],'il_instructions':int(p[2])})
    assert resources == missing['required_resources']
    assert handlers == missing['local_handlers'] and len(handlers) > 30
    results += 'PASS APK entries, source preservation, original native engines, MonoManager registration, permissions, signature and alignment\n'
    results += 'NOT TESTED: Android installation/launch, native rendering, touch, gameplay and save/reload\n'
    (output/'verification.txt').write_text(results)
    print(results)


if __name__ == '__main__': main()
