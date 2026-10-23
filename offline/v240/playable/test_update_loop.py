"""Actual signed APK regression; UI and Unity I/O instrumentation stay in /tmp."""
import argparse,hashlib,json,os,shutil,subprocess,tempfile,zipfile
from pathlib import Path
ROOT=Path('/workspace');CODE=ROOT/'offline/v240/playable';CECIL='/usr/lib/mono/gac/Mono.Cecil/0.11.1.0__0738eb9f132ed756/Mono.Cecil.dll'
def run(*a,**kw):return subprocess.check_output(list(map(str,a)),text=True,stderr=subprocess.STDOUT,timeout=180,**kw)
def main():
 p=argparse.ArgumentParser();p.add_argument('report',type=Path);a=p.parse_args()
 r=json.loads(a.report.read_text());apk=Path(r['output']);assert hashlib.sha256(apk.read_bytes()).hexdigest()==r['sha256']
 w=Path(tempfile.mkdtemp(prefix='dg240-update-test-',dir='/tmp'));tools=w/'tools';tools.mkdir()
 with zipfile.ZipFile(apk) as z:
  for n in z.namelist():
   if n.startswith('assets/bin/Data/Managed/') and n.endswith('.dll'):(w/Path(n).name).write_bytes(z.read(n))
 original=Path(r['work_directory'])/'original/assets/bin/Data/Managed'
 tests=ROOT/'offline/tests'
 run('mcs','-target:library','-out:'+str(w/'EngineSupport.dll'),'-r:'+str(original/'UnityEngine.dll'),tests/'EngineSupport.cs')
 run('mcs','-r:'+CECIL,'-out:'+str(tools/'TestEngine.exe'),tests/'TestEngine.cs')
 run('mono',tools/'TestEngine.exe',original/'UnityEngine.dll',w/'EngineSupport.dll',w/'UnityEngine.dll')
 refs=['-r:'+str(w/n) for n in ['Assembly-CSharp.dll','Assembly-CSharp-firstpass.dll','UnityEngine.dll','DivineGateOffline.dll']]
 run('mcs','-target:library','-out:'+str(w/'UpdateUiSupport.dll'),*refs,CODE/'UpdateUiSupport.cs')
 run('mcs','-r:'+CECIL,'-out:'+str(tools/'TestUpdateUi.exe'),CODE/'TestUpdateUi.cs')
 run('mono',tools/'TestUpdateUi.exe',w/'Assembly-CSharp.dll',w/'UpdateUiSupport.dll',tools/'Assembly-CSharp.dll')
 shutil.copy2(tools/'Assembly-CSharp.dll',w/'Assembly-CSharp.dll')
 run('mcs','-out:'+str(w/'UpdateLoopTests.exe'),*refs,'-r:'+str(w/'UpdateUiSupport.dll'),CODE/'UpdateLoopTests.cs')
 outputs=[]
 def case(label,mode,prefs,expected):
  env=dict(os.environ,DG_TEST_PREFS=str(prefs),DG_EXPECTED_SAVE=str(expected))
  try:out=run('mono',w/'UpdateLoopTests.exe',mode,cwd=w,env=env)
  except subprocess.CalledProcessError as ex:print(ex.output);raise
  outputs.append({'case':label,'output':out});print(out,flush=True)
 case('fresh account with Index clears','fresh',w/'prefs.txt',w/'expected.txt')
 case('fresh process restart','reload',w/'prefs.txt',w/'expected.txt')
 case('repair existing account stale marker','migrate',w/'prefs.txt',w/'expected.txt')
 case('repaired process restart','reload',w/'prefs.txt',w/'expected.txt')
 assert hashlib.sha256(apk.read_bytes()).hexdigest()==r['sha256']
 result={'apk_sha256':r['sha256'],'work':str(w),'android_tested':False,'cases':outputs,'scope':'Original MainMenuSeqMasterUpdateChk.Update executed; UI presentation endpoints and Unity native I/O stubbed only in temporary DLL copies. API quest start/clear exercises managed server simulation, not combat.'}
 out=a.report.parent/'update-loop-regression.json';out.write_text(json.dumps(result,indent=2)+'\n')
if __name__=='__main__':main()
