using System;using System.IO;using System.Reflection;using LitJson;using ServerDataDefine;
class ImportFailureTests291 {
 static void Check(bool b,string s){if(!b)throw new Exception(s);}
 static void Main(){
  Singleton<TimeManager>.Instance=new TimeManager();Singleton<MasterDataManager>.Instance=new MasterDataManager();Singleton<LocalSaveManager>.Instance=new LocalSaveManager();
  var server=new ServerForgery();Singleton<ServerForgery>.Instance=server;
  typeof(ServerForgery).GetMethod("Start",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(server,null);
  server.UserParamCreate(new SendCreateUser{uuid="backup-failure",terminal=new PacketStructTerminal{platform=1}});
  var save=DivineGateOffline.Local291Player(server);save.user_name="Original";
  save.unit_party_assign=new PacketStructPartyAssign[5];for(int i=0;i<5;i++)save.unit_party_assign[i]=new PacketStructPartyAssign();
  LocalSaveUtil.SetValueInt("divinegate_TUTORIAL_SKIP",1);LocalSaveUtil.SetValueString("divinegate_SCRATCH","original-scratch");
  string exported=DivineGateOffline.ExportSaveBackup(server);
  string before=JsonMapper.ToJson(DivineGateOffline.Local291Player(server)),disk=LocalSaveUtil.GetValueString("SERVER_SAVE","");
  var envelope=JsonMapper.ToObject<DGSaveEnvelope>(exported);var payload=JsonMapper.ToObject<DGSavePayload>(envelope.payload);
  var incoming=JsonMapper.ToObject<ServerForgerySaveUser>(payload.server_save);incoming.user_name="Imported";incoming.have_money=999;
  payload.server_save=JsonMapper.ToJson(incoming);envelope.payload=JsonMapper.ToJson(payload);envelope.sha256=DivineGateOffline.SaveChecksum(envelope.payload);
  string text=JsonMapper.ToJson(envelope),directory=Environment.GetEnvironmentVariable("DG_TEST_SAVE_DIR");
  typeof(ServerForgery).Assembly.GetType("BackupFault291").GetField("FailLoads").SetValue(null,1);
  bool failed=false;try{DivineGateOffline.ImportSaveBackup(server,text,directory);}catch(Exception){failed=true;}
  Check(failed,"Injected account-load failure not surfaced");
  Check(JsonMapper.ToJson(DivineGateOffline.Local291Player(server))==before &&
        LocalSaveUtil.GetValueString("SERVER_SAVE","")==disk,"Import failure did not restore full account");
  Check(LocalSaveUtil.GetValueInt("divinegate_TUTORIAL_SKIP",0)==1 &&
        LocalSaveUtil.GetValueString("divinegate_SCRATCH","")=="original-scratch","Import failure lost preferences or old transient state");
  Check(!File.Exists(Path.Combine(directory,"restore-journal.json")) &&
        Directory.GetFiles(directory,"before-import-*.json").Length==1,"Rollback journal/recovery snapshot wrong");
  DivineGateOffline.ImportSaveBackup(server,text,directory);
  Check(DivineGateOffline.Local291Player(server).user_name=="Imported" &&
        DivineGateOffline.Local291Player(server).have_money==999,"Retry after rollback failed");
  Console.WriteLine("PASS injected imported-account load failure restores exact SERVER_SAVE, in-memory account, tutorial flag and old transient state; recovery snapshot retained; retry succeeds. Fault DLL exists only in temporary test directory.");
 }
}
