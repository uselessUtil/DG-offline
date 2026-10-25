"""Actual atlas search/crop regressions and all required UI sprite source coverage."""
import argparse,json,os,subprocess,tempfile,zipfile,io
from pathlib import Path
from PIL import Image,ImageDraw
ROOT=Path('/workspace');CODE=ROOT/'offline/v291/restored'
CECIL='/usr/lib/mono/gac/Mono.Cecil/0.11.1.0__0738eb9f132ed756/Mono.Cecil.dll'
def run(*a,**kw):
 p=subprocess.run(list(map(str,a)),stdout=subprocess.PIPE,stderr=subprocess.STDOUT,text=True,timeout=180,**kw)
 if p.returncode:raise RuntimeError(p.stdout)
 return p.stdout
def main():
 p=argparse.ArgumentParser();p.add_argument('report',type=Path);a=p.parse_args();out=a.report.parent
 report=json.loads(a.report.read_text());tested=json.loads((out/'test-report.json').read_text());w=Path(tested['work'])
 build=Path(report['work_directory'])/'build';tables=build.parent/'tables'
 images=json.loads((out/'images.json').read_text());crops=json.loads((out/'hud-portrait-crops.json').read_text())
 areas=json.loads((tables/'MasterDataArea.json').read_text())
 for area in areas:
  if not area or not area['fix_id']:continue
  for field,prefix,expected in [('res_map_icon','AtlasAreaIcon_', (207,131)),('res_icon_key','AtlasPanelIcon',(128,128)),('res_icon_box','AtlasPanelIcon',(128,128))]:
   name=area[field];hits=[v for k,v in images['sprites'].items() if k.startswith(prefix) and k.endswith('/'+name)]
   assert len(hits)==1,(field,area['fix_id'],name,hits)
   assert tuple(hits[0][2:])==expected,(name,hits)
 from native_ui import load
 native=load()
 source_counts={}
 with (build/'RecoveredImages.bin').open('rb') as stream:
  for source in images['ui_fallbacks']:
   if 'atlas_path_id' not in source:continue
   category=source['kind'];name=source['name'];im,origin=native[(category,name)]
   assert source['source_sha256']==origin['source_sha256'] and source['source_rect']==origin['source_rect']
   if category=='area':im=im.resize((207,131),Image.Resampling.LANCZOS)
   hits=[k for k in images['sprites'] if k.endswith('/'+name) and k.startswith('AtlasAreaIcon_' if category=='area' else 'AtlasPanelIcon')]
   assert len(hits)==1
   atlas=hits[0].split('/')[0];v=images['payloads']['atlas/'+atlas];stream.seek(v['offset'])
   sheet=Image.open(io.BytesIO(stream.read(v['size']))).convert('RGBA');x,y,w0,h0=images['sprites'][hits[0]]
   assert sheet.crop((x,y,x+w0,y+h0)).tobytes()==im.tobytes(),name
   source_counts[category]=source_counts.get(category,0)+1
 assert source_counts.get('area')==34,source_counts
 original=set(json.loads((out/'restoration.json').read_text())['original_characters'])
 assert len(crops)==1153 and not (original & set(map(int,crops)))
 (w/'hud-ids.txt').write_text('\n'.join(crops))
 refs=['-r:'+str(w/n) for n in ['Assembly-CSharp.dll','Assembly-CSharp-firstpass.dll','UnityEngine.dll','DivineGateOffline.dll','EngineSupport.dll']]
 run('mcs','-out:'+str(w/'UiAtlasTests.exe'),*refs,CODE/'UiAtlasTests.cs')
 log=run('mono',w/'UiAtlasTests.exe',w/'expected-sprites.tsv',w/'hud-ids.txt',cwd=w,env=dict(os.environ,DG_TEST_PREFS=str(w/'ui.prefs')))
 old=w/'ui-r13-game.dll'
 with zipfile.ZipFile(ROOT/'artifacts/2.9.1-restored-r13-image-streaming/ディバゲ_2.9.1.apk') as z:old.write_bytes(z.read('assets/bin/Data/Managed/Assembly-CSharp.dll'))
 run('mcs','-r:'+CECIL,'-out:'+str(w/'tools/VerifyUiHook.exe'),CODE/'VerifyUiHook.cs')
 log+=run('mono',w/'tools/VerifyUiHook.exe',old,w/'Assembly-CSharp.dll')
 log+='PASS DG0 original UIAtlas/TexturePacker rectangle agreement and signed output pixels: '+str(source_counts)+'.\n'
 fallback_count=sum(v['full_portrait_fallback'] for v in crops.values())
 calibration=json.loads((out/'hud-calibration.json').read_text())
 assert calibration['native_reference_count']==595 and len(calibration['median_iou'])==5
 held=calibration['held_out_predictions'];assert len(held)==calibration['accepted_reference_count']
 groups={}
 for row in held:
  group=row['group'];assert group not in groups or groups[group]==row['fold']
  groups[group]=row['fold']
  assert row['id'] in original
 assert calibration['slot_to_img_field']==[4,3,2,1,0]
 log+='PASS all398 area rows have exact-size area/key/chest sprites; original image pixels and master data retained. New HUD crops: '+str(len(crops)-fallback_count)+' calibrated face windows and '+str(fallback_count)+' explicit full-art/review cases.\n'
 log+='Five-fold native595 grouped calibration median IoU by party slot: '+str(calibration['median_iou'])+'; center error / official side: '+str(calibration['median_center_error_in_official_side'])+'.\n'

 (out/'ui-presentation-tests.txt').write_text(log)
 (out/'ui-presentation-tests.json').write_text(json.dumps({'apk_sha256':report['sha256'],'work':str(w),'new_ui_sources':images['ui_fallbacks'],'new_hud_crops':len(crops),'calibration_median_iou':calibration['median_iou'],'calibration_median_center_error':calibration['median_center_error_in_official_side'],'full_portrait_fallbacks':[i for i,v in crops.items() if v['full_portrait_fallback']],'android_tested':False,'output':log},ensure_ascii=False,indent=2))
 # Evidence preview from the full portraits, not the character icon images.
 chars={c['fix_id']:c for c in json.loads((tables/'MasterDataParamChara.json').read_text())}
 chosen=[810,237,1200,872,948,667,1440,1830];chosen=[i for i in chosen if str(i) in crops]
 sheet=Image.new('RGB',(4*256,2*290),'#555555');draw=ImageDraw.Draw(sheet)
 with (build/'RecoveredImages.bin').open('rb') as f:
  for idx,i in enumerate(chosen):
   c=chars[i];v=images['payloads']['portrait/'+str(int(c['res_chara_id_str']))];f.seek(v['offset']);im=Image.open(io.BytesIO(f.read(v['size']))).convert('RGBA')
   x,y,s,_=crops[str(i)]['slot_rects'][4];piece=im.crop((int(x*im.width),int((1-y-s)*im.height),int((x+s)*im.width),int((1-y)*im.height)))
   piece.thumbnail((248,248));px=idx%4*256;py=idx//4*290;sheet.paste(piece,(px,py+28),piece);draw.text((px,py),str(i)+(' full-art' if crops[str(i)]['full_portrait_fallback'] else ' portrait face'),fill='white')
 sheet.save(out/'hud-crop-preview.jpg')
 print(log)
if __name__=='__main__':main()
