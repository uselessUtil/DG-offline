import json,os,shutil,subprocess,tempfile,zipfile,hashlib,argparse
from pathlib import Path
ROOT=Path('/workspace');CODE=ROOT/'offline/v290/playable'
CECIL='/usr/lib/mono/gac/Mono.Cecil/0.11.1.0__0738eb9f132ed756/Mono.Cecil.dll'
def run(*a,**kw):return subprocess.check_output(list(map(str,a)),text=True,stderr=subprocess.STDOUT,timeout=120,**kw)
def main():
 p=argparse.ArgumentParser();p.add_argument('report',type=Path);a=p.parse_args();r=json.loads(a.report.read_text());apk=Path(r['output'])
 assert hashlib.sha256(apk.read_bytes()).hexdigest()==r['sha256']
 w=Path(tempfile.mkdtemp(prefix='dg290-update-test-'));tools=w/'tools';tools.mkdir();orig=Path(r['work_directory'])/'original/assets/bin/Data/Managed'
 with zipfile.ZipFile(apk) as z:
  for n in z.namelist():
   if n.startswith('assets/bin/Data/Managed/') and n.endswith('.dll'):(w/Path(n).name).write_bytes(z.read(n))
 tests=ROOT/'offline/tests'
 run('mcs','-target:library','-out:'+str(w/'EngineSupport.dll'),'-r:'+str(orig/'UnityEngine.dll'),tests/'EngineSupport.cs')
 run('mcs','-r:'+CECIL,'-out:'+str(tools/'TestEngine.exe'),tests/'TestEngine.cs')
 run('mono',tools/'TestEngine.exe',orig/'UnityEngine.dll',w/'EngineSupport.dll',w/'UnityEngine.dll')
 refs=['-r:'+str(w/n) for n in ['Assembly-CSharp.dll','Assembly-CSharp-firstpass.dll','UnityEngine.dll','DivineGateOffline.dll']]
 run('mcs','-target:library','-out:'+str(w/'UpdateUiSupport.dll'),*refs,CODE/'UpdateUiSupport.cs')
 run('mcs','-r:'+CECIL,'-out:'+str(tools/'TestUpdateUi.exe'),CODE/'TestUpdateUi.cs')
 run('mono',tools/'TestUpdateUi.exe',w/'Assembly-CSharp.dll',w/'UpdateUiSupport.dll',w/'test-game.dll')
 shutil.move(w/'test-game.dll',w/'Assembly-CSharp.dll')
 run('mcs','-out:'+str(w/'UpdateLoopTests.exe'),*refs,'-r:'+str(w/'UpdateUiSupport.dll'),CODE/'UpdateLoopTests.cs')
 env=dict(os.environ,DG_TEST_PREFS=str(w/'prefs.txt'),DG_EXPECTED_SAVE=str(w/'expected-save.txt'))
 try:
  result=run('mono',w/'UpdateLoopTests.exe',cwd=w,env=env)+run('mono',w/'UpdateLoopTests.exe','reload',cwd=w,env=env)
 except subprocess.CalledProcessError as ex:print(ex.output);raise
 (a.report.parent/'update-loop-regression.txt').write_text(result)
 print(result)
if __name__=='__main__':main()
