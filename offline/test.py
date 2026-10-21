"""Test the exact adapter and game assembly packaged in the generated APK.

The engine stand-in covers Resources, PlayerPrefs, math and selected UI flags, not Android,
rendering, native plug-ins, animations, touches or Unity scene execution.
"""
import argparse
import json
import os
from pathlib import Path
import shutil
import subprocess
import tempfile
import zipfile
from build import ROOT, CECIL, MANAGED, digest, run


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('report', type=Path)
    args = parser.parse_args()
    report = json.loads(args.report.read_text())
    artifact = Path(report['output'])
    if digest(artifact.read_bytes()) != report['sha256']:
        raise ValueError('APK differs from the build report')
    work = Path(report['work_directory'])
    test = Path(tempfile.mkdtemp(prefix='dg106-test-', dir='/tmp'))
    tools = test/'tools'
    tools.mkdir()
    managed = work/'original'/MANAGED
    for source in managed.glob('*.dll'):
        shutil.copy2(source,test/source.name)
    with zipfile.ZipFile(artifact) as archive:
        for name in ['Assembly-CSharp.dll','DivineGateOffline.dll']:
            (test/name).write_bytes(archive.read(MANAGED+name))
    tests = ROOT/'offline/tests'
    run('mcs','-out:'+str(tools/'VerifyReferences.exe'),'-r:'+CECIL,tests/'VerifyReferences.cs')
    run('mono',tools/'VerifyReferences.exe',managed,test)
    if report.get('recovered_artwork'):
        images = json.loads((args.report.parent/'recovered-images.json').read_text())
        expected = test/'expected-images.tsv'
        expected.write_text(''.join(key+'\t'+row['sha256']+'\n' for key,row in images['payloads'].items()))
        rects = test/'expected-sprites.tsv'
        rects.write_text(''.join(key.split('/',1)[0]+'\t'+key.split('/',1)[1]+'\t'+','.join(map(str,rect))+'\n'
                                 for key,rect in images['sprites'].items()))
        run('mcs','-out:'+str(tools/'VerifyRecoveredImages.exe'),tests/'VerifyRecoveredImages.cs')
        verified = subprocess.check_output(['mono',str(tools/'VerifyRecoveredImages.exe'),
            str(test/'DivineGateOffline.dll'),str(expected),str(rects)],text=True)
        print(verified)
        (args.report.parent/'recovered-image-tests.txt').write_text(verified)
        report['recovered_image_tests'] = verified.strip()
        if images.get('shadow_masks'):
            import io
            from PIL import Image, ImageChops
            packed = work/'build/RecoveredImages.bin'
            assert digest(packed.read_bytes()) == images['image_pack_sha256']
            with packed.open('rb') as stream:
                def image(key):
                    row = images['payloads'][key]
                    stream.seek(row['offset'])
                    value = Image.open(io.BytesIO(stream.read(row['size']))).convert('RGBA')
                    value.load()
                    return value
                for char in images['characters']:
                    rid = str(int(char['resource_id']))
                    portrait, shadow = image('portrait/'+rid), image('shadow/'+rid)
                    assert portrait.size == shadow.size
                    assert ImageChops.difference(portrait.getchannel('A'), shadow.getchannel('A')).getbbox() is None
                    assert all(extrema == (255,255) for extrema in shadow.convert('RGB').getextrema())
            run('mcs','-out:'+str(tools/'VerifyRenderFixes.exe'),'-r:'+CECIL,tests/'VerifyRenderFixes.cs')
            render = subprocess.check_output(['mono',str(tools/'VerifyRenderFixes.exe'),
                str(test/'Assembly-CSharp.dll'),str(test/'DivineGateOffline.dll')],text=True)
            render += 'PASS: '+str(images['shadow_masks'])+' white shadow masks preserve portrait alpha exactly. Android rendering remains unverified.\n'
            print(render)
            (args.report.parent/'render-fix-tests.txt').write_text(render)
            report['render_fix_tests'] = render.strip()
    run('mcs','-target:library','-out:'+str(test/'EngineSupport.dll'),'-r:'+str(managed/'UnityEngine.dll'),tests/'EngineSupport.cs')
    run('mcs','-out:'+str(tools/'TestEngine.exe'),'-r:'+CECIL,tests/'TestEngine.cs')
    run('mono',tools/'TestEngine.exe',managed/'UnityEngine.dll',test/'EngineSupport.dll',test/'UnityEngine.dll')
    run('mcs','-out:'+str(test/'LocalApiTests.exe'),'-r:'+str(test/'Assembly-CSharp.dll'),
        '-r:'+str(test/'Assembly-CSharp-firstpass.dll'),'-r:'+str(test/'UnityEngine.dll'),tests/'LocalApiTests.cs')
    env = dict(os.environ, DG_TEST_TABLES=str(work/'tables'),DG_TEST_PREFS=str(test/'player-prefs.txt'))
    run('mcs','-out:'+str(tools/'VerifyEventPatch.exe'),'-r:'+CECIL,tests/'VerifyEventPatch.cs')
    event_patch = subprocess.check_output(['mono',str(tools/'VerifyEventPatch.exe'),
        str(managed/'Assembly-CSharp.dll'),str(test/'Assembly-CSharp.dll')],text=True)
    run('mcs','-out:'+str(test/'VerifyEventUnlock.exe'),'-r:'+str(test/'Assembly-CSharp.dll'),
        '-r:'+str(test/'DivineGateOffline.dll'),'-r:'+str(test/'Assembly-CSharp-firstpass.dll'),
        '-r:'+str(test/'UnityEngine.dll'),tests/'VerifyEventUnlock.cs')
    event_env = dict(env, DG_TEST_PREFS=str(test/'event-prefs.txt'))
    event_test = subprocess.check_output(['mono',str(test/'VerifyEventUnlock.exe')],env=event_env,text=True,timeout=60)
    print(event_patch + event_test)
    (args.report.parent/'event-unlock-tests.txt').write_text(event_patch + event_test)
    report['event_unlock_tests'] = (event_patch + event_test).strip()
    run('mcs','-out:'+str(test/'VerifyFreeChipShop.exe'),'-r:'+str(test/'Assembly-CSharp.dll'),
        '-r:'+str(test/'DivineGateOffline.dll'),'-r:'+str(test/'Assembly-CSharp-firstpass.dll'),
        '-r:'+str(test/'UnityEngine.dll'),tests/'VerifyFreeChipShop.cs')
    shop_env = dict(env, DG_TEST_PREFS=str(test/'free-chip-prefs.txt'))
    shop_test = ''
    for mode in [[], ['reload']]:
        shop_test += subprocess.check_output(['mono',str(test/'VerifyFreeChipShop.exe'),*mode],
            env=shop_env,text=True,timeout=45)
    print(shop_test)
    (args.report.parent/'free-chip-shop-tests.txt').write_text(shop_test)
    report['free_chip_shop_tests'] = shop_test.strip()
    run('mcs','-out:'+str(tools/'VerifyShopPrefabHook.exe'),'-r:'+CECIL,tests/'VerifyShopPrefabHook.cs')
    prefab_test = subprocess.check_output(['mono',str(tools/'VerifyShopPrefabHook.exe'),
        str(test/'Assembly-CSharp.dll')],text=True)
    import sys
    sys.path.insert(0, str(tests))
    from verify_shop_prefab import verify as verify_shop_prefab
    prefab_test += verify_shop_prefab(artifact, args.report.parent)
    print(prefab_test)
    (args.report.parent/'shop-prefab-tests.txt').write_text(prefab_test)
    report['shop_prefab_tests'] = prefab_test.strip()
    run('mcs','-out:'+str(tools/'VerifyCollabHooks.exe'),'-r:'+CECIL,tests/'VerifyCollabHooks.cs')
    collab_test = subprocess.check_output(['mono',str(tools/'VerifyCollabHooks.exe'),
        str(managed/'Assembly-CSharp.dll'),str(test/'Assembly-CSharp.dll'),str(test/'DivineGateOffline.dll')],text=True)
    run('mcs','-out:'+str(test/'VerifyCollabGacha.exe'),'-r:'+str(test/'Assembly-CSharp.dll'),
        '-r:'+str(test/'DivineGateOffline.dll'),'-r:'+str(test/'Assembly-CSharp-firstpass.dll'),
        '-r:'+str(test/'UnityEngine.dll'),tests/'VerifyCollabGacha.cs')
    collab_env = dict(env, DG_TEST_PREFS=str(test/'collab-prefs.txt'))
    for mode in [[], ['reload']]:
        collab_test += subprocess.check_output(['mono',str(test/'VerifyCollabGacha.exe'),*mode],
            env=collab_env,text=True,timeout=45)
    run('mcs','-out:'+str(test/'VerifyCollabReturn.exe'),'-r:'+str(test/'Assembly-CSharp.dll'),
        '-r:'+str(test/'DivineGateOffline.dll'),'-r:'+str(test/'UnityEngine.dll'),
        '-r:'+str(test/'EngineSupport.dll'),tests/'VerifyCollabReturn.cs')
    collab_test += subprocess.check_output(['mono',str(test/'VerifyCollabReturn.exe')],
        env=collab_env,text=True,timeout=45)
    run('mcs','-out:'+str(test/'VerifyCollabAnimation.exe'),'-r:'+str(test/'Assembly-CSharp.dll'),
        '-r:'+str(test/'DivineGateOffline.dll'),'-r:'+str(test/'UnityEngine.dll'),
        '-r:'+str(test/'EngineSupport.dll'),tests/'VerifyCollabAnimation.cs')
    collab_test += subprocess.check_output(['mono',str(test/'VerifyCollabAnimation.exe')],
        env=collab_env,text=True,timeout=45)
    from verify_collab_prefab import verify as verify_collab_prefab
    collab_test += verify_collab_prefab(artifact, args.report.parent)
    print(collab_test)
    (args.report.parent/'collab-gacha-tests.txt').write_text(collab_test)
    report['collab_gacha_tests'] = collab_test.strip()
    if report.get('recovered_artwork') and images.get('sprite_aliases'):
        from PIL import Image, ImageChops
        import io
        source = ROOT/'artifacts/web-asset-search-2026-10-20'
        records = json.loads((source/'dgdb-downloaded-assets.json').read_text())
        supplement = source/'dgdb-additional-assets.json'
        if supplement.exists():
            records += json.loads(supplement.read_text())
        files = {r['path']: r for r in records}
        packed = (work/'build/RecoveredImages.bin').read_bytes()
        def recovered_png(key):
            row = images['payloads'][key]
            value = Image.open(io.BytesIO(packed[row['offset']:row['offset']+row['size']])).convert('RGBA')
            value.load()
            return value
        empty_row = files['public/img/panel/chara_icon_empty.png']
        empty_raw = (ROOT/empty_row['local_path']).read_bytes()
        assert digest(empty_raw) == empty_row['sha256']
        empty_source = Image.open(io.BytesIO(empty_raw)).convert('RGBA')
        assert empty_source.getchannel('A').getextrema()[0] > 0
        empty_atlases = []
        for key in images['payloads']:
            if not key.startswith('atlas/AtlasCharaIcon_'):
                continue
            atlas_name = key.split('/',1)[1]
            x,y,w,h = images['sprites'][atlas_name+'/chara_icon_empty']
            actual = recovered_png(key).crop((x,y,x+w,y+h))
            expected_empty = empty_source.resize((w,h),Image.Resampling.LANCZOS)
            assert not any(channel.getbbox() for channel in ImageChops.difference(actual,expected_empty).split())
            empty_atlases.append(atlas_name)
        assert len(empty_atlases) == 5
        empty_result = 'PASS empty friend/party slot: all 5 character atlases match the verified dark Empty artwork; no transparent empty substitute.\n'
        print(empty_result)
        (args.report.parent/'empty-slot-tests.txt').write_text(empty_result)
        report['empty_slot_tests'] = empty_result.strip()
        icon_rows = test/'character-atlas-ownership.tsv'
        icon_rows.write_text(''.join(str(c['fix_id'])+'\t'+c['resource_id']+'\n' for c in images['characters']))
        run('mcs','-out:'+str(test/'VerifyEnemyAtlasSearch.exe'),
            '-r:'+str(test/'Assembly-CSharp.dll'),'-r:'+str(test/'UnityEngine.dll'),
            '-r:'+str(test/'EngineSupport.dll'),tests/'VerifyEnemyAtlasSearch.cs')
        enemy_search = subprocess.check_output(['mono',str(test/'VerifyEnemyAtlasSearch.exe'),str(icon_rows)],
            env=env,text=True,timeout=45)
        print(enemy_search)
        (args.report.parent/'enemy-atlas-tests.txt').write_text(enemy_search)
        report['enemy_atlas_tests'] = enemy_search.strip()
        run('mcs','-out:'+str(test/'VerifyHudPopup.exe'),'-r:'+str(test/'Assembly-CSharp.dll'),
            '-r:'+str(test/'UnityEngine.dll'),'-r:'+str(test/'EngineSupport.dll'),tests/'VerifyHudPopup.cs')
        popup = subprocess.check_output(['mono',str(test/'VerifyHudPopup.exe'),str(test/'DivineGateOffline.dll')],env=env,text=True,timeout=45)
        print(popup)
        (args.report.parent/'hud-popup-tests.txt').write_text(popup)
        report['hud_popup_tests'] = popup.strip()
        if images.get('area_icon_sources'):
            area_sheet = recovered_png('atlas/AtlasAreaIcon_000_020')
            areas = json.loads((work/'tables/MasterDataArea.json').read_bytes(), strict=False)
            coverage = {}
            area_rects = []
            for row in images['area_icon_sources']:
                raw = (ROOT/row['local_path']).read_bytes()
                assert digest(raw) == row['sha256']
                original = Image.open(io.BytesIO(raw)).convert('RGBA')
                if 'crop_box' in row:
                    assert original.size == (1024,315)
                    assert row['crop_box'] == [348,108,675,315]
                    original = original.convert('RGB').crop((348,108,675,315)).resize(
                        (207,131),Image.Resampling.LANCZOS).convert('RGBA')
                    derived = (args.report.parent/row['derived_file']).read_bytes()
                    assert digest(derived) == row['derived_sha256']
                    assert not any(c.getbbox() for c in ImageChops.difference(
                        Image.open(io.BytesIO(derived)).convert('RGBA'),original).split())
                    if row['map_name'] == 'map_0006':
                        assert digest(derived) == '5354c5721c3f3240fec187b1f4ee3ad4bcb2a6d4e2aeb1bb71a402016a6cbf56'
                elif row.get('derived_file'):
                    derived = (args.report.parent/row['derived_file']).read_bytes()
                    assert derived == raw  # Preserve the Wiki PNG bytes as well as its pixels.
                    assert digest(derived) == row['derived_sha256']
                if 'area_ids' in row:
                    for area_id in row['area_ids']:
                        assert area_id not in coverage
                        coverage[area_id] = row
                x,y,w,h = images['sprites']['AtlasAreaIcon_000_020/'+row['sprite_name']]
                assert (w,h) == original.size
                assert 0 <= x and 0 <= y and x+w <= area_sheet.width and y+h <= area_sheet.height
                assert not any(x < ox+ow and x+w > ox and y < oy+oh and y+h > oy
                               for ox,oy,ow,oh in area_rects)
                area_rects.append((x,y,w,h))
                actual = area_sheet.crop((x,y,x+w,y+h))
                assert not any(channel.getbbox() for channel in ImageChops.difference(original,actual).split())
            if coverage:
                assert set(coverage) == {a['fix_id'] for a in areas if a['fix_id']}
                for area in areas:
                    if not area['fix_id']:
                        continue
                    row = coverage[area['fix_id']]
                    assert area['res_map'] == row['map_name']+'_tex'
                    assert area['res_map_icon'] == row['sprite_name']
                assert area_sheet.width <= 2048 and area_sheet.height <= 2048
                cropped = [r for r in images['area_icon_sources'] if 'crop_box' in r]
                wiki = [r for r in images['area_icon_sources'] if 'crop_box' not in r]
                wiki_manifest = json.loads((source/'fandom-area-images.json').read_text())
                assert {r['sprite_name'] for r in wiki} == {r['sprite_name'] for r in wiki_manifest}
                for reference in wiki_manifest:
                    row = next(r for r in wiki if r['sprite_name'] == reference['sprite_name'])
                    assert row['sha256'] == reference['sha256']
                area_result = (f'PASS selection art: {len(coverage)} original areas, '
                    f'{len(wiki)} Wiki icons preserved byte-for-byte, {len(cropped)} A-cropped icons '
                    f'from {len({r["map_name"] for r in cropped})} upper textures; '
                    f'{len(area_rects)} non-overlapping 207x131 sprites, source/derived hashes and embedded atlas pixels match.\n')
                expected_area_rects = test/'area-sprite-rects.tsv'
                expected_area_rects.write_text(''.join(r['sprite_name']+'\t'+','.join(map(str,
                    images['sprites']['AtlasAreaIcon_000_020/'+r['sprite_name']]))+'\n'
                    for r in images['area_icon_sources']))
                run('mcs','-out:'+str(test/'VerifyAreaIcons.exe'),'-r:'+str(test/'Assembly-CSharp.dll'),
                    '-r:'+str(test/'Assembly-CSharp-firstpass.dll'),'-r:'+str(test/'UnityEngine.dll'),
                    '-r:'+str(test/'EngineSupport.dll'),tests/'VerifyAreaIcons.cs')
                area_result += subprocess.check_output(['mono',str(test/'VerifyAreaIcons.exe'),str(expected_area_rects)],
                    env=env,text=True,timeout=45)
            else:
                area_result = f'PASS {len(images["area_icon_sources"])} Fandom town-selection icons match downloaded source pixels and retain source aspect/dimensions.\n'
            print(area_result)
            (args.report.parent/'area-icon-tests.txt').write_text(area_result)
            report['area_icon_tests'] = area_result.strip()
        areas = json.loads((work/'tables/MasterDataArea.json').read_bytes(), strict=False)
        names = {a['res_map'].removesuffix('_tex') for a in areas if a.get('res_map','').startswith('map_')}
        for name in names:
            for part in ['lower','upper']:
                row = files[f'public/img/area/{name}_tex_{part}.png']
                raw = (ROOT/row['local_path']).read_bytes()
                assert digest(raw) == row['sha256']
                original = Image.open(io.BytesIO(raw)).convert('RGBA')
                actual = recovered_png(f'map/{name}/{part}')
                assert original.size == actual.size
                assert not any(channel.getbbox() for channel in ImageChops.difference(original, actual).split())
        panel_atlas = recovered_png('atlas/AtlasPanelIcon')
        for alias, row in images['sprite_aliases'].items():
            assert images['sprites']['AtlasPanelIcon/'+alias] == images['sprites']['AtlasPanelIcon/'+row['source']]
            source_path = next(path for path in files if path.startswith(('public/img/panel/','public/img/trap/'))
                               and Path(path).stem == row['source'])
            original = Image.open(ROOT/files[source_path]['local_path']).convert('RGBA')
            x,y,w,h = images['sprites']['AtlasPanelIcon/'+alias]
            original = original.resize((w,h),Image.Resampling.LANCZOS)
            actual = panel_atlas.crop((x,y,x+w,y+h))
            assert not any(channel.getbbox() for channel in ImageChops.difference(original,actual).split())
        for color in ['b','y','r']:
            for count in range(1,7):
                rect = images['sprites'][f'AtlasPanelIcon/panel_{color}_{count}']
                assert rect == images['sprites'][f'AtlasPanelIcon/panel_{count}']
                assert rect != images['sprites']['AtlasPanelIcon/__missing']
        run('mcs','-out:'+str(test/'VerifyMapTextureRouting.exe'),
            '-r:'+str(test/'Assembly-CSharp.dll'),'-r:'+str(test/'UnityEngine.dll'),tests/'VerifyMapTextureRouting.cs')
        run('mcs','-out:'+str(tools/'PrepareMapProbe.exe'),'-r:'+CECIL,tests/'PrepareMapProbe.cs')
        run('mono',tools/'PrepareMapProbe.exe',test/'DivineGateOffline.dll',test/'MapProbe.dll')
        shutil.move(test/'MapProbe.dll',test/'DivineGateOffline.dll')
        routing = subprocess.check_output(['mono',str(test/'VerifyMapTextureRouting.exe'),
            str(test/'DivineGateOffline.dll')],env=env,text=True,timeout=45)
        routing += f'PASS source pixels: {len(names)*2} required split map PNGs; {len(images["sprite_aliases"])} sprite aliases match source pixels and declared rectangles.\n'
        print(routing)
        (args.report.parent/'map-variant-tests.txt').write_text(routing)
        report['map_variant_tests'] = routing.strip()
    logs = []
    for args2 in [[],['reload']]:
        log = test/('reload.log' if args2 else 'api.log')
        with log.open('w') as stream:
            result = subprocess.run(['mono',str(test/'LocalApiTests.exe'),*args2],env=env,stdout=stream,stderr=subprocess.STDOUT,timeout=45)
        text = log.read_text(errors='replace')
        print(text[:10000])
        if result.returncode:
            raise RuntimeError('Local API verification failed; full log: '+str(log))
        logs.append(text)
    (args.report.parent/'local-api-tests.txt').write_text(
        'Managed API integration tests with a stubbed engine. Android gameplay remains unverified.\n\n'+''.join(logs))
    report['local_api_tests'] = 'PASS: create, rename, starter party, authentication, tables, login pack, tutorial start/clear/rewards, separate-process reload'
    report['test_engine_limitations'] = 'Does not verify Android installation, Unity rendering, native plug-ins, animation, touch or scene lifecycle.'
    args.report.write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n')


if __name__ == '__main__':
    main()
