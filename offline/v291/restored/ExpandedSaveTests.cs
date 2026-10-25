using System;using System.IO;using System.Reflection;using LitJson;using ServerDataDefine;
class ExpandedSaveTests {
 static ServerForgery server;
 static void Check(bool b,string s){if(!b)throw new Exception(s);}
 static ServerForgerySaveUser Save {get{return DivineGateOffline.Local291Player(server);}}
 static void Main(string[] args){
  Singleton<TimeManager>.Instance=new TimeManager();Singleton<MasterDataManager>.Instance=new MasterDataManager();
  Singleton<LocalSaveManager>.Instance=new LocalSaveManager();Singleton<UserDataAdmin>.Instance=new UserDataAdmin();
  server=new ServerForgery();Singleton<ServerForgery>.Instance=server;
  typeof(ServerForgery).GetMethod("Start",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(server,null);
  string dir=args[1];Directory.CreateDirectory(dir);
  if(args.Length>2){
   Check(JsonMapper.ToJson(Save)==File.ReadAllText(dir+"/expected.json"),"Expanded account changed across process");
   Check(Save.unit_list[Save.unit_list.Length-1].id==237,"New character lost");
   Console.WriteLine("PASS separate-process added character, new quest flag and all original assets persist.");return;
  }
  server.UserParamCreate(new SendCreateUser{uuid="expanded-profile-disposable",terminal=new PacketStructTerminal{platform=1}});
  Save.unit_party_assign=new PacketStructPartyAssign[5];for(int i=0;i<5;i++)Save.unit_party_assign[i]=new PacketStructPartyAssign();
  string incoming=File.ReadAllText(args[0]);var envelope=JsonMapper.ToObject<DGSaveEnvelope>(incoming);
  Check(envelope.schema==2 && envelope.data_version=="dgdb-1","Source is not the actual r10 schema2 export");
  var old=JsonMapper.ToObject<DGSavePayload>(envelope.payload);
  DivineGateOffline.ImportSaveBackup(server,incoming,dir);
  Check(JsonMapper.ToJson(Save)==JsonMapper.ToJson(JsonMapper.ToObject<ServerForgerySaveUser>(old.server_save)),"r10 import changed account fields");
  var persistent=(string[])typeof(DivineGateOffline).GetField("saveStringKeys",BindingFlags.NonPublic|BindingFlags.Static).GetValue(null);
  foreach(var pref in old.preferences)if(pref.kind==1 && pref.present && Array.IndexOf(persistent,pref.key)>=0)
   Check(LocalSaveUtil.GetValueString(pref.key,"")==pref.text,"r10 string preference lost "+pref.key);
  Check(Save.unit_list.Length==595,"Source fixture missing original inventory");
  typeof(ServerForgery).GetMethod("AddUnit",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(server,new object[]{237u,1u,2u,3u});
  var added=Save.unit_list[Save.unit_list.Length-1];Check(added.id==237 && added.add_pow==2 && added.add_hp==3,"Expanded award failed");
  ServerDataUtil.AddBitFlag(ref Save.flag_quest_clear,529u);ServerDataUtil.AddBitFlag(ref Save.flag_quest_check,529u);
  server.UserParamSave();
  string expected=JsonMapper.ToJson(Save);string expanded=DivineGateOffline.ExportSaveBackup(server);
  var e=JsonMapper.ToObject<DGSaveEnvelope>(expanded);Check(e.schema==3 && e.data_version=="dgdb-2-restored","Expanded export identity");
  File.WriteAllText(dir+"/expanded.dgbackup.json",expanded);
  DivineGateOffline.ImportSaveBackup(server,incoming,dir);
  DivineGateOffline.ImportSaveBackup(server,expanded,dir);
  Check(JsonMapper.ToJson(Save)==expected,"Expanded backup roundtrip changed account");
  File.WriteAllText(dir+"/expected.json",expected);
  Console.WriteLine("PASS actual r10 schema2 export imported with exact account preservation; new character237 and quest529 persist through native save and schema3 roundtrip.");
 }
}
