"""Derive selection icons from each area's own upper map texture, in Docker."""
import hashlib
import io
import json
from pathlib import Path

from PIL import Image, ImageDraw


# User-approved A preview, 2026-10-21. Preserve the exact crop and filter.
CROP_BOX = (348, 108, 675, 315)
SOURCE_SIZE = (1024, 315)
ICON_SIZE = (207, 131)
APPROVED_MAP6_SHA256 = '5354c5721c3f3240fec187b1f4ee3ad4bcb2a6d4e2aeb1bb71a402016a6cbf56'


def create(areas, files, read, output: Path, wiki_sources=()):
    output.mkdir()
    preserved = {row['sprite_name']: row for row in wiki_sources}
    icons = {}
    for area in areas:
        if not area['fix_id']:
            continue
        name = area['res_map_icon']
        map_name = area['res_map'].removesuffix('_tex')
        if not name.startswith('area_icon_') or not map_name.startswith('map_'):
            raise ValueError('Unexpected area icon/map: ' + str(area['fix_id']))
        if name in icons and icons[name]['map_name'] != map_name:
            raise ValueError('Conflicting maps for shared sprite: ' + name)
        if name not in icons:
            icons[name] = {'sprite_name': name, 'map_name': map_name, 'area_ids': [], 'area_names': []}
        icons[name]['area_ids'].append(area['fix_id'])
        icons[name]['area_names'].append(area['area_name'])

    entries, rows, cached = [], [], {}
    for name, row in sorted(icons.items()):
        wiki = preserved.get(name)
        path = wiki['path'] if wiki else 'public/img/area/' + row['map_name'] + '_tex_upper.png'
        source = files[path]
        if path not in cached:
            if wiki:
                data = read(path, original=True)
                image = Image.open(io.BytesIO(data)).convert('RGBA')
                if image.size != ICON_SIZE:
                    raise ValueError('Unexpected preserved Wiki icon dimensions: ' + path)
            else:
                image = read(path)
                if image.size != SOURCE_SIZE:
                    raise ValueError('Crop A requires 1024x315 source: ' + path)
                # RGB preserves the exact approved preview's pixels and PNG bytes.
                image = image.convert('RGB').crop(CROP_BOX).resize(ICON_SIZE, Image.Resampling.LANCZOS)
                buffer = io.BytesIO()
                image.save(buffer, format='PNG')
                data = buffer.getvalue()
            cached[path] = image, data
        image, data = cached[path]
        image_hash = hashlib.sha256(data).hexdigest()
        if not wiki and row['map_name'] == 'map_0006' and image_hash != APPROVED_MAP6_SHA256:
            raise ValueError('map_0006 differs from the user-approved A preview')
        (output / (name + '.png')).write_bytes(data)
        row.update(path=path, local_path=source['local_path'], sha256=source['sha256'],
                   source_dimensions=list(ICON_SIZE if wiki else SOURCE_SIZE),
                   dimensions=list(ICON_SIZE), derived_file='area-icons/' + name + '.png',
                   derived_sha256=image_hash, method='preserved Wiki PNG, unchanged' if wiki else 'crop A then Lanczos resize; no repainting')
        if wiki:
            row['url'] = wiki['url']
        else:
            row['crop_box'] = list(CROP_BOX)
        entries.append((name, image))
        rows.append(row)

    # Contact sheets are review artifacts only, never consumed by the game.
    distinct = {row['map_name']: row for row in rows}
    for start in range(0, len(distinct), 24):
        page = list(sorted(distinct.items()))[start:start+24]
        sheet = Image.new('RGB', (4 * 223, ((len(page)+3)//4)*161), '#ededed')
        draw = ImageDraw.Draw(sheet)
        for index, (map_name, row) in enumerate(page):
            x, y = index % 4 * 223 + 8, index // 4 * 161 + 6
            draw.text((x, y), map_name + ' / ' + row['sprite_name'], fill='black')
            sheet.paste(cached[row['path']][0], (x, y+18))
        sheet.save(output / ('contact-' + str(start//24+1) + '.png'))
    manifest = {'recipe': 'user-approved A', 'crop_box': list(CROP_BOX),
                'source_dimensions': list(SOURCE_SIZE), 'dimensions': list(ICON_SIZE),
                'source_maps': len({r['map_name'] for r in rows if 'crop_box' in r}),
                'wiki_icons': sum('crop_box' not in r for r in rows),
                'cropped_icons': sum('crop_box' in r for r in rows), 'sprite_count': len(rows),
                'area_count': sum(len(row['area_ids']) for row in rows), 'icons': rows}
    (output.parent / 'area-icon-manifest.json').write_text(json.dumps(manifest, ensure_ascii=False, indent=2)+'\n')
    return entries, rows
