"""Package every door asset/data into one file. No HTTP or external scripts."""
import json,base64,re,hashlib,subprocess,io,collections
from pathlib import Path
from PIL import Image
HERE=Path(__file__).resolve().parent;ROOT=Path('/workspace');OUT=ROOT/'artifacts/door-animation-lab';CACHE=HERE/'runtime'
GROUPS=[('雙扇鉸鏈旋開','double-swing',['0001','0029','3001','4001','4003','4021','4022','4027','5001','5002','5003','6003','6004']),('單扇鉸鏈旋開','single-swing',['0008']),('單片升降','single-lift',['0015','5004']),('左右雙片滑開','horizontal-slide',['0022','2001','5005','5006']),('上下雙片滑開','vertical-slide',['1001','2007','4025']),('八片複合展開','compound-eight',['0036']),('四片分層伸縮','layered-four',['4009'])]
def fallback():
 def mesh():return{'positions':[[-.45,0,-.5],[.45,0,-.5],[.45,0,.5],[-.45,0,.5]],'uv':[[0,0],[1,0],[1,1],[0,1]],'triangles':[0,2,1,0,3,2]}
 root={'name':'FallbackRoot','path':'','parent':-1,'position':[0,0,0],'rotation':[0,0,0,1],'scale':[1,1,1],'active':True}
 nodes=[root]+[{'name':name,'path':name,'parent':0,'position':[0,y,3],'rotation':[0,0,0,1],'scale':[1,1,1],'active':True,'uvScale':[1,1],'uvOffset':[0,0],'mesh':mesh(),'part':part}for name,part,y in [('NormalDoor','normal',-.15),('BossDoor','boss',-.14)]]
 clips=[]
 for slot,name,boss,opened in [(0,'dg_fallback_normal_open',False,True),(1,'dg_fallback_boss_closed',True,False),(2,'dg_fallback_normal_closed',False,False),(3,'dg_fallback_boss_open',True,True)]:
  tracks=[]
  for path,v0,v1 in [('NormalDoor',0 if boss else 1,0 if boss or opened else 1),('BossDoor',1 if boss else 0,1 if boss and not opened else 0)]:
   slope=(v1-v0)/.3;tracks.append({'path':path,'kind':'scale','attribute':'','classID':0,'keys':[{'time':0,'value':[v0,1,1],'in':[slope,0,0],'out':[slope,0,0]},{'time':.3,'value':[v1,1,1],'in':[slope,0,0],'out':[slope,0,0]}]})
  clips.append({'name':name,'slot':slot,'duration':.3,'sampleRate':30,'events':[],'tracks':tracks})
 return{'id':'fallback','native':False,'label':'r18 分離貼圖水平收合','nodes':nodes,'clips':clips,'source':'offline/v291/restored/Presentation291.cs:RestorationDoorClip291'}
def main():
 d=json.loads((CACHE/'doors.json').read_text());d['animationTypes']=[];covered=set()
 for title,kind,ids in GROUPS:
  names=['map_'+i+'_door'for i in ids]
  assert all(n in d['families']for n in names)
  for n in names:d['families'][n]['label']=title+' / '+n;d['families'][n]['type']=kind
  d['animationTypes'].append({'name':title,'type':kind,'families':names,'example':names[0]});covered.update(names)
 assert covered==set(d['families'])and len(covered)==25
 d['families']['fallback']=fallback();assets={}
 for resource,t in d['textures'].items():
  raw=Path(t['file']).read_bytes();assert hashlib.sha256(raw).hexdigest()==t['webp_sha256'];assets[resource]=raw
  im=Image.open(io.BytesIO(raw)).convert('RGBA');thumb=im.crop((0,0,1024,316));thumb.thumbnail((260,90));s=io.BytesIO();thumb.save(s,format='WEBP',lossless=True,exact=True);assets[resource+'/thumb']=s.getvalue()
  t.pop('file')
 bootstrap='const DOOR_ASSETS=JSON.parse(document.getElementById("door-assets").textContent);const URLS=new Map();function assetURL(key){if(URLS.has(key))return URLS.get(key);const id=DOOR_ASSETS[key];if(!id)throw Error("缺少門貼圖："+key);const url="data:image/webp;base64,"+document.getElementById(id).textContent.trim();URLS.set(key,url);return url;}\n'
 code='\n'.join(re.sub(r'^import[^\n]*\n','',(HERE/n).read_text(),flags=re.M).replace('export const ','const ').replace('export function ','function ').replace('export class ','class ')for n in ['core.js','renderer.js','app.js'])
 code=bootstrap+code
 assert not re.search(r'\b(fetch|XMLHttpRequest)\s*\(',code)
 (CACHE/'bundle.js').write_text('(()=>{"use strict";'+code+'})();')
 subprocess.run(['node','--check',str(CACHE/'bundle.js')],check=True)
 html=(HERE/'index.html').read_text().replace('<link rel="stylesheet" href="style.css">','<style>'+(HERE/'style.css').read_text()+'</style>').replace('<script type="module" src="app.js"></script>','')
 html=html.replace('<meta charset="utf-8">','<meta charset="utf-8"><meta http-equiv="Content-Security-Policy" content="default-src &apos;none&apos;; img-src data: blob:; style-src &apos;unsafe-inline&apos;; script-src &apos;unsafe-inline&apos;; connect-src &apos;none&apos;; object-src &apos;none&apos;; base-uri &apos;none&apos;">')
 raw_data=json.dumps(d,ensure_ascii=False,separators=(',',':')).replace('</','<'+chr(92)+'/')
 index={k:'door-image-'+str(i)for i,k in enumerate(assets)}
 blocks='<script id="door-data" type="application/json">'+raw_data+'</script><script id="door-assets" type="application/json">'+json.dumps(index)+'</script>'
 blocks+=''.join('<script type="application/octet-stream" id="'+index[k]+'">'+base64.b64encode(v).decode()+'</script>'for k,v in assets.items())
 html=html.replace('</body>',blocks+'<script>'+ (CACHE/'bundle.js').read_text()+'</script></body>')
 path=OUT/'DG-Door-Motion-Lab.html';path.write_text(html)
 report={'html':path.name,'sha256':hashlib.sha256(path.read_bytes()).hexdigest(),'bytes':path.stat().st_size,'source_apk_sha256':d['sourceApkSha256'],'quests':len(d['quests']),'areas':len(d['areas']),'textures':len(d['textures']),'embedded_images':len(assets),'native_families':25,'native_clips':100,'animation_types':d['animationTypes'],'fallback_families':sorted({a['doorFamily']for a in d['areas']if not a['nativeRig']}),'exact_family_and_texture_areas':sum(a['nativeRig']and a['nativeTexture']for a in d['areas']),'shared_native_family_areas':sum(a['nativeRig']and not a['nativeTexture']for a in d['areas']),'fallback_areas':sum(not a['nativeRig']for a in d['areas']),'original_image_rgba_verified':True,'no_http_server':True,'browser_visual_verified':False}
 (OUT/'build-report.json').write_text(json.dumps(report,ensure_ascii=False,indent=2));(CACHE/'packed-data.json').write_text(raw_data);(OUT/'SHA256SUMS').write_text(report['sha256']+'  '+path.name+'\n')
 print(json.dumps({k:v for k,v in report.items()if k!='animation_types'},ensure_ascii=False,indent=2))
if __name__=='__main__':main()
