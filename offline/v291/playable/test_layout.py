"""Original106 IL oracle plus actual signed291 APK layout and restart regression."""
import argparse,hashlib,json,os,shutil,subprocess,tempfile,zipfile
from pathlib import Path
ROOT=Path('/workspace');CODE=ROOT/'offline/v291/playable'
CECIL='/usr/lib/mono/gac/Mono.Cecil/0.11.1.0__0738eb9f132ed756/Mono.Cecil.dll'
def run(*a,**kw):
 p=subprocess.run(list(map(str,a)),text=True,stdout=subprocess.PIPE,stderr=subprocess.STDOUT,timeout=180,**kw)
 if p.returncode:raise RuntimeError(p.stdout)
 return p.stdout
def main():
 p=argparse.ArgumentParser();p.add_argument('report',type=Path);a=p.parse_args()
 report=json.loads(a.report.read_text());apk=Path(report['output'])
 assert hashlib.sha256(apk.read_bytes()).hexdigest()==report['sha256']
 oldapk=ROOT/'ディバゲ_1.0.6.apk'
 assert hashlib.sha256(oldapk.read_bytes()).hexdigest()=='ba3ccd42c28e4d9362cb6bda2b4d21483b98085441f9513ca1b825bc71be81c2'
 work=Path(tempfile.mkdtemp(prefix='dg291-layout-',dir='/tmp'))
 old=work/'106';new=work/'291';old.mkdir();new.mkdir()
 for path,d in [(oldapk,old),(apk,new)]:
  with zipfile.ZipFile(path) as z:
   for n in z.namelist():
    if '/Managed/' in n and n.endswith('.dll'):(d/Path(n).name).write_bytes(z.read(n))
 # Stub Unity I/O in test copies only.
 run('mcs','-r:'+CECIL,'-out:'+str(work/'TestEngine.exe'),ROOT/'offline/tests/TestEngine.cs')
 for d in [old,new]:
  run('mcs','-target:library','-out:'+str(d/'EngineSupport.dll'),'-r:'+str(d/'UnityEngine.dll'),ROOT/'offline/tests/EngineSupport.cs')
  run('mono',work/'TestEngine.exe',d/'UnityEngine.dll',d/'EngineSupport.dll',d/'TestUnity.dll')
  shutil.move(d/'TestUnity.dll',d/'UnityEngine.dll')
 run('mcs','-r:'+CECIL,'-out:'+str(work/'Prepare.exe'),CODE/'PrepareLayoutOracle106.cs')
 results=[run('mono',work/'Prepare.exe',old/'Assembly-CSharp.dll',old/'Oracle.dll')]
 shutil.move(old/'Oracle.dll',old/'Assembly-CSharp.dll')
 def compile(d,name):
  run('mcs','-out:'+str(d/(name+'.exe')),*['-r:'+str(d/n) for n in ['Assembly-CSharp.dll','Assembly-CSharp-firstpass.dll','UnityEngine.dll']+(['DivineGateOffline.dll'] if d==new else [])],CODE/(name+'.cs'))
 compile(old,'LayoutOracle106')
 tables=Path(report['work_directory'])/'tables'
 categories=json.loads((tables/'MasterDataCategoryPattern.json').read_text())
 for c in categories:c.pop('cate5',None)
 (work/'category106.json').write_text(json.dumps(categories))
 results.append(run('mono',old/'LayoutOracle106.exe',work/'category106.json',tables/'MasterDataExpectPattern.json',work/'oracle.bin',cwd=old))
 compile(new,'LayoutTests291')
 env=dict(os.environ,DG_TEST_PREFS=str(work/'prefs'))
 results.append(run('mono',new/'LayoutTests291.exe',work/'oracle.bin',work/'expected',cwd=new,env=env))
 results.append(run('mono',new/'LayoutTests291.exe','reload',work/'expected',cwd=new,env=env))
 output=chr(10).join(results);print(output)
 (a.report.parent/'layout-regression.txt').write_text(output)
 (a.report.parent/'layout-regression.json').write_text(json.dumps({'apk_sha256':report['sha256'],'original106_sha256':hashlib.sha256(oldapk.read_bytes()).hexdigest(),'work':str(work),'output':output,'scope':'Original106 layout IL oracle, actual291 hooked quest builder; Unity I/O stubbed in temporary copies. No player save touched.'},indent=2))
if __name__=='__main__':main()
