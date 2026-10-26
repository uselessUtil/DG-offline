import json,hashlib,re,subprocess,base64
from pathlib import Path
from html.parser import HTMLParser
ROOT=Path('/workspace');HERE=Path(__file__).resolve().parent;OUT=ROOT/'artifacts/door-animation-lab'
class Check(HTMLParser):
 def __init__(self):super().__init__(convert_charrefs=False);self.current=None;self.parts=[];self.data=None;self.assets=None;self.payloads={};self.script=None;self.external=[];self.policy=None
 def handle_starttag(self,tag,attrs):
  a=dict(attrs)
  if(tag in ['script','img','iframe','link'])and(a.get('src')or a.get('href')):self.external.append(a)
  if tag=='meta'and a.get('http-equiv')=='Content-Security-Policy':self.policy=a['content']
  if tag=='script':self.current=a.get('id','app');self.parts=[]
 def handle_data(self,text):
  if self.current:self.parts.append(text)
 def handle_endtag(self,tag):
  if tag!='script'or not self.current:return
  value=''.join(self.parts)
  if self.current=='door-data':self.data=json.loads(value)
  elif self.current=='door-assets':self.assets=json.loads(value)
  elif self.current.startswith('door-image-'):self.payloads[self.current]=hashlib.sha256(base64.b64decode(value)).hexdigest()
  elif self.current=='app':self.script=value
  self.current=None;self.parts=[]
def main():
 path=OUT/'DG-Door-Motion-Lab.html';p=Check();digest=hashlib.sha256()
 with path.open('r')as f:
  while chunk:=f.read(1024*1024):p.feed(chunk)
 with path.open('rb')as f:digest=hashlib.file_digest(f,'sha256').hexdigest()
 assert not p.external and 'connect-src' in p.policy and 'none'in p.policy
 assert len(p.data['quests'])==921 and len(p.data['areas'])==397 and len(p.assets)==272 and len(p.payloads)==272
 assert p.data==json.loads((HERE/'runtime/packed-data.json').read_text())
 for resource,t in p.data['textures'].items():assert p.payloads[p.assets[resource]]==t['webp_sha256']
 assert not re.search(r'\b(fetch|XMLHttpRequest)\s*\(|https?://|localhost',p.script)
 (HERE/'runtime/delivered-script.js').write_text(p.script);subprocess.run(['node','--check',str(HERE/'runtime/delivered-script.js')],check=True)
 assert digest==json.loads((OUT/'build-report.json').read_text())['sha256']
 result={'html':path.name,'sha256':digest,'quests':921,'areas':397,'embedded_images':272,'external_dependencies':0,'network_calls':0,'delivered_data_exactly_matches_tested_data':True,'delivered_javascript_syntax_valid':True,'all_atlas_hashes_match':True,'browser_visual_verified':False,'browser_limit':'file:// browser automation is blocked; no HTTP or alternate browser workaround used.'}
 (OUT/'standalone-validation.json').write_text(json.dumps(result,indent=2));print(json.dumps(result,indent=2))
if __name__=='__main__':main()
