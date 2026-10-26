import sys,json,collections
from pathlib import Path
import UnityPy
sys.path.insert(0,'tools/battle-layout')
from unity_scene import Scene
R=Path('artifacts/2.9.1-restored-r18-enemy-size')
report=json.loads((R/'build-report.json').read_text());work=Path(report['work_directory'])
manifest=json.loads((R/'native-bundle-manifest.json').read_text())
areas=[a for a in json.loads((work/'tables/MasterDataArea.json').read_text())if a['fix_id']]
quests=[q for q in json.loads((work/'tables/MasterDataQuest.json').read_text())if q['fix_id']and q['active']==2]
used={q['area_id']for q in quests};areas=[a for a in areas if a['fix_id']in used]
families=collections.Counter(a['mesh_door']for a in areas)
print('AREAS',len(areas),'QUESTS',len(quests),'DOOR FAMILIES',dict(families),'TEXTURES',len({a['res_map']for a in areas}))
for name,count in families.items():
 key='GameObject/'+name.lower();cab=manifest['routes'].get(key)
 print('\nFAMILY',name,'cab',cab,'areas',count)
 if not cab:continue
 scene=Scene(work/'build/native-bundles'/(cab+'.unity3d'),'tools/battle-layout/runtime/serialized-types.json')
 objects={o.path_id:o for o in scene.env.objects};root=objects[manifest['path_ids'][key]]
 def walk(go,path=''):
  n=go.read().m_Name;p=path+'/'+n if path else n
  print('NODE',p,'transform',scene.transform(go).read_typetree()['m_LocalPosition'])
  for c in scene.components(go):
   if c.type.name=='MonoBehaviour':print('MONO',scene.mono(c))
  for ptr in scene.transform(go).read_typetree()['m_Children']:
   tr=scene.ref(go,ptr);walk(scene.ref(tr,tr.read_typetree()['m_GameObject']),p)
 walk(root)
 for o in scene.env.objects:
  if o.type.name!='AnimationClip':continue
  d=o.read_typetree();print('CLIP',o.path_id,d['m_Name'],'keys',list(d))
  for k,v in d.items():
   if 'Curves'in k and v:print(k,json.dumps(v)[:5000])
