"""Offline reference rasterization for native UV/geometry checks; not browser QA."""
import json,math,functools
from pathlib import Path
from PIL import Image,ImageDraw,ImageChops,ImageFont
HERE=Path(__file__).resolve().parent;OUT=Path('/workspace/artifacts/battle-layout-editor')
data=json.loads((HERE/'runtime/renderer-test-items.json').read_text());rows={r['url']:r for r in json.loads((OUT/'embedded-image-sources.json').read_text())}
@functools.lru_cache(maxsize=12)
def image(url):
 if url in rows:return Image.open(rows[url]['path']).convert('RGBA')
 return Image.open(HERE/'dist'/url).convert('RGBA')
def solve3(a,b):
 m=[a[i]+[b[i]]for i in range(3)]
 for j in range(3):
  k=max(range(j,3),key=lambda k:abs(m[k][j]))
  if abs(m[k][j])<1e-10:return None
  m[j],m[k]=m[k],m[j];v=m[j][j];m[j]=[x/v for x in m[j]]
  for i in range(3):
   if i!=j:
    v=m[i][j];m[i]=[x-v*y for x,y in zip(m[i],m[j])]
 return[r[-1]for r in m]
canvas=Image.new('RGBA',(640,data['H']),(0,0,0,255))
for item in data['items']:
 points=item['points'];texture=item.get('texture');color=item.get('color',[1,1,1,1]);src=image(texture)if texture else Image.new('RGBA',(1,1),'white')
 for start in range(0,len(item['triangles']),3):
  idx=item['triangles'][start:start+3];ps=[points[i]for i in idx]
  x0=max(0,math.floor(min(p[0]for p in ps)));y0=max(0,math.floor(min(p[1]for p in ps)));x1=min(640,math.ceil(max(p[0]for p in ps)));y1=min(data['H'],math.ceil(max(p[1]for p in ps)))
  if x0>=x1 or y0>=y1:continue
  a=[[p[0]-x0,p[1]-y0,1]for p in ps];uv=[item['uv'][i]for i in idx]
  u=solve3(a,[p[0]*src.width for p in uv]);v=solve3(a,[(1-p[1])*src.height for p in uv])
  if not u or not v:continue
  piece=src.transform((x1-x0,y1-y0),Image.Transform.AFFINE,u+v,Image.Resampling.BILINEAR)
  if color!=[1,1,1,1]:
   channels=list(piece.split())
   for channel,factor in enumerate(color):
    if factor!=1:channels[channel]=channels[channel].point([round(i*factor)for i in range(256)])
   piece=Image.merge('RGBA',channels)
  mask=Image.new('L',piece.size);ImageDraw.Draw(mask).polygon([(p[0]-x0,p[1]-y0)for p in ps],fill=255);piece.putalpha(ImageChops.multiply(piece.getchannel('A'),mask));canvas.alpha_composite(piece,(x0,y0))
draw=ImageDraw.Draw(canvas)
for t in data['texts']:
 # This rasterizer checks asset positions; actual HTML text uses browser fonts.
 font=ImageFont.truetype('/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf',t['size'])
 x=t['x'];bbox=draw.textbbox((0,0),t['value'],font=font)
 if t['align']=='right':x-=bbox[2]
 draw.text((x,t['y']-t['size']),t['value'],fill=t['color'],font=font,stroke_width=2,stroke_fill='black')
canvas.convert('RGB').save(OUT/'native-geometry-reference.jpg')
canvas.crop((0,data['H']-235,640,data['H'])).convert('RGB').save(OUT/'native-party-reference.jpg')
print('Offline reference saved; does not confirm browser rendering.')
