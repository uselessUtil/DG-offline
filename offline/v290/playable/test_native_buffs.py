"""Exercise actual signed APK game logic. Unity I/O stubs stay in /tmp."""
import argparse,hashlib,json,os,subprocess,tempfile,zipfile
from pathlib import Path
ROOT=Path('/workspace');CODE=ROOT/'offline/v290/playable'
CECIL='/usr/lib/mono/gac/Mono.Cecil/0.11.1.0__0738eb9f132ed756/Mono.Cecil.dll'
def run(*a,**kw):
 try:return subprocess.check_output(list(map(str,a)),text=True,stderr=subprocess.STDOUT,timeout=180,**kw)
 except subprocess.CalledProcessError as ex:print(ex.output,flush=True);raise
def main():
 p=argparse.ArgumentParser();p.add_argument('report',type=Path);p.add_argument('--baseline',action='store_true');a=p.parse_args()
 r=json.loads(a.report.read_text());apk=Path(r['output'])
 assert hashlib.sha256(apk.read_bytes()).hexdigest()==r['sha256']
 w=Path(tempfile.mkdtemp(prefix='dg290-native-buffs-',dir='/tmp'));original=Path(r['work_directory'])/'original/assets/bin/Data/Managed'
 tools=w/'tools';tools.mkdir()
 with zipfile.ZipFile(apk) as z:
  for n in z.namelist():
   if n.startswith('assets/bin/Data/Managed/') and n.endswith('.dll'):(w/Path(n).name).write_bytes(z.read(n))
 run('mcs','-target:library','-out:'+str(w/'EngineSupport.dll'),'-r:'+str(original/'UnityEngine.dll'),ROOT/'offline/tests/EngineSupport.cs')
 run('mcs','-r:'+CECIL,'-out:'+str(tools/'TestEngine.exe'),ROOT/'offline/tests/TestEngine.cs')
 run('mono',tools/'TestEngine.exe',original/'UnityEngine.dll',w/'EngineSupport.dll',w/'UnityEngine.dll')
 run('mcs','-out:'+str(w/'NativeBuffTests290.exe'),*['-r:'+str(w/n) for n in ['Assembly-CSharp.dll','Assembly-CSharp-firstpass.dll','UnityEngine.dll','DivineGateOffline.dll']],CODE/'NativeBuffTests290.cs')
 env=dict(os.environ,DG_TEST_PREFS=str(w/'prefs'),DG_BUFF_STATE_FILE=str(w/'state.json'))
 try:
  out=run('mono',w/'NativeBuffTests290.exe',*(['baseline'] if a.baseline else []),env=env,cwd=w)
  if not a.baseline:out+=run('mono',w/'NativeBuffTests290.exe','reload',env=env,cwd=w)
 except subprocess.CalledProcessError as ex:print(ex.output);raise
 label='native-buffs-baseline' if a.baseline else 'native-buffs-regression'
 (a.report.parent/(label+'.txt')).write_text(out)
 (a.report.parent/(label+'.json')).write_text(json.dumps(dict(apk_sha256=r['sha256'],work=str(w),baseline=a.baseline,output=out,android_tested=False,scope='Signed APK assemblies. Original damage calculation, native status application, status multipliers and turn update execute unchanged; Unity I/O stubs are confined to temporary test copy.'),indent=2)+'\n')
 print(out,flush=True)
if __name__=='__main__':main()
