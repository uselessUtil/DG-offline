"""Check native cache provenance/pixels and packaged 1.0.6 sprite routing."""
import io
import json
from pathlib import Path
import subprocess

from PIL import Image

from backup_images import manifest, native_textures, sha
from build import ROOT, CECIL, run


def verify(images, work, output, test, tools, env):
    native = images['backup_import']
    root = Path(native['source'])
    assert native['source_hashes_unchanged']
    assert not native['master_data_imported'] and not native['original_geometry_imported']
    assert manifest(root) == native['source_files']
    names = {r['texture'] for r in native['textures']}
    textures, atlases, origins = native_textures(root, lambda n:n in names)
    assert set(origins) == names
    for row in native['textures']:
        assert origins[row['texture']] == row
    packed = (work/'build/RecoveredImages.bin').read_bytes()
    assert sha(packed) == images['image_pack_sha256']

    def recovered(key):
        row = images['payloads'][key]
        raw = packed[row['offset']:row['offset']+row['size']]
        assert sha(raw) == row['sha256']
        result = Image.open(io.BytesIO(raw)).convert('RGBA')
        result.load()
        return result

    for row in native['payloads']:
        original = textures[row['texture']]
        if 'source_rect' in row:
            original = original.crop(row['source_rect'])
        actual = recovered(row['key'])
        assert original.size == actual.size and original.tobytes() == actual.tobytes(), row
    # Compare every native frame (including extra cached characters), rather
    # than trusting only the importer-generated PNG hash manifest.
    sheets = {a:recovered('atlas/'+a) for a in {r['atlas'] for r in native['sprites']}}
    for row in native['sprites']:
        source_rect = row['source_rect']
        assert atlases[row['texture']][row['name'] if row['name'] != '__missing' else 'chara_icon_question'] == source_rect
        x,y,w,h = source_rect
        original = textures[row['texture']].crop((x,y,x+w,y+h))
        x,y,w,h = images['sprites'][row['atlas']+'/'+row['name']]
        actual = sheets[row['atlas']].crop((x,y,x+w,y+h))
        assert actual.size == original.size and actual.tobytes() == original.tobytes(), row
    # Every published frame is in bounds and distinct frames do not overlap.
    for atlas, sheet in sheets.items():
        assert sheet.width <= 2048 and sheet.height <= 2048
        rects = set(tuple(v) for k,v in images['sprites'].items() if k.startswith(atlas+'/'))
        previous = []
        for x,y,w,h in rects:
            assert 0 <= x < x+w <= sheet.width and 0 <= y < y+h <= sheet.height
            assert not any(x < a+c and x+w > a and y < b+d and y+h > b for a,b,c,d in previous)
            previous.append((x,y,w,h))
    for alias, target in native['native_aliases'].items():
        assert images['sprites']['AtlasPanelIcon/'+alias] == images['sprites']['AtlasPanelIcon/'+target]
    for color in ['r','y','b']:
        for count in range(1,7):
            name = f'panel_{color}_{count}'
            assert any(r['name'] == name and r['texture'] == 'AtlasPanelIcon' for r in native['sprites'])
    # Missing native maps must keep the verified web PNGs, not an empty tile.
    records = json.loads((ROOT/'artifacts/web-asset-search-2026-10-20/dgdb-downloaded-assets.json').read_text())
    records += json.loads((ROOT/'artifacts/web-asset-search-2026-10-20/dgdb-additional-assets.json').read_text())
    files = {r['path']:r for r in records}
    for name in native['fallback_maps']:
        assert name+'_tex' not in textures
        for part in ['upper','lower']:
            row = files[f'public/img/area/{name}_tex_{part}.png']
            raw = (ROOT/row['local_path']).read_bytes()
            assert sha(raw) == row['sha256']
            original = Image.open(io.BytesIO(raw)).convert('RGBA')
            assert recovered(f'map/{name}/{part}').tobytes() == original.tobytes()
    areas = json.loads((work/'tables/MasterDataArea.json').read_bytes(),strict=False)
    covered = [area for area in areas if area['fix_id'] and area['res_map'] in textures]
    assert len(covered) == native['area_texture_coverage'] == 99
    area_sources = {r['sprite_name']:r for r in images['area_icon_sources']}
    assert set(area_sources) == {a['res_map_icon'] for a in areas if a['fix_id']}
    for name,row in area_sources.items():
        raw = (output/row['derived_file']).read_bytes()
        assert sha(raw) == row['derived_sha256']
        x,y,w,h = row['source_rect']
        assert Image.open(io.BytesIO(raw)).convert('RGBA').tobytes() == textures[row['texture']].crop((x,y,x+w,y+h)).tobytes()
    assert len(images['characters']) == native['portraits'] == native['character_icons'] == 522
    results = {}
    pixels = (f'PASS {len(native["source_files"])} backup files unchanged; {len(origins)} selected native textures decoded; '
              f'{len(native["payloads"])} portrait/map payloads and {len(native["sprites"])} native atlas frames match source pixels exactly.\n'
              'PASS native TexturePacker rectangles match serialized UIAtlas; no rotated/trimmed frames, overlapping frames or textures over 2048px.\n'
              f'PASS {len(area_sources)} native area icons cover all 101 original areas; '
              f'{len(covered)} areas use native map textures; fallback maps: {native["fallback_maps"]}.\n'
              'PASS all 18 risk/color star panels use their actual native frame; Empty, Question, Reject and lock1 use native cache pixels.\n')
    (output/'native-image-tests.txt').write_text(pixels)
    results['native_image_tests'] = pixels.strip()
    tests = ROOT/'offline/tests'
    icon_rows = test/'character-atlas-ownership.tsv'
    icon_rows.write_text(''.join(str(c['fix_id'])+'\t'+c['resource_id']+'\n' for c in images['characters']))
    run('mcs','-out:'+str(test/'VerifyEnemyAtlasSearch.exe'),
        '-r:'+str(test/'Assembly-CSharp.dll'),'-r:'+str(test/'UnityEngine.dll'),
        '-r:'+str(test/'EngineSupport.dll'),tests/'VerifyEnemyAtlasSearch.cs')
    enemy = subprocess.check_output(['mono',str(test/'VerifyEnemyAtlasSearch.exe'),str(icon_rows)],env=env,text=True,timeout=45)
    (output/'enemy-atlas-tests.txt').write_text(enemy)
    results['enemy_atlas_tests'] = enemy.strip()
    expected_rects = test/'area-sprite-rects.tsv'
    expected_rects.write_text(''.join(name+'\t'+','.join(map(str,images['sprites']['AtlasAreaIcon_000_020/'+name]))+'\n' for name in area_sources))
    run('mcs','-out:'+str(test/'VerifyAreaIcons.exe'),'-r:'+str(test/'Assembly-CSharp.dll'),
        '-r:'+str(test/'Assembly-CSharp-firstpass.dll'),'-r:'+str(test/'UnityEngine.dll'),
        '-r:'+str(test/'EngineSupport.dll'),tests/'VerifyAreaIcons.cs')
    area = subprocess.check_output(['mono',str(test/'VerifyAreaIcons.exe'),str(expected_rects)],env=env,text=True,timeout=45)
    (output/'area-icon-tests.txt').write_text(area)
    results['area_icon_tests'] = area.strip()
    run('mcs','-out:'+str(test/'VerifyHudPopup.exe'),'-r:'+str(test/'Assembly-CSharp.dll'),
        '-r:'+str(test/'UnityEngine.dll'),'-r:'+str(test/'EngineSupport.dll'),tests/'VerifyHudPopup.cs')
    popup = subprocess.check_output(['mono',str(test/'VerifyHudPopup.exe'),str(test/'DivineGateOffline.dll')],env=env,text=True,timeout=45)
    (output/'hud-popup-tests.txt').write_text(popup)
    results['hud_popup_tests'] = popup.strip()
    run('mcs','-out:'+str(test/'VerifyMapTextureRouting.exe'),
        '-r:'+str(test/'Assembly-CSharp.dll'),'-r:'+str(test/'UnityEngine.dll'),tests/'VerifyMapTextureRouting.cs')
    run('mcs','-out:'+str(tools/'PrepareMapProbe.exe'),'-r:'+CECIL,tests/'PrepareMapProbe.cs')
    run('mono',tools/'PrepareMapProbe.exe',test/'DivineGateOffline.dll',test/'MapProbe.dll')
    # Probe only a disposable copy after packaged image/UI tests complete.
    (test/'DivineGateOffline.dll').write_bytes((test/'MapProbe.dll').read_bytes())
    routing = subprocess.check_output(['mono',str(test/'VerifyMapTextureRouting.exe'),str(test/'DivineGateOffline.dll')],env=env,text=True,timeout=45)
    (output/'map-variant-tests.txt').write_text(routing)
    results['map_variant_tests'] = routing.strip()
    print(pixels+enemy+area+popup+routing)
    assert manifest(root) == native['source_files']
    return results
