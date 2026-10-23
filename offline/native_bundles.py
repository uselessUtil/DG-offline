"""Wrap original Android UnityCache SerializedFiles in UnityRaw v3 containers.

No CAB object bytes are edited. Only matching map/character packages are used;
cached master tables and UI atlases never enter the native loading path.
"""
import hashlib
import json
from pathlib import Path

import UnityPy
from UnityPy.files.BundleFile import BundleFile
from UnityPy.files.SerializedFile import SerializedFile
from UnityPy.streams import EndianBinaryReader


def digest(raw):
    return hashlib.sha256(raw).hexdigest()


def wrap(name, raw):
    bundle = BundleFile.__new__(BundleFile)
    bundle.signature = 'UnityRaw'
    bundle.version = 3
    bundle.version_player = '3.x.x'  # Legacy UnityRaw v3 compatibility marker.
    bundle.version_engine = '4.1.0f4'
    # Reader preserves the exact SerializedFile, including its cache header.
    bundle.files = {name:EndianBinaryReader(raw)}
    return bundle.save()


def disabled(output):
    (output/'NativeAssetIndex.cs').write_text('''internal static class NativeAssetIndex {
public const bool Enabled = false;
public static string Find(string kind, string name) { return null; }
}''')


def create(backup, output, tables, *, expected_characters=522, expected_areas=99, script_hashes=None):
    output.mkdir(parents=True,exist_ok=True)
    folder = output/'native-bundles'
    folder.mkdir()
    areas = [a for a in json.loads((tables/'MasterDataArea.json').read_bytes(),strict=False) if a['fix_id']]
    chars = [c for c in json.loads((tables/'MasterDataParamChara.json').read_bytes(),strict=False) if c['fix_id']]
    wanted_go = {a[k].lower() for a in areas for k in ['mesh_map','mesh_door','area_effect'] if a[k]}
    wanted_go |= {'chara_prefab_'+c['res_chara_id_str'] for c in chars}
    wanted_tex = {a['res_map'].lower() for a in areas}
    wanted_tex |= {'tex_chara_l_'+c['res_chara_id_str'] for c in chars}
    routes, names, records, input_hashes = {}, {}, [], []
    previous = SerializedFile.set_version
    SerializedFile.set_version = lambda self,s:previous(self,s.splitlines()[0])
    try:
        for path in sorted(backup.rglob('CAB-*')):
            raw = path.read_bytes()
            input_hashes.append((path,digest(raw)))
            env = UnityPy.load(raw)
            bundle_obj = next(o for o in env.objects if o.type.name=='AssetBundle')
            tree = bundle_obj.read_typetree(check_read=False)
            containers = tree['m_Container']
            objects = {o.path_id:o for o in env.objects}
            entries = []
            for name, row in containers:
                p = row['asset']
                if p['m_FileID']:
                    raise ValueError('External AssetBundle entry: '+name)
                kind = objects[p['m_PathID']].type.name
                lower = name.lower()
                if (kind=='GameObject' and lower in wanted_go) or (kind=='Texture2D' and lower in wanted_tex):
                    entries.append((kind,lower,p['m_PathID']))
            if not entries:
                continue
            dependencies = [e.path for f in env.files.values() for e in f.externals]
            if any(n!='library/unity default resources' for n in dependencies):
                raise ValueError('Unresolved native CAB dependency: '+str(dependencies))
            scripts = tree.get('m_ScriptCompatibility',[])
            if any(s['className'] not in ['AnimationArray','AnimationEvent'] for s in scripts):
                raise ValueError('Unexpected executable component in native scene/character package')
            for s in scripts:
                expected = (script_hashes or {'AnimationArray':3721557914,'AnimationEvent':3033599902})[s['className']]
                if s['hash'] != expected:
                    raise ValueError('Target APK component schema differs: '+s['className'])
            for kind,name,pid in entries:
                key = ('Texture' if kind=='Texture2D' else kind)+'/'+name
                if key in routes and routes[key]!=path.name:
                    raise ValueError('Ambiguous native resource: '+key)
                routes[key] = path.name
                names[key] = pid
            material = [(name,row) for name,row in containers
                        if name.lower()=='mapmaterial' and objects[row['asset']['m_PathID']].type.name=='Material']
            if any(name.startswith('map_') and kind=='GameObject' for kind,name,_ in entries):
                if len(material)!=1:
                    raise ValueError('Map package must have one shared mapMaterial')
                for kind,name,pid in entries:
                    if kind=='GameObject' and name.startswith('map_') and name.endswith('_mesh'):
                        key='Material/'+name.removesuffix('_mesh')
                        routes[key]=path.name
                        names[key]=material[0][1]['asset']['m_PathID']
            packed = wrap(path.name,raw)
            check = UnityPy.load(packed)
            actual = next(iter(check.files.values()))
            # Read the original CAB bytes directly from the wrapper's node.
            assert actual.files[path.name].reader.bytes == raw
            (folder/(path.name+'.unity3d')).write_bytes(packed)
            records.append({'cab':str(path.relative_to(backup)),'id':path.name,
                            'cab_sha256':digest(raw),'cab_size':len(raw),
                            'file':path.name+'.unity3d','size':len(packed),'sha256':digest(packed),
                            'dependencies':dependencies,'script_compatibility':scripts,
                            'object_types':dict(__import__('collections').Counter(o.type.name for o in env.objects))})
    finally:
        SerializedFile.set_version = previous
    for path,sha in input_hashes:
        if digest(path.read_bytes())!=sha:
            raise ValueError('Backup modified during wrapping')
    lines = ['using System.Collections.Generic;','internal static class NativeAssetIndex {',
             'public const bool Enabled = true;',
             'private static readonly Dictionary<string,string> entries = new Dictionary<string,string> {']
    lines += ['{'+json.dumps(k)+','+json.dumps(v)+'},' for k,v in sorted(routes.items())]
    lines += ['};','public static string Find(string kind, string name) {',
              'string value; return entries.TryGetValue(kind + "/" + name.ToLowerInvariant(), out value) ? value : null;',
              '}','}']
    (output/'NativeAssetIndex.cs').write_text('\n'.join(lines))
    coverage=[]
    for a in areas:
        coverage.append({'area_id':a['fix_id'],'name':a['area_name'],
                         **{k:routes.get(('Texture/' if k=='res_map' else 'GameObject/')+a[k].lower())
                            for k in ['mesh_map','mesh_door','res_map','area_effect']}})
    character_count=sum('GameObject/chara_prefab_'+c['res_chara_id_str'] in routes for c in chars)
    area_count = sum(bool(a['mesh_map'] and a['mesh_door'] and a['res_map']) for a in coverage)
    if character_count!=expected_characters or area_count!=expected_areas:
        raise ValueError('Unexpected native restoration coverage')
    report={'source':str(backup),'container':'UnityRaw v3; embedded CAB bytes unchanged',
            'bundles':records,'routes':routes,'path_ids':names,'area_coverage':coverage,
            'characters':character_count,'areas':area_count,'effects':sum(bool(a['area_effect']) for a in coverage),
            'native_bytes':sum(r['size'] for r in records),
            'source_hashes_unchanged':True,'cached_master_data_imported':False,
            'fallback_policy':'Missing resource or native load error uses the existing simplified request path.',
            'android_load_and_render_verified':False}
    (output/'native-bundle-manifest.json').write_text(json.dumps(report,ensure_ascii=False,indent=2))
    print('Native bundles:',len(records),'packages;',character_count,'characters;',area_count,'maps;',report['native_bytes'],'bytes',flush=True)
    return report
