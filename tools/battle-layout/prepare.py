"""Extract the actual2.9.1 battle geometry, atlases and editable visual metadata."""
import argparse,hashlib,json,math,sys,subprocess
from pathlib import Path
from collections import Counter
from UnityPy.helpers.MeshHelper import MeshHandler
from unity_scene import Scene
ROOT=Path('/workspace');HERE=Path(__file__).resolve().parent
def dump(p,value):p.parent.mkdir(parents=True,exist_ok=True);p.write_text(json.dumps(value,ensure_ascii=False,separators=(',',':')))
def matmul(a,b):return [[sum(a[i][k]*b[k][j]for k in range(4))for j in range(4)]for i in range(4)]
def identity():return [[int(i==j)for j in range(4)]for i in range(4)]
def transform(t):
 q=t['m_LocalRotation'];x,y,z,w=[q[k]for k in ['x','y','z','w']];p=t['m_LocalPosition'];s=t['m_LocalScale']
 a=[[1-2*(y*y+z*z),2*(x*y-z*w),2*(x*z+y*w),p['x']],[2*(x*y+z*w),1-2*(x*x+z*z),2*(y*z-x*w),p['y']],[2*(x*z-y*w),2*(y*z+x*w),1-2*(x*x+y*y),p['z']],[0,0,0,1]]
 for i in range(3):
  for j,key in enumerate(['x','y','z']):a[i][j]*=s[key]
 return a
