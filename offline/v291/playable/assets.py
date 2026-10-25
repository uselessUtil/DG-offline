"""Prepare version-specific backup images; preserve original CABs and pixels."""
from pathlib import Path
import hashlib
import json
import io
from PIL import Image
from backup_images import native_textures, png

def create(backup, tables, output):
    chars = [x for x in json.loads((tables/'MasterDataParamChara.json').read_bytes(),strict=False) if x['fix_id']]
    areas = [x for x in json.loads((tables/'MasterDataArea.json').read_bytes(),strict=False) if x['fix_id']]
    textures, atlases, origins = native_textures(backup, lambda n: n.startswith(('tex_chara_l_','map_','Atlas')))
    payloads, sprites, missing = {}, {}, []
    for atlas, rects in atlases.items():
        payloads['atlas/'+atlas] = png(textures[atlas])
        for name, rect in rects.items(): sprites[atlas+'/'+name] = rect
    # The engine requests five 200-ID icon groups, including shared empty/lock frames.
    shared = {}
    for atlas,rects in atlases.items():
        if not atlas.startswith('AtlasCharaIcon_'): continue
        for name,rect in rects.items():
            if name in ('chara_icon_empty','chara_icon_question','chara_icon_reject','lock1'):
                x,y,w,h=rect;shared[name]=textures[atlas].crop((x,y,x+w,y+h))
    from backup_images import pack
    for start in range(0,1000,200):
        name=f'AtlasCharaIcon_{start:03d}_{start+199:03d}'
        entries=dict(shared)
        if name in atlases:
            for n,(x,y,w,h) in atlases[name].items():entries[n]=textures[name].crop((x,y,x+w,y+h))
        entries['__missing']=Image.new('RGBA',(32,32),(0,0,0,0))
        sheet,rects=pack(entries);payloads['atlas/'+name]=png(sheet)
        for n,rect in rects.items():sprites[name+'/'+n]=rect
    for c in chars:
        name='tex_chara_l_'+c['res_chara_id_str']
        if name not in textures:raise ValueError('Missing backup portrait '+name)
        image=textures[name];key=str(int(c['res_chara_id_str']))
        payloads['portrait/'+key]=png(image)
        shadow=Image.new('RGBA',image.size,(255,255,255,0));shadow.putalpha(image.getchannel('A'))
        payloads['shadow/'+key]=png(shadow)
    for name,image in textures.items():
        if name.startswith('map_') and name.endswith('_tex'):
            base=name[:-4]
            # Simplified geometry only uses these on missing/failed native loads.
            for part in ['lower','upper']:payloads['map/'+base+'/'+part]=png(image)
    aliases=json.loads((Path('/workspace/offline/dgdb-sprite-aliases.json')).read_text())
    for name,row in aliases.items():
        key='AtlasPanelIcon/'+name;source='AtlasPanelIcon/'+row['source']
        if key not in sprites and source in sprites:sprites[key]=sprites[source]
    for name,source in [('heal_happytresure','heal_happytreasure'),('toaru_box_01','toaru_box'),('toaru_key','toaru_Key')]:
        if 'AtlasPanelIcon/'+source in sprites:sprites['AtlasPanelIcon/'+name]=sprites['AtlasPanelIcon/'+source]
    offsets={}
    with (output/'RecoveredImages.bin').open('wb') as stream:
        for index,(key,data) in enumerate(sorted(payloads.items())):
            offsets[key]=(index,stream.tell(),len(data));stream.write(data)
    lines=['using System.Collections.Generic;','internal static class RecoveredImages {','public const bool Enabled=true;',
           'static readonly Dictionary<string,int> files=new Dictionary<string,int>{']
    lines += ['{'+json.dumps(k)+','+str(v[0])+'},' for k,v in sorted(offsets.items())]
    lines += ['};','static readonly Dictionary<string,int[]> sprites=new Dictionary<string,int[]>{']
    lines += ['{'+json.dumps(k)+',new int[]{'+','.join(map(str,v))+'}},' for k,v in sorted(sprites.items())]
    lines += ['};','public static int[] Sprite(string atlas,string name){int[] r;return sprites.TryGetValue(atlas+"/"+name,out r)?r:null;}',
              'public static byte[] Read(string key){int i;return files.TryGetValue(key,out i)?Payload(i):null;}',
              'public static byte[] Payload(int index){return null;}','}']
    (output/'RecoveredImages.cs').write_text('\n'.join(lines))
    (output/'image-offsets.tsv').write_text(''.join(f'{i}\t{o}\t{n}\n' for i,o,n in offsets.values()))
    report={'portraits':len(chars),'sprites':sprites,'source_textures':list(origins.values()),
            'payloads':{k:{'index':offsets[k][0],'offset':offsets[k][1],'size':len(v),'sha256':hashlib.sha256(v).hexdigest()} for k,v in payloads.items()},
            'note':'Original cache pixels; native prefabs preferred. Missing native map uses simplified geometry.'}
    (output/'images.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n')
    return report
