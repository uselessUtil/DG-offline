"""Export original map geometry/UVs; never treat a Unity texture atlas as a backdrop."""
import json,zipfile,hashlib,re
import UnityPy
from UnityPy.helpers.MeshHelper import MeshHandler
from prepare import SOURCE,EXPECTED,PROJECT

def identity():return [[int(r==c) for c in range(4)] for r in range(4)]
def mul(a,b):return [[sum(a[r][k]*b[k][c] for k in range(4)) for c in range(4)] for r in range(4)]
def matrix(t):
    p=t['m_LocalPosition'];s=t['m_LocalScale'];q=t['m_LocalRotation']
    x,y,z,w=[q[k] for k in 'xyzw']
    rot=[[1-2*(y*y+z*z),2*(x*y-z*w),2*(x*z+y*w)],
        [2*(x*y+z*w),1-2*(x*x+z*z),2*(y*z-x*w)],
        [2*(x*z-y*w),2*(y*z+x*w),1-2*(x*x+y*y)]]
    m=identity()
    for r in range(3):
        for c in range(3):m[r][c]=rot[r][c]*s['xyz'[c]]
        m[r][3]=p['xyz'[r]]
    return m

def main():
    assert hashlib.sha256(SOURCE.read_bytes()).hexdigest()==EXPECTED
    out=PROJECT/'Reference/battle';out.mkdir(exist_ok=True)
    maps={};textures={}
    with zipfile.ZipFile(SOURCE) as apk:
        for name in apk.namelist():
            if not name.startswith('assets/dg-native/'):continue
            env=UnityPy.load(apk.read(name));objs={o.path_id:o for o in env.objects}
            def obj(ptr):return objs.get(ptr['m_PathID'])
            roots=[]
            for o in objs.values():
                if o.type.name=='Texture2D':
                    n=o.peek_name()
                    if re.fullmatch(r'map_\d+_tex',n or ''):
                        o.read().image.save(out/(n+'.png'));textures[n]='battle/'+n+'.png'
                elif o.type.name=='GameObject':
                    n=o.peek_name()
                    if re.fullmatch(r'map_\d+_(mesh|door)',n or ''):roots.append(o)
            def walk(go,parent):
                gd=go.read_typetree();cs=[obj(p) for _,p in gd['m_Component']]
                tr=next(o for o in cs if o.type.name=='Transform');td=tr.read_typetree();world=mul(parent,matrix(td));parts=[]
                mf=next((o for o in cs if o.type.name=='MeshFilter'),None)
                if mf:
                    mo=obj(mf.read_typetree()['m_Mesh']);h=MeshHandler(mo.read(),version=(4,6,5,1));h.process()
                    vs=[[sum(world[r][k]*[*v,1][k] for k in range(4)) for r in range(3)] for v in h.m_Vertices]
                    parts.append({'name':gd['m_Name'],'vertices':vs,'uv':h.m_UV0,'indices':h.m_IndexBuffer})
                for p in td['m_Children']:
                    ct=obj(p);cg=obj(ct.read_typetree()['m_GameObject']);parts+=walk(cg,world)
                return parts
            for go in roots:maps[go.peek_name()]=walk(go,identity())
    (out/'maps.json').write_text(json.dumps({'maps':maps,'textures':textures},separators=(',',':')))
    print('Map roots',len(maps),'original textures',len(textures))

if __name__=='__main__':main()
