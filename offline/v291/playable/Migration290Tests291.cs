using System;using System.IO;using System.Reflection;using LitJson;using ServerDataDefine;
class Migration290Tests291 {
 static void Check(bool b,string s){if(!b)throw new Exception(s);}
 static ServerForgery server;
 static void Main(string[] args) {
  Singleton<TimeManager>.Instance=new TimeManager();Singleton<MasterDataManager>.Instance=new MasterDataManager();
  Singleton<LocalSaveManager>.Instance=new LocalSaveManager();Singleton<UserDataAdmin>.Instance=new UserDataAdmin();
  server=new ServerForgery();Singleton<ServerForgery>.Instance=server;
  typeof(ServerForgery).GetMethod("Start",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(server,null);
  string directory=args[1];Directory.CreateDirectory(directory);
  if(args.Length>2) {
   var save=DivineGateOffline.Local291Player(server);
   foreach(var u in save.unit_list)if(u.id==148)Check(u.limitbreak_lv==5,"Capped skill changed after process reload");
   Check(JsonMapper.ToJson(save)==File.ReadAllText(Path.Combine(directory,"expected.json")),"Migrated account changed on reload");
   Check(LocalSaveUtil.GetValueString("DG291_MIGRATION_REPORT","").Contains("2.9.0"),"Migration report not retained");
   Console.WriteLine("PASS 2.9.0 migration cross-process account and skill cap persistence.");return;
  }
  server.UserParamCreate(new SendCreateUser{uuid="migration291-before",terminal=new PacketStructTerminal{platform=1}});
  var before=DivineGateOffline.Local291Player(server);before.unit_party_assign=new PacketStructPartyAssign[5];
  for(int i=0;i<5;i++)before.unit_party_assign[i]=new PacketStructPartyAssign();
  string text=File.ReadAllText(args[0]);
  DivineGateOffline.ImportSaveBackup(server,text,directory);
  string first=JsonMapper.ToJson(DivineGateOffline.Local291Player(server));
  string exported=DivineGateOffline.ExportSaveBackup(server);
  var envelope=JsonMapper.ToObject<DGSaveEnvelope>(exported);
  Check(envelope.game_version=="2.9.1" && envelope.schema==2 && envelope.data_version=="dgdb-1","New backup identity");
  File.WriteAllText(Path.Combine(directory,"migrated.json"),exported);
  File.WriteAllText(Path.Combine(directory,"expected.json"),first);
  DivineGateOffline.ImportSaveBackup(server,text,directory);
  Check(first==JsonMapper.ToJson(DivineGateOffline.Local291Player(server)),"Repeated migration changed account");
  bool rejected=false;envelope.data_version="unknown";try{DivineGateOffline.ValidateSaveBackup(server,JsonMapper.ToJson(envelope));}catch{rejected=true;}
  Check(rejected,"Unknown data revision accepted");
  Console.WriteLine("PASS real r17 2.9.0 export -> native 2.9.1 migration, repeated import stable, schema2/data revision enforced.");
 }
}
