"""Recreate excluded game data from user-supplied, hash-locked local inputs."""
import hashlib
import json
import os
from pathlib import Path
import zipfile

ROOT = Path(__file__).resolve().parents[1]
SOURCE_SHA = 'ba3ccd42c28e4d9362cb6bda2b4d21483b98085441f9513ca1b825bc71be81c2'
CATALOG_SHA = '9d3631ede72090ae63eaa13efbfc470fd0b1888002a46d1dc157e51b77b07f0c'
TABLE_HASHES = {
    'MasterDataParamChara': '066a62103dd14401e6afcf394cae9fc7a991c6f42b9d26047a499990ca337c40',
    'MasterDataQuest': '49947c97346ea910ad5c7b6e6d2669d51363f85282dc3d19a43588323fb4b0c5',
    'MasterDataUserRank': 'c024b5410dcf096730eb38af0204dc21303da6ed4510e6bcbcc93be161119fc6',
}

def digest(data):
    return hashlib.sha256(data).hexdigest()

def canonical(value):
    return json.dumps(value, sort_keys=True, ensure_ascii=True, separators=(',', ':')).encode()

def read_tables(apk=None):
    import UnityPy
    apk = Path(apk or os.environ.get('DG_SOURCE_106', ROOT / 'ディバゲ_1.0.6.apk'))
    if not apk.is_file():
        raise FileNotFoundError(f'Provide the original 1.0.6 APK at {apk} or set DG_SOURCE_106; expected SHA-256 {SOURCE_SHA}')
    if digest(apk.read_bytes()) != SOURCE_SHA:
        raise ValueError('Unsupported 1.0.6 APK: SHA-256 mismatch')
    result = {}
    with zipfile.ZipFile(apk) as archive:
        names = set(archive.namelist())
        def asset(name):
            name = 'assets/bin/Data/' + name
            if name in names:
                return archive.read(name)
            parts = []
            while name + '.split' + str(len(parts)) in names:
                parts.append(archive.read(name + '.split' + str(len(parts))))
            if not parts:
                raise ValueError('Missing Unity asset: ' + name)
            return b''.join(parts)
        main = UnityPy.load(asset('mainData'))
        resource = next(o for o in main.objects if o.type.name == 'ResourceManager')
        refs = dict(resource.read_typetree()['m_Container'])
        for name, expected in TABLE_HASHES.items():
            ref = refs['carddm/masterdata/' + name.lower()]
            env = UnityPy.load(asset(resource.assets_file.externals[ref['m_FileID'] - 1].path))
            raw = next(o for o in env.objects if o.path_id == ref['m_PathID']).read().m_Script
            if isinstance(raw, str):
                raw = raw.encode('utf8', 'surrogateescape')
            if digest(raw) != expected:
                raise ValueError('Source table fingerprint mismatch: ' + name)
            result[name] = raw
    return result

def legacy106_catalog(apk=None):
    tables = read_tables(apk)
    catalog = {
        'prototype': '1.0.6 offline prototype 22r7',
        'apk_sha256': '6f8374a4d7c1f51d109d2aac55e05cc562d651b4dcfabfe24ff7e74a03e463e0',
    }
    for name, group, fields in [
        ('MasterDataParamChara', 'characters', ('fix_id', 'name', 'level_max')),
        ('MasterDataQuest', 'quests', ('fix_id', 'area_id', 'quest_name')),
    ]:
        catalog[group] = [{k: row[k] for k in fields}
                          for row in json.loads(tables[name], strict=False) if row and row['fix_id']]
    catalog['rank_sha256'] = digest(tables['MasterDataUserRank'])
    if digest(canonical(catalog)) != CATALOG_SHA:
        raise ValueError('Recovered catalog differs from the fixed migration contract')
    return catalog

if __name__ == '__main__':
    catalog = legacy106_catalog()
    print(json.dumps({'characters': len(catalog['characters']), 'quests': len(catalog['quests']),
                      'catalog_sha256': digest(canonical(catalog))}, indent=2))
