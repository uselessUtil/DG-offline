"""Read-only inventory of the hash-locked 2.4.0 APK; run inside divine-gate-dev."""
import hashlib,json,subprocess,zipfile
from pathlib import Path
import UnityPy
ROOT=Path('/workspace');CODE=ROOT/'offline/v240/playable'
SOURCE=ROOT/'ディバゲ_2.4.0.apk'
SHA='8e754c6509405a5deb2aebe0389ca914d3b1723eb1e64c8c7fb9c6e018652e64'
OUT=ROOT/'artifacts/2.4.0-port-audit';WORK=Path('/tmp/dg240-audit')
CECIL='/usr/lib/mono/gac/Mono.Cecil/0.11.1.0__0738eb9f132ed756/Mono.Cecil.dll'
def run(*a):return subprocess.check_output(list(map(str,a)),text=True,stderr=subprocess.STDOUT)
def main():
 raw=SOURCE.read_bytes();assert hashlib.sha256(raw).hexdigest()==SHA
 OUT.mkdir(exist_ok=True);original=WORK/'original';original.mkdir(parents=True,exist_ok=True)
 with zipfile.ZipFile(SOURCE) as z:
  assert z.testzip() is None and len(z.namelist())==len(set(z.namelist()))
  for n in z.namelist():
   assert not n.startswith('/') and '..' not in Path(n).parts
   if n.startswith('assets/bin/Data/') and not n.endswith('/'):z.extract(n,original)
 for name,args in [('badging',['aapt','dump','badging',SOURCE]),('signature',['apksigner','verify','--verbose','--print-certs',SOURCE])]:
  (OUT/(name+'.txt')).write_text(run(*args))
 assert "versionName='2.4.0'" in (OUT/'badging.txt').read_text()
 run('mcs','-r:'+CECIL,'-out:'+str(WORK/'Inspect.exe'),CODE/'Inspect.cs')
 (OUT/'original-schema.tsv').write_text(run('mono',WORK/'Inspect.exe',original/'assets/bin/Data/Managed/Assembly-CSharp.dll'))
 env=UnityPy.load(str(original/'assets/bin/Data'))
 scripts={o.read().m_ClassName:{'hash':str(o.read().m_PropertiesHash),'file':o.assets_file.name} for o in env.objects if o.type.name=='MonoScript'}
 report={'source':str(SOURCE),'source_sha256':SHA,'size':len(raw),'work':str(WORK),'unity_versions':sorted(set(str(o.assets_file.unity_version) for o in env.objects)),'scripts':scripts}
 (OUT/'inventory.json').write_text(json.dumps(report,indent=2)+'\n')
 assert hashlib.sha256(SOURCE.read_bytes()).hexdigest()==SHA
 print('PASS read-only 2.4.0 inventory',SHA)
if __name__=='__main__':main()
