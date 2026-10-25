"""Versioned roundtrip/recovery tests based on git c600717, using delivered DLLs."""
import json,os,subprocess,zipfile,hashlib,shutil
from pathlib import Path
from build import ROOT
from manifest import chunks,strings_from,attributes,U32
def run(*a,**kw):
 try:return subprocess.check_output(list(map(str,a)),text=True,stderr=subprocess.STDOUT,timeout=180,**kw)
 except subprocess.CalledProcessError as e:print(e.output,flush=True);raise

def verify(test,tools,env,output):
 report=json.loads((output/'build-report.json').read_text())
 refs=['-r:'+str(test/n) for n in ['Assembly-CSharp.dll','DivineGateOffline.dll','Assembly-CSharp-firstpass.dll','UnityEngine.dll']]
 source=(ROOT/'offline/tests/VerifySaveBackup.cs').read_text()
 source=source.replace('Singleton<MasterDataManager>.Instance=new MasterDataManager();','Singleton<TimeManager>.Instance=new TimeManager();Singleton<MasterDataManager>.Instance=new MasterDataManager();')
 source=source.replace('for(int i=0;i<Save.unit_party_assign.Length;i++)','Save.unit_party_assign=new PacketStructPartyAssign[5];\n        for(int i=0;i<Save.unit_party_assign.Length;i++)')
 source=source.replace('string a=DivineGateOffline.ExportSaveBackup(server);', '''LocalSaveUtil.SetValueInt("divinegate_TUTORIAL_SKIP",1);
        var filter=new LocalSaveFilter{m_FilterElements=new[]{1,0,1,0,1,0,1,0},m_FilterPriority=2};
        string expectedFilter=JsonMapper.ToJson(filter);
        LocalSaveUtil.SetValueString("divinegate_FILTER_UNIT_LIST",expectedFilter);
        string a=DivineGateOffline.ExportSaveBackup(server);''')
 source=source.replace('string b=DivineGateOffline.ExportSaveBackup(server)', '''LocalSaveUtil.SetValueInt("divinegate_TUTORIAL_SKIP",0);
        string b=DivineGateOffline.ExportSaveBackup(server)''')
 source=source.replace('Check(Account()==expected,"Roundtrip changed', '''Check(LocalSaveUtil.GetValueInt("divinegate_TUTORIAL_SKIP",-1)==1 &&
            LocalSaveUtil.GetValueString("divinegate_FILTER_UNIT_LIST","")==expectedFilter,"2.9 tutorial/filter preferences lost");
        Check(Account()==expected,"Roundtrip changed''')

 source=source.replace('Save.quest_id=86;Rejected(a,directory', 'Save.evolve_quest_active=true;Rejected(a,directory,"current evolution quest");Save.evolve_quest_active=false;\n        Save.quest_id=86;Rejected(a,directory')
 source=source.replace('File.WriteAllText(Path.Combine(directory,"expected-account.json"),Account());',
                       'File.WriteAllText(Path.Combine(directory,"expected-filter.json"),LocalSaveUtil.GetValueString("divinegate_FILTER_UNIT_LIST",""));\n        File.WriteAllText(Path.Combine(directory,"expected-account.json"),Account());')
 source=source.replace('Check(Account()==File.ReadAllText', '''Check(LocalSaveUtil.GetValueInt("divinegate_TUTORIAL_SKIP",-1)==0 &&
                LocalSaveUtil.GetValueString("divinegate_FILTER_UNIT_LIST","")==File.ReadAllText(Path.Combine(directory,"expected-filter.json")),"Crash recovery lost 2.9 settings");
            Check(Account()==File.ReadAllText''')
 source=source.replace('LocalSaveUtil.SetValueString("SERVER_SAVE",payload.server_save);',
                       'LocalSaveUtil.SetValueInt("divinegate_TUTORIAL_SKIP",42);LocalSaveUtil.SetValueString("divinegate_FILTER_UNIT_LIST","partial");\n        LocalSaveUtil.SetValueString("SERVER_SAVE",payload.server_save);')
 (test/'VerifySaveBackup290.cs').write_text(source)
 run('mcs','-out:'+str(test/'VerifySaveBackup290.exe'),*refs,test/'VerifySaveBackup290.cs')
 directory=test/'save-backups290';backup_env=dict(env,DG_TEST_PREFS=str(test/'backup290.prefs'),DG_TEST_SAVE_DIR=str(directory))
 log=''
 for mode in [[],['reload']]:log+=run('mono',test/'VerifySaveBackup290.exe',*mode,env=backup_env,cwd=test)
 log+='PASS 2.9.0 version restriction, tutorial-skip flag and JSON filter preference roundtrip/recovery.\n'
 native=test/'native-save290';native.mkdir()
 actual=Path(report['work_directory'])/'build/save-android'
 java=(ROOT/'offline/tests/SavePreviewTest.java').read_text().replace('jp.gungho.dg.offline106','jp.gungho.dg.offline290')
 (native/'SavePreviewTest.java').write_text(java)
 libraries='/opt/dg-save-tools/android.jar:/opt/dg-save-tools/json.jar'
 run('javac','--release','8','-cp',libraries,'-d',native,actual/'SaveTransferActivity.java',native/'SavePreviewTest.java')
 for f in (actual/'classes').rglob('*.class'):
  assert f.read_bytes()==(native/f.relative_to(actual/'classes')).read_bytes()
 log+=run('java','-cp',str(native)+':'+libraries,'SavePreviewTest',directory/'sample.dgbackup.json')
 with zipfile.ZipFile(report['output']) as z:
  manifest=list(chunks(z.read('AndroidManifest.xml')));pool=strings_from(next(c for k,c in manifest if k==1));found=0
  for kind,c in manifest:
   if kind!=0x102 or pool[U32.unpack_from(c,20)[0]]!='activity':continue
   attrs=list(attributes(c,pool))
   if not any(k=='name' and v=='jp.gungho.dg.offline290.SaveTransferActivity' for k,v,p in attrs):continue
   assert [U32.unpack_from(c,p+16)[0] for k,v,p in attrs if k=='exported']==[0];found+=1
  assert found==1
  dex=test/'save290.dex';dex.write_bytes(z.read('classes.dex'))
 classes=run('baksmali','list','classes',dex)
 assert 'Ljp/gungho/dg/offline290/SaveTransferActivity;' in classes
 assert 'Ljp/gungho/dg/offline106/SaveTransferActivity;' not in classes
 assert report['save_transfer']['permissions_added']==[]
 log+='PASS version-specific Java preview matches compiled build inputs; final DEX contains the 2.9 bridge only, manifest Activity is non-exported, no permissions added. Android picker lifecycle still requires device testing.\n'
 failure=test/'import-failure';failure.mkdir()
 for f in test.glob('*.dll'):shutil.copy2(f,failure/f.name)
 run('mcs','-r:/usr/lib/mono/gac/Mono.Cecil/0.11.1.0__0738eb9f132ed756/Mono.Cecil.dll','-out:'+str(tools/'PrepareImportFailure290.exe'),ROOT/'offline/v290/playable/PrepareImportFailure290.cs')
 run('mono',tools/'PrepareImportFailure290.exe',test/'Assembly-CSharp.dll',failure/'Assembly-CSharp.dll')
 failure_refs=['-r:'+str(failure/n) for n in ['Assembly-CSharp.dll','DivineGateOffline.dll','Assembly-CSharp-firstpass.dll','UnityEngine.dll']]
 run('mcs','-out:'+str(failure/'ImportFailureTests290.exe'),*failure_refs,ROOT/'offline/v290/playable/ImportFailureTests290.cs')
 log+=run('mono',failure/'ImportFailureTests290.exe',cwd=failure,env=dict(env,DG_TEST_PREFS=str(failure/'prefs'),DG_TEST_SAVE_DIR=str(failure/'backups')))
 from verify_save_prefab290 import verify as verify_prefab
 log+=verify_prefab(report['output'],output)
 run('mcs','-r:/usr/lib/mono/gac/Mono.Cecil/0.11.1.0__0738eb9f132ed756/Mono.Cecil.dll','-out:'+str(tools/'VerifySaveHooks290.exe'),ROOT/'offline/v290/playable/VerifySaveHooks290.cs')
 log+=run('mono',tools/'VerifySaveHooks290.exe',test/'Assembly-CSharp.dll',test/'DivineGateOffline.dll')
 from verify_legacy106 import verify as verify_legacy
 log+=verify_legacy(test,tools,env,output)
 (output/'save-transfer-tests.txt').write_text(log);print(log,flush=True)
 return log
