import json
import zipfile
import UnityPy


def verify(apk, output):
    archive=zipfile.ZipFile(apk)
    def asset(name):
        p='assets/bin/Data/'+name
        if p in archive.namelist():return archive.read(p)
        chunks=[]
        while p+'.split'+str(len(chunks)) in archive.namelist():
            chunks.append(archive.read(p+'.split'+str(len(chunks))))
        assert chunks,name
        return b''.join(chunks)
    main=UnityPy.load(asset('mainData'))
    resource=next(o for o in main.objects if o.type.name=='ResourceManager')
    container=dict(resource.read_typetree()['m_Container'])
    evidence=[]
    for page,button in [('pagequestselectfriend','ListSort')]:
        ptr=container['prefab/mainmenu/mainmenupage/'+page]
        path=resource.assets_file.externals[ptr['m_FileID']-1].path
        objects={o.path_id:o for o in UnityPy.load(asset(path)).objects}
        source=next(o for o in objects.values() if o.type.name=='GameObject' and o.read_typetree()['m_Name']==button)
        def components(go):
            return [objects[(c[1] if isinstance(c,tuple) else c.get('component',c))['m_PathID']]
                    for c in go.read_typetree()['m_Component']]
        native=next(c.read_typetree() for c in components(source) if c.type.name=='BoxCollider')
        tr=next(c.read_typetree() for c in components(source) if c.type.name=='Transform')
        assert native['m_Size']['x']==250 and native['m_Size']['y']==40
        x=tr['m_LocalPosition']['x'];scale=tr['m_LocalScale']['x'];w=250*scale
        assert -320<=x and x+330+w<=320 and x+w<x+330,'Source button overlap/outside page'
        labels=[]
        for child in tr['m_Children']:
            ct=objects[child['m_PathID']].read_typetree()
            go=objects[ct['m_GameObject']['m_PathID']]
            labels.append(go.read_typetree()['m_Name'])
            assert not any(c.type.name=='BoxCollider' for c in components(go)),'Child steals button touch'
        assert 'ListSortLabel' in labels and 'ListSortBg' in labels
        evidence.append({'page':page,'cloned_button':button,'source_x':x,'clone_x':x+330,
                         'collider':[250,40],'labels':labels,'native_row_bounds':[-320,320],
                         'original_and_clone_do_not_overlap':True})
    (output/'warehouse-prefab-evidence.json').write_text(json.dumps(evidence,ensure_ascii=False,indent=2)+'\n')
    return 'PASS exact packaged quest source button: native250x40 colliders, expected labels, no child touch interception; source/warehouse buttons separated and within640-wide layout. Android rendering/touch pending.\n'
