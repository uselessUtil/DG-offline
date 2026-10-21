"""Test explicitly labelled 1.0.6 content in the original 1.1.0 backend, not an APK port.

Run inside Docker after audit.py. Native engine I/O uses disposable test stubs.
Never inject missing tables or fabricate new field values.
"""
import hashlib
import argparse
import json
from pathlib import Path
import shutil
import subprocess
import tempfile
import zipfile

import UnityPy
from audit import CECIL, ROOT, SHA, SOURCE, WORK

OLD_SHA = 'ba3ccd42c28e4d9362cb6bda2b4d21483b98085441f9513ca1b825bc71be81c2'
MANAGED = 'assets/bin/Data/Managed'


def run(*args):
    return subprocess.check_output(list(map(str,args)),text=True,timeout=60)


def main():
    old = ROOT/'ディバゲ_1.0.6.apk'
    assert hashlib.sha256(SOURCE.read_bytes()).hexdigest() == SHA
    assert hashlib.sha256(old.read_bytes()).hexdigest() == OLD_SHA
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output',type=Path,default=ROOT/'artifacts/1.1.0-old-content-probe1-clean')
    output = parser.parse_args().output.resolve()
    output.mkdir(exist_ok=False)
    work = Path(tempfile.mkdtemp(prefix='dg110-probe-',dir='/tmp'))
    with zipfile.ZipFile(old) as z:
        for n in z.namelist():
            if n.startswith('assets/bin/Data/'): z.extract(n,work/'old')
    source_tables = {}
    for o in UnityPy.load(str(work/'old/assets/bin/Data')).objects:
        if o.type.name != 'TextAsset': continue
        v = o.read()
        if not v.m_Name.startswith('MasterData'): continue
        raw = v.m_Script
        if isinstance(raw,str): raw = raw.encode('utf8','surrogateescape')
        source_tables[v.m_Name] = json.loads(raw,strict=False)
    schemas = {}
    for line in (ROOT/'artifacts/1.1.0-security-audit/backend-inventory.tsv').read_text().splitlines():
        p = line.split('\t')
        if p[0] == 'table': schemas[p[2]] = set()
        elif p[0] == 'column': schemas[p[1]].add(p[2])
    converted, raw = work/'converted',work/'raw'
    converted.mkdir(); raw.mkdir()
    changes, missing = {}, []
    for name, columns in schemas.items():
        alias = {'MasterDataParamEnemy':'MasterDataEnemyChara'}.get(name,name)
        if alias not in source_tables:
            missing.append(name)
            continue
        rows = source_tables[alias]
        assert isinstance(rows,list)
        removed = sorted(set().union(*(r.keys() for r in rows))-columns)
        (raw/(name+'.json')).write_text(json.dumps(rows,ensure_ascii=False))
        (converted/(name+'.json')).write_text(json.dumps([
            {k:v for k,v in row.items() if k in columns} for row in rows],ensure_ascii=False))
        changes[name] = {'source':alias,'rows':len(rows),'removed_columns':removed}
    original = WORK/'original'/MANAGED
    game_hashes = {name:hashlib.sha256((original/name).read_bytes()).hexdigest()
        for name in ['Assembly-CSharp.dll','Assembly-CSharp-firstpass.dll']}
    # Keep trimmed Android BCLs away from host-side Cecil executables.
    for name in game_hashes: shutil.copy2(original/name,work/name)
    run('mcs','-out:'+str(work/'Prepare.exe'),'-r:'+CECIL,ROOT/'offline/v110/PrepareTestEngine.cs')
    run('mono',work/'Prepare.exe',original/'UnityEngine.dll',work/'EngineDefault.dll')
    run('mcs','-target:library','-out:'+str(work/'MemoryPrefs.dll'),ROOT/'offline/comparison/MemoryPrefs.cs')
    run('mcs','-out:'+str(work/'Prefs.exe'),'-r:'+CECIL,ROOT/'offline/comparison/PreparePrefs.cs')
    run('mono',work/'Prefs.exe',work/'EngineDefault.dll',work/'MemoryPrefs.dll',work/'UnityEngine.dll')
    run('mcs','-out:'+str(work/'Probe.exe'),'-r:'+str(work/'Assembly-CSharp.dll'),
        '-r:'+str(work/'Assembly-CSharp-firstpass.dll'),'-r:'+str(work/'UnityEngine.dll'),
        ROOT/'offline/comparison/Probe.cs')
    # LitJson requires this APK's Silverlight-profile System assembly. Copy it
    # only after all Cecil tools have completed; do not shadow their host BCL.
    for name in ['System.dll','System.Core.dll']:
        shutil.copy2(original/name,work/name)
    result = run('mono',work/'Probe.exe',converted,raw)
    (output/'probe.txt').write_text(result)
    assert all(hashlib.sha256((work/n).read_bytes()).hexdigest() == h for n,h in game_hashes.items())
    report = {'source_sha256':SHA,'old_content_sha256':OLD_SHA,'tables':changes,
        'missing_old_source_types':sorted(missing),'work':str(work),'game_dlls_unchanged':game_hashes,
        'apk_modified':False,'android_tested':False,
        'failed_stages':[s for s in result.splitlines() if s.startswith('FAIL_STAGE')],
        'passed_stages':[s.split('\t')[1] for s in result.splitlines() if s.startswith('PASS_STAGE')]}
    (output/'report.json').write_text(json.dumps(report,indent=2)+'\n')
    print(result)
    print('CANDIDATE_TABLES',len(changes),'MISSING_TYPES',sorted(missing))
    if report['failed_stages']: raise RuntimeError('Old content has failing stages; see probe.txt')
    assert 'LOADED\t28\tRAW_FAILURES\t1' in result
    assert len(missing) == 8
    assert report['passed_stages'] == ['106_rank_UserParamCreate','106_ConvertMasterSkillLimitBreak',
        '106_CreateUser_API','106_SelectDefaultParty_API','106_LoginPack_API','106_memory_save_roundtrip']
    assert 'Could not load' not in result, 'Test dependency unresolved'


if __name__ == '__main__': main()
