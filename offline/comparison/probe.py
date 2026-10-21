"""Try 1.0.6 rows in original newer backends, with native engine I/O stubbed.

Only unknown JSON columns are removed and known resource/type aliases applied.
No missing tables or added field values are fabricated. This is not an APK build.
Run compare.py first; extracted inputs and experiment DLLs stay in /tmp.
"""
import argparse
import json
from pathlib import Path
import shutil
import subprocess
import tempfile

ROOT = Path(__file__).resolve().parents[2]
CECIL = '/usr/lib/mono/gac/Mono.Cecil/0.11.1.0__0738eb9f132ed756/Mono.Cecil.dll'
MANAGED = 'assets/bin/Data/Managed'


def run(*args):
    return subprocess.check_output(list(map(str, args)), text=True, timeout=60)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('comparison', type=Path)
    args = parser.parse_args()
    report = json.loads(args.comparison.read_text())
    work = Path(report['work'])
    prepare = work/'PrepareEngine.exe'
    run('mcs', '-out:'+str(prepare), '-r:'+CECIL, ROOT/'offline/v210/PrepareTestEngine.cs')
    prefs = work/'PreparePrefs.exe'
    run('mcs', '-out:'+str(prefs), '-r:'+CECIL, Path(__file__).with_name('PreparePrefs.cs'))
    source = Path(report['versions']['1.0.6']['work'])/'tables'
    for version in ['2.1.0', '2.9.0']:
        target = report['versions'][version]
        probe = Path(tempfile.mkdtemp(prefix='probe-', dir=work/version))
        original = Path(target['work'])/MANAGED
        for library in original.glob('*.dll'):
            if library.name != 'UnityEngine.dll': shutil.copy2(library, probe/library.name)
        run('mono', prepare, original/'UnityEngine.dll', probe/'EngineDefault.dll')
        run('mcs', '-target:library', '-out:'+str(probe/'MemoryPrefs.dll'), Path(__file__).with_name('MemoryPrefs.cs'))
        run('mono', prefs, probe/'EngineDefault.dll', probe/'MemoryPrefs.dll', probe/'UnityEngine.dll')
        converted, raw = probe/'converted', probe/'raw'
        converted.mkdir(); raw.mkdir()
        changes = {}
        for name in set(target['tables'].values()):
            alias = {'MasterDataParamEnemy': 'MasterDataEnemyChara',
                     'MasterDataEventBase': 'MasterDataEvent'}.get(name, name)
            filename = source/(alias+'.json')
            if not filename.exists(): continue
            rows = json.loads(filename.read_bytes(), strict=False)
            assert isinstance(rows, list)
            columns = target['types'][name]
            removed = sorted(set().union(*(r.keys() for r in rows)) - columns.keys())
            filtered = [{k: v for k, v in row.items() if k in columns} for row in rows]
            (raw/(name+'.json')).write_text(json.dumps(rows, ensure_ascii=False))
            (converted/(name+'.json')).write_text(json.dumps(filtered, ensure_ascii=False))
            changes[name] = {'source': alias, 'rows': len(rows), 'removed_columns': removed}
        run('mcs', '-out:'+str(probe/'Probe.exe'), '-r:'+str(probe/'Assembly-CSharp.dll'),
            '-r:'+str(probe/'Assembly-CSharp-firstpass.dll'),
            '-r:'+str(probe/'UnityEngine.dll'), Path(__file__).with_name('Probe.cs'))
        result = run('mono', probe/'Probe.exe', converted, raw)
        assert 'LOADED\t28\tRAW_FAILURES\t2' in result
        assert 'EXPECTED_FAIL\tbaseline_no_tables_UserParamCreate\tNullReferenceException' in result
        for stage in ['106_rank_UserParamCreate', '106_ConvertMasterSkillLimitBreak',
                      '106_CreateUser_API', '106_SelectDefaultParty_API', '106_LoginPack_API',
                      '106_memory_save_roundtrip']:
            assert 'PASS_STAGE\t'+stage+'\n' in result, stage
        for name in ['Assembly-CSharp.dll', 'Assembly-CSharp-firstpass.dll']:
            assert (original/name).read_bytes() == (probe/name).read_bytes()
        (args.comparison.parent/(version+'-transplant-probe.txt')).write_text(result)
        (args.comparison.parent/(version+'-conversion.json')).write_text(json.dumps(changes, indent=2)+'\n')
        print(version+'\n'+result, flush=True)


if __name__ == '__main__': main()
