"""Verify the exact APK prefab, including a negative control for prototype 11."""
import json
import zipfile
import UnityPy


def verify(apk, output):
    prefix = 'assets/bin/Data/'
    with zipfile.ZipFile(apk) as archive:
        def asset_bytes(name):
            path = prefix+name
            if path in archive.namelist():
                return archive.read(path)
            chunks = []
            while path+'.split'+str(len(chunks)) in archive.namelist():
                chunks.append(archive.read(path+'.split'+str(len(chunks))))
            assert chunks, 'Missing serialized asset '+name
            return b''.join(chunks)
        main = UnityPy.load(asset_bytes('mainData'))
        manager = next(o for o in main.objects if o.type.name == 'ResourceManager')
        container = dict(manager.read_typetree()['m_Container'])

        def prefab(name):
            ref = container['prefab/mainmenu/mainmenupage/'+name]
            path = manager.assets_file.externals[ref['m_FileID']-1].path
            env = UnityPy.load(asset_bytes(path))
            objects = {o.path_id: o for o in env.objects}
            nodes = {o.read_typetree()['m_Name']: o for o in env.objects if o.type.name == 'GameObject'}
            return objects, nodes

        objects, nodes = prefab('_old_pageshopbuytip')
        _, broken_nodes = prefab('pageshopbuytip')
        required = ['MessageMain', 'ButtonYes', 'ButtonNo']
        assert all(n not in broken_nodes for n in required), 'Negative control does not reproduce the missing controls'
        evidence = {'old_title_only_container': sorted(broken_nodes), 'restored_controls': {}}
        for name in required:
            tree = nodes[name].read_typetree()
            assert tree['m_IsActive'], name+' is disabled'
            components = []
            for component in tree['m_Component']:
                ref = component[1] if isinstance(component, tuple) else component.get('component', component)
                assert ref['m_FileID'] == 0
                components.append(objects[ref['m_PathID']])
            transform = next(c.read_typetree() for c in components if c.type.name == 'Transform')
            evidence['restored_controls'][name] = {'active': True, 'position': transform['m_LocalPosition'],
                'components': [c.type.name for c in components]}
            # Every parent below the prefab root must already be enabled.
            parent = transform
            while parent['m_Father']['m_PathID']:
                parent = objects[parent['m_Father']['m_PathID']].read_typetree()
                go = objects[parent['m_GameObject']['m_PathID']].read_typetree()
                if parent['m_Father']['m_PathID']:
                    assert go['m_IsActive'], 'Disabled control parent '+go['m_Name']
            if name.startswith('Button'):
                collider = next(c.read_typetree() for c in components if c.type.name == 'BoxCollider')
                assert collider['m_Enabled'] and collider['m_Size']['x'] > 0 and collider['m_Size']['y'] > 0
                evidence['restored_controls'][name]['collider'] = collider
            else:
                found_label = False
                for c in components:
                    if c.type.name != 'MonoBehaviour':
                        continue
                    base = c.read_typetree(check_read=False)
                    script = base['m_Script']
                    if script['m_FileID']:
                        script_path = c.assets_file.externals[script['m_FileID']-1].path
                        script_env = UnityPy.load(asset_bytes(script_path))
                        script_obj = next(o for o in script_env.objects if o.path_id == script['m_PathID'])
                    else:
                        script_obj = objects[script['m_PathID']]
                    script_tree = script_obj.read_typetree()
                    if script_tree.get('m_ClassName') == 'UILabel':
                        found_label = True
                assert found_label, 'MessageMain has no actual UILabel component'
        (output/'shop-prefab-evidence.json').write_text(json.dumps(evidence, ensure_ascii=False, indent=2)+'\n')
    return ('PASS exact APK prefab: enabled MessageMain UILabel, enabled Yes/No colliders and control parents; '
            'negative control confirms all three controls absent from the previously selected title-only container. '
            'Android rendering/touch remain unverified.\n')
