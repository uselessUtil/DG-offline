"""Test assemblies re-extracted from the actual candidate APK, not build-only DLLs."""
import argparse
import hashlib
import json
import os
import shutil
import subprocess
import tempfile
import zipfile
from pathlib import Path
ROOT=Path('/workspace');CODE=ROOT/'offline/v210/playable'
CECIL='/usr/lib/mono/gac/Mono.Cecil/0.11.1.0__0738eb9f132ed756/Mono.Cecil.dll'
def run(*args,**kw):return subprocess.check_output(list(map(str,args)),text=True,stderr=subprocess.STDOUT,timeout=180,**kw)
def main():
 p=argparse.ArgumentParser();p.add_argument('report',type=Path);args=p.parse_args()
 report=json.loads(args.report.read_text());apk=Path(report['output']);assert hashlib.sha256(apk.read_bytes()).hexdigest()==report['sha256']
 build=Path(report['work_directory']);original=build/'original/assets/bin/Data/Managed'
 work=Path(tempfile.mkdtemp(prefix='dg210-playable-test-',dir='/tmp'));tools=work/'tools';tools.mkdir()
 with zipfile.ZipFile(apk) as z:
  for n in z.namelist():
   if n.startswith('assets/bin/Data/Managed/') and n.endswith('.dll'):(work/Path(n).name).write_bytes(z.read(n))
 tests=ROOT/'offline/tests';results=[]
 def capture(name,*command,**kw):
  try:out=run(*command,**kw)
  except subprocess.CalledProcessError as ex:
   (args.report.parent/(name+'.txt')).write_text(ex.output);print(ex.output);raise
  (args.report.parent/(name+'.txt')).write_text(out);results.append({'name':name,'output':out});print(out,flush=True)
 run('mcs','-r:'+CECIL,'-out:'+str(tools/'VerifyReferences.exe'),tests/'VerifyReferences.cs')
 capture('references','mono',tools/'VerifyReferences.exe',original,work)
 run('mcs','-r:'+CECIL,'-out:'+str(tools/'VerifyChanges.exe'),CODE/'VerifyChanges.cs')
 capture('original-methods','mono',tools/'VerifyChanges.exe',original/'Assembly-CSharp.dll',work/'Assembly-CSharp.dll',args.report.parent/'modified-methods.txt')
 # Read native packages from the actual signed APK and compare original CAB bytes.
 import UnityPy
 from UnityPy.files.SerializedFile import SerializedFile
 old_version=SerializedFile.set_version
 SerializedFile.set_version=lambda self,v:old_version(self,v.splitlines()[0])
 native=json.loads((args.report.parent/'native-bundle-manifest.json').read_text())
 try:
  with zipfile.ZipFile(apk) as z:
   for row in native['bundles']:
    raw=z.read('assets/dg-native/'+row['file'])
    assert hashlib.sha256(raw).hexdigest()==row['sha256']
    bundle=next(iter(UnityPy.load(raw).files.values()))
    cab=bundle.files[row['id']].reader.bytes
    assert hashlib.sha256(cab).hexdigest()==row['cab_sha256']
 finally:SerializedFile.set_version=old_version
 native_result='PASS '+str(len(native['bundles']))+' actual APK native packages retain original CAB bytes, matched target script hashes; Android load pending.\n'
 (args.report.parent/'native-packages.txt').write_text(native_result)
 results.append({'name':'native-packages','output':native_result});print(native_result)

 images=json.loads((args.report.parent/'images.json').read_text())
 (work/'expected-images.tsv').write_text(''.join(k+'\t'+v['sha256']+'\n' for k,v in images['payloads'].items()))
 (work/'expected-sprites.tsv').write_text(''.join(k.split('/',1)[0]+'\t'+k.split('/',1)[1]+'\t'+','.join(map(str,v))+'\n' for k,v in images['sprites'].items()))
 run('mcs','-out:'+str(tools/'VerifyImages.exe'),tests/'VerifyRecoveredImages.cs')
 capture('images','mono',tools/'VerifyImages.exe',work/'DivineGateOffline.dll',work/'expected-images.tsv',work/'expected-sprites.tsv')
 run('mcs','-target:library','-out:'+str(work/'EngineSupport.dll'),'-r:'+str(original/'UnityEngine.dll'),tests/'EngineSupport.cs')
 run('mcs','-r:'+CECIL,'-out:'+str(tools/'TestEngine.exe'),tests/'TestEngine.cs')
 run('mono',tools/'TestEngine.exe',original/'UnityEngine.dll',work/'EngineSupport.dll',work/'UnityEngine.dll')
 run('mcs','-out:'+str(work/'PlayableTests.exe'),'-r:'+str(work/'Assembly-CSharp.dll'),'-r:'+str(work/'Assembly-CSharp-firstpass.dll'),'-r:'+str(work/'UnityEngine.dll'),CODE/'PlayableTests.cs')
 env=dict(os.environ,DG_TEST_TABLES=str(build/'tables'),DG_TEST_PREFS=str(work/'prefs.txt'))
 capture('local-api','mono',work/'PlayableTests.exe',env=env,cwd=work)
 capture('save-restart','mono',work/'PlayableTests.exe','reload',env=env,cwd=work)
 run('mcs','-out:'+str(work/'SkillTests.exe'),'-r:'+str(work/'Assembly-CSharp.dll'),'-r:'+str(work/'Assembly-CSharp-firstpass.dll'),'-r:'+str(work/'UnityEngine.dll'),'-r:'+str(work/'DivineGateOffline.dll'),CODE/'SkillTests.cs')
 capture('skills','mono',work/'SkillTests.exe',env=env,cwd=work)
 # Exercise the actual candidate's new table grouping, API paths and 2.1.0 shop gate.
 run('mcs','-out:'+str(work/'CommerceTests.exe'),'-r:'+str(work/'Assembly-CSharp.dll'),'-r:'+str(work/'Assembly-CSharp-firstpass.dll'),'-r:'+str(work/'UnityEngine.dll'),'-r:'+str(work/'DivineGateOffline.dll'),CODE/'CommerceTests.cs')
 commerce_env=dict(env,DG_TEST_PREFS=str(work/'commerce-prefs.txt'))
 capture('commerce','mono',work/'CommerceTests.exe',env=commerce_env,cwd=work)
 capture('commerce-restart','mono',work/'CommerceTests.exe','reload',env=commerce_env,cwd=work)
 for name,source in [('free-chip-shop','VerifyFreeChipShop')]:
  run('mcs','-out:'+str(work/(source+'.exe')),'-r:'+str(work/'Assembly-CSharp.dll'),'-r:'+str(work/'Assembly-CSharp-firstpass.dll'),'-r:'+str(work/'UnityEngine.dll'),'-r:'+str(work/'DivineGateOffline.dll'),tests/(source+'.cs'))
  case_env=dict(env,DG_TEST_PREFS=str(work/(name+'-prefs.txt')))
  capture(name,'mono',work/(source+'.exe'),env=case_env,cwd=work)
  capture(name+'-restart','mono',work/(source+'.exe'),'reload',env=case_env,cwd=work)
 import sys
 sys.path.insert(0,str(tests))
 from verify_shop_prefab import verify
 prefab_result=verify(apk,args.report.parent)
 (args.report.parent/'shop-prefab.txt').write_text(prefab_result)
 results.append({'name':'shop-prefab','output':prefab_result})
 # Verify original game code outside the explicit changes is preserved by Cecil.
 assert hashlib.sha256(apk.read_bytes()).hexdigest()==report['sha256']
 result={'apk_sha256':report['sha256'],'work':str(work),'tests':results,'android_tested':False,'scope':'Original managed API, core table consumption, per-quest generation, file-backed PlayerPrefs process restart, embedded image hashes; Unity engine is stubbed only in temporary tests.'}
 (args.report.parent/'test-report.json').write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n')
 report['status']='Managed gameplay flow and asset verification passed; Android gameplay pending'
 report['managed_tests_passed']=True
 args.report.write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n')
if __name__=='__main__':main()
