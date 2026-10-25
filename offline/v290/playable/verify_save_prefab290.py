"""Check the actual packaged Others prefab, including 2.9-specific child labels."""
import zipfile,json
import UnityPy
def verify(apk,output):
 with zipfile.ZipFile(apk) as z:
  def asset(name):
   p='assets/bin/Data/'+name
   if p in z.namelist():return z.read(p)
   parts=[];i=0
   while p+'.split'+str(i) in z.namelist():parts.append(z.read(p+'.split'+str(i)));i+=1
   assert parts,name
   return b''.join(parts)
  resource=next(o for o in UnityPy.load(asset('mainData')).objects if o.type.name=='ResourceManager')
  cache={}
  def deref(p,owner):
   if not p['m_FileID']:return owner.assets_file.objects[p['m_PathID']]
   name=owner.assets_file.externals[p['m_FileID']-1].path
   if name not in cache:cache[name]={o.path_id:o for o in UnityPy.load(asset(name)).objects}
   return cache[name][p['m_PathID']]
  ref=dict(resource.read_typetree()['m_Container'])['prefab/mainmenu/mainmenupage/pageothers']
  page=deref(ref,resource);nodes={}
  def walk(go,path):
   tree=go.read_typetree();path=path+'/'+tree['m_Name'];parts=[];transform=None
   for entry in tree['m_Component']:
    c=deref(entry[1] if isinstance(entry,tuple) else entry.get('component',entry),go)
    if c.type.name=='Transform':transform=c.read_typetree()
    elif c.type.name=='MonoBehaviour':
     parts.append(deref(c.read_typetree(check_read=False)['m_Script'],c).read_typetree()['m_ClassName'])
    else:
     parts.append(c.type.name)
     if c.type.name=='BoxCollider':
      collider=c.read_typetree();assert collider['m_Enabled'] and collider['m_Size']['x']>0 and collider['m_Size']['y']>0
   nodes[path]=parts
   if transform:
    for p in transform['m_Children']:
     tr=deref(p,go);walk(deref(tr.read_typetree()['m_GameObject'],tr),path)
  for name in ['OthersWindow_150_Transfer','ListElement150']:
   go=next(o for o in page.assets_file.objects.values() if o.type.name=='GameObject' and o.read_typetree()['m_Name']==name)
   walk(go,'')
  root='/OthersWindow_150_Transfer'
  for name in ['ButtonTransferBackup','ButtonTransferInfomation']:
   assert 'BoxCollider' in nodes[root+'/'+name]
   assert 'UILabel' in nodes[root+'/'+name+'/ButtonMsg']
   assert not any('BoxCollider' in types for path,types in nodes.items() if path.startswith(root+'/'+name+'/'))
  assert 'UILabel' in nodes[root+'/PartsDetail/Detail']
  assert any('UILabel' in types for path,types in nodes.items() if path.startswith(root+'/PartsTitle/'))
  assert 'UILabel' in nodes['/ListElement150/ElementLabel']
 (output/'save-ui-prefab.json').write_text(json.dumps(nodes,indent=2)+'\n')
 return 'PASS actual packaged 2.9 Others prefab: transfer entry, two enabled colliders, export/import label nodes, nested title/detail labels and no child touch interception. Android rendering remains manual.\n'
