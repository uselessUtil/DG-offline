"""Read-only feasibility audit; does not modify APKs, cache CABs or runtime code."""
import argparse
import collections
import hashlib
import json
from pathlib import Path
import struct

import UnityPy
from UnityPy.files.SerializedFile import SerializedFile


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--backup', type=Path, required=True)
    parser.add_argument('--audit', type=Path, required=True)
    parser.add_argument('--tables', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    args.output.mkdir(parents=True,exist_ok=True)
    def read(name):
        return json.loads((args.tables/(name+'.json')).read_bytes(),strict=False)
    areas = [a for a in read('MasterDataArea') if a['fix_id']]
    chars = [c for c in read('MasterDataParamChara') if c['fix_id']]
    inventory = json.loads((args.audit/'inventory.json').read_text())
    hashes = json.loads((args.audit/'source-files.json').read_text())
    def unchanged():
        return all(hashlib.sha256((args.backup/r['path']).read_bytes()).hexdigest()==r['sha256'] for r in hashes)
    assert unchanged()
    native, characters, errors = {}, {}, []
    counts = collections.Counter()
    previous = SerializedFile.set_version
    SerializedFile.set_version = lambda self,s:previous(self,s.splitlines()[0])
    try:
        for bundle in inventory:
            cnames = {r['name'] for r in bundle['containers']}
            if not any(n.startswith(('map_','chara_prefab_')) for n in cnames):
                continue
            cab = bundle['path']
            env = UnityPy.load((args.backup/cab).read_bytes())
            objects = {o.path_id:o for o in env.objects}
            def tree(obj):
                return obj.read_typetree(check_read=False)
            def resolve(ptr, kind=None):
                if ptr['m_FileID'] or not ptr['m_PathID']:
                    raise ValueError('Missing/external required object '+str(ptr))
                o = objects[ptr['m_PathID']]
                if kind and o.type.name != kind:
                    raise ValueError('Expected '+kind+', got '+o.type.name)
                return o
            def nodes(root):
                result = []
                def visit(go, path):
                    value = tree(go)
                    components = {kind:resolve(ptr) for kind,ptr in value['m_Component']}
                    result.append((path,value,components))
                    transform = tree(components[4])
                    for p in transform['m_Children']:
                        child = resolve(tree(resolve(p,'Transform'))['m_GameObject'],'GameObject')
                        visit(child,(path+'/' if path else '')+tree(child)['m_Name'])
                visit(root,'')
                return result
            def geometry(graph):
                result = []
                for path,go,components in graph:
                    if 33 not in components:
                        continue
                    mesh = resolve(tree(components[33])['m_Mesh'],'Mesh').read()
                    vd = mesh.m_VertexData
                    uv = vd.m_Channels[3]
                    stream = vd.m_Streams[uv.stream]
                    assert uv.dimension >= 2 and uv.format in (0,1)
                    uv_format = '<2f' if uv.format == 0 else '<2e'
                    uvs = [struct.unpack_from(uv_format,vd.m_DataSize,stream.offset+i*stream.stride+uv.offset)
                           for i in range(vd.m_VertexCount)]
                    mats = []
                    for ptr in tree(components[23])['m_Materials']:
                        material = tree(resolve(ptr,'Material'))
                        shader = material['m_Shader']
                        mats.append({'name':material['m_Name'],'shader':shader,
                                     'shader_name':tree(resolve(shader,'Shader'))['m_Name'] if not shader['m_FileID'] else 'engine built-in',
                                     'textures':[entry[1]['m_Texture'] for entry in material['m_SavedProperties']['m_TexEnvs']]})
                    result.append({'path':path,'mesh':mesh.m_Name,'vertices':vd.m_VertexCount,
                                   'uv_format':'Float32' if uv.format == 0 else 'Float16',
                                   'uv_range':[[min(v[i] for v in uvs),max(v[i] for v in uvs)] for i in range(2)],
                                   'materials':mats})
                return result
            for row in bundle['containers']:
                obj = resolve(row['asset'])
                if obj.type.name!='GameObject':
                    continue
                name = tree(obj)['m_Name']
                if not (name.startswith('chara_prefab_') or
                        (name.startswith('map_') and name.endswith(('_mesh','_door')))):
                    continue
                if name in native or name in characters:
                    continue
                record = {'cab':cab,'path_id':obj.path_id,'name':name}
                try:
                    graph = nodes(obj)
                    record['nodes'] = [p for p,_,_ in graph]
                    record['geometry'] = geometry(graph)
                    if name.endswith('_door'):
                        clips = []
                        for path,go,components in graph:
                            for kind,ptr in go['m_Component']:
                                if kind!=114:
                                    continue
                                component = resolve(ptr,'MonoBehaviour')
                                script = resolve(tree(component)['m_Script'],'MonoScript').read()
                                if script.m_ClassName!='AnimationArray':
                                    continue
                                raw = component.get_raw_data()
                                n = struct.unpack_from('<I',raw,20)[0]
                                offset = (24+n+3)//4*4
                                count = struct.unpack_from('<I',raw,offset)[0]
                                offset += 4
                                assert count == 4 and offset+count*8==len(raw)
                                for i in range(count):
                                    f,p = struct.unpack_from('<ii',raw,offset+i*8)
                                    clip = tree(resolve({'m_FileID':f,'m_PathID':p},'AnimationClip'))
                                    curves = sum([clip.get(k,[]) for k in ['m_RotationCurves','m_PositionCurves','m_ScaleCurves','m_EulerCurves','m_FloatCurves']],[])
                                    targets = sorted({c.get('path','') for c in curves})
                                    available = set(record['nodes'])
                                    unknown = [p for p in targets if p not in available]
                                    assert not unknown, unknown
                                    clips.append({'index':i,'name':clip['m_Name'],'curves':len(curves),'targets':targets})
                        assert len(clips)==4
                        record['animation_clips']=clips
                    target = characters if name.startswith('chara_prefab_') else native
                    target[name]=record
                except Exception as ex:
                    errors.append({'cab':cab,'name':name,'error':type(ex).__name__+': '+str(ex)})
            counts.update(bundle['types'])
    finally:
        SerializedFile.set_version = previous
    named = collections.defaultdict(list)
    for b in inventory:
        for c in b['containers']:
            named[c['name']].append(b['path'])
    coverage=[]
    for a in areas:
        coverage.append({'area_id':a['fix_id'],'name':a['area_name'],
                         'floor':a['mesh_map'] in native,'door':a['mesh_door'] in native,
                         'four_clip_door':len(native.get(a['mesh_door'],{}).get('animation_clips',[]))==4,
                         'texture':a['res_map'].lower() in named,
                         'effect':not a['area_effect'] or a['area_effect'].lower() in named,
                         'requests':{k:a[k] for k in ['mesh_map','mesh_door','res_map','area_effect']}})
    char_missing=[c['res_chara_id_str'] for c in chars if 'chara_prefab_'+c['res_chara_id_str'] not in characters]
    summary={'audit_only':True,'apk_modified':False,'runtime_modified':False,
             'source_files_unchanged':unchanged(),'parse_validation_errors':errors,
             'original_areas':len(areas),'original_characters':len(chars),
             'area_coverage':{k:sum(r[k] for r in coverage) for k in ['floor','door','four_clip_door','texture','effect']},
             'missing_areas':[r for r in coverage if not all(r[k] for k in ['floor','door','texture','effect'])],
             'character_prefabs_validated':sum('chara_prefab_'+c['res_chara_id_str'] in characters for c in chars),
             'missing_character_prefabs':char_missing,
             'original_distinct_floor_models':len({r['requests']['mesh_map'] for r in coverage}),
             'original_distinct_door_models':len({r['requests']['mesh_door'] for r in coverage}),
             'restorable_distinct_floor_models':len({r['requests']['mesh_map'] for r in coverage if r['floor']}),
             'restorable_distinct_door_models':len({r['requests']['mesh_door'] for r in coverage if r['door']}),
             'original_distinct_effects':len({r['requests']['area_effect'] for r in coverage if r['requests']['area_effect']}),
             'native_map_prefabs_validated':len(native),
             'native_door_animation_slots':sum(len(r.get('animation_clips',[])) for r in native.values()),
             'android_load_and_render_verified':False,
             'note':'Static object/UV/material/animation binding validation, not an Android native bundle loading test.'}
    for name,value in [('summary.json',summary),('native-map-prefabs.json',native),('native-character-prefabs.json',characters),('area-coverage.json',coverage)]:
        (args.output/name).write_text(json.dumps(value,ensure_ascii=False,indent=2)+'\n')
    print(json.dumps(summary,ensure_ascii=False,indent=2))


if __name__=='__main__':
    main()
