"""Read-only comparison of the three original APKs. Run in the isolated project tool environment.

Raw extraction stays in /tmp. Reports go to a new artifacts directory.
Schema equality means field layout only, not identical data or behavior.
"""
import argparse
from collections import Counter
import hashlib
import json
from pathlib import Path
import subprocess
import tempfile
import zipfile

import UnityPy

ROOT = Path(__file__).resolve().parents[2]
CECIL = '/usr/lib/mono/gac/Mono.Cecil/0.11.1.0__0738eb9f132ed756/Mono.Cecil.dll'
MANAGED = 'assets/bin/Data/Managed/'
SOURCES = {
    '1.0.6': ('ディバゲ_1.0.6.apk', 'ba3ccd42c28e4d9362cb6bda2b4d21483b98085441f9513ca1b825bc71be81c2'),
    '2.1.0': ('ディバゲ_2.1.0.apk', 'af40f477e5455caba4f4bbfa24d6fde94e174fc4614db39025c9411be207bb29'),
    '2.9.0': ('jp.gungho.dg-v2.9.0.apk', '3c6ce05ca09837b07faaf99aa80716cc2754eb22887ca580d17e8c86559f2cc4'),
}


def run(*args):
    return subprocess.check_output(list(map(str, args)), text=True)


def difference(before, after):
    return {
        'added': {k: after[k] for k in sorted(after.keys() - before.keys())},
        'removed': {k: before[k] for k in sorted(before.keys() - after.keys())},
        'changed': {k: [before[k], after[k]] for k in sorted(before.keys() & after.keys()) if before[k] != after[k]},
    }


def inspect(executable, dll):
    data = {'types': {}, 'constants': {}, 'tables': {}, 'resources': [], 'dispatch': []}
    raw = run('mono', executable, dll)
    for line in raw.splitlines():
        p = line.split('\t')
        if p[0] == 'type': data['types'][p[1]] = {}
        elif p[0] == 'field': data['types'][p[1]][p[2]] = p[3]
        elif p[0] == 'constant': data['constants'].setdefault(p[1], {})[p[2]] = p[3]
        elif p[0] == 'table': data['tables'][p[1]] = p[2]
        elif p[0] == 'resource': data['resources'].append(p[1].rsplit('/', 1)[1])
        elif p[0] == 'dispatch': data['dispatch'].append(p[1])
    data['dispatch'] = sorted(set(data['dispatch']))
    return data, raw


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    out = args.output.resolve()
    out.mkdir(parents=True, exist_ok=False)
    work = Path(tempfile.mkdtemp(prefix='dg-version-comparison-', dir='/tmp'))
    executable = work/'Inspect.exe'
    run('mcs', '-out:'+str(executable), '-r:'+CECIL, Path(__file__).with_name('Inspect.cs'))
    versions = {}
    for version, (filename, expected) in SOURCES.items():
        source = ROOT/filename
        digest = hashlib.sha256(source.read_bytes()).hexdigest()
        assert digest == expected, version
        dest = work/version
        with zipfile.ZipFile(source) as archive:
            assert archive.testzip() is None, version
            assert len(archive.namelist()) == len(set(archive.namelist()))
            names = [n for n in archive.namelist() if n.startswith('assets/bin/Data/') and not n.endswith('/')]
            assert all('..' not in Path(n).parts for n in names)
            for name in names: archive.extract(name, dest)
        metadata, raw = inspect(executable, dest/MANAGED/'Assembly-CSharp.dll')
        (out/(version+'-metadata.tsv')).write_text(raw)
        env = UnityPy.load(str(dest/'assets/bin/Data'))
        counts, unity, tables = Counter(), set(), {}
        table_dir = dest/'tables'
        table_dir.mkdir()
        for obj in env.objects:
            counts[obj.type.name] += 1
            unity.add(str(obj.assets_file.unity_version))
            if obj.type.name != 'TextAsset': continue
            value = obj.read()
            if not value.m_Name.startswith('MasterData'): continue
            script = value.m_Script
            if isinstance(script, str): script = script.encode('utf8', 'surrogateescape')
            rows = json.loads(script, strict=False)
            is_rows = isinstance(rows, list) and all(isinstance(r, dict) for r in rows)
            tables[value.m_Name] = {'rows': len(rows) if is_rows else None,
                                    'json_kind': type(rows).__name__,
                                    'columns': sorted(set().union(*(r.keys() for r in rows))) if is_rows else []}
            (table_dir/(value.m_Name+'.json')).write_bytes(script)
        metadata.update(source=filename, sha256=digest, size=source.stat().st_size,
                        unity_versions=sorted(unity), object_counts=dict(counts), embedded_tables=tables,
                        badging=run('aapt', 'dump', 'badging', source), work=str(dest))
        versions[version] = metadata
        print(version, 'tables:', len(tables), 'required:', len(metadata['resources']),
              'Unity:', sorted(unity), flush=True)
    base = versions['1.0.6']
    comparisons = {}
    for version in ['2.1.0', '2.9.0']:
        target = versions[version]
        table_diffs = {}
        for name in sorted(set(target['tables'].values())):
            before = base['types'].get(name)
            after = target['types'][name]
            table_diffs[name] = {'status': 'new' if before is None else 'same' if before == after else 'changed',
                                 **difference(before or {}, after)}
        constants = {name: difference(base['constants'].get(name, {}), values)
                     for name, values in target['constants'].items()}
        constants = {k: v for k, v in constants.items() if any(v.values())}
        comparisons[version] = {
            'table_schemas': table_diffs,
            'schema_counts': dict(Counter(d['status'] for d in table_diffs.values())),
            'old_resource_names_available': sorted(set(target['resources']) & base['embedded_tables'].keys()),
            'missing_even_in_106': sorted(set(target['resources']) - base['embedded_tables'].keys()),
            'constant_diffs': constants,
            'save_diff': difference(base['types']['ServerForgerySaveUser'], target['types']['ServerForgerySaveUser']),
            'dispatch_added': sorted(set(target['dispatch']) - set(base['dispatch'])),
            'dispatch_removed': sorted(set(base['dispatch']) - set(target['dispatch'])),
        }
        print(version, json.dumps({k: v for k, v in comparisons[version].items()
                                  if k in ['schema_counts', 'missing_even_in_106', 'dispatch_added']}, ensure_ascii=False), flush=True)
    a, b = versions['2.1.0'], versions['2.9.0']
    later_diff = {name: {'status': 'new' if name not in a['types'] else
                        'same' if a['types'][name] == b['types'][name] else 'changed',
                        **difference(a['types'].get(name, {}), b['types'][name])}
                  for name in sorted(set(b['tables'].values()))}
    between_later = {'table_schemas': later_diff,
                     'schema_counts': dict(Counter(x['status'] for x in later_diff.values())),
                     'new_resources': sorted(set(b['resources']) - set(a['resources']))}
    (out/'comparison.json').write_text(json.dumps({'versions': versions, 'comparisons': comparisons,
        '2.1.0_to_2.9.0': between_later,
        'work': str(work), 'scope': 'Static metadata and original embedded data; no Android execution'}, ensure_ascii=False, indent=2)+'\n')
    print('Report:', out/'comparison.json', flush=True)


if __name__ == '__main__': main()
