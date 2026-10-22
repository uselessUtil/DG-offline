"""Use native UnityCache textures before web-recovered substitutes (Docker only).

Only Texture2D pixels and matching UIAtlas rectangles are imported. Cached master
tables, scripts, meshes and account state are never loaded into the Android app.
"""
from collections import defaultdict
import hashlib
import io
import json
import math
from pathlib import Path
import struct

from PIL import Image
import UnityPy
from UnityPy.files.SerializedFile import SerializedFile


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def png(image):
    stream = io.BytesIO()
    image.save(stream, format='PNG')
    return stream.getvalue()


def rgba(raw):
    image = Image.open(io.BytesIO(raw)).convert('RGBA')
    image.load()
    return image


def atlas_rectangles(raw):
    """UIAtlas from the cache's matching Unity 4 / NGUI serialization schema."""
    offset = 20
    length = struct.unpack_from('<I', raw, offset)[0]
    offset = (offset + 4 + length + 3) // 4 * 4
    offset += 8  # material PPtr
    count = struct.unpack_from('<I', raw, offset)[0]
    offset += 4
    if not 0 < count < 4096:
        raise ValueError('Unexpected UIAtlas sprite count')
    result = {}
    for _ in range(count):
        length = struct.unpack_from('<I', raw, offset)[0]
        offset += 4
        name = raw[offset:offset+length].decode('utf8')
        offset = (offset + length + 3) // 4 * 4
        outer = struct.unpack_from('<4f', raw, offset)
        inner = struct.unpack_from('<4f', raw, offset+16)
        offset += 32
        rotated = raw[offset]
        offset += 4
        padding = struct.unpack_from('<4f', raw, offset)
        offset += 16
        if outer != inner or rotated or any(padding) or any(int(v) != v for v in outer):
            raise ValueError('Runtime requires an unrotated, untrimmed pixel sprite: '+name)
        if name in result:
            raise ValueError('Duplicate native sprite: '+name)
        result[name] = list(map(int, outer))
    # coordinates: Pixels=0, pixel size=1, no replacement atlas.
    if offset+16 != len(raw) or struct.unpack_from('<ifii', raw, offset) != (0, 1., 0, 0):
        raise ValueError('Unexpected native atlas coordinate system or replacement')
    return result


def manifest(root):
    return [{'path':str(p.relative_to(root)), 'size':p.stat().st_size,
             'sha256':sha(p.read_bytes())} for p in sorted(root.rglob('*')) if p.is_file()]


def native_textures(root, wanted):
    """Decode without rewriting the cache's nonstandard version-string header."""
    previous = SerializedFile.set_version
    textures, atlases, sources = {}, {}, []
    SerializedFile.set_version = lambda self, value: previous(self, value.splitlines()[0])
    try:
        for path in sorted(root.rglob('CAB-*')):
            raw = path.read_bytes()
            env = UnityPy.load(raw)
            cab = str(path.relative_to(root))
            local_textures, json_frames, mono_frames = {}, {}, []
            for obj in env.objects:
                if obj.type.name == 'Texture2D':
                    data = obj.read()
                    if not wanted(data.m_Name):
                        continue
                    if data.m_Name in textures:
                        raise ValueError('Ambiguous native texture: '+data.m_Name)
                    image = data.image.convert('RGBA')
                    image.load()
                    textures[data.m_Name] = image
                    source = {'cab':cab, 'cab_sha256':sha(raw), 'path_id':obj.path_id,
                              'texture':data.m_Name, 'width':image.width, 'height':image.height,
                              'texture_format':int(data.m_TextureFormat),
                              'rgba_sha256':sha(image.tobytes())}
                    sources.append(source)
                    local_textures[data.m_Name] = source
                elif obj.type.name == 'TextAsset':
                    data = obj.read()
                    if data.m_Name.startswith('Atlas') and wanted(data.m_Name):
                        frames = json.loads(data.m_Script)['frames']
                        result = {}
                        for filename, frame in frames.items():
                            if frame['rotated'] or frame['trimmed']:
                                raise ValueError('Unsupported native frame transform')
                            r = frame['frame']
                            result[Path(filename).stem] = [r[k] for k in ('x','y','w','h')]
                        json_frames[data.m_Name] = result
                elif obj.type.name == 'MonoBehaviour':
                    # Only atlas CABs have these TextAssets/textures. Parse below
                    # after checking the MonoScript class, never arbitrary data.
                    tree = obj.read_typetree(check_read=False)
                    script = tree['m_Script']
                    if not script['m_FileID']:
                        pointer = env.objects
                        script_obj = next((o for o in pointer if o.path_id == script['m_PathID']), None)
                        if script_obj and script_obj.read().m_ClassName == 'UIAtlas':
                            mono_frames.append(atlas_rectangles(obj.get_raw_data()))
            for name, frames in json_frames.items():
                if name not in local_textures or not any(frames == m for m in mono_frames):
                    raise ValueError('TexturePacker/native UIAtlas rectangle mismatch: '+name)
                image = textures[name]
                rectangles = []
                for key, (x,y,w,h) in frames.items():
                    if x < 0 or y < 0 or w <= 0 or h <= 0 or x+w > image.width or y+h > image.height:
                        raise ValueError('Native sprite outside texture: '+key)
                    if any(x < ox+ow and x+w > ox and y < oy+oh and y+h > oy for ox,oy,ow,oh in rectangles):
                        raise ValueError('Native sprites overlap: '+key)
                    rectangles.append((x,y,w,h))
                atlases[name] = frames
            print('Native cache:', path.name, len(local_textures), 'selected textures', flush=True)
    finally:
        SerializedFile.set_version = previous
    return textures, atlases, {r['texture']:r for r in sources}


