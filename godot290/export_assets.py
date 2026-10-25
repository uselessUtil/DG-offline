"""Lossless texture export and serialized NGUI scene conversion (Docker only)."""
import hashlib,json,pathlib,sys,shutil
from UnityPy.helpers.MeshHelper import MeshHandler
from inspect_ui import objects,read,deref
from prepare import PROJECT,WORK,SOURCE,EXPECTED
OUT=PROJECT/'Reference';OUT.mkdir(exist_ok=True)
textures={};atlases={};fonts={};meshes={};issues=[];counts={}
def ident(o):return str(o.assets_file.name)+'_'+str(o.path_id)
def texture(o):
    if o is None:return ''
    key=ident(o)
    if key not in textures:
        data=o.read();name='textures/'+key+'.png';(OUT/'textures').mkdir(exist_ok=True)
        data.image.save(OUT/name)
        textures[key]={'name':data.m_Name,'path':name,'width':data.m_Width,'height':data.m_Height}
    return textures[key]['path']
def material(o):
    if o is None:return ''
    d=read(o)
    for k,v in d.get('m_SavedProperties',{}).get('m_TexEnvs',[]):
        if (k.get('name') if isinstance(k,dict) else k)=='_MainTex':return texture(deref(v['m_Texture'],o))
    return ''
def atlas(o):
    if o is None:return ''
    key=ident(o)
    if key not in atlases:
        d=read(o)
        if d.get('mReplacement',{}).get('m_PathID'):return atlas(deref(d['mReplacement'],o))
        atlases[key]={'texture':material(deref(d['material'],o)),'sprites':d['sprites'],'pixelSize':d.get('mPixelSize',1)}
    return key
def font(o):
    if o is None:return ''
    key=ident(o)
    if key not in fonts:
        d=read(o)
        if d.get('mReplacement',{}).get('m_PathID'):return font(deref(d['mReplacement'],o))
        fonts[key]={'texture':material(deref(d['mMat'],o)),'font':d['mFont'],'atlas':atlas(deref(d['mAtlas'],o)) if d.get('mAtlas',{}).get('m_PathID') else ''}
        f=d['mFont'];tex=fonts[key]['texture']
        if tex:
            lines=['info face="'+key+'" size='+str(f['mSize'])+' bold=0 italic=0 charset="" unicode=1 stretchH=100 smooth=1 aa=1 padding=0,0,0,0 spacing=0,0',
                   f'common lineHeight={f["mSize"]} base={f["mBase"]} scaleW={f["mWidth"]} scaleH={f["mHeight"]} pages=1 packed=0',
                   f'page id=0 file="../{tex}"',f'chars count={len(f["mSaved"])}']
            for g in f['mSaved']:
                lines.append('char '+' '.join(f'{k}={v}' for k,v in {'id':g['index'],'x':g['x'],'y':g['y'],'width':g['width'],'height':g['height'],'xoffset':g['offsetX'],'yoffset':g['offsetY'],'xadvance':g['advance'],'page':0,'chnl':15}.items()))
            (OUT/'fonts').mkdir(exist_ok=True);(OUT/'fonts'/f'{key}.fnt').write_text('\n'.join(lines))
    return key
def mesh(o):
    key=ident(o)
    if key not in meshes:
        h=MeshHandler(o.read(),version=(4,6,5,1));h.process()
        meshes[key]={'vertices':h.m_Vertices,'uv':h.m_UV0,'indices':h.m_IndexBuffer}
    return key
def component(o):
    try:d=read(o)
    except Exception as ex:
        issues.append({'id':ident(o),'type':o.type.name,'error':str(ex)});return {'type':o.type.name,'unsupported':True}
    kind=d.get('_class',o.type.name);counts[kind]=counts.get(kind,0)+1
    if kind in ['UISprite','UISlicedSprite','UITiledSprite','UIFilledSprite']:
        d['atlas']=atlas(deref(d['mAtlas'],o))
    if kind=='UILabel':d['font']=font(deref(d['mFont'],o))
    if kind=='UITexture' and d.get('mTex',{}).get('m_PathID'):d['texture']=texture(deref(d['mTex'],o))
    if kind in ['MeshFilter','MeshCollider'] and d['m_Mesh']['m_PathID']:d['mesh']=mesh(deref(d['m_Mesh'],o))
    if kind=='MeshRenderer':d['textures']=[material(deref(p,o)) for p in d['m_Materials']]
    d['type']=kind
    return d
def node(go):
    d=read(go);components=[deref(p,go) for _,p in d['m_Component']]
    tr=next(c for c in components if c.type.name=='Transform');td=read(tr)
    result={'id':ident(go),'name':d['m_Name'],'active':d['m_IsActive'],'position':td['m_LocalPosition'],'rotation':td['m_LocalRotation'],'scale':td['m_LocalScale'],'components':[],'children':[]}
    for c in components:
        if c.type.name=='Transform':continue
        result['components'].append(component(c))
    for p in td['m_Children']:
        child=deref(p,tr);result['children'].append(node(deref(read(child)['m_GameObject'],child)))
    return result
def main():
    # Only regenerate owned output folders, never source APKs or recovered art.
    assert OUT.resolve()==pathlib.Path('/tmp/dg-godot290/project/Reference')
    for name in ['textures','fonts','scenes']:
        folder=OUT/name
        if folder.exists():shutil.rmtree(folder)
    r=next(o for o in objects('mainData').values() if o.type.name=='ResourceManager');refs=dict(read(r)['m_Container'])
    pages={}
    for key,p in refs.items():
        if not key.startswith('prefab/'):continue
        try:
            obj=deref(p,r)
            if obj.type.name!='GameObject':continue
            data=node(obj);filename='scenes/'+key.removeprefix('prefab/')+'.json'
            (OUT/filename).parent.mkdir(parents=True,exist_ok=True)
            (OUT/filename).write_text(json.dumps(data,ensure_ascii=False,separators=(',',':')))
            pages[key]=filename
        except Exception as ex:issues.append({'path':key,'error':str(ex)})
    # Export explicit atlas resources even if currently unused by a page.
    for key,p in refs.items():
        if key.startswith('carddm/atlas/'):
            obj=deref(p,r)
            if obj.type.name=='GameObject':
                for _,cp in read(obj)['m_Component']:
                    c=deref(cp,obj)
                    if c.type.name=='MonoBehaviour' and read(c).get('_class')=='UIAtlas':atlas(c)
    index={'scenes':pages,'textures':textures,'atlases':atlases,'fonts':fonts,'meshes':meshes,'reference_sha256':EXPECTED}
    (OUT/'index.json').write_text(json.dumps(index,ensure_ascii=False,separators=(',',':')))
    report={'prefabs':len(pages),'textures':len(textures),'atlases':len(atlases),'fonts':len(fonts),'component_types':counts,'issues':issues}
    (WORK/'asset-conversion.json').write_text(json.dumps(report,ensure_ascii=False,indent=2))
    print(json.dumps({'prefabs':len(pages),'textures':len(textures),'atlases':len(atlases),'fonts':len(fonts),'issues':len(issues)},ensure_ascii=False))
if __name__=='__main__':main()
