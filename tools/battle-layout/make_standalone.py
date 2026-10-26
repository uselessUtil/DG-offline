"""Build one double-clickable HTML with all game images/data embedded; no HTTP."""
import argparse,base64,collections,subprocess,concurrent.futures,functools,hashlib,io,json,re,struct,zipfile
from pathlib import Path
from PIL import Image
import UnityPy
from UnityPy.helpers.MeshHelper import MeshHandler
HERE=Path(__file__).resolve().parent;ROOT=Path('/workspace')
def main():
 p=argparse.ArgumentParser();p.add_argument('--output',type=Path,default=ROOT/'artifacts/battle-layout-editor');p.add_argument('--qa-only',action='store_true');args=p.parse_args()
 out=args.output;out.mkdir(parents=True,exist_ok=True);cache=HERE/'runtime/embedded-assets';cache.mkdir(exist_ok=True)
 cfg=json.loads((HERE/'runtime/source-config.json').read_text());images=json.loads(Path(cfg['images']).read_text());native=json.loads(Path(cfg['nativeManifest']).read_text())
 catalog=json.loads((HERE/'dist/game/catalog.json').read_text());allunits=catalog['units'];units={c['id']:c for c in allunits};layout=json.loads((HERE/'dist/game/layout.json').read_text())
 apk=Path(cfg['apk'])
 with zipfile.ZipFile(apk)as z:
  entry=z.getinfo('assets/dg-images.bin');assert entry.compress_type==0
  with apk.open('rb')as f:f.seek(entry.header_offset);h=f.read(30);base=entry.header_offset+30+sum(struct.unpack_from('<HH',h,26))
 def payload(key):
  row=images['payloads'][key]
  with apk.open('rb')as f:f.seek(base+row['offset']);b=f.read(row['size'])
  assert len(b)==row['size'] and hashlib.sha256(b).hexdigest()==row['sha256'];return b
 @functools.lru_cache(maxsize=10)
 def atlas(name):return Image.open(io.BytesIO(payload('atlas/'+name))).convert('RGBA')
 rows={}
 def prepare_image(job):
  url,source,raw=job;key=hashlib.sha256(raw).hexdigest();dest=cache/(key+'.webp');metadata=cache/(key+'.json')
  if not dest.exists() or not metadata.exists():
   im=Image.open(io.BytesIO(raw)).convert('RGBA');stream=io.BytesIO();im.save(stream,format='WEBP',lossless=True,exact=True,method=4);encoded=stream.getvalue()
   check=Image.open(io.BytesIO(encoded)).convert('RGBA');assert check.tobytes()==im.tobytes(),('Non-lossless image',url)
   dest.write_bytes(encoded);metadata.write_text(json.dumps({'source_png_sha256':key,'rgba_sha256':hashlib.sha256(im.tobytes()).hexdigest(),'width':im.width,'height':im.height,'webp_sha256':hashlib.sha256(encoded).hexdigest()}))
  record=json.loads(metadata.read_text());record.update(url=url,source=source,path=str(dest),mime='image/webp');return record
 def jobs(ids):
  for i in ids:
   c=units[i];yield('/asset/portrait/'+str(i)+'.png','portrait/'+str(int(c['resource'])),payload('portrait/'+str(int(c['resource']))))
   start=i//200*200;name='AtlasCharaIcon_%03d_%03d'%(start,start+199);x,y,w,h=images['sprites'][name+'/chara_icon_'+str(i).zfill(3)]
   im=atlas(name).crop((x,y,x+w,y+h));s=io.BytesIO();im.save(s,format='PNG');yield('/asset/icon/'+str(i)+'.png','native-r17-icon/'+str(i),s.getvalue())
 def prepare(ids):
  pending=[i for i in ids if '/asset/portrait/'+str(i)+'.png'not in rows]
  with concurrent.futures.ThreadPoolExecutor(max_workers=6)as pool:
   for n,row in enumerate(pool.map(prepare_image,jobs(pending))):
    rows[row['url']]=row
    if n%200==199:print('image progress',n+1,'/',len(pending)*2,flush=True)
  extra=[]
  for path in (HERE/'dist/game').glob('*.png'):extra.append((str(path.relative_to(HERE/'dist')),str(path.relative_to(HERE)),path.read_bytes()))
  extra+=[('/asset/image?key='+key,key,payload(key))for key in ['restored-map/map_0001/upper','atlas/AtlasPanelIcon']]
  for job in extra:
   row=prepare_image(job);rows[row['url']]=row
 geometries={}
 def geometry(ids):
  required={i for i in ids if units[i]['native']and units[i]['enemies']and str(i)not in geometries}
  bycab=collections.defaultdict(list)
  for i in required:
   key='GameObject/chara_prefab_'+units[i]['resource'];cab=native['routes'][key];bycab[cab].append((i,key))
  for cab,items in bycab.items():
   env=UnityPy.load((Path(cfg['work'])/'build/native-bundles'/(cab+'.unity3d')).read_bytes());objs={o.path_id:o for o in env.objects}
   for i,key in items:
    go=objs[native['path_ids'][key]];t=go.read_typetree();comps=[objs[(v[1]if isinstance(v,tuple)else v['component'])['m_PathID']]for v in t['m_Component']]
    mf=next(o for o in comps if o.type.name=='MeshFilter');o=objs[mf.read_typetree()['m_Mesh']['m_PathID']]
    h=MeshHandler(o.read(),version=(4,6,5,1));h.process()
    geometries[str(i)]={'native':True,'positions':[list(v)for v in h.m_Vertices],'uv':[list(v[:2])for v in h.m_UV0],'triangles':[i for sub in h.get_triangles()for tri in sub for i in tri]}
  print('native enemy meshes',len(geometries),flush=True)
 def bundle(ids,name):
  # Re-read UI source so visual QA repairs can be packaged without re-encoding art.
  html=(HERE/'dist/index.html').read_text();css=(HERE/'dist/style.css').read_text()
  code='\n'.join(re.sub(r'^import[^\n]*\n','', (HERE/'dist'/n).read_text(),flags=re.M).replace('export const ','const ').replace('export function ','function ').replace('export class ','class ')for n in ['profiles.js','renderer.js','app.js'])
  assert 'fetch('not in code and 'http://'not in code
  subset={**catalog,'units':[units[i]for i in ids]}
  assets=[v for k,v in rows.items()if not k.startswith(('/asset/icon/','/asset/portrait/'))or int(Path(k).stem)in ids]
  index={v['url']:{'id':'dg-asset-'+str(j),'mime':v['mime']}for j,v in enumerate(assets)}
  data={'catalog':subset,'layout':json.loads((HERE/'dist/game/layout.json').read_text()),'geometries':{k:v for k,v in geometries.items()if int(k)in ids}}
  data_raw=json.dumps(data,ensure_ascii=False,separators=(',',':')).replace('</','<'+chr(92)+'/')
  index_raw=json.dumps(index,separators=(',',':'))
  bootstrap="""const DG_ASSET_INDEX=JSON.parse(document.getElementById("dg-asset-index").textContent);
window.DG_LOCAL_DATA=JSON.parse(document.getElementById("dg-game-data").textContent);
const assetURLCache=new Map();
function assetUrl(key){if(assetURLCache.has(key))return assetURLCache.get(key);const row=DG_ASSET_INDEX[key];if(!row)throw Error("缺少內嵌素材："+key);const value="data:"+row.mime+";base64,"+document.getElementById(row.id).textContent.trim();assetURLCache.set(key,value);if(assetURLCache.size>180)assetURLCache.delete(assetURLCache.keys().next().value);return value;}
"""
  compiled='(()=>{"use strict";'+bootstrap+code+'})();'
  (HERE/'runtime/bundled-app.js').write_text(compiled)
  subprocess.run(['node','--check',str(HERE/'runtime/bundled-app.js')],check=True)
  html=html.replace('<link rel="stylesheet" href="style.css">','<style>'+css+'</style>').replace('<script type="module" src="app.js"></script>','')
  html=html.replace('<meta name="theme-color"', '<meta http-equiv="Content-Security-Policy" content="default-src &apos;none&apos;; img-src data: blob:; script-src &apos;unsafe-inline&apos;; style-src &apos;unsafe-inline&apos;; font-src data:; connect-src &apos;none&apos;; object-src &apos;none&apos;; base-uri &apos;none&apos;"><meta name="theme-color"')
  if len(ids)!=1748:html=html.replace('r17 · 原生素材','QA · '+str(len(ids))+' 名測試')
  html=html.replace('href="./"','href="#"')
  prefix,tail=html.split('</body>',1);path=out/name
  with path.open('wb')as file:
   file.write(prefix.encode());file.write(('<script id="dg-game-data" type="application/json">'+data_raw+'</script>').encode())
   file.write(('<script id="dg-asset-index" type="application/json">'+index_raw+'</script>').encode())
   for j,row in enumerate(assets):
    file.write(('<script id="dg-asset-'+str(j)+'" type="application/octet-stream">').encode());file.write(base64.b64encode(Path(row['path']).read_bytes()));file.write(b'</script>')
   file.write(('<script>(()=>{"use strict";'+bootstrap+code+'})();</script></body>'+tail).encode())
  report={'html':str(path),'bytes':path.stat().st_size,'units':len(ids),'embedded_images':len(assets),'native_enemy_meshes':len(data['geometries']),'all_images_verified_lossless_rgba':True,'no_http_server':True,'external_resources':0,'sha256':hashlib.sha256(path.read_bytes()).hexdigest(),'source_apk_sha256':cfg['apkSha256']}
  (out/(name+'.report.json')).write_text(json.dumps(report,ensure_ascii=False,indent=2));print('BUNDLE READY',report,flush=True)
 qa=[810,801,788,789,791,1576,1579,9,1,5,207]
 prepare(qa);geometry(qa);bundle(qa,'DG-Battle-Lab-QA.html')
 if args.qa_only:return
 prepare(list(units));geometry(list(units));bundle(list(units),'DG-Battle-Lab.html')
 (out/'embedded-image-sources.json').write_text(json.dumps(list(rows.values()),ensure_ascii=False,indent=2))
if __name__=='__main__':main()
