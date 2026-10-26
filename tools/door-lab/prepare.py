"""Extract r18 active quest door meshes, original curve keys and atlas provenance."""
import sys,json,hashlib,io,zipfile,struct,collections,math
from pathlib import Path
from PIL import Image
from UnityPy.helpers.MeshHelper import MeshHandler
sys.path.insert(0,'tools/battle-layout')
from unity_scene import Scene
ROOT=Path('/workspace');HERE=Path(__file__).resolve().parent
REPORT=ROOT/'artifacts/2.9.1-restored-r18-enemy-size'
OUT=ROOT/'artifacts/door-animation-lab';CACHE=HERE/'runtime'
def save(p,v):p.write_text(json.dumps(v,ensure_ascii=False,separators=(',',':'),allow_nan=False))
def xyz(v):return [v[k]for k in 'xyz']
def main():
 OUT.mkdir(parents=True,exist_ok=True);CACHE.mkdir(exist_ok=True)
 previous=json.loads((CACHE/'doors.json').read_text())if(CACHE/'doors.json').exists()else{}
 r=json.loads((REPORT/'build-report.json').read_text());work=Path(r['work_directory'])
 assert r['sha256']=='d90c903f172bfca44ca8f0138b3dc465bc82611ad648db0e5c593a6fac48f4a4'
 with Path(r['output']).open('rb')as f:assert hashlib.file_digest(f,'sha256').hexdigest()==r['sha256'],'Source APK hash mismatch'
 manifest=json.loads((REPORT/'native-bundle-manifest.json').read_text());images=json.loads((REPORT/'images.json').read_text())
 areas={a['fix_id']:a for a in json.loads((work/'tables/MasterDataArea.json').read_text())if a['fix_id']}
 quests=[q for q in json.loads((work/'tables/MasterDataQuest.json').read_text())if q['fix_id']and q['active']==2]
 used={q['area_id']for q in quests};areas={i:a for i,a in areas.items()if i in used}
 families={};audits=[]
 for name in sorted({a['mesh_door']for a in areas.values()}):
  key='GameObject/'+name.lower();cab=manifest['routes'].get(key)
  if not cab:continue
  source=next(b for b in manifest['bundles']if b['id']==cab)
  assert hashlib.sha256((work/'build/native-bundles'/(cab+'.unity3d')).read_bytes()).hexdigest()==source['sha256']
  scene=Scene(work/'build/native-bundles'/(cab+'.unity3d'),ROOT/'tools/battle-layout/runtime/serialized-types.json')
  objs={o.path_id:o for o in scene.env.objects};go=objs[manifest['path_ids'][key]]
  nodes=[];clips=[];cliprefs=[]
  def walk(go,parent=-1,path=''):
   n=go.read().m_Name;tr=scene.transform(go);t=tr.read_typetree();idx=len(nodes)
   row={'name':n,'path':path,'parent':parent,'position':xyz(t['m_LocalPosition']),'rotation':[t['m_LocalRotation'][k]for k in 'xyzw'],'scale':xyz(t['m_LocalScale']),'active':bool(go.read_typetree()['m_IsActive'])}
   nodes.append(row);components=scene.components(go)
   for c in components:
    if c.type.name=='MonoBehaviour':
     d=scene.mono(c)
     if d['class']=='AnimationArray':cliprefs.extend(scene.ref(c,p)for p in d['data']['m_Clips'])
   mf=next((c for c in components if c.type.name=='MeshFilter'),None);mr=next((c for c in components if c.type.name=='MeshRenderer'),None)
   if mf and mr:
    m=scene.ref(mf,mf.read_typetree()['m_Mesh']);h=MeshHandler(m.read(),version=(4,6,5,1));h.process()
    row['mesh']={'positions':[list(v)for v in h.m_Vertices],'uv':[list(v[:2])for v in h.m_UV0],'colors':[list(v)for v in h.m_Colors]if h.m_Colors else[],'triangles':[i for sub in h.get_triangles()for tri in sub for i in tri]}
    assert len(row['mesh']['positions'])==len(row['mesh']['uv'])
    mt=scene.ref(mr,mr.read_typetree()['m_Materials'][0]).read_typetree();env={k['name']if isinstance(k,dict)else k:v for k,v in mt['m_SavedProperties']['m_TexEnvs']};tex=env['_MainTex']
    row['uvScale']=[tex['m_Scale'][k]for k in 'xy'];row['uvOffset']=[tex['m_Offset'][k]for k in 'xy']
   for ptr in t['m_Children']:
    childtr=scene.ref(tr,ptr);child=scene.ref(childtr,childtr.read_typetree()['m_GameObject']);childname=child.read().m_Name
    walk(child,idx,path+'/'+childname if path else childname)
  walk(go)
  assert len(cliprefs)==4,(name,len(cliprefs))
  paths={n['path']for n in nodes}
  for slot,obj in enumerate(cliprefs):
   t=obj.read_typetree();tracks=[]
   assert not t['m_CompressedRotationCurves']and not t['m_PPtrCurves'],(name,'Unsupported compressed/object curves')
   for field,kind in [('m_RotationCurves','rotation'),('m_PositionCurves','position'),('m_ScaleCurves','scale'),('m_FloatCurves','float')]:
    for c in t[field]:
     assert c['path']in paths,(name,c['path'])
     keys=[]
     for k in c['curve']['m_Curve']:
      def vector(v):return [v[a]for a in ('xyzw'if kind=='rotation'else'xyz')]if isinstance(v,dict)else[v]
      keys.append({'time':k['time'],'value':vector(k['value']),'in':vector(k['inSlope']),'out':vector(k['outSlope'])})
     tracks.append({'path':c['path'],'kind':kind,'attribute':c.get('attribute',''),'classID':c.get('classID',0),'keys':keys})
   duration=max(k['time']for track in tracks for k in track['keys'])
   clip={'name':t['m_Name'],'slot':slot,'duration':duration,'tracks':tracks,'sampleRate':t['m_SampleRate'],'events':t['m_Events']}
   clips.append(clip)
  source=next(b for b in manifest['bundles']if b['id']==cab)
  families[name]={'id':name,'native':True,'nodes':nodes,'clips':clips,'cab':cab,'sha256':source['sha256']}
  audits.append({'family':name,'nodes':[n['path']for n in nodes if'mesh'in n],'clips':[{'name':c['name'],'duration':c['duration'],'channels':[(t['path'],t['kind'],t['attribute'])for t in c['tracks']]}for c in clips]})
 map_models={}
 for name in sorted({a['mesh_map']for a in areas.values()}):
  key='GameObject/'+name.lower();cab=manifest['routes'].get(key)
  if not cab:continue
  source=next(b for b in manifest['bundles']if b['id']==cab)
  assert hashlib.sha256((work/'build/native-bundles'/(cab+'.unity3d')).read_bytes()).hexdigest()==source['sha256']
  scene=Scene(work/'build/native-bundles'/(cab+'.unity3d'),ROOT/'tools/battle-layout/runtime/serialized-types.json')
  objs={o.path_id:o for o in scene.env.objects};nodes=[];cliprefs=[]
  walk(objs[manifest['path_ids'][key]])
  map_models[name]={'nodes':nodes}
 # Read original payload bytes directly from the signed r18 APK; never rewrite it.
 apk=Path(r['output'])
 with zipfile.ZipFile(apk)as z:
  entry=z.getinfo('assets/dg-images.bin');assert entry.compress_type==0
  with apk.open('rb')as f:f.seek(entry.header_offset);head=f.read(30);base=entry.header_offset+30+sum(struct.unpack_from('<HH',head,26))
 textures={}
 def payload(key):
  row=images['payloads'][key]
  with apk.open('rb')as f:f.seek(base+row['offset']);raw=f.read(row['size'])
  assert hashlib.sha256(raw).hexdigest()==row['sha256'];return raw
 for resource in sorted({a['res_map']for a in areas.values()}):
  name=resource.removesuffix('_tex');key='restored-map/'+name+'/atlas';raw=payload(key);im=Image.open(io.BytesIO(raw)).convert('RGBA')
  dest=CACHE/(resource+'.webp')
  prior=previous.get('textures',{}).get(resource,{})
  if not dest.exists()or prior.get('source_sha256')!=hashlib.sha256(raw).hexdigest():im.save(dest,format='WEBP',lossless=True,exact=True,method=4)
  assert Image.open(dest).convert('RGBA').tobytes()==im.tobytes()
  textures[resource]={'file':str(dest),'source':key,'source_sha256':hashlib.sha256(raw).hexdigest(),'webp_sha256':hashlib.sha256(dest.read_bytes()).hexdigest(),'width':im.width,'height':im.height}
 result={'schema':1,'sourceApkSha256':r['sha256'],'dataVersion':'dgdb-2-restored','areas':[{'id':i,'name':a['area_name'],'resource':a['res_map'],'doorFamily':a['mesh_door'],'mapFamily':a['mesh_map'],'nativeRig':a['mesh_door']in families,'nativeTexture':('Texture/'+a['res_map'].lower())in manifest['routes']}for i,a in areas.items()],'quests':[{'id':q['fix_id'],'areaId':q['area_id'],'name':q.get('quest_name',q.get('name',''))}for q in quests],'families':families,'mapModels':map_models,'textures':textures}
 save(CACHE/'doors.json',result);save(OUT/'native-animation-audit.json',audits)
 print('EXTRACTED',len(quests),'quests',len(areas),'areas',len(textures),'atlases',len(families),'native families',sum(len(f['clips'])for f in families.values()),'original clips')
 for row in audits:
  print(row['family'],'nodes',row['nodes'],'duration',[round(c['duration'],3)for c in row['clips']],'tracks',row['clips'][0]['channels'])
if __name__=='__main__':main()
