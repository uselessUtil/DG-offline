"""Download missing map variants from the already pinned DGDB Git tree, in Docker."""
import concurrent.futures
import hashlib
import io
import json
from pathlib import Path
import time
import urllib.request

from PIL import Image

ROOT = Path('/workspace')
SOURCE = ROOT / 'artifacts/web-asset-search-2026-10-20'
OUTPUT = ROOT / 'artifacts/dgdb-name-mapping-2026-10-20'


def main():
    OUTPUT.mkdir(exist_ok=True)
    commit = json.loads((SOURCE / 'dgdb-commit.json').read_text())['commit']
    tree = {r['path']: r for r in json.loads(
        (SOURCE / 'kennyfong19931-dgdb-tree.json').read_text())['tree']}
    existing = {r['path']: r for r in json.loads(
        (SOURCE / 'dgdb-downloaded-assets.json').read_text())}
    audit = json.loads((ROOT / 'artifacts/missing-image-audit-2026-10-20/summary.json').read_text())
    maps = audit['original_map_texture_names']
    paths = [f'public/img/area/{name}_{part}.png' for name in maps for part in ['lower', 'upper']]
    # These status icons are candidates for the missing field panels, NOT aliases:
    # their dimensions and presentation differ from the original field atlas.
    paths += ['public/img/icon/icon_heal_' + name + '.png'
              for name in ['happytreasure', 'moneyup', 'removetrap', 'torch']]

    def fetch(path):
        entry = tree[path]
        local = (ROOT / existing[path]['local_path'] if path in existing else
                 SOURCE / 'recovered-dgdb' / path)
        url = f'https://raw.githubusercontent.com/kennyfong19931/dgdb/{commit}/{path}'
        if local.exists():
            raw = local.read_bytes()
        else:
            for attempt in range(3):
                try:
                    with urllib.request.urlopen(url, timeout=45) as response:
                        raw = response.read()
                    break
                except Exception:
                    if attempt == 2:
                        raise
                    time.sleep(attempt + 1)
        blob = hashlib.sha1(b'blob ' + str(len(raw)).encode() + b'\0' + raw).hexdigest()
        if blob != entry['sha'] or len(raw) != entry['size']:
            raise ValueError('Pinned Git blob mismatch: ' + path)
        image = Image.open(io.BytesIO(raw))
        image.load()
        if image.format != 'PNG':
            raise ValueError('Not PNG: ' + path)
        local.parent.mkdir(parents=True, exist_ok=True)
        if not local.exists():
            local.write_bytes(raw)
        return {'path': path, 'url': url, 'bytes': len(raw),
                'sha256': hashlib.sha256(raw).hexdigest(), 'git_blob_sha1': blob,
                'git_blob_matches': True, 'format': image.format,
                'width': image.width, 'height': image.height, 'mode': image.mode,
                'has_transparency': image.convert('RGBA').getchannel('A').getextrema()[0] < 255,
                'local_path': str(local.relative_to(ROOT))}

    with concurrent.futures.ThreadPoolExecutor(max_workers=6) as pool:
        records = list(pool.map(fetch, paths))
    additional = sorted([r for r in records if r['path'] not in existing], key=lambda r: r['path'])
    (SOURCE / 'dgdb-additional-assets.json').write_text(json.dumps(additional, indent=2))
    report = {'source': 'https://github.com/kennyfong19931/dgdb', 'commit': commit,
              'required_map_variants': len(maps), 'verified_split_map_images': len(maps) * 2,
              'new_map_images': sum('/area/' in r['path'] for r in additional),
              'new_status_icon_candidates': sum('/icon/' in r['path'] for r in additional),
              'additional_bytes': sum(r['bytes'] for r in additional),
              'verified_files': records}
    (OUTPUT / 'download-report.json').write_text(json.dumps(report, indent=2))
    print(json.dumps({k: v for k, v in report.items() if k != 'verified_files'}))


if __name__ == '__main__':
    main()
