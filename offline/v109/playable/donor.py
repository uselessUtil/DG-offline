"""Read only the missing scratch tables from the hash-locked 1.0.6 APK."""
import hashlib
import zipfile
import UnityPy

SHA106 = 'ba3ccd42c28e4d9362cb6bda2b4d21483b98085441f9513ca1b825bc71be81c2'
TABLES = {
    'MasterDataGacha': '312994310d4826371986b69a3bb7944c2a75d40b5ad5259087fe2c6111e8fe6e',
    'MasterDataGachaAssign': 'fbb1d0b7a1e07d724ba20ae8f835232f24f98c4907c4d9d201e6b15b1477abd4',
}

def read(apk):
    assert hashlib.sha256(apk.read_bytes()).hexdigest() == SHA106
    result = {}
    with zipfile.ZipFile(apk) as z:
        def asset(name):
            name = 'assets/bin/Data/' + name
            if name in z.namelist():
                return z.read(name)
            chunks = []
            while name + '.split' + str(len(chunks)) in z.namelist():
                chunks.append(z.read(name + '.split' + str(len(chunks))))
            assert chunks, name
            return b''.join(chunks)
        main = UnityPy.load(asset('mainData'))
        resources = next(o for o in main.objects if o.type.name == 'ResourceManager')
        container = dict(resources.read_typetree()['m_Container'])
        for name, expected in TABLES.items():
            ref = container['carddm/masterdata/' + name.lower()]
            external = resources.assets_file.externals[ref['m_FileID'] - 1].path
            env = UnityPy.load(asset(external))
            raw = next(o for o in env.objects if o.path_id == ref['m_PathID']).read().m_Script
            if isinstance(raw, str):
                raw = raw.encode('utf8', 'surrogateescape')
            assert hashlib.sha256(raw).hexdigest() == expected
            result[name] = raw
    return result