def point(m,p):return [sum(m[i][j]*(p[j]if j<3 else 1)for j in range(4))for i in range(3)]
def main():
 ap=argparse.ArgumentParser();ap.add_argument('--report',type=Path,default=ROOT/'artifacts/2.9.1-restored-r17-native-ui-calibrated/build-report.json');args=ap.parse_args()
 report=json.loads(args.report.read_text());work=Path(report['work_directory'])
 assert report['sha256']=='93eb5e82c3f7bce10c8c55c31cfd99182dbaf414a308e2dd6d8383f91fa29705'
 dist=HERE/'dist';assets=dist/'game';assets.mkdir(parents=True,exist_ok=True);cache=HERE/'runtime';cache.mkdir(exist_ok=True)
 schema=cache/'serialized-types.json'
 subprocess.run(['mcs','-r:System.Web.Extensions','-r:/usr/lib/mono/gac/Mono.Cecil/0.11.1.0__0738eb9f132ed756/Mono.Cecil.dll','-out:/tmp/DGVisualSerializedTypes.exe',str(HERE/'SerializedTypes.cs')],check=True)
 with schema.open('w')as f:subprocess.run(['mono','/tmp/DGVisualSerializedTypes.exe',str(work/'original/assets/bin/Data/Managed')],stdout=f,check=True)
 scene=Scene(work/'original/assets/bin/Data',schema);sobj=scene.files['level7'].objects[1876];refs=scene.mono(sobj)['data']
 sprites={};texture_paths={};texture_sizes={};origins=[];meshes={};errors=[]
 def texture(o):
  if o is None:return None
  key=(o.assets_file.name,o.path_id)
  if key in texture_paths:return texture_paths[key]
  data=o.read();name='game/'+data.m_Name+'.png';data.image.convert('RGBA').save(dist/name);texture_paths[key]=name;texture_sizes[name]=[data.m_Width,data.m_Height]
  origins.append(dict(file=o.assets_file.name,path_id=o.path_id,name=data.m_Name,sha256=hashlib.sha256(o.get_raw_data()).hexdigest()))
  return name
 def material(o):
  if o is None:return {}
  t=o.read_typetree();envs={(k['name']if isinstance(k,dict)else k):v for k,v in t['m_SavedProperties']['m_TexEnvs']};v=envs.get('_MainTex');tex=scene.ref(o,v['m_Texture'])if v else None
  cols={(k['name']if isinstance(k,dict)else k):v for k,v in t['m_SavedProperties']['m_Colors']}
  return {'color':list(cols.get('_Color',{'r':1,'g':1,'b':1,'a':1}).values()),'texture':texture(tex),'scale':list(v['m_Scale'].values())if v else [1,1],'offset':list(v['m_Offset'].values())if v else[0,0],'name':t['m_Name']}
 # All native UI atlases, preserving exact original sprite rectangles.
 atlas_data={}
 for o in scene.env.objects:
  if o.type.name!='MonoBehaviour' or o.assets_file.name not in ['sharedassets8.assets','sharedassets0.assets']:continue
  prefix=o.read_typetree(check_read=False);script=scene.ref(o,prefix['m_Script'])
  if script.read().m_ClassName!='UIAtlas':continue
  a=scene.mono(o)['data'];mt=material(scene.ref(o,a['material']))
  if not mt.get('texture'):continue
  key=(o.assets_file.name,o.path_id);atlas_data[key]={'texture':mt['texture'],'sprites':a['sprites']}
  for item in a['sprites']:
   if item['name']not in sprites:sprites[item['name']]={'texture':mt['texture'],'rect':item['outer'],'inner':item['inner'],'padding':[item[k]for k in ['paddingLeft','paddingRight','paddingTop','paddingBottom']]}
 print('atlases',len(atlas_data),'sprites',len(sprites),flush=True)
 def mesh(o):
  key=(o.assets_file.name,o.path_id)
  if key not in meshes:
   h=MeshHandler(o.read(),version=(4,6,5,1));h.process()
   meshes[key]={'name':o.read().m_Name,'positions':[list(v)for v in h.m_Vertices],'uv':[list(v[:2])for v in h.m_UV0],'colors':[list(v)for v in h.m_Colors]if h.m_Colors else [],'triangles':[i for group in h.get_triangles()for tri in group for i in tri]}
  return meshes[key]
 def local_matrix(go,stop=None):
  t=scene.transform(go);chain=[]
  while t:
   d=t.read_typetree();g=scene.ref(t,d['m_GameObject'])
   if stop and g.assets_file.name==stop.assets_file.name and g.path_id==stop.path_id:break
   chain.append(transform(d));t=scene.ref(t,d['m_Father'])
  m=identity()
  for a in reversed(chain):m=matmul(m,a)
  return m
 def mesh_node(go,m=None,descend=False):
  comps=scene.components(go);mf=next((c for c in comps if c.type.name=='MeshFilter'),None);mr=next((c for c in comps if c.type.name=='MeshRenderer'),None)
  if not mf or not mr:
   if not descend:return None
   root_matrix=m or local_matrix(go)
   tr=scene.transform(go);nodes=[]
   for ptr in tr.read_typetree()['m_Children']:
    childtr=scene.ref(tr,ptr);child=scene.ref(childtr,childtr.read_typetree()['m_GameObject'])
    found=mesh_node(child,matmul(root_matrix,transform(childtr.read_typetree())),True)
    if found:nodes.append(found)
   assert len(nodes)<=1,('Multiple prefab draw surfaces',go.read().m_Name)
   return nodes[0]if nodes else None
  obj=scene.ref(mf,mf.read_typetree()['m_Mesh']);v=mesh(obj);m=m or local_matrix(go)
  materials=[material(scene.ref(mr,p))for p in mr.read_typetree()['m_Materials']]
  return dict(name=go.read().m_Name,positions=[point(m,p)for p in v['positions']],uv=v['uv'],colors=v['colors'],triangles=v['triangles'],material=materials[0]if materials else{},source={'file':obj.assets_file.name,'id':obj.path_id,'sha256':hashlib.sha256(obj.get_raw_data()).hexdigest()})
 def walk(go):
  yield go
  tr=scene.transform(go)
  for p in tr.read_typetree()['m_Children']:
   ch=scene.ref(tr,p);yield from walk(scene.ref(ch,ch.read_typetree()['m_GameObject']))
 def widgets(go,stop):
  rows=[]
  for child in walk(go):
   m=local_matrix(child,stop)
   for comp in scene.components(child):
    if comp.type.name!='MonoBehaviour':continue
    prefix=comp.read_typetree(check_read=False);sc=scene.ref(comp,prefix['m_Script']).read().m_ClassName
    if sc not in ['UISprite','UIFilledSprite','UISlicedSprite','UILabel','UITexture']:continue
    try:d=scene.mono(comp)['data']
    except Exception as ex:errors.append([sc,child.read().m_Name,str(ex)]);continue
    data={'name':child.read().m_Name,'class':sc,'matrix':m,'data':d,'active':child.read_typetree()['m_IsActive']}
    if d.get('mAtlas'):
     atlas=scene.ref(comp,d['mAtlas']);a=atlas_data.get((atlas.assets_file.name,atlas.path_id))if atlas else None
     sp=next((v for v in a['sprites']if v['name']==d.get('mSpriteName')),None)if a else None
     if sp:data['sprite']={'texture':a['texture'],'rect':sp['outer'],'inner':sp['inner'],'padding':[sp[k]for k in ['paddingLeft','paddingRight','paddingTop','paddingBottom']]}
    if sc=='UITexture':data['texture']=texture(scene.ref(comp,d['mTexture']))
    rows.append(data)
  return rows
 partyroot=scene.ref(sobj,refs['m_UIInstanceGroupInGameParty'])
 portraits=[mesh_node(scene.ref(sobj,p),local_matrix(scene.ref(sobj,p),partyroot))for p in refs['m_UIInstancePartyChara']]
 frames={};backs={}
 for name in ['Empty','Blue','Gray','Green','Purple','Red','Yellow']:
  for target,prefix,rootname in [(frames,'m_UIInstancePartyCharaFrame','m_UIInstancePartyCharaFrameRoot'),(backs,'m_UIInstancePartyCharaBack','m_UIInstancePartyCharaBackRoot')]:
   rm=local_matrix(scene.ref(sobj,refs[rootname]),partyroot)
   target[name.lower()]=[]
   for index,pointer in enumerate(refs[prefix+name]):
    placement=identity();placement[0][3]=[.7682,.6162,.5385,.427161,.2981][index]
    target[name.lower()].append(mesh_node(scene.ref(sobj,pointer),matmul(rm,placement),True))
 ids={p['m_PathID']for p in refs['m_UIInstancePartyChara']}
 static=[mesh_node(go,local_matrix(go,partyroot))for go in walk(partyroot)if go.path_id not in ids]
 static=[v for v in static if v]
 cutin=scene.ref(sobj,refs['m_GameObjSkillCutinNaught'])
 cutin_meshes=[mesh_node(go,local_matrix(go,cutin))for go in walk(cutin)];cutin_meshes=[v for v in cutin_meshes if v]
 cutin_widgets=widgets(cutin,cutin)
 gauges={};gauge_positions={}
 for label in ['S','M','L','Boss']:
  go=scene.ref(sobj,refs['m_GameObjBattleEnemyHP_'+label]);gauges[label]=widgets(go,go);gauge_positions[label]=scene.transform(go).read_typetree()['m_LocalPosition']
 enemyroot=scene.ref(sobj,refs['m_UIInstanceGroupBattleEnemy'])
 anchor=scene.files['level7'].objects[561]
 header_meshes=[mesh_node(scene.files['level7'].objects[i],local_matrix(scene.files['level7'].objects[i],anchor))for i in [454,456]]
 battle_frame=mesh_node(scene.files['level7'].objects[373],local_matrix(scene.files['level7'].objects[373],anchor))
 # Font data are source assets, copied only when the original Font embeds bytes.
 fonts=[]
 for o in scene.env.objects:
  if o.type.name=='Font':
   f=o.read();raw=getattr(f,'m_FontData',None)
   if raw and len(raw)>100:
    fn='game/font-'+str(len(fonts))+'.ttf';(dist/fn).write_bytes(bytes(raw));fonts.append({'name':getattr(f,'m_Name',''),'path':fn})
 manifest={'source_apk_sha256':report['sha256'],'viewport':{'width':640,'baseHeight':960,'defaultHeight':1136},'portraits':portraits,'headerMeshes':header_meshes,'battleFrame':battle_frame,'frames':frames,'backs':backs,'partyStatic':static,'partyWidgets':widgets(partyroot,partyroot),'cutinMeshes':cutin_meshes,'cutinWidgets':cutin_widgets,'cutinRootScale':scene.transform(cutin).read_typetree()['m_LocalScale'],'enemyRootAncestry':scene.ancestry(enemyroot),'gauges':gauges,'gaugeRootPositions':gauge_positions,'textureSizes':texture_sizes,'sprites':sprites,'fonts':fonts,'sources':origins,'parseErrors':errors}
 images=json.loads((args.report.parent/'images.json').read_text())
 from PIL import Image
 import io
 entry=images['payloads']['atlas/AtlasPanelIcon']
 with (work/'build/RecoveredImages.bin').open('rb')as file:
  file.seek(entry['offset']);sheet=Image.open(io.BytesIO(file.read(entry['size'])));shape=list(sheet.size)
 manifest['panelSprites']={k.split('/',1)[1]:{'rect':v,'sheet':shape}for k,v in images['sprites'].items()if k.startswith('AtlasPanelIcon/')}
 assert all(v for group in frames.values()for v in group)and all(v for group in backs.values()for v in group)
 dump(dist/'game/layout.json',manifest)
 chars=json.loads((work/'tables/MasterDataParamChara.json').read_text());enemies=json.loads((work/'tables/MasterDataEnemyChara.json').read_text())
 native=set(json.loads((args.report.parent/'restoration.json').read_text())['original_characters']);crops=json.loads((args.report.parent/'hud-portrait-crops.json').read_text())
 reviews={r['id']:r for r in json.loads((args.report.parent/'hud-review-list.json').read_text())}
 sys.path.insert(0,str(ROOT/'offline/v291/playable'));from dgdb import sql_tables,SQL
 db,_=sql_tables(SQL);sql={r['fix_id']:r for r in db['unit']}
 # size is a category, not a pixel dimension. Resolve it through the same
 # verified 595-role native reference profiles used by the restored adapter.
 sys.path.insert(0,str(ROOT/'offline/v291/restored'));import size_layout
 size_groups=size_layout.references({c['fix_id']:c for c in chars if c['fix_id']in native},sql)
 size_changes=[]
 skills={r['fix_id']:r for r in json.loads((work/'tables/MasterDataSkillActive.json').read_text())}
 bychar={}
 for row in enemies:
  if row['fix_id']:bychar.setdefault(row['chara_id'],[]).append(row)
 catalog=[]
 for c in chars:
  i=c['fix_id']
  if not i:continue
  fields={k:c[k]for k in c if k.startswith('img_')or k in ['size_width','size_height','pivot','side_offset']}
  size_adapter={'method':'original-native-profile','exactOfficial':True}
  if i not in native:
   chosen,method,refs=size_layout.select(sql[i],size_groups)
   mapped=size_layout.profile(chosen);fields.update(mapped)
   size_adapter={'method':method,'referenceIds':refs,'exactOfficial':False,'fields':mapped}
   if any(c[k]!=mapped[k]for k in size_layout.FIELDS):
    size_changes.append({'id':i,'dgdbSize':sql[i]['size'],'before':{k:c[k]for k in size_layout.FIELDS},'after':mapped,'method':method})
  if str(i)in crops:
   for slot,rect in enumerate(crops[str(i)]['slot_rects']):
    n=4-slot
    fields['img_'+str(n)+'_tiling']=round(rect[2]*1000);fields['img_'+str(n)+'_offsetX']=round(rect[0]*1000);fields['img_'+str(n)+'_offsetY']=round(rect[1]*1000)
  names=[skills.get(c[k],{}).get('name','')for k in ['skill_active0','skill_active1']]
  catalog.append(dict(id=i,name=c['name'],drawId=c['draw_id'],resource=c['res_chara_id_str'],element=c['element'],rare=c['rare']+1,kind=c['kind'],native=i in native,missingCrop=i not in native,missingSize=i not in native and sql[i]['size']!=1,dgdbSize=sql[i]['size'],sizeAdapter=size_adapter,review=reviews.get(i,{}).get('reason',[]),fields=fields,masterFields={k:c[k]for k in fields},enemies=bychar.get(i,[]),skills=names,hp=c['base_hp_max'],attack=c['base_attack_max']))
 assert len(size_changes)==304
 dump(ROOT/'artifacts/battle-layout-editor/size-adaptation.json',{'revision':2,'policy':'Keep original595. Resolve DGDB size with native size/kind/rarity consensus or explicit category mode fallback. Inferred sub-size is not official per-character metadata.','changes':size_changes})
 dump(dist/'game/catalog.json',{'schema':1,'dataVersion':'dgdb-2-restored','sourceApkSha256':report['sha256'],'units':catalog})
 dump(cache/'source-config.json',{'apk':report['output'],'work':report['work_directory'],'images':str(args.report.parent/'images.json'),'nativeManifest':str(args.report.parent/'native-bundle-manifest.json'),'dataVersion':'dgdb-2-restored','apkSha256':report['sha256']})
 dump(cache/'preparation-report.json',{'units':len(catalog),'native':len(native),'missingCrop':sum(c['missingCrop']for c in catalog),'missingSize':sum(c['missingSize']for c in catalog),'enemyReferenced':sum(bool(c['enemies'])for c in catalog),'textures':len(texture_paths),'sprites':len(sprites),'partyMeshes':len(portraits),'staticMeshes':len(static),'cutinMeshes':len(cutin_meshes),'cutinWidgets':len(cutin_widgets),'fontCount':len(fonts),'parserErrors':errors})
 print('complete',json.loads((cache/'preparation-report.json').read_text()),flush=True)
if __name__=='__main__':main()
