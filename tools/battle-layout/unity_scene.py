"""Read stripped Unity4 MonoBehaviours using the native managed field schema."""
import json,struct
from pathlib import Path
import UnityPy

class Scene:
 def __init__(self,data,types):
  self.env=UnityPy.load(str(data));self.files={f.name:f for f in self.env.files.values() if hasattr(f,'objects')}
  self.types=json.loads(Path(types).read_text());self.cache={}
 def ref(self,obj,p):
  if not p or not p['m_PathID']:return None
  f=obj.assets_file
  if p['m_FileID']:f=self.files[Path(f.externals[p['m_FileID']-1].path).name]
  return f.objects[p['m_PathID']]
 def is_object(self,t):
  if t.startswith('UnityEngine.'):return t not in ['UnityEngine.Color','UnityEngine.Color32','UnityEngine.Vector2','UnityEngine.Vector3','UnityEngine.Vector4','UnityEngine.Rect','UnityEngine.Quaternion','UnityEngine.Matrix4x4','UnityEngine.Bounds','UnityEngine.LayerMask']
  seen=set()
  while t and t not in seen:
   seen.add(t);d=self.types.get(t)
   if not d:return False
   t=d.get('baseType','')
   if t and t.startswith('UnityEngine.'):return True
   if t and '<'in t:t=t.split('<')[0]
  return False
 def supported(self,t):
  if t.endswith('[]'):return self.supported(t[:-2])
  if t.startswith('System.Collections.Generic.List'):return self.supported(t[t.index('<')+1:-1])
  if t in ['System.String','System.Int32','System.UInt32','System.Single','System.Boolean','System.Byte','System.Int64','System.UInt64','System.Double']:return True
  if self.is_object(t):return True
  if t.startswith('UnityEngine.'):return True
  d=self.types.get(t);return bool(d and (d['kind'] in ['enum','struct'] or d.get('serializable')))
 def fields(self,t):
  d=self.types.get(t,{})
  b=d.get('baseType')
  if b and '<'in b:b=b.split('<')[0]
  return (self.fields(b) if b and not b.startswith(('UnityEngine.','System.')) else [])+[f for f in d.get('fields',[]) if self.supported(f['type'])]
 def mono(self,o):
  k=(o.assets_file.name,o.path_id)
  if k in self.cache:return self.cache[k]
  prefix=o.read_typetree(check_read=False);script=self.ref(o,prefix['m_Script']);name=script.read().m_ClassName
  reader=Reader(o.get_raw_data(),self);reader.p=20;reader.string()
  tree={}
  for f in self.fields(name):
   try:tree[f['name']]=reader.value(f['type'])
   except Exception as ex:raise ValueError((name,f['name'],f['type'],reader.p,len(reader.b),str(ex)))
  assert reader.p==len(reader.b),(name,'unread data',reader.p,len(reader.b),list(tree)[-5:])
  result={'class':name,'data':tree,'prefix':prefix}
  self.cache[k]=result;return result
 def game(self,o):return o.read_typetree()
 def components(self,go):
  t=go.read_typetree()
  return [self.ref(go,(p[1] if isinstance(p,tuple) else p.get('component',p))) for p in t['m_Component']]
 def transform(self,go):return next(o for o in self.components(go) if o.type.name=='Transform')
 def ancestry(self,go):
  t=self.transform(go);res=[]
  while t:
   d=t.read_typetree();g=self.ref(t,d['m_GameObject']);res.append((g.read().m_Name,d))
   t=self.ref(t,d['m_Father'])
  return res

class Reader:
 def __init__(self,b,scene):self.b=b;self.p=0;self.scene=scene
 def read(self,fmt):
  vs=struct.unpack_from('<'+fmt,self.b,self.p);self.p+=struct.calcsize('<'+fmt);return vs[0]if len(vs)==1 else list(vs)
 def align(self):self.p=(self.p+3)//4*4
 def string(self):
  n=self.read('i');assert 0<=n<10**7,n
  v=self.b[self.p:self.p+n].decode('utf8');self.p+=n;self.align();return v
 def value(self,t):
  if t=='System.String':return self.string()
  if t in ['System.Boolean','System.Byte']:
   v=self.read('B');self.align();return v
  if t in ['System.Int32','System.UInt32','System.Single','System.Int64','System.UInt64','System.Double']:return self.read({'System.Int32':'i','System.UInt32':'I','System.Single':'f','System.Int64':'q','System.UInt64':'Q','System.Double':'d'}[t])
  if t.endswith('[]')or t.startswith('System.Collections.Generic.List'):
   n=self.read('i');assert 0<=n<100000,n;elem=t[:-2]if t.endswith('[]')else t[t.index('<')+1:-1]
   return [self.value(elem)for _ in range(n)]
  if self.scene.is_object(t):return {'m_FileID':self.read('i'),'m_PathID':self.read('i')}
  if t.startswith('UnityEngine.'):
   n={'Color':4,'Color32':1,'Vector2':2,'Vector3':3,'Vector4':4,'Quaternion':4,'Rect':4,'Bounds':6,'Matrix4x4':16,'LayerMask':1}[t.split('.')[-1]]
   return self.read('f'*n)
  d=self.scene.types[t]
  if d['kind']=='enum':return self.read('i')
  return {f['name']:self.value(f['type'])for f in self.scene.fields(t)}
