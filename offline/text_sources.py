"""Read resident text locally and resolve explicit null placeholders. No game text is embedded."""
import hashlib
import json
from pathlib import Path
import re
import zipfile
import UnityPy
ROOT = Path(__file__).resolve().parents[1]
SOURCES = {
    'v290': ('jp.gungho.dg-v2.9.0.apk', '3c6ce05ca09837b07faaf99aa80716cc2754eb22887ca580d17e8c86559f2cc4', ','),
    'v291': ('jp.gungho.dg-291.apk', '3832bed7dee0530ff821c141622bc518d335dcab9ee664caa321425490968136', ','),
    'v400': ('ディバゲ_4.0.0(紀念)-v20220207073837.apk', 'b3f2a366d553d0ab67f2c6fee76bde79dceb3a372f7ed64c3de4849fc1d9600e', '\t'),
}
def resident_text(apk, expected, separator):
    if hashlib.sha256(apk.read_bytes()).hexdigest() != expected:
        raise ValueError('Source APK fingerprint mismatch: ' + str(apk))
    databases = []
    with zipfile.ZipFile(apk) as archive:
        names = set(archive.namelist())
        bases = sorted({re.sub(r'\.split\d+$', '', name) for name in names
                        if re.fullmatch(r'assets/bin/Data/(?:[^/]+\.assets|[0-9a-f]{32})(?:\.split\d+)?', name)})
        for base in bases:
            if base in names:
                raw = archive.read(base)
            else:
                chunks = []
                while base + '.split' + str(len(chunks)) in names:
                    chunks.append(archive.read(base + '.split' + str(len(chunks))))
                raw = b''.join(chunks)
            for obj in UnityPy.load(raw).objects:
                if obj.type.name != 'TextAsset':
                    continue
                asset = obj.read()
                if asset.m_Name != 'TextDatabase':
                    continue
                text = asset.m_Script
                if isinstance(text, bytes):
                    text = text.decode('utf-8')
                parts = text.split(separator)
                count = int(parts[0])
                if len(parts) < 1 + 2 * count:
                    raise ValueError('Invalid resident text database')
                databases.append(dict(zip(parts[1:1+2*count:2], parts[2:1+2*count:2])))
    if len(databases) != 1:
        raise ValueError('Expected one resident TextDatabase: ' + str(apk))
    return databases[0]

def supplemental_text(path, version, apk=None):
    data = json.loads(Path(path).read_text())
    if any(value is None for value in data.values()):
        filename, expected, separator = SOURCES[version]
        resident = resident_text(Path(apk or ROOT / filename), expected, separator)
        for key, value in data.items():
            if value is None:
                if key not in resident:
                    raise ValueError('Missing source text key: ' + key)
                data[key] = resident[key]
    if not all(isinstance(value, str) for value in data.values()):
        raise ValueError('Supplemental text must contain strings or resident null placeholders')
    return data
