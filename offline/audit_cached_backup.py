"""Read-only audit of an Android UnityCache against the archived 1.0.6 data.

Run in the isolated project tool environment. Original files are never rewritten or executed.
"""
import argparse
import collections
import csv
from datetime import datetime, timezone
import hashlib
import json
from pathlib import Path
import struct

import UnityPy
from UnityPy.files.SerializedFile import SerializedFile


def sha(path):
    h = hashlib.sha256()
    with path.open('rb') as stream:
        for block in iter(lambda: stream.read(1024*1024), b''):
            h.update(block)
    return h.hexdigest()


def write(path, value):
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2)+'\n')


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('backup', type=Path)
    parser.add_argument('--tables', type=Path, required=True)
    parser.add_argument('--original-data', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    output = args.output
    output.mkdir(parents=True, exist_ok=True)
    if (output/'inventory.json').exists():
        raise FileExistsError('Use a new audit directory')
    files = sorted(p for p in args.backup.rglob('*') if p.is_file())
    manifest = [{'path':str(p.relative_to(args.backup)), 'size':p.stat().st_size, 'sha256':sha(p)} for p in files]
    write(output/'source-files.json', manifest)
    print('Hashed',len(files),'source files',flush=True)
    original_scripts = {}
    original = UnityPy.load(str(args.original_data))
    for obj in original.objects:
        if obj.type.name == 'MonoScript':
            data = obj.read()
            original_scripts[data.m_ClassName] = data.m_PropertiesHash
    del original
    priority = (Path('/workspace/artifacts/missing-image-audit-2026-10-20/priority-image-filenames.txt')
                .read_text().splitlines()) + ['lock1.png','chara_icon_empty.png']
    priority = [Path(n).stem for n in priority if n.strip()]
    bundles, objects, errors, compatibility, raw_hits = [], [], [], [], []
    parse_version = SerializedFile.set_version

    def cache_version(self, value):
        # These Unity 4 caches store "4.1.0f4\n1" in the version field.
        # Only parser schema selection is normalized; input bytes stay intact.
        return parse_version(self, value.splitlines()[0])

    SerializedFile.set_version = cache_version
    try:
        cabs = [p for p in files if p.name.startswith('CAB-')]
        for index, path in enumerate(cabs):
            relative = str(path.relative_to(args.backup))
            raw = path.read_bytes()
            header_version = raw[20:raw.index(b'\0',20)].decode('ascii')
            row = {'path':relative, 'sha256':sha(path), 'size':len(raw),
                   'raw_unity_version':header_version, 'types':{}, 'containers':[], 'dependencies':[]}
            info = path.parent/'__info'
            if info.exists():
                lines = info.read_text().splitlines()
                if len(lines)>1 and lines[0]=='-1' and lines[1].isdigit():
                    row['cache_unix_timestamp'] = int(lines[1])
                    row['cache_time_utc'] = datetime.fromtimestamp(int(lines[1]),timezone.utc).isoformat()
            try:
                env = UnityPy.load(raw)
                row['types'] = dict(collections.Counter(o.type.name for o in env.objects))
                for asset in env.files.values():
                    row['unity_version'] = asset.unity_version
                    row['platform'] = int(asset.target_platform)
                    for external in asset.externals:
                        row['dependencies'].append(external.path)
                for obj in env.objects:
                    kind = obj.type.name
                    entry = {'cab':relative,'path_id':obj.path_id,'type':kind}
                    if kind == 'AssetBundle':
                        tree = obj.read_typetree(check_read=False)
                        row['containers'] = [{'name':n, **value} for n,value in tree['m_Container']]
                        for script in tree.get('m_ScriptCompatibility',[]):
                            expected = original_scripts.get(script['className'])
                            compatibility.append({'cab':relative, **script,'original_106_hash':expected,
                                                  'matches_106':expected == script['hash']})
                    elif kind == 'MonoBehaviour':
                        tree = obj.read_typetree(check_read=False)
                        entry.update(name=tree.get('m_Name',''),game_object=tree.get('m_GameObject'),
                                     script=tree.get('m_Script'),raw_size=len(obj.get_raw_data()))
                        binary = obj.get_raw_data()
                        for name in priority:
                            token = name.encode()
                            start = binary.find(token)
                            if start >= 4 and binary[start-4:start] == struct.pack('<I',len(token)):
                                raw_hits.append({'name':name,'cab':relative,'path_id':obj.path_id,'offset':start})
                    elif kind in ('GameObject','Texture2D','Mesh','AnimationClip','Material','Shader','AudioClip','TextAsset','MonoScript'):
                        try:
                            data = obj.read()
                            entry['name'] = getattr(data,'m_Name','')
                            if kind == 'Texture2D':
                                entry.update(width=data.m_Width,height=data.m_Height,format=int(data.m_TextureFormat))
                            elif kind == 'Mesh':
                                entry.update(vertices=getattr(getattr(data,'m_VertexData',None),'m_VertexCount',None),
                                             submeshes=len(getattr(data,'m_SubMeshes',[])))
                            elif kind == 'MonoScript':
                                entry.update(class_name=data.m_ClassName,properties_hash=data.m_PropertiesHash,
                                             matches_106=original_scripts.get(data.m_ClassName)==data.m_PropertiesHash)
                            elif kind == 'GameObject':
                                entry['components'] = len(data.m_Component)
                            elif kind == 'TextAsset':
                                entry['text_bytes'] = len(data.m_Script)
                        except Exception as ex:
                            entry['read_error'] = type(ex).__name__+': '+str(ex)[:250]
                            errors.append(entry)
                    objects.append(entry)
                del env
            except Exception as ex:
                row['load_error'] = type(ex).__name__+': '+str(ex)[:300]
                errors.append({'cab':relative,'load_error':row['load_error']})
            bundles.append(row)
            if (index+1)%10 == 0:
                print('Parsed',index+1,'/',len(cabs),'cache assets',flush=True)
    finally:
        SerializedFile.set_version = parse_version
    names = collections.defaultdict(list)
    for obj in objects:
        if obj.get('name'):
            names[(obj['type'],obj['name'].lower())].append(obj)
    areas = json.loads((args.tables/'MasterDataArea.json').read_bytes(),strict=False)
    chars = json.loads((args.tables/'MasterDataParamChara.json').read_bytes(),strict=False)
    area_coverage = []
    for area in areas:
        if not area['fix_id']:
            continue
        required = {'texture':('Texture2D',area['res_map']), 'floor':('GameObject',area['mesh_map']),
                    'door':('GameObject',area['mesh_door']), 'boss_door':('GameObject',area['mesh_door_boss'])}
        area_coverage.append({'area_id':area['fix_id'],'name':area['area_name'],'type':area['type'],
            'requirements':{key:{'name':name,'matches':names.get((kind,name.lower()),[])}
                            for key,(kind,name) in required.items()}})
    character_coverage = []
    for char in chars:
        if char['fix_id']:
            name = 'chara_prefab_'+char['res_chara_id_str']
            character_coverage.append({'fix_id':char['fix_id'],'resource_id':char['res_chara_id_str'],
                                       'name':char['name'],'matches':names.get(('GameObject',name.lower()),[])})
    unchanged = all((args.backup/r['path']).stat().st_size==r['size'] and
                    sha(args.backup/r['path'])==r['sha256'] for r in manifest)
    times = [r['cache_time_utc'] for r in bundles if r.get('cache_time_utc')]
    summary = {'source':str(args.backup),'source_files':len(files),'source_bytes':sum(r['size'] for r in manifest),
               'cache_assets':len(bundles),'loaded_assets':sum('load_error' not in r for r in bundles),
               'object_types':dict(collections.Counter(o['type'] for o in objects)),
               'unity_versions':sorted({r.get('unity_version','unreadable') for r in bundles}),
               'cache_time_utc_range':[min(times),max(times)] if times else [],
               'read_errors':len(errors),'script_compatibility_records':len(compatibility),
               'script_compatibility_matches':sum(r['matches_106'] for r in compatibility),
               'area_count':len(area_coverage),'area_coverage':{k:sum(bool(r['requirements'][k]['matches']) for r in area_coverage)
                                                            for k in ['texture','floor','door','boss_door']},
               'area_requirements':{k:sum(bool(r['requirements'][k]['name']) for r in area_coverage)
                                    for k in ['texture','floor','door','boss_door']},
               'area_requirement_note':'boss_door has no non-empty requirements in 1.0.6; zero matches does not mean 101 missing boss doors.',
               'character_prefabs':sum(bool(r['matches']) for r in character_coverage),
               'character_count':len(character_coverage),'priority_raw_name_hits':sorted({r['name'] for r in raw_hits}),
               'source_hashes_unchanged':unchanged,'apk_modified':False}
    for name,value in [('inventory.json',bundles),('objects.json',objects),('parse-errors.json',errors),
                       ('script-compatibility.json',compatibility),('sprite-name-raw-hits.json',raw_hits),
                       ('area-coverage.json',area_coverage),('character-coverage.json',character_coverage),('summary.json',summary)]:
        write(output/name,value)
    with (output/'objects.csv').open('w',newline='') as stream:
        writer = csv.DictWriter(stream,fieldnames=['cab','path_id','type','name','width','height','format','vertices','submeshes'],extrasaction='ignore')
        writer.writeheader();writer.writerows(objects)
    if not unchanged:
        raise ValueError('Source files changed during audit')
    print(json.dumps(summary,ensure_ascii=False,indent=2))


if __name__ == '__main__':
    main()