def pack(entries):
    """Shelf pack exact native pixels with extruded 1px borders, <=2048px."""
    positions = {}
    x = y = row_height = 0
    for name, image in sorted(entries.items(), key=lambda r:(-r[1].height, -r[1].width, r[0])):
        w, h = image.size
        if x+w+2 > 2048:
            y += row_height
            x = row_height = 0
        if y+h+2 > 2048:
            raise ValueError('Native/fallback atlas exceeds Unity 4 texture limit')
        positions[name] = [x+1, y+1, w, h]
        x += w+2
        row_height = max(row_height, h+2)
    height = 2**math.ceil(math.log2(max(2, y+row_height)))
    width = 2048 if len(entries) > 15 else 2**math.ceil(math.log2(max(2,x)))
    sheet = Image.new('RGBA', (width,height))
    for name, image in entries.items():
        x,y,w,h = positions[name]
        sheet.paste(image,(x,y))
        sheet.paste(image.crop((0,0,w,1)),(x,y-1))
        sheet.paste(image.crop((0,h-1,w,h)),(x,y+h))
        sheet.paste(image.crop((0,0,1,h)),(x-1,y))
        sheet.paste(image.crop((w-1,0,w,h)),(x+w,y))
        for dx,dy,sx,sy in [(-1,-1,0,0),(w,-1,w-1,0),(-1,h,0,h-1),(w,h,w-1,h-1)]:
            sheet.putpixel((x+dx,y+dy),image.getpixel((sx,sy)))
    return sheet, positions


