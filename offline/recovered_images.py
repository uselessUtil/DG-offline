"""Prepare verified DGDB PNGs for the old Unity runtime, inside Docker only."""
import hashlib
import io
import json
import math
from pathlib import Path
from PIL import Image, ImageDraw
from area_icons import create as create_area_icons


def png(image):
    data = io.BytesIO()
    image.save(data, format='PNG')
    return data.getvalue()


def create(output: Path, tables: Path, source: Path, backup: Path = None):
    records = json.loads((source / 'dgdb-downloaded-assets.json').read_text())
    supplement = source / 'dgdb-additional-assets.json'
    if supplement.exists():
        records += json.loads(supplement.read_text())
    wiki_sources = []
    fandom = source / 'fandom-area-images.json'
    if fandom.exists():
        wiki_sources = json.loads(fandom.read_text())
        records += wiki_sources
    ui_sources = json.loads((source / 'fandom-ui-images.json').read_text())
    ui_overrides = {row['sprite_name']: row for row in ui_sources}
    if len(ui_overrides) != len(ui_sources) or 'chara_icon_empty' not in ui_overrides:
        raise ValueError('Missing or duplicate Wiki UI icon override')
    records += ui_sources
    files = {row['path']: row for row in records}
    chars = json.loads((tables / 'MasterDataParamChara.json').read_bytes(), strict=False)
    payloads, sprites, provenance = {}, {}, []

    def read(path, original=False):
        row = files[path]
        raw = (Path('/workspace') / row['local_path']).read_bytes()
        if hashlib.sha256(raw).hexdigest() != row['sha256']:
            raise ValueError('Recovered image hash mismatch: ' + path)
        image = Image.open(io.BytesIO(raw)).convert('RGBA')
        image.load()
        provenance.append({'source_path': path, 'source_sha256': row['sha256']})
        return raw if original else image

    groups = {f'AtlasCharaIcon_{start:03d}_{start+199:03d}': []
              for start in range(0, 1000, 200)}
    mapping = []
    for char in chars:
        if not char['fix_id']:
            continue
        resource = char['res_chara_id_str']
        draw = char['draw_id']
        portrait = f'public/img/large/{draw:03d}.png'
        icon = f'public/img/icon/{draw:03d}-icon.png'
        # Preserve the compact, already verified PNG bytes and transparency.
        portrait_data = read(portrait, original=True)
        payloads['portrait/' + str(int(resource))] = portrait_data
        # Original shadow materials paint an untextured silhouette mesh. Our
        # replacement is a rectangle, so give its shadow an explicit alpha mask.
        alpha = Image.open(io.BytesIO(portrait_data)).convert('RGBA').getchannel('A')
        shadow = Image.new('RGBA', alpha.size, (255, 255, 255, 0))
        shadow.putalpha(alpha)
        payloads['shadow/' + str(int(resource))] = png(shadow)
        start = int(char['fix_id']) // 200 * 200
        groups[f'AtlasCharaIcon_{start:03d}_{start+199:03d}'].append(
            ('chara_icon_' + resource, read(icon)))
        mapping.append({'fix_id': char['fix_id'], 'resource_id': resource,
                        'draw_id': draw, 'name': char['name']})

    groups['AtlasPanelIcon'] = [(Path(path).stem, read(path)) for path in sorted(files)
                                if path.startswith(('public/img/panel/', 'public/img/trap/'))
                                and Path(path).stem not in ui_overrides]
    areas = json.loads((tables / 'MasterDataArea.json').read_bytes(), strict=False)
    area_entries, area_sources = create_area_icons(areas, files, read, output / 'area-icons', wiki_sources)
    groups['AtlasAreaIcon_000_020'] = area_entries
    alias_records = json.loads((Path(__file__).parent / 'dgdb-sprite-aliases.json').read_text())
    aliases = {name: row['source'] for name, row in alias_records.items()}
    # The public backup preserves one set of 1..6 star panels. Use the same
    # star-count art for all three old risk variants; no guessed recoloring.
    for color in ['b', 'y', 'r']:
        for count in range(1, 7):
            name = f'panel_{color}_{count}'
            aliases[name] = f'panel_{count}'
            alias_records[name] = {'source': f'panel_{count}',
                'basis': 'compatible star-count panel requested by user; original color variant unavailable'}
    for atlas, entries in groups.items():
        # Keep the full 128px icon resolution. Alpha variants share these textures.
        # Null friend/party slots request this sprite from the character atlas.
        # A transparent substitute exposes the prefab's white backing tile.
        empty = read(ui_overrides['chara_icon_empty']['path'])
        missing = Image.new('RGBA', (128, 128), '#555568')
        ImageDraw.Draw(missing).text((56, 55), '?', fill='white')
        entries = [('__missing', missing), ('chara_icon_empty', empty),
                   ('chara_icon_question', missing), *entries]
        size = 96 if atlas == 'AtlasPanelIcon' else 128
        area_atlas = atlas == 'AtlasAreaIcon_000_020'
        cell_width, cell_height = (256, 160) if area_atlas else (size, size)
        cols = min(8 if area_atlas else 16, max(1, len(entries)))
        width = 2 ** math.ceil(math.log2(cols * cell_width))
        height = 2 ** math.ceil(math.log2(math.ceil(len(entries) / cols) * cell_height))
        if width > 2048 or height > 2048:
            raise ValueError('Unexpected atlas dimensions: ' + atlas)
        sheet = Image.new('RGBA', (width, height))
        for index, (name, image) in enumerate(entries):
            x, y = index % cols * cell_width, index // cols * cell_height
            # The larger panel collection uses 96px cells to stay within 2048px.
            if area_atlas:
                image = image.copy()
                image.thumbnail((cell_width, cell_height), Image.Resampling.LANCZOS)
            else:
                image = image.resize((size, size), Image.Resampling.LANCZOS)
            sheet.paste(image, (x, y))
            sprites[atlas + '/' + name] = [x, y, image.width, image.height]
        for alias, name in aliases.items():
            if atlas + '/' + name in sprites:
                sprites[atlas + '/' + alias] = sprites[atlas + '/' + name]
        payloads['atlas/' + atlas] = png(sheet)

    bundle_paths = json.loads((tables / 'MasterDataAssetBundlePath.json').read_bytes(), strict=False)
    map_names = sorted({row['name'].removeprefix('[1.0.6]').removesuffix('.unity3d')
                        for row in bundle_paths if row['name'].startswith('[1.0.6]map_')})
    # Models are shared by areas with different res_map textures. Select by the
    # actual master texture names as well as the model bundle names.
    map_names = sorted(set(map_names) | {row['res_map'].removesuffix('_tex')
                       for row in areas if row.get('res_map', '').startswith('map_')})
    maps = []
    for name in map_names:
        for part in ['lower', 'upper']:
            path = 'public/img/area/' + name + '_tex_' + part + '.png'
            payloads['map/' + name + '/' + part] = png(read(path))
        maps.append(name)

    backup_import = None
    if backup:
        from backup_images import apply as apply_backup
        backup_import, area_sources = apply_backup(backup, output, tables, payloads,
            sprites, mapping, areas, maps, alias_records)
        ui_sources = [{'sprite_name':n, 'source_kind':'native UnityCache atlas'}
                      for n in ['chara_icon_empty','chara_icon_question','chara_icon_reject','lock1']]

    offsets = {}
    pack = output / 'RecoveredImages.bin'
    with pack.open('wb') as stream:
        for index, (key, data) in enumerate(sorted(payloads.items())):
            offsets[key] = [index, stream.tell(), len(data)]
            stream.write(data)
    lines = ['using System;', 'using System.Collections.Generic;',
             'internal static class RecoveredImages {',
             'public const bool Enabled = true;',
             'private static readonly Dictionary<string, int[]> files = new Dictionary<string, int[]> {']
    lines += ['{' + json.dumps(key) + ', new int[]{' + str(value[0]) + '}},'
              for key, value in offsets.items()]
    lines += ['};', 'private static readonly Dictionary<string, int[]> sprites = new Dictionary<string, int[]> {']
    lines += ['{' + json.dumps(key) + ', new int[]{' + ','.join(map(str, value)) + '}},'
              for key, value in sorted(sprites.items())]
    lines += ['};', '''
public static int[] Sprite(string atlas, string name) {
 int[] rect; return sprites.TryGetValue(atlas + "/" + name, out rect) ? rect : null;
}
public static byte[] Read(string key) {
 int[] entry;
 if (!files.TryGetValue(key, out entry)) return null;
 return Payload(entry[0]);
}
// Replaced by EmbedImages.cs with the same RuntimeHelpers.InitializeArray
// instructions already used by the archived game. No reflection API required.
public static byte[] Payload(int index) { return null; }
}''']
    (output / 'RecoveredImages.cs').write_text('\n'.join(lines))
    report = {'source': 'https://github.com/kennyfong19931/dgdb',
              'commit': json.loads((source / 'dgdb-commit.json').read_text())['commit'],
              'portraits': len(mapping), 'character_icons': len(mapping), 'shadow_masks': len(mapping), 'maps': maps,
              'panel_and_trap_images': len(groups['AtlasPanelIcon']),
              'area_icons': len(area_sources), 'area_icon_sources': area_sources,
              'ui_icon_sources': ui_sources,
              'image_pack_bytes': pack.stat().st_size, 'image_pack_sha256': hashlib.sha256(pack.read_bytes()).hexdigest(),
              'payloads': {key: {'index': offsets[key][0], 'offset': offsets[key][1], 'size': len(data),
                                'sha256': hashlib.sha256(data).hexdigest()} for key, data in payloads.items()},
              'sprites': sprites, 'sprite_aliases': alias_records,
              'characters': mapping, 'provenance': provenance,
              'limitations': 'PNG art with rebuilt quads and simplified map geometry; original map meshes and animations remain absent.'}
    if backup_import:
        report['backup_import'] = backup_import
        report['source'] = str(backup)
        report['fallback_source'] = 'https://github.com/kennyfong19931/dgdb'
        report['fallback_commit'] = report['commit']
        report['commit'] = None  # A local cache is not the DGDB Git revision.
        report['native_panel_frames'] = 108
        report['fallback_panel_frames'] = len(backup_import['fallback_sprites'])
        report['limitations'] = backup_import['limitations']
    (output / 'recovered-images.json').write_text(json.dumps(report, ensure_ascii=False, indent=2))
    (output / 'image-offsets.tsv').write_text(''.join(
        f'{index}\t{offset}\t{size}\n' for index, offset, size in offsets.values()))
    return report


def disabled(output):
    (output / 'RecoveredImages.cs').write_text('''internal static class RecoveredImages {
public const bool Enabled = false;
public static byte[] Read(string key) { return null; }
public static int[] Sprite(string atlas, string name) { return null; }
}''')
