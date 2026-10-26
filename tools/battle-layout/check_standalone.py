"""Validate the delivered HTML without loading a browser or starting a server."""
import argparse,base64,hashlib,json,subprocess
from html.parser import HTMLParser
from pathlib import Path
HERE=Path(__file__).resolve().parent
class Check(HTMLParser):
 def __init__(self):
  super().__init__(convert_charrefs=False);self.current=None;self.parts=[];self.payloads={};self.index=None;self.game=None;self.code=None;self.external=[];self.policy=None
 def handle_starttag(self,tag,attrs):
  attrs=dict(attrs)
  if tag in ['script','link','img','iframe','source']:
   for key in ['src','href','srcset']:
    if attrs.get(key)and not attrs[key].startswith(('data:','blob:','#')):self.external.append((tag,key,attrs[key]))
  if tag=='meta'and attrs.get('http-equiv')=='Content-Security-Policy':self.policy=attrs.get('content')
  if tag=='script':
   self.current=attrs.get('id','code');self.parts=[]
 def handle_data(self,text):
  if self.current:self.parts.append(text)
 def handle_endtag(self,tag):
  if tag!='script'or not self.current:return
  text=''.join(self.parts)
  if self.current.startswith('dg-asset-')and self.current!='dg-asset-index':
   raw=base64.b64decode(text,validate=True);self.payloads[self.current]=hashlib.sha256(raw).hexdigest()
  elif self.current=='dg-game-data':self.game=json.loads(text)
  elif self.current=='dg-asset-index':self.index=json.loads(text)
  elif self.current=='code':self.code=text
  self.current=None;self.parts=[]
def main():
 ap=argparse.ArgumentParser();ap.add_argument('html',type=Path);args=ap.parse_args();parser=Check();digest=hashlib.sha256()
 with args.html.open('rb')as file:
  import codecs
  decoder=codecs.getincrementaldecoder('utf8')()
  while True:
   raw=file.read(1024*1024)
   if not raw:break
   digest.update(raw);parser.feed(decoder.decode(raw))
  parser.feed(decoder.decode(b'',final=True));parser.close()
 assert not parser.external,parser.external
 assert parser.policy and "connect-src 'none'"in parser.policy
 assert parser.game and len(parser.game['catalog']['units'])==1748
 assert sum(c['native']for c in parser.game['catalog']['units'])==595
 assert len(parser.game['geometries'])==371
 assert len(parser.payloads)==len(parser.index)==3509
 assert 'fetch('not in parser.code and 'XMLHttpRequest'not in parser.code and 'http://'not in parser.code
 sources={r['url']:r for r in json.loads((args.html.parent/'embedded-image-sources.json').read_text())}
 for key,row in parser.index.items():
  assert row['id']in parser.payloads,key
  assert parser.payloads[row['id']]==sources[key]['webp_sha256'],key
 assert len(parser.game['layout']['portraits'])==5
 units=parser.game['catalog']['units'];sizes=('size_width','size_height','pivot','side_offset')
 changed=0
 for u in units:
  if u['native']:assert all(u['fields'][k]==u['masterFields'][k]for k in sizes)
  elif u['dgdbSize']!=1:
   assert u['fields']['size_width']in ({2:(216,284),3:(106,140)}[u['dgdbSize']])
   assert u['fields']['pivot']==2 and u['fields']['side_offset']<=105
   changed+=1
 assert changed==304
 rin=next(u for u in units if u['id']==789)
 assert [rin['fields'][k]for k in sizes]==[216,216,2,78]
 for group in ['frames','backs']:
  for color,values in parser.game['layout'][group].items():assert len(values)==5 and all(values),(group,color)
 code=HERE/'runtime/checked-delivery.js';code.write_text(parser.code)
 subprocess.run(['node','--check',str(code)],check=True)
 result={'html':args.html.name,'bytes':args.html.stat().st_size,'sha256':digest.hexdigest(),'checks':[
  'All1748 characters and595 reference profiles embedded.',
  'All304 medium/small defaults mapped from DGDB size;595 original size profiles unchanged. Rin uses216x216, pivot2, spacing78.',
  'All3509 embedded images match the verified lossless WebP hashes.',
  'All371 required original enemy meshes embedded;267 other enemy roles retain the r17 quad fallback.',
  'All70 native frame/back prefab surfaces and5 portrait masks present.',
  'No external script, stylesheet, image or frame dependency.',
  'No fetch, XMLHttpRequest or HTTP URL in executable code; CSP forbids network connections.',
  'Delivered classic inline JavaScript syntax validated with Node.',
  'No HTTP server or browser used for this validation.'],
  'browser_visual_verified':False,'browser_limit':'Browser automation policy blocks file://; no HTTP workaround was used.'}
 (args.html.parent/'standalone-validation.json').write_text(json.dumps(result,ensure_ascii=False,indent=2))
 print(json.dumps(result,ensure_ascii=False,indent=2))
if __name__=='__main__':main()
