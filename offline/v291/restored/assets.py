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
            'public static byte[] Payload(int index){return null;}','}']
    (output/'RecoveredImages.cs').write_text('\n'.join(lines))
    (output/'image-offsets.tsv').write_text(''.join('%d\t%d\t%d\n'%v for v in offsets.values()))
    result=dict(report,portraits=len(chars),payloads=payloads,sprites=sprites,restored_sources=sources,
                note='Original native595 prefab/image routes retained. Extra PNG art is only a fallback for missing models/resources; split door/frame/lower/upper assets and four-state fallback animation.')
    (output/'images.json').write_text(json.dumps(result,ensure_ascii=False,indent=2))
    return result
