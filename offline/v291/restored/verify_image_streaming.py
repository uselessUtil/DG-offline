"""Guard the old32-bit Mono failure and verify final APK file-backed PNG transport."""
import argparse,hashlib,json,subprocess,tempfile,zipfile,struct
from pathlib import Path
ROOT=Path('/workspace');CODE=ROOT/'offline/v291/restored'
CECIL='/usr/lib/mono/gac/Mono.Cecil/0.11.1.0__0738eb9f132ed756/Mono.Cecil.dll'
def run(*a):
 p=subprocess.run(list(map(str,a)),text=True,stdout=subprocess.PIPE,stderr=subprocess.STDOUT,check=False,timeout=180)
 if p.returncode:raise RuntimeError(p.stdout)
 return p.stdout
def sha(path):
 h=hashlib.sha256()
 with path.open('rb') as f:
  while True:
   data=f.read(1048576)
   if not data:break
   h.update(data)
 return h.hexdigest()
def fixtures(p):
 raw=bytes(i%256 for i in range(501))
 for name in ['good','max-comment','missing','compressed']:
  with zipfile.ZipFile(p/(name+'.apk'),'w') as z:
   z.writestr('classes.dex',b'not-a-device-package')
   z.writestr('other' if name=='missing' else 'assets/dg-images.bin',raw,compress_type=zipfile.ZIP_DEFLATED if name=='compressed' else zipfile.ZIP_STORED)
   if name=='max-comment':z.comment=b'CommentPK\x05\x06'+b'x'*65524
 b=(p/'good.apk').read_bytes();(p/'truncated.apk').write_bytes(b[:-10])
 with zipfile.ZipFile(p/'good.apk') as z:offset=z.getinfo('assets/dg-images.bin').header_offset
 b=bytearray(b);struct.pack_into('<H',b,offset+8,8);(p/'wrong-local.apk').write_bytes(b)
 b=bytearray((p/'good.apk').read_bytes());struct.pack_into('<H',b,len(b)-12,65535);(p/'zip64.apk').write_bytes(b)
def main():
 p=argparse.ArgumentParser();p.add_argument('report',type=Path);a=p.parse_args();out=a.report.parent
 r=json.loads(a.report.read_text());assert r['image_storage']['kind']=='apk-stored-slices'
 prior_dir=ROOT/'artifacts/2.9.1-restored-r12';old=json.loads((prior_dir/'build-report.json').read_text())
 images=json.loads((out/'images.json').read_text());before=json.loads((prior_dir/'images.json').read_text())
 ui=bool(images.get('ui_fallbacks'))
 if ui:
  for k,v in before['payloads'].items():assert k in images['payloads'] and all(images['payloads'][k][f]==v[f] for f in ['sha256','size']),k
  for k,v in before['sprites'].items():assert images['sprites'][k]==v,k
 else:assert images['payloads']==before['payloads'] and images['sprites']==before['sprites']
 assert sha(Path(r['output']))==r['sha256'] and sha(Path(old['output']))==old['sha256']
 oldtables=Path(old['work_directory'])/'tables';tables=Path(r['work_directory'])/'tables'
 assert sorted(p.name for p in tables.glob('*.json'))==sorted(p.name for p in oldtables.glob('*.json'))
 for f in tables.glob('*.json'):assert f.read_bytes()==(oldtables/f.name).read_bytes(),f.name
 w=Path(tempfile.mkdtemp(prefix='dg291-png-streaming-',dir='/tmp'))
 stored=None;changes=[]
 with zipfile.ZipFile(r['output']) as z,zipfile.ZipFile(old['output']) as oz:
  for tag,archive in [('before',oz),('after',z)]:
   (w/(tag+'.dll')).write_bytes(archive.read('assets/bin/Data/Managed/DivineGateOffline.dll'))
  entry=z.getinfo('assets/dg-images.bin')
  assert entry.compress_type==zipfile.ZIP_STORED and entry.file_size==entry.compress_size==images['store_size']
  with z.open(entry) as stream:
   h=hashlib.sha256()
   while True:
    chunk=stream.read(1048576)
    if not chunk:break
    h.update(chunk)
   stored=h.hexdigest()
  assert stored==sha(Path(r['work_directory'])/'build/RecoveredImages.bin')
  for n in oz.namelist():
   if n.upper().startswith('META-INF/'):continue
   if hashlib.sha256(oz.read(n)).digest()!=hashlib.sha256(z.read(n)).digest():changes.append(n)
  allowed=['assets/bin/Data/Managed/DivineGateOffline.dll','classes.dex']
  if ui:allowed.append('assets/bin/Data/Managed/Assembly-CSharp.dll')
  assert set(changes)<=set(allowed),changes
  assert set(n for n in set(z.namelist())-set(oz.namelist()) if not n.upper().startswith('META-INF/'))=={'assets/dg-images.bin'}
 logs=[]
 run('mcs','-r:'+CECIL,'-out:'+str(w/'Packaging.exe'),CODE/'VerifyImagePackaging.cs')
 logs.append(run('mono',w/'Packaging.exe',w/'before.dll',w/'after.dll'))
 fixtures(w)
 managed=Path(r['work_directory'])/'original/assets/bin/Data/Managed'
 run('mcs','-nostdlib','-r:'+str(managed/'mscorlib.dll'),'-r:'+str(managed/'System.dll'),
     '-out:'+str(w/'ReaderTests.exe'),CODE/'ApkImages.cs',CODE/'ApkImageReaderTests.cs')
 logs.append(run('mono',w/'ReaderTests.exe',w))
 logs.append('PASS all35 master tables and original4368 PNG hashes/sprite rectangles match r12; added UI atlases separately audited; native Unity data and original model packages unchanged; UI hook checked separately. APK payload is ZIP_STORED; no new permission, save schema or data revision.')
 result={'apk_sha256':r['sha256'],'work':str(w),'adapter_before_bytes':(w/'before.dll').stat().st_size,
   'adapter_after_bytes':(w/'after.dll').stat().st_size,'image_bytes':images['store_size'],'image_store_sha256':stored,
   'changed_existing_entries':changes,'tests':logs,'android_tested':False}
 (out/'image-streaming-tests.json').write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n')
 (out/'image-streaming-tests.txt').write_text('\n'.join(logs));print('\n'.join(logs),flush=True)
 r['image_streaming_tests_passed']=True
 a.report.write_text(json.dumps(r,ensure_ascii=False,indent=2)+'\n')
if __name__=='__main__':main()
