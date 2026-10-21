import json
import zipfile
import UnityPy


def verify(apk, output):
    prefix = 'assets/bin/Data/'
    with zipfile.ZipFile(apk) as archive:
        def asset(name):
            path = prefix+name
            if path in archive.namelist():
                return archive.read(path)
            chunks = []
            while path+'.split'+str(len(chunks)) in archive.namelist():
                chunks.append(archive.read(path+'.split'+str(len(chunks))))
            assert chunks, name
            return b''.join(chunks)
        main = UnityPy.load(asset('mainData'))
        resource = next(o for o in main.objects if o.type.name == 'ResourceManager')
        container = dict(resource.read_typetree()['m_Container'])
        scripts = {}

        def objects_for(name):
            ptr = container['prefab/mainmenu/mainmenupage/'+name]
            path = resource.assets_file.externals[ptr['m_FileID']-1].path
            return {o.path_id: o for o in UnityPy.load(asset(path)).objects}

        def components(objects, go):
            return [objects[(c[1] if isinstance(c, tuple) else c.get('component', c))['m_PathID']]
                    for c in go.read_typetree()['m_Component']]

        def script_names(objects, go):
            names = []
            for c in components(objects, go):
                if c.type.name != 'MonoBehaviour':
                    continue
                ptr = c.read_typetree(check_read=False)['m_Script']
                if ptr['m_FileID']:
                    path = c.assets_file.externals[ptr['m_FileID']-1].path
                    if path not in scripts:
                        scripts[path] = {o.path_id: o for o in UnityPy.load(asset(path)).objects}
                    script = scripts[path][ptr['m_PathID']]
                else:
                    script = objects[ptr['m_PathID']]
                names.append(script.read_typetree()['m_ClassName'])
            return names

        root = objects_for('pagegacharoot')
        go = {o.read_typetree()['m_Name']: o for o in root.values() if o.type.name == 'GameObject'}
        for name in ['PageRoot', 'PageTitle', 'PageTitleLabel', 'GachaType3', 'ListObject', 'Element00', 'Element01', 'Element02']:
            assert name in go, 'Missing native scratch node '+name
        card = go['Element02']
        original_cards = {}
        for name in ['Element00', 'Element01', 'Element02']:
            tree = go[name].read_typetree()
            assert tree['m_IsActive'], 'Original card disabled '+name
            native = next(c.read_typetree() for c in components(root, go[name]) if c.type.name == 'BoxCollider')
            assert native['m_Enabled'] and native['m_Size']['x'] == 178 and native['m_Size']['y'] == 215
            transform = next(c.read_typetree() for c in components(root, go[name]) if c.type.name == 'Transform')
            original_cards[name] = {'parent':transform['m_Father'], 'position':transform['m_LocalPosition'], 'layer':tree['m_Layer']}
        assert all(v['parent'] == original_cards['Element02']['parent'] for v in original_cards.values())
        collider = next(c.read_typetree() for c in components(root, card) if c.type.name == 'BoxCollider')
        assert collider['m_Enabled'] and collider['m_Size'] == {'x':178.0, 'y':215.0, 'z':1.0}
        transform = next(c.read_typetree() for c in components(root, card) if c.type.name == 'Transform')
        children = {}
        def visit(tree):
            for child in tree['m_Children']:
                sub = root[child['m_PathID']].read_typetree()
                obj = root[sub['m_GameObject']['m_PathID']]
                children[obj.read_typetree()['m_Name']] = obj
                assert not any(c.type.name == 'BoxCollider' and c.read_typetree()['m_Enabled'] for c in components(root, obj)), 'Child collider would intercept pool card touch'
                visit(sub)
        visit(transform)
        for label in ['ElementLabel', 'ElementHint']:
            assert 'UILabel' in script_names(root, children[label]), 'Missing actual card UILabel '+label
        assert 'UISprite' in script_names(root, children['ElementObj'])
        assert 'UILabel' in script_names(root, go['PageTitleLabel'])
        confirm = objects_for('pagegachaconfirm')
        nodes = {o.read_typetree()['m_Name']: o for o in confirm.values() if o.type.name == 'GameObject'}
        for button in ['GachaStartOnce', 'GachaStartMore', 'PageTitle']:
            assert any(c.type.name == 'BoxCollider' and c.read_typetree()['m_Enabled'] for c in components(confirm, nodes[button])), 'Missing confirmation collider '+button
        for label in ['DialogTitle', 'DialogScratchDetail']:
            assert 'UILabel' in script_names(confirm, nodes[label])
        evidence = {'source_card':'PageGachaRoot/PageRoot/GachaType3/Element02',
            'reused_original_cards':original_cards,
            'collider':collider, 'source_labels':['ElementLabel','ElementHint'], 'source_image':'ElementObj (UISprite)',
            'source_has_no_child_colliders':True, 'original_confirmation_buttons':['GachaStartOnce','GachaStartMore','PageTitle'],
            'layout':'2 columns by 2 rows; positions and separation checked by actual adapter in VerifyCollabGacha',
            'android_rendering_touch':'pending user verification'}
        (output/'collab-prefab-evidence.json').write_text(json.dumps(evidence,ensure_ascii=False,indent=2)+'\n')
    return 'PASS exact APK collab template: source card collider 178x215, native UILabels/UISprite, no child colliders to intercept touches; original single/multi/back confirmation controls exist.\n'
