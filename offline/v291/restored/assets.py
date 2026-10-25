"""Append fallback images only where the locked native cache lacks a resource."""
from pathlib import Path
import json,hashlib,io
from PIL import Image
from backup_images import pack,png
from map_layout import canonical_map,parts
ROOT=Path('/workspace')
def create(tables,output,baseline_report):
    baseline=json.loads(baseline_report.read_text());cached=Path(baseline['work_directory'])/'build'
    report=json.loads((cached/'images.json').read_text())
    oldpayloads=report['payloads'];sprites=dict(report['sprites'])
    chars=[r for r in json.loads((tables/'MasterDataParamChara.json').read_text()) if r['fix_id']]
    areas=json.loads((tables/'MasterDataArea.json').read_text())
    groups=set(range(0,((max(r['fix_id'] for r in chars)//200)+1)*200,200))
    replace={'atlas/AtlasCharaIcon_%03d_%03d'%(i,i+199) for i in groups}
    payloads={};offsets={};sources=[]
    source=(cached/'RecoveredImages.bin').open('rb')
    def oldread(key):
        r=oldpayloads[key];source.seek(r['offset']);data=source.read(r['size'])
        assert hashlib.sha256(data).hexdigest()==r['sha256'];return data
    target=(output/'RecoveredImages.bin').open('wb')
    def add(key,data):
        assert key not in payloads,key
        idx=len(payloads);off=target.tell();target.write(data);offsets[key]=(idx,off,len(data))
        payloads[key]={'index':idx,'offset':off,'size':len(data),'sha256':hashlib.sha256(data).hexdigest()}
    for key in oldpayloads:
        if key not in replace:add(key,oldread(key))
    def local_image(kind,c):
        number=str(c['draw_id']).zfill(3);suffix='-icon.png' if kind=='icon' else '.png'
        paths=[ROOT/'external/dgdata'/kind/(number+suffix),ROOT/'external/dgdb/public/img'/kind/(number+suffix)]
        path=next((p for p in paths if p.exists()),None);assert path,paths
        raw=path.read_bytes();sources.append({'path':str(path.relative_to(ROOT)),'sha256':hashlib.sha256(raw).hexdigest(),'reason':'Native model/image absent; exact recovered PNG fallback'})
        return Image.open(io.BytesIO(raw)).convert('RGBA')
    # Preserve every original portrait/shadow payload; only append missing ones.
    for c in chars:
        key=str(int(c['res_chara_id_str']))
        if 'portrait/'+key in payloads:continue
        im=local_image('large',c);add('portrait/'+key,png(im))
        shadow=Image.new('RGBA',im.size,(255,255,255,0));shadow.putalpha(im.getchannel('A'));add('shadow/'+key,png(shadow))
    for start in sorted(groups):
        name='AtlasCharaIcon_%03d_%03d'%(start,start+199);key='atlas/'+name
        entries={}
        if key in oldpayloads:
            sheet=Image.open(io.BytesIO(oldread(key))).convert('RGBA')
            for sk,rect in report['sprites'].items():
                if sk.startswith(name+'/'):
                    x,y,w,h=rect;entries[sk.split('/',1)[1]]=sheet.crop((x,y,x+w,y+h))
        # Shared empty/lock icons retain their original pixels for all groups.
        if not entries:
            donor='AtlasCharaIcon_000_199'
            sheet=Image.open(io.BytesIO(oldread('atlas/'+donor))).convert('RGBA')
            for label in ['chara_icon_empty','chara_icon_question','chara_icon_reject','lock1','__missing']:
                rect=report['sprites'].get(donor+'/'+label)
                if rect:
                    x,y,w,h=rect;entries[label]=sheet.crop((x,y,x+w,y+h))
        for c in chars:
            if not start<=c['fix_id']<start+200:continue
            label='chara_icon_'+str(c['fix_id']).zfill(3)
            if label not in entries:entries[label]=local_image('icon',c)
        entries.setdefault('__missing',Image.new('RGBA',(32,32)))
        sheet,rects=pack(entries);add(key,png(sheet))
        for label,rect in rects.items():sprites[name+'/'+label]=rect
    # Missing icon names must have a real image in the later atlas. Never
    # fabricate a sprite in atlas0 and stop the native multi-atlas search.
    ui_fallbacks=[]
    from native_ui import load as native_ui_load
    native_ui=native_ui_load()
    needed_panels={a[k] for a in areas if a and a['fix_id'] for k in ['res_icon_key','res_icon_box'] if a.get(k)}
    kinds=['all','fire','water','light','dark','wind','naught','human','dragon','god','demon','fairy','beast','machine']
    needed_panels.update('trap_atk_down_'+k for k in kinds)
    needed_panels.update('trap_damage_up_'+k for k in kinds[:7])
    needed_panels.update(['heal_sp','panel_Heart_SP'])
    needed_panels.update('heal_atk_up_'+k for k in kinds)
    extras={}
    for name in sorted(needed_panels):
        if any(k.endswith('/'+name) and k.startswith('AtlasPanelIcon/') for k in sprites):continue
        candidates=[ROOT/'external/dgdb/public/img'/d/(name+'.png') for d in ['panel','trap']]
        path=next((p for p in candidates if p.exists()),None)
        native=native_ui.get(('panel',name))
        if native:
            im,origin=native;assert im.size==(128,128)
            extras[name]=im
            ui_fallbacks.append(dict(origin,kind='panel',name=name,method='exact-native-pixels',size=[128,128]))
        elif path:
            extras[name]=Image.open(path).convert('RGBA')
            ui_fallbacks.append({'kind':'panel','name':name,'source':str(path.relative_to(ROOT)),'sha256':hashlib.sha256(path.read_bytes()).hexdigest(),'method':'exact-recovered-png'})
        else:
            # The source has no mugen_box. Reuse the original neutral chest,
            # with an explicit alias and the original square128px contract.
            if name=='mugen_box':source_name='town_box'
            elif name in ['heal_sp','panel_Heart_SP']:source_name='panel_Heart'
            elif name.startswith('heal_atk_up_'):source_name='heal_removetrap'
            else:raise AssertionError(('Missing panel source',name))
            key='AtlasPanelIcon/'+source_name;x,y,w,h=sprites[key]
            sheet=Image.open(io.BytesIO(oldread('atlas/AtlasPanelIcon'))).convert('RGBA')
            extras[name]=sheet.crop((x,y,x+w,y+h))
            ui_fallbacks.append({'kind':'panel','name':name,'source':'native '+key,'reason':'Exact asset unavailable; original neutral chest/SP heart/positive-effect symbol. Effect values and status text unchanged.'})
    if extras:
        name='AtlasPanelIcon_2';sheet,rects=pack(extras);add('atlas/'+name,png(sheet))
        for label,rect in rects.items():sprites[name+'/'+label]=rect
    missing_icons={}
    for a in areas:
        if not a or not a['fix_id']:continue
        icon=a['res_map_icon']
        if not any(k.endswith('/'+icon) and k.startswith('AtlasAreaIcon_') for k in sprites):missing_icons.setdefault(icon,a)
    extras={}
    for icon,a in sorted(missing_icons.items()):
        native=native_ui.get(('area',icon))
        assert native,('No original thumbnail for required area',icon)
        im,origin=native
        # Preserve the original 2.9.1 menu's 207x131 pixel size. DG0 enlarged
        # its thumbnails to248x152; import that exact named image, not its UVs.
        extras[icon]=im.resize((207,131),Image.Resampling.LANCZOS)
        ui_fallbacks.append(dict(origin,kind='area',name=icon,size=[207,131],
            method='native-thumbnail-resized-to-291-contract',
            reason='Original named DG0 thumbnail; normalized to the existing menu dimensions. No doorway reconstruction.'))
    if extras:
        name='AtlasAreaIcon_021_040';sheet,rects=pack(extras);add('atlas/'+name,png(sheet))
        for label,rect in rects.items():sprites[name+'/'+label]=rect
    from portrait_layout import match
    original_ids=set(json.loads((baseline_report.parent/'dgdb-projection.json').read_text())['characters'])
    added=[c for c in chars if c['fix_id'] not in original_ids]
    reference_atlases={}
    def reference_image(kind,c):
        if kind=='large':
            return Image.open(io.BytesIO(oldread('portrait/'+str(int(c['res_chara_id_str']))))).convert('RGBA')
        group=c['fix_id']//200*200;atlas='AtlasCharaIcon_%03d_%03d'%(group,group+199)
        label='chara_icon_'+str(c['fix_id']).zfill(3)
        x,y,w,h=report['sprites'][atlas+'/'+label]
        if atlas not in reference_atlases:reference_atlases[atlas]=Image.open(io.BytesIO(oldread('atlas/'+atlas))).convert('RGBA')
        return reference_atlases[atlas].crop((x,y,x+w,y+h))
    references=[c for c in chars if c['fix_id'] in original_ids]
    evolutions=json.loads((tables/'MasterDataParamCharaEvol.json').read_text())
    crops=match(added,output,references,evolutions,reference_image)
    (output/'hud-portrait-crops.json').write_text(json.dumps(crops,ensure_ascii=False,indent=2))
    code=['using System.Collections.Generic;','internal static class RestoredHudLayouts {',
          'static readonly Dictionary<uint,float[]> crops=new Dictionary<uint,float[]>{']
    code+=['{'+str(i)+'u,new float[]{'+','.join(format(v,'.9f')+'f' for r in row['slot_rects'] for v in r)+'}},' for i,row in crops.items()]
    code+=['};','public static float[] Get(uint id){float[] r;return crops.TryGetValue(id,out r)?r:null;}','}']
    (output/'RestoredHudLayouts.cs').write_text('\n'.join(code))
    maps={}
    for a in areas:
        if a and a['fix_id'] and a.get('res_map'):maps[a['res_map']]=a
    for resource,a in sorted(maps.items()):
        name=resource.removesuffix('_tex');key='map/'+name+'/lower'
        if key in oldpayloads:
            # The original fallback payload is the original full atlas.
            raw=oldread(key);im=Image.open(io.BytesIO(raw)).convert('RGBA');origin='original-native-cache'
        else:
            path=ROOT/'external/dgdata/map'/(resource+'.png');assert path.exists(),path
            raw=path.read_bytes();im=Image.open(io.BytesIO(raw)).convert('RGBA');origin=str(path.relative_to(ROOT))
        assert im.size==(1024,1024),(resource,im.size)
        im=canonical_map(im,native=origin=='original-native-cache')
        sections,separated=parts(im)
        sources.append({'resource':resource,'origin':origin,'sha256':hashlib.sha256(raw).hexdigest(),
            'source_orientation':'native-top-down' if origin=='original-native-cache' else 'dgdata-bottom-up',
            'separate_panels':separated,
            'reason':'Exact native routes win. External atlas vertically normalized for native UV; black atlas padding made transparent only on simplified split meshes.'})
        root='restored-map/'+name+'/'
        add(root+'atlas',raw if origin=='original-native-cache' else png(im))
        for part,section in sections.items():add(root+part,png(section))
    target.close();source.close()
    lines=['using System.Collections.Generic;','internal static class RecoveredImages {','public const bool Enabled=true;',
           'static readonly Dictionary<string,int> files=new Dictionary<string,int>{']
    lines+=['{'+json.dumps(k)+','+str(v['index'])+'},' for k,v in payloads.items()]
    lines+=['};','static readonly Dictionary<string,int[]> sprites=new Dictionary<string,int[]>{']
    lines+=['{'+json.dumps(k)+',new int[]{'+','.join(map(str,v))+'}},' for k,v in sorted(sprites.items())]
    lines+=['};','public static int[] Sprite(string atlas,string name){int[] r;return sprites.TryGetValue(atlas+"/"+name,out r)?r:null;}',
            'public static byte[] Read(string key){int i;return files.TryGetValue(key,out i)?Payload(i):null;}',
            'static readonly int[] offsets=new int[]{'+','.join(str(v['offset']) for v in payloads.values())+'};',
            'static readonly int[] sizes=new int[]{'+','.join(str(v['size']) for v in payloads.values())+'};',
            'public static byte[] Payload(int index){return index<0 || index>=sizes.Length?null:DGApkImages.Read(UnityEngine.Application.dataPath,offsets[index],sizes[index],'+str((output/'RecoveredImages.bin').stat().st_size)+');}','}']
    (output/'RecoveredImages.cs').write_text('\n'.join(lines))
    (output/'image-offsets.tsv').write_text(''.join('%d\t%d\t%d\n'%v for v in offsets.values()))
    result=dict(report,ui_fallbacks=ui_fallbacks,hud_crop_count=len(crops),portraits=len(chars),payloads=payloads,sprites=sprites,restored_sources=sources,storage='apk-stored-slices',store_entry='assets/dg-images.bin',store_size=(output/'RecoveredImages.bin').stat().st_size,
                note='Original native595 prefab/image routes retained. Extra PNG art is only a fallback for missing models/resources; split door/frame/lower/upper assets and four-state fallback animation.')
    (output/'images.json').write_text(json.dumps(result,ensure_ascii=False,indent=2))
    return result
