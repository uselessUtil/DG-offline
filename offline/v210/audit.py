"""Read-only 2.1.0 inventory; execute in the isolated project tool environment."""
import hashlib
import json
from pathlib import Path
import subprocess
import zipfile

import UnityPy

ROOT = Path('/workspace')
SOURCE = ROOT/'ディバゲ_2.1.0.apk'
SHA = 'af40f477e5455caba4f4bbfa24d6fde94e174fc4614db39025c9411be207bb29'
OUT = ROOT/'artifacts/2.1.0-port-audit'
WORK = Path('/tmp/dg210-audit')
CECIL = '/usr/lib/mono/gac/Mono.Cecil/0.11.1.0__0738eb9f132ed756/Mono.Cecil.dll'


def run(*args):
    return subprocess.check_output(list(map(str, args)), text=True)


def main():
    assert hashlib.sha256(SOURCE.read_bytes()).hexdigest() == SHA
    OUT.mkdir(parents=True, exist_ok=True)
    WORK.mkdir(exist_ok=True)
    with zipfile.ZipFile(SOURCE) as archive:
        assert archive.testzip() is None
        assert len(archive.namelist()) == len(set(archive.namelist()))
        assert not any(n.startswith('/') or '..' in Path(n).parts for n in archive.namelist())
        archive.extractall(WORK/'original')
        (OUT/'files.json').write_text(json.dumps([
            {'name':i.filename,'size':i.file_size,'sha256':hashlib.sha256(archive.read(i)).hexdigest()}
            for i in archive.infolist()], indent=2))
    for name, args in [('manifest.txt', ['aapt','dump','xmltree',SOURCE,'AndroidManifest.xml']),
                       ('badging.txt', ['aapt','dump','badging',SOURCE]),
                       ('signature.txt', ['apksigner','verify','--verbose','--print-certs',SOURCE])]:
        (OUT/name).write_text(run(*args))
    env = UnityPy.load(str(WORK/'original/assets/bin/Data'))
    tables, text_assets, counts, versions = {}, [], {}, set()
    (WORK/'tables').mkdir(exist_ok=True)
    for obj in env.objects:
        versions.add(str(obj.assets_file.unity_version))
        counts[obj.type.name] = counts.get(obj.type.name,0)+1
        if obj.type.name != 'TextAsset':
            continue
        value = obj.read()
        text_assets.append({'name':value.m_Name,'file':obj.assets_file.name,'path_id':obj.path_id})
        if not value.m_Name.startswith('MasterData'):
            continue
        raw = value.m_Script
        if isinstance(raw,str): raw = raw.encode('utf8','surrogateescape')
        (WORK/'tables'/(value.m_Name+'.json')).write_bytes(raw)
        tables[value.m_Name] = len(json.loads(raw,strict=False))
    managed = WORK/'original/assets/bin/Data/Managed/Assembly-CSharp.dll'
    run('mcs','-out:'+str(WORK/'Inspect.exe'),'-r:'+CECIL,ROOT/'offline/v210/Inspect.cs')
    inventory = run('mono',WORK/'Inspect.exe',managed)
    (OUT/'backend-inventory.tsv').write_text(inventory)
    resources, schemas, handlers = [], {}, []
    for line in inventory.splitlines():
        p = line.split('\t')
        if p[0] == 'resource': resources.append(p[1])
        elif p[0] == 'table': schemas[p[2]] = {'server_field':p[1],'columns':{}}
        elif p[0] == 'column': schemas[p[1]]['columns'][p[2]] = p[3]
        elif p[0] == 'handler': handlers.append({'method':p[1],'il_instructions':int(p[2])})
    report = {'source_sha256':SHA,'source_size':SOURCE.stat().st_size,
              'unity_versions':sorted(versions),'object_counts':counts,'embedded_tables':tables,
              'text_assets':text_assets,'required_resources':resources,'schemas':schemas,
              'handlers':handlers,'work':str(WORK)}
    (OUT/'inventory.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n')
    print(json.dumps({k:v for k,v in report.items() if k not in ['text_assets','schemas','handlers']},indent=2),flush=True)
    dest = WORK/'decompiled'
    dest.mkdir(exist_ok=True)
    for name in ['ServerForgery','ServerDataManager','ResidentManager','MasterDataManager',
                 'TitleMenuManager','StoreManager','PatchManager','AssetBundleManager',
                 'LocalSaveManager','ServerForgerySaveUser','TitleMenuSeqLoadAssetBundle']:
        (dest/(name+'.cs')).write_text(run('/opt/dotnet/dotnet',
            '/opt/ilspy/tools/net8.0/any/ilspycmd.dll','-t',name,managed))


if __name__ == '__main__': main()
