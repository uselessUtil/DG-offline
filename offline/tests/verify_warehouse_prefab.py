import json
import zipfile
import struct
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
    cache={}
    def deref(ref,owner):
        if ref['m_FileID']:
            path=owner.assets_file.externals[ref['m_FileID']-1].path
            if path not in cache:cache[path]={o.path_id:o for o in UnityPy.load(asset(path)).objects}
            return cache[path][ref['m_PathID']]
        return owner.assets_file.objects[ref['m_PathID']]
    def components(go):
        return [deref(c[1] if isinstance(c,tuple) else c.get('component',c),go)
                for c in go.read_typetree()['m_Component']]
    def node(page,name):
        ptr=container['prefab/mainmenu/mainmenupage/'+page]
        path=resource.assets_file.externals[ptr['m_FileID']-1].path
        objects={o.path_id:o for o in UnityPy.load(asset(path)).objects}
        return next(o for o in objects.values() if o.type.name=='GameObject' and o.read_typetree()['m_Name']==name)
    source=node('pagequestselectfriend','ListSort')
    tr=next(c.read_typetree() for c in components(source) if c.type.name=='Transform')
    original_hit=next(c.read_typetree() for c in components(source) if c.type.name=='BoxCollider')
    template=node('pagepartyblendbuildup','SwitchButtonClear')
    native=next(c.read_typetree() for c in components(template) if c.type.name=='BoxCollider')
    assert native['m_Center']['x']==0 and native['m_Size']['x']==184 and native['m_Size']['y']==38
    template_tr=next(c.read_typetree() for c in components(template) if c.type.name=='Transform')
    children={}
    for child in template_tr['m_Children']:
        ct=deref(child,template).read_typetree();go=deref(ct['m_GameObject'],template)
        children[go.read_typetree()['m_Name']]=(go,ct)
        assert not any(c.type.name=='BoxCollider' for c in components(go)),'Child steals button touch'
    assert set(children)=={'ButtonBG','ButtonMsg'}
    background,bgtr=children['ButtonBG']
    assert bgtr['m_LocalScale']['x']==184 and bgtr['m_LocalScale']['y']==41
    sprite=None
    for c in components(background):
        if c.type.name!='MonoBehaviour':continue
        base=c.read_typetree(check_read=False)
        if deref(base['m_Script'],c).read_typetree()['m_ClassName']=='UISprite':sprite=c
    assert sprite is not None
    # Exact 1.0.6 UISprite payload: empty MonoBehaviour name, UIWidget base,
    # atlas PPtr, then the serialized sprite name. Assert rather than guess.
    raw=sprite.get_raw_data()
    assert struct.unpack_from('<I',raw,20)[0]==0
    assert struct.unpack_from('<i',raw,56)[0]==4,'Background is not center-pivoted'
    size=struct.unpack_from('<I',raw,72)[0]
    assert raw[76:76+size]==b'buttonn_small'
    atlas=deref(dict(zip(['m_FileID','m_PathID'],struct.unpack_from('<ii',raw,64))),sprite)
    assert b'buttonn_small' in atlas.get_raw_data(),'Referenced atlas lacks native background'
    x=tr['m_LocalPosition']['x'];scale=tr['m_LocalScale']['x']
    right=-x;clone_x=right-92*scale;left=clone_x-92*scale
    original_right=x+(original_hit['m_Center']['x']+original_hit['m_Size']['x']/2)*scale
    assert -320<left<right<320 and original_right<left
    evidence={'page':'pagequestselectfriend','row_source':'ListSort','cloned_prefab':'PagePartyBlendBuildUp/SwitchButtonClear',
              'background_sprite':'buttonn_small','background_dimensions':[184,41],'collider':[184,38],
              'source_x':x,'clone_x':clone_x,'clone_left':left,'clone_right':right,'right_inset':320-right,
              'mirrors_original_left_inset':True,'no_sort_button_overlap':True,'no_child_colliders':True}
    (output/'warehouse-prefab-evidence.json').write_text(json.dumps(evidence,ensure_ascii=False,indent=2)+'\n')
    return 'PASS exact packaged warehouse button: original clear-button prefab and buttonn_small atlas sprite, centered184x38 collider,184x41 background, no child touch interception; mirrored row inset aligns right edge, no sort-button overlap. Android rendering/touch pending.\n'
