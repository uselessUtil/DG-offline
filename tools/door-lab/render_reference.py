"""Offline atlas/mesh/curve raster QA. This is not a browser screenshot."""
import json,math,functools
from pathlib import Path
from PIL import Image,ImageDraw,ImageChops,ImageFont
HERE=Path(__file__).resolve().parent;ROOT=Path('/workspace');OUT=ROOT/'artifacts/door-animation-lab'
frames=json.loads((HERE/'runtime/reference-frames.json').read_text());data=json.loads((HERE/'runtime/doors.json').read_text())
@functools.lru_cache(maxsize=12)
def source(resource):return Image.open(data['textures'][resource]['file']).convert('RGBA')
def solve(a,b):
 m=[a[i]+[b[i]]for i in range(3)]
 for j in range(3):
  k=max(range(j,3),key=lambda k:abs(m[k][j]))
  if abs(m[k][j])<1e-9:return
  m[j],m[k]=m[k],m[j];v=m[j][j];m[j]=[x/v for x in m[j]]
  for i in range(3):
   if i!=j:
    v=m[i][j];m[i]=[x-v*y for x,y in zip(m[i],m[j])]
 return[r[-1]for r in m]
def triangle(canvas,src,ps,uv):
 x0=max(0,math.floor(min(p[0]for p in ps)));y0=max(0,math.floor(min(p[1]for p in ps)));x1=min(canvas.width,math.ceil(max(p[0]for p in ps)));y1=min(canvas.height,math.ceil(max(p[1]for p in ps)))
 if x0>=x1 or y0>=y1:return
 a=[[p[0]-x0,p[1]-y0,1]for p in ps];u=solve(a,[p[0]*src.width for p in uv]);v=solve(a,[(1-p[1])*src.height for p in uv])
 if not u or not v:return
 piece=src.transform((x1-x0,y1-y0),Image.Transform.AFFINE,u+v,Image.Resampling.BILINEAR)
 mask=Image.new('L',piece.size);ImageDraw.Draw(mask).polygon([(p[0]-x0,p[1]-y0)for p in ps],fill=255);piece.putalpha(ImageChops.multiply(piece.getchannel('A'),mask));canvas.alpha_composite(piece,(x0,y0))
sheet=Image.new('RGB',(4*330,7*270),(17,20,25));font=ImageFont.truetype('/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf',13)
for number,frame in enumerate(frames):
 tile=Image.new('RGBA',(320,230),(24,29,36,255));src=source(frame['texture'])
 for part in sorted(frame['parts'],key=lambda p:sum(v[1]for v in p['positions'])/len(p['positions'])):
  points=[(160+v[0]*145,210-(v[2]-2.35)*145)for v in part['positions']]
  for start in range(0,len(part['triangles']),3):
   tri=part['triangles'][start:start+3];triangle(tile,src,[points[i]for i in tri],[part['uv'][i]for i in tri])
 x=number%4*330;y=number//4*270;sheet.paste(tile.convert('RGB'),(x+5,y+30));draw=ImageDraw.Draw(sheet);draw.text((x+8,y+5),frame['family']+'  '+str(round(frame['phase']*100))+'%',font=font,fill='#e7b68a')
sheet.save(OUT/'native-door-animation-reference.jpg',quality=92)
print('Saved geometry/UV offline reference, not browser QA.')