def apply(root, output, tables, payloads, sprites, mapping, areas, maps, aliases):
    root = root.resolve()
    before = manifest(root)
    if not before:
        raise ValueError('Backup is empty')
    wanted_portraits = {'tex_chara_l_'+c['resource_id'] for c in mapping}
    wanted_maps = {n+'_tex' for n in maps}
    textures, atlases, origins = native_textures(root,
        lambda n:n in wanted_portraits or n in wanted_maps or n.startswith('Atlas'))
    missing = wanted_portraits-set(textures)
    if missing:
        raise ValueError('Backup lacks required portraits: '+str(sorted(missing)))
    evidence = {'source':str(root), 'source_files':before,
                'textures':list(origins.values()), 'sprites':[], 'payloads':[],
                'fallback_sprites':[], 'fallback_maps':[], 'native_aliases':{},
                'master_data_imported':False, 'original_geometry_imported':False}
    for c in mapping:
        image = textures['tex_chara_l_'+c['resource_id']]
        key = 'portrait/'+str(int(c['resource_id']))
        payloads[key] = png(image)
        evidence['payloads'].append({'key':key,'texture':'tex_chara_l_'+c['resource_id']})
        shadow = Image.new('RGBA', image.size, (255,255,255,0))
        shadow.putalpha(image.getchannel('A'))
        payloads['shadow/'+str(int(c['resource_id']))] = png(shadow)
    native_entries = {}
    native_refs = {}
    for atlas, frames in atlases.items():
        native_entries[atlas] = {}
        for name, (x,y,w,h) in frames.items():
            native_entries[atlas][name] = textures[atlas].crop((x,y,x+w,y+h))
            native_refs[(atlas,name)] = {'texture':atlas, 'source_rect':[x,y,w,h]}
    first = 'AtlasCharaIcon_000_199'
    special = ['chara_icon_empty','chara_icon_question','chara_icon_reject','lock1']
    char_atlases = [f'AtlasCharaIcon_{start:03d}_{start+199:03d}' for start in range(0,1000,200)]
    for atlas in char_atlases+['AtlasPanelIcon','AtlasAreaIcon_000_020']:
        entries = native_entries.setdefault(atlas,{})
        for name in special:
            if name not in entries:
                entries[name] = native_entries[first][name]
                native_refs[(atlas,name)] = native_refs[(first,name)]
    # Exact spelling/case variants; retain these as references to native art.
    panel_aliases = {'toaru_key':'toaru_Key','toaru_box_01':'toaru_box',
                     'trap_telepot':'trap_teleport','noah_box':'noha_box','noah_key':'noah_kye',
                     'heal_happytresure':'heal_happytreasure'}
    panel_aliases.update({f'panel_{n}':f'panel_y_{n}' for n in range(1,7)})
    old_panel = rgba(payloads['atlas/AtlasPanelIcon'])
    panel = native_entries['AtlasPanelIcon']
    for key, rect in list(sprites.items()):
        if not key.startswith('AtlasPanelIcon/'):
            continue
        name = key.split('/',1)[1]
        if name in panel or name in panel_aliases or name == '__missing':
            continue
        x,y,w,h = rect
        panel[name] = old_panel.crop((x,y,x+w,y+h))
        evidence['fallback_sprites'].append(name)
    evidence['native_aliases'] = panel_aliases
    # Full native star panels supersede the old single-color web aliases.
    aliases.clear()
    for atlas, entries in native_entries.items():
        entries['__missing'] = entries['chara_icon_question']
        native_refs[(atlas,'__missing')] = native_refs[(atlas,'chara_icon_question')]
        sheet, rects = pack(entries)
        for key in [k for k in sprites if k.startswith(atlas+'/')]:
            del sprites[key]
        for name, rect in rects.items():
            sprites[atlas+'/'+name] = rect
            ref = native_refs.get((atlas,name))
            if ref:
                evidence['sprites'].append({'atlas':atlas,'name':name,**ref})
        if atlas == 'AtlasPanelIcon':
            for alias, target in panel_aliases.items():
                sprites[atlas+'/'+alias] = rects[target]
        payloads['atlas/'+atlas] = png(sheet)
    for c in mapping:
        name = 'chara_icon_'+c['resource_id']
        owning = [a for a in char_atlases if a+'/'+name in sprites]
        if len(owning) != 1:
            raise ValueError('Missing/ambiguous original character atlas: '+name)
        c['native_atlas'] = owning[0]
    for name in maps:
        texture_name = name+'_tex'
        if texture_name not in textures:
            evidence['fallback_maps'].append(name)
            continue
        image = textures[texture_name]
        if image.size != (1024,1024):
            raise ValueError('Unexpected native map texture size: '+name)
        # Native sheet's wall strip and lower floor region, excluding packing
        # margins. Keep the existing quad layout; do not pretend to restore 3D.
        for part, box in [('upper',(0,0,1024,315)),('lower',(4,320,556,1019))]:
            key = f'map/{name}/{part}'
            payloads[key] = png(image.crop(box))
            evidence['payloads'].append({'key':key,'texture':texture_name,'source_rect':list(box)})
    area_sources = []
    by_icon = defaultdict(list)
    for area in areas:
        if area['fix_id']:
            by_icon[area['res_map_icon']].append(area['fix_id'])
    for name, area_ids in sorted(by_icon.items()):
        atlas = 'AtlasAreaIcon_000_020'
        if (atlas,name) not in native_refs:
            raise ValueError('Backup lacks area icon: '+name)
        image = native_entries[atlas][name]
        path = output/'area-icons'/ (name+'.png')
        path.write_bytes(png(image))
        area_sources.append({'sprite_name':name,'area_ids':area_ids,
                             'derived_file':'area-icons/'+path.name,
                             'derived_sha256':sha(path.read_bytes()),
                             'source_kind':'native UnityCache atlas', **native_refs[(atlas,name)],
                             **{k:v for k,v in origins[atlas].items() if k in ('cab','cab_sha256','path_id')}})
    (output/'area-icon-manifest.json').write_text(json.dumps(area_sources,ensure_ascii=False,indent=2))
    for name in special:
        (output/(name+'.png')).write_bytes(png(native_entries[first][name]))
    evidence['portraits'] = len(mapping)
    evidence['character_icons'] = len(mapping)
    evidence['area_icons'] = len(area_sources)
    evidence['native_maps'] = len(maps)-len(evidence['fallback_maps'])
    evidence['area_texture_coverage'] = sum(a.get('res_map') in textures for a in areas if a['fix_id'])
    evidence['source_hashes_unchanged'] = before == manifest(root)
    if not evidence['source_hashes_unchanged']:
        raise ValueError('Backup changed during image import')
    evidence['limitations'] = ('Native cache pixels decoded from GPU formats; atlas frames are repacked without resizing. '
        'Map images are split for existing simplified quads. Native meshes/animations/effects are not imported; '
        'master tables and save format remain those of 1.0.6. Cache date/application version is unconfirmed.')
    (output/'backup-image-import.json').write_text(json.dumps(evidence,ensure_ascii=False,indent=2))
    print('Native import:',len(mapping),'portraits;',len(area_sources),'area icons;',
          evidence['native_maps'],'map textures; fallback maps:',evidence['fallback_maps'],flush=True)
    return evidence, area_sources
