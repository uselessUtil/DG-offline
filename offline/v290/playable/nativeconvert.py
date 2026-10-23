"""Transcode Unity 4.1 objects to the target 4.6 binary layout, preserving content."""
import importlib
import UnityPy
from collections import Counter
get_node=importlib.import_module('UnityPy.files.ObjectReader').get_typetree_node
VERSION=(4,6,5,1)
# Values reported by the actual 2.9.0 Unity player on MuMu, not bypasses.
HASHES={1:0xa5034cb0,4:0x7b2bfb09,21:0xe7995a6a,23:0x9e74a915,
        28:0x54687809,33:0xb53c3687,43:0x3785eabd,48:0x138dceaa,74:0xeb88f048,111:0x3d84b67a,114:0x2bfc047d,
        115:0xe916ea52,198:0x3ac2c1ca,199:0x40ef231e}
def default(node):
    if node.m_Type=='string':return ''
    if node.m_Type=='TypelessData':return b''
    if node.m_Children and node.m_Children[0].m_Type=='Array':return []
    if not node.m_Children:return False if node.m_Type=='bool' else 0
    return {x.m_Name:default(x) for x in node.m_Children}
def project(value,node,path=''):
    if node.m_Type in ('string','TypelessData') or not node.m_Children:return value
    if node.m_Children[0].m_Type=='Array':
        child=node.m_Children[0].m_Children[1]
        if not child.m_Children:return value
        return [project(v,child,path+'[]') for v in value]
    if node.m_Type=='pair' and isinstance(value,(tuple,list)):
        return tuple(project(v,n,path+'/'+n.m_Name) for v,n in zip(value,node.m_Children))
    if not isinstance(value,dict):raise ValueError((path,node.m_Type,type(value)))
    result={}
    for child in node.m_Children:
        key=child.m_Name
        result[key]=project(value[key],child,path+'/'+key) if key in value else default(child)
    lost=set(value)-set(result)
    for key in lost:
        # Bounds moved out of Animation; obsolete empty blend-shape / muscle arrays.
        if key=='m_UserAABB':continue
        if key in ('m_ShapeVertices','m_AdditionalCurveIndexArray') and not value[key]:continue
        raise ValueError('Unmapped non-empty field '+path+'/'+key)
    return result
def convert(raw):
    env=UnityPy.load(raw);file=next(iter(env.files.values()))
    expected={};raw_scripts={};counts=Counter()
    for o in env.objects:
        if o.type.name=='MonoBehaviour':
            # Identical AnimationArray/Event fields plus identical serialized base.
            raw_scripts[o.path_id]=o.get_raw_data();continue
        d=o.read_typetree()
        target=get_node(o.class_id,VERSION)
        if o.type.name=='Mesh':
            assert not d['m_Shapes'] and not d['m_ShapeVertices'],'Nonempty blend shape needs explicit conversion'
            d['m_Shapes']=default(next(n for n in target.m_Children if n.m_Name=='m_Shapes'))
        out=project(d,target,o.type.name)
        if o.type.name=='Material':out['m_CustomRenderQueue']=-1
        if o.type.name=='AnimationClip':
            assert d['m_AnimationType']==1 and d['m_MuscleClipSize']==0,'Only legacy animation conversion supported'
            out['m_UseHighQualityCurve']=True
        if o.type.name=='AssetBundle':
            assert all(k in HASHES for k,v in out['m_ClassCompatibility']),'Unverified target native type'
            out['m_ClassCompatibility']=[(k,HASHES[k]) for k,v in out['m_ClassCompatibility']]
            out['m_RuntimeCompatibility']=1
        o.save_typetree(out,target);expected[o.path_id]=out;counts[o.type.name]+=1
    file.set_version('4.6.5p1')
    converted=file.save()
    loaded=UnityPy.load(converted)
    for o in loaded.objects:
        if o.path_id in raw_scripts:
            assert o.get_raw_data()==raw_scripts[o.path_id]
        else:
            assert o.read_typetree()==expected[o.path_id],(o.path_id,o.type.name,'roundtrip differs')
    return converted,dict(counts)
