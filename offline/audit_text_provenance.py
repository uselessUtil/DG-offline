"""Compare maintained supplemental text with hash-locked APK resident text.

This checks content correspondence, not legal authorship. Commit/documentation
evidence remains necessary for the classification of independently authored text.
Only keys, counts and hashes are written to the report, never resident text.
"""
import argparse
import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
from text_sources import SOURCES, resident_text

def audit():
    report = {}
    for version, (filename, expected, separator) in SOURCES.items():
        resident = resident_text(ROOT / filename, expected, separator)
        group = {'apk_sha256': expected, 'resident_keys': len(resident), 'files': {}}
        for path in sorted((ROOT / 'offline' / version / 'playable').glob('text-*.json')):
            if path.name == 'text-audit-exclusions.json':
                continue
            raw = path.read_bytes()
            data = json.loads(raw)
            if any(v is None and k not in resident for k, v in data.items()):
                raise ValueError('Unresolved resident placeholder: ' + str(path))
            group['files'][path.name] = {
                'entries': len(data), 'sha256': hashlib.sha256(raw).hexdigest(),
                'source_resident': [k for k, v in data.items() if v is None and k in resident],
                'same_resident': [k for k, v in data.items() if v is not None and k in resident and v == resident[k]],
                'changed_resident': [k for k, v in data.items() if v is not None and k in resident and v != resident[k]],
                'absent_resident': [k for k in data if k not in resident],
            }
        report[version] = group
    return report

if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    report = audit()
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(report, ensure_ascii=False, indent=2) + '\n')
    for version, group in report.items():
        for name, item in group['files'].items():
            print(version, name, {k: len(v) if isinstance(v, list) else v for k, v in item.items() if k != 'sha256'})
