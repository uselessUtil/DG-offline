"""Exercise target-specific additions through the actual APK's original consumers."""
import argparse,hashlib,json,os,subprocess,tempfile,zipfile
from pathlib import Path
ROOT=Path('/workspace');CODE=ROOT/'offline/v240/playable';CECIL='/usr/lib/mono/gac/Mono.Cecil/0.11.1.0__0738eb9f132ed756/Mono.Cecil.dll'
def run(*a,**kw):
 p=subprocess.run(list(map(str,a)),stdout=subprocess.PIPE,stderr=subprocess.STDOUT,text=True,timeout=180,**kw)
 if p.returncode:print(p.stdout,flush=True);p.check_returncode()
 return p.stdout
def main():
 p=argparse.ArgumentParser();p.add_argument('report',type=Path);a=p.parse_args();r=json.loads(a.report.read_text());apk=Path(r['output'])
 assert hashlib.sha256(apk.read_bytes()).hexdigest()==r['sha256']
 w=Path(tempfile.mkdtemp(prefix='dg240-compat-test-',dir='/tmp'));tools=w/'tools';tools.mkdir();original=Path(r['work_directory'])/'original/assets/bin/Data/Managed'
 with zipfile.ZipFile(apk) as z:
  for n in z.namelist():
   if n.startswith('assets/bin/Data/Managed/') and n.endswith('.dll'):(w/Path(n).name).write_bytes(z.read(n))
 tests=ROOT/'offline/tests'
 run('mcs','-target:library','-out:'+str(w/'EngineSupport.dll'),'-r:'+str(original/'UnityEngine.dll'),tests/'EngineSupport.cs')
 run('mcs','-r:'+CECIL,'-out:'+str(tools/'TestEngine.exe'),tests/'TestEngine.cs')
 run('mono',tools/'TestEngine.exe',original/'UnityEngine.dll',w/'EngineSupport.dll',w/'UnityEngine.dll')
 refs=['-r:'+str(w/n) for n in ['Assembly-CSharp.dll','Assembly-CSharp-firstpass.dll','UnityEngine.dll','DivineGateOffline.dll']]
 run('mcs','-out:'+str(w/'CompatibilityTests.exe'),*refs,CODE/'CompatibilityTests.cs')
 output=run('mono',w/'CompatibilityTests.exe',cwd=w,env=dict(os.environ,DG_TEST_PREFS=str(w/'prefs.txt')))
 assert hashlib.sha256(apk.read_bytes()).hexdigest()==r['sha256']
 (a.report.parent/'compatibility.txt').write_text(output)
 (a.report.parent/'compatibility.json').write_text(json.dumps({'apk_sha256':r['sha256'],'work':str(w),'output':output,'android_tested':False,'scope':'Target original quest admission, Boost predicate, gacha selection and title store loader; Unity native I/O stubbed only in temporary copy.'},indent=2)+'\n')
 print(output)
if __name__=='__main__':main()
