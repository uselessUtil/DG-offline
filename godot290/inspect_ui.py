import pathlib,zipfile,json,sys
import UnityPy
from UnityPy.helpers.TypeTreeGenerator import TypeTreeGenerator
sys.path.insert(0,str(pathlib.Path(__file__).parent))
from prepare import SOURCE,WORK
z=zipfile.ZipFile(SOURCE); cache={}
def asset(name):
    name=name.rsplit('/',1)[-1];prefix='assets/bin/Data/'+name
    if prefix in z.namelist():return z.read(prefix)
    pieces=[];i=0
    while prefix+'.split'+str(i) in z.namelist():pieces.append(z.read(prefix+'.split'+str(i)));i+=1
    if not pieces:raise KeyError(name)
    return b''.join(pieces)
def objects(name):
    if name not in cache:
        cache[name]={o.path_id:o for o in UnityPy.load(asset(name)).objects}
        for o in cache[name].values():o.assets_file.name=name.rsplit('/',1)[-1]
    return cache[name]
def deref(p,owner):
    if not p['m_PathID']:return None
    if not p['m_FileID']:return owner.assets_file.objects[p['m_PathID']]
    return objects(owner.assets_file.externals[p['m_FileID']-1].path)[p['m_PathID']]
gen=TypeTreeGenerator('4.6.5p1');gen.load_local_dll_folder(str(WORK/'reference/Managed'))
def read(o):
    d=o.read_typetree(check_read=False)
    if o.type.name=='MonoBehaviour':
        s=deref(d['m_Script'],o).read_typetree()
        name=(s['m_Namespace']+'.' if s['m_Namespace'] else '')+s['m_ClassName']
        d=o.read_typetree(nodes=gen.get_nodes_up(s['m_AssemblyName'],name))
        d['_class']=name
    return d
def main():
    resource=next(o for o in objects('mainData').values() if o.type.name=='ResourceManager')
    refs=dict(read(resource)['m_Container'])
    (WORK/'resources.json').write_text(json.dumps(refs,ensure_ascii=False,indent=2))
    page=deref(refs['prefab/mainmenu/mainmenupage/pagescratch'],resource) if 'prefab/mainmenu/mainmenupage/pagescratch' in refs else deref(refs['prefab/mainmenu/mainmenupage/pageothers'],resource)
    found={}
    for o in page.assets_file.objects.values():
        if o.type.name!='MonoBehaviour':continue
        try:d=read(o)
        except Exception as ex:print(o.path_id,type(ex).__name__,str(ex)[:200]);continue
        if d['_class'] not in found:found[d['_class']]=d
    (WORK/'ui-samples.json').write_text(json.dumps(found,ensure_ascii=False,indent=2))
    print(json.dumps(found,ensure_ascii=False)[:18000])
if __name__=='__main__':main()
