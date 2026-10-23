"""Export verified Unity 4 map geometry/legacy curves for Unity 5 runtime rebuild.

No native bundle or executable component is copied into the Unity 5 loader.
Only the exact map/door hierarchies referenced by the donor area table are read.
"""
import base64
import hashlib
import io
import json
import struct
from pathlib import Path
import UnityPy
from UnityPy.files.SerializedFile import SerializedFile
from UnityPy.helpers.MeshHelper import MeshHandler

class Writer:
    def __init__(self):self.stream=io.BytesIO()
    def integer(self,n):self.stream.write(struct.pack('<i',n))
    def number(self,n):self.stream.write(struct.pack('<f',n))
    def string(self,s):
        data=s.encode('utf8');self.integer(len(data));self.stream.write(data)
    def vector(self,d,keys):
        for k in keys:self.number(d[k])

def create(backup,tables,build):
    areas=[r for r in json.loads((tables/'MasterDataArea.json').read_text(),strict=False) if r['fix_id']]
    wanted={r[k].lower() for r in areas for k in ['mesh_map','mesh_door'] if r[k]}
    result={};records=[];previews={}
    previous=SerializedFile.set_version
    SerializedFile.set_version=lambda self,value:previous(self,value.splitlines()[0])
    try:
        for path in sorted(backup.rglob('CAB-*')):
            raw=path.read_bytes();env=UnityPy.load(raw)
            objects={o.path_id:o for o in env.objects}
            bundle=next(o for o in env.objects if o.type.name=='AssetBundle').read_typetree(check_read=False)
            entries=[(n.lower(),r['asset']) for n,r in bundle['m_Container'] if n.lower() in wanted and objects[r['asset']['m_PathID']].type.name=='GameObject']
            if not entries:continue
            def tree(ptr,kind=None):
                assert ptr['m_FileID']==0,'External map object'
                obj=objects[ptr['m_PathID']]
                if kind:assert obj.type.name==kind,(kind,obj.type.name)
                return obj,obj.read_typetree(check_read=False)
            for name,ptr in entries:
                assert name not in result,'Duplicate map '+name
                nodes=[];clips=[];shader_names=set()
                def visit(pointer,parent,nodepath):
                    obj,go=tree(pointer,'GameObject')
                    components={}
                    for _,p in go['m_Component']:
                        component,t=tree(p)
                        components.setdefault(component.type.name,[]).append((component,t))
                    assert set(components)<= {'Transform','MeshFilter','MeshRenderer','Animation','MonoBehaviour'},set(components)
                    transform=components['Transform'][0][1]
                    row={'name':go['m_Name'],'parent':parent,'path':nodepath,'transform':transform,'layer':go['m_Layer'],'active':go['m_IsActive']}
                    if 'MeshFilter' in components:
                        mesh_obj,_=tree(components['MeshFilter'][0][1]['m_Mesh'],'Mesh')
                        mesh=MeshHandler(mesh_obj.read(),version=(4,1,0,4));mesh.process()
                        assert len(mesh.src.m_SubMeshes)==1 and mesh.src.m_SubMeshes[0].topology==0
                        row['mesh']={'vertices':mesh.m_Vertices,'uv':mesh.m_UV0,'triangles':mesh.m_IndexBuffer,'colors':mesh.m_Colors}
                        assert len(mesh.m_UV0)==len(mesh.m_Vertices) and len(mesh.m_IndexBuffer)%3==0
                        assert all(0<=i<len(mesh.m_Vertices) for i in mesh.m_IndexBuffer)
                        renderer=components['MeshRenderer'][0][1]
                        assert len(renderer['m_Materials'])==1
                        _,material=tree(renderer['m_Materials'][0],'Material')
                        assert material['m_Name']=='mapMaterial'
                        _,shader=tree(material['m_Shader'],'Shader');shader_names.add(shader['m_Name'])
                        row['enabled']=renderer['m_Enabled']
                    for component,t in components.get('MonoBehaviour',[]):
                        _,script=tree(t['m_Script'],'MonoScript')
                        assert script['m_ClassName'] in ('AnimationArray','AnimationEvent')
                        if script['m_ClassName']=='AnimationArray':
                            assert parent==-1
                            data=component.get_raw_data();length=struct.unpack_from('<i',data,20)[0]
                            offset=(24+length+3)//4*4
                            count=struct.unpack_from('<i',data,offset)[0];offset+=4
                            assert count==4 and len(data)==offset+8*count
                            for i in range(count):
                                fileid,pid=struct.unpack_from('<ii',data,offset+i*8)
                                _,clip=tree({'m_FileID':fileid,'m_PathID':pid},'AnimationClip')
                                assert not clip['m_Compressed'] and not clip['m_CompressedRotationCurves'] and not clip['m_FloatCurves']
                                clips.append(clip)
                    idx=len(nodes);nodes.append(row)
                    for child in transform['m_Children']:
                        _,ct=tree(child,'Transform');_,cg=tree(ct['m_GameObject'],'GameObject')
                        visit(ct['m_GameObject'],idx,(nodepath+'/' if nodepath else '')+cg['m_Name'])
                visit(ptr,-1,'')
                w=Writer();w.integer(4001);w.integer(len(nodes))
                for row in nodes:
                    w.string(row['name']);w.integer(row['parent']);w.integer(row['layer']);w.integer(int(row['active']))
                    t=row['transform'];w.vector(t['m_LocalPosition'],'xyz');w.vector(t['m_LocalRotation'],'xyzw');w.vector(t['m_LocalScale'],'xyz')
                    mesh=row.get('mesh');w.integer(len(mesh['vertices']) if mesh else 0)
                    if mesh:
                        for i,v in enumerate(mesh['vertices']):
                            for f in v:w.number(f)
                            for f in mesh['uv'][i]:w.number(f)
                            for f in (mesh['colors'][i] if mesh['colors'] else [1,1,1,1]):w.number(f)
                        w.integer(len(mesh['triangles']))
                        for i in mesh['triangles']:w.integer(i)
                        w.integer(int(row['enabled']))
                w.integer(len(clips));paths={r['path'] for r in nodes}
                for clip in clips:
                    w.string(clip['m_Name']);w.integer(clip['m_WrapMode']);w.number(clip['m_SampleRate'])
                    curves=[]
                    for field,prop,axes in [('m_PositionCurves','localPosition','xyz'),('m_RotationCurves','localRotation','xyzw'),('m_ScaleCurves','localScale','xyz')]:
                        for c in clip[field]:
                            assert c['path'] in paths,(name,c['path'],paths)
                            for axis in axes:curves.append((c,prop+'.'+axis,axis))
                    w.integer(len(curves))
                    for c,prop,axis in curves:
                        w.string(c['path']);w.string(prop);curve=c['curve']
                        # Serialized curve infinity 2 means hold, not public
                        # WrapMode.Loop (also 2). Clip-level looping is separate.
                        assert curve['m_PreInfinity']==2 and curve['m_PostInfinity']==2
                        w.integer(8);w.integer(8);w.integer(len(curve['m_Curve']))
                        for key in curve['m_Curve']:
                            w.number(key['time'])
                            for k in ['value','inSlope','outSlope']:w.number(key[k][axis])
                    w.integer(len(clip['m_Events']))
                    for event in clip['m_Events']:
                        assert event['functionName'] in ['AnimEvent_PlaySE_DoorOpen_Normal','AnimEvent_PlaySE_DoorBossTataki','AnimEvent_PlaySE_DoorOpen_Boss','AnimEvent_DoorSmoke'],event['functionName']
                        assert event['objectReferenceParameter']['m_PathID']==0
                        w.number(event['time']);w.string(event['functionName'])
                data=w.stream.getvalue();result[name]=data
                records.append({'name':name,'cab':str(path.relative_to(backup)),'cab_sha256':hashlib.sha256(raw).hexdigest(),'bytes':len(data),'sha256':hashlib.sha256(data).hexdigest(),'nodes':len(nodes),'meshes':sum('mesh' in r for r in nodes),'clips':len(clips),'shaders':sorted(shader_names)})
                previews[name]={'nodes':nodes,'clips':clips}
    finally:SerializedFile.set_version=previous
    missing=sorted(wanted-result.keys())
    assert set(missing)<= {'map_6001_mesh','map_6001_door','map_6002_mesh','map_6002_door'},missing
    aliases={}
    for name in missing:
        source='map_0001_'+name.rsplit('_',1)[1]
        assert source in result,(source,sorted(result))
        result[name]=result[source];aliases[name]=source
    lines=['using System;','internal static class MapData400 {','public static byte[] Read(string name){switch(name.ToLowerInvariant()){']
    for name,data in sorted(result.items()):lines.append('case '+json.dumps(name)+': return Convert.FromBase64String('+json.dumps(base64.b64encode(data).decode())+');')
    lines+=['default:return null;}}}'];(build/'MapData400.cs').write_text('\n'.join(lines))
    (build/'map-geometry.json').write_text(json.dumps(previews,separators=(',',':')))
    report={'maps':records,'areas':len(areas),'resources':len(result),'missing_geometry_aliases':aliases,'source':'Original cache meshes, UVs, transforms and legacy animation curves; reconstructed at runtime, no native bundle conversion.'}
    (build/'map-restore.json').write_text(json.dumps(report,indent=2)+'\n')
    return report

if __name__=='__main__':
    import sys
    work=Path(sys.argv[1]);r=create(Path('/workspace/jp.gungho.dg'),work/'tables',work/'build');print(json.dumps({k:v for k,v in r.items() if k!='maps'},indent=2));print('SHADERS',sorted({s for x in r['maps'] for s in x['shaders']}))
