"""Icon-to-portrait matching plus native595 five-slot layout calibration."""
from pathlib import Path
import json,struct,subprocess,hashlib
from PIL import Image
from hud_calibration import calibration,predict
ROOT=Path('/workspace');CODE=ROOT/'offline/v291/restored'

def match(characters,output,references,evolutions,reference_image):
    data=output/'portrait-match.bin';dest=output/'portrait-match.tsv'
    def asset(kind,c):
        name=str(c['draw_id']).zfill(3)+('-icon.png' if kind=='icon' else '.png')
        ps=[ROOT/'external/dgdata'/kind/name,ROOT/'external/dgdb/public/img'/kind/name]
        return next(p for p in ps if p.exists())
    visible={};reference_ids={c['fix_id'] for c in references};sources=[]
    allchars=references+characters
    with data.open('wb') as f:
        f.write(struct.pack('<i',len(allchars)))
        for c in allchars:
            imgs={}
            for kind in ['large','icon']:
                if c['fix_id'] in reference_ids:im=reference_image(kind,c)
                else:im=Image.open(asset(kind,c)).convert('RGBA')
                sources.append({'id':c['fix_id'],'kind':kind,'rgba_sha256':hashlib.sha256(im.tobytes()).hexdigest(),'size':list(im.size),'reference':c['fix_id']in reference_ids})
                imgs[kind]=im
            full=imgs['large'];box=full.getchannel('A').getbbox();visible[c['fix_id']]=(box,full.size)
            large=full.resize((256,256),Image.Resampling.BILINEAR)
            icon=imgs['icon'].convert('RGB').resize((128,128),Image.Resampling.BILINEAR)
            face=icon.crop((30,18,100,100)).resize((20,24),Image.Resampling.BILINEAR)
            f.write(struct.pack('<i',c['fix_id']));f.write(large.tobytes());f.write(face.tobytes())
    exe=output/'MatchPortrait.exe'
    subprocess.run(['mcs','-optimize+','-out:'+str(exe),str(CODE/'MatchPortrait.cs')],check=True)
    subprocess.run(['mono',str(exe),str(data),str(dest)],check=True)
    matches={}
    for line in dest.read_text().splitlines():
        i,x,y,w,h,error=line.split('\t');i,x,y,w,h=int(i),int(x),int(y),int(w),int(h)
        matches[i]={'id':i,'match_rect_256':[x,y,w,h],'normalized_error':min(float(error),1.)}
    learned=calibration(references,matches,evolutions)
    (output/'hud-calibration.json').write_text(json.dumps(learned,ensure_ascii=False,indent=2))
    (output/'hud-image-inputs.json').write_text(json.dumps(sources,indent=2))
    rows={}
    for c in characters:
        i=c['fix_id'];row=dict(matches[i]);reasons=[]
        if row['normalized_error']>.055:reasons.append('low-confidence-icon-match')
        if c['kind']==8:reasons.append('material-character-whole-art-composition')
        if '巨人'in c['name']:reasons.append('giant-character-composition-review')
        if reasons:
            box,(width,height)=visible[i]
            if box:
                left,top,right,bottom=box;s=min(1,max((right-left)/width,(bottom-top)/height)*1.05)
                u=max(0,min(1-s,(left+right)/width/2-s/2))
                v=max(0,min(1-s,1-(top+bottom)/height/2-s/2))
            else:u=v=0;s=1
            rects=[[u,v,s,s] for _ in range(5)]
        else:rects=[predict(learned['final_model'],row,slot) for slot in range(5)]
        row.update(slot_rects=rects,rect=rects[4],slot_mapping=[4,3,2,1,0],
            full_portrait_fallback=bool(reasons),review_reasons=reasons,
            layout_method='Whole visible art fallback' if reasons else 'Native595 calibrated five separate position rules; inferred composition, not recovered official metadata.')
        rows[i]=row
    data.unlink()
    return rows
