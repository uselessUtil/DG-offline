"""Regression on actual signed APK DLLs; Unity I/O stubs stay in /tmp."""
import argparse,hashlib,json,os,subprocess,tempfile,zipfile
from pathlib import Path
import UnityPy
from UnityPy.files.SerializedFile import SerializedFile
ROOT=Path('/workspace');CODE=ROOT/'offline/v210/playable';CECIL='/usr/lib/mono/gac/Mono.Cecil/0.11.1.0__0738eb9f132ed756/Mono.Cecil.dll'
def run(*a,**kw):return subprocess.check_output(list(map(str,a)),text=True,stderr=subprocess.STDOUT,timeout=180,**kw)
def main():
 p=argparse.ArgumentParser();p.add_argument('report',type=Path);p.add_argument('--baseline',action='store_true');a=p.parse_args()
 r=json.loads(a.report.read_text());apk=Path(r['output']);assert hashlib.sha256(apk.read_bytes()).hexdigest()==r['sha256']
 w=Path(tempfile.mkdtemp(prefix='dg210-key-test-',dir='/tmp'));tools=w/'tools';tools.mkdir()
 with zipfile.ZipFile(apk) as z:
  for n in z.namelist():
   if n.startswith('assets/bin/Data/Managed/') and n.endswith('.dll'):(w/Path(n).name).write_bytes(z.read(n))
 original=Path(r['work_directory'])/'original/assets/bin/Data/Managed'
 inv=json.loads((ROOT/'artifacts/cache-backup-audit-2026-10-22/objects.json').read_text());cab=next(x['cab'] for x in inv if x.get('name')=='MasterDataCategoryPattern' and x['type']=='TextAsset')
 raw=(ROOT/'jp.gungho.dg'/cab).read_bytes();source_sha=hashlib.sha256(raw).hexdigest();old=SerializedFile.set_version;SerializedFile.set_version=lambda self,v:old(self,v.splitlines()[0])
 try:
  asset=next(o.read() for o in UnityPy.load(raw).objects if o.type.name=='TextAsset' and o.read().m_Name=='MasterDataCategoryPattern')
  data=asset.m_Script.encode('utf8','surrogateescape') if isinstance(asset.m_Script,str) else asset.m_Script
  (w/'source-pattern.json').write_bytes(data)
 finally:SerializedFile.set_version=old
 tests=ROOT/'offline/tests'
 run('mcs','-target:library','-out:'+str(w/'EngineSupport.dll'),'-r:'+str(original/'UnityEngine.dll'),tests/'EngineSupport.cs')
 run('mcs','-r:'+CECIL,'-out:'+str(tools/'TestEngine.exe'),tests/'TestEngine.cs')
 run('mono',tools/'TestEngine.exe',original/'UnityEngine.dll',w/'EngineSupport.dll',w/'UnityEngine.dll')
 run('mcs','-out:'+str(w/'KeyFloorTests.exe'),'-r:'+str(w/'Assembly-CSharp.dll'),'-r:'+str(w/'Assembly-CSharp-firstpass.dll'),'-r:'+str(w/'UnityEngine.dll'),CODE/'KeyFloorTests.cs')
 env=dict(os.environ,DG_KEY_SOURCE=str(w/'source-pattern.json'),DG_TEST_PREFS=str(w/'prefs.txt'))
 try:result=run('mono',w/'KeyFloorTests.exe',*(['baseline'] if a.baseline else []),cwd=w,env=env)
 except subprocess.CalledProcessError as ex:print(ex.output);raise
 assert hashlib.sha256((ROOT/'jp.gungho.dg'/cab).read_bytes()).hexdigest()==source_sha
 assert hashlib.sha256(apk.read_bytes()).hexdigest()==r['sha256']
 label='key-floor-baseline' if a.baseline else 'key-floor-regression'
 (a.report.parent/(label+'.txt')).write_text(result)
 (a.report.parent/(label+'.json')).write_text(json.dumps({'apk_sha256':r['sha256'],'work':str(w),'source_master_cab_sha256':source_sha,'original_pattern_sha256':hashlib.sha256(data).hexdigest(),'baseline':a.baseline,'output':result,'android_tested':False,'scope':'Actual signed game DLLs, original quest builder, FieldFloor key tracking and InGameManager.StepFuncInitFloorCreate; Unity native I/O stubbed only in temporary test copy.'},indent=2)+'\n')
 print(result,flush=True)
if __name__=='__main__':main()
