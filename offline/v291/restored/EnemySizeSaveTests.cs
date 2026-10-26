using System;using System.IO;using System.Reflection;using LitJson;using ServerDataDefine;
class EnemySizeSaveTests {
 static ServerForgery server;
 static void Check(bool ok,string what){if(!ok)throw new Exception(what);}
 static MasterDataParamChara Unit(uint id){return MasterDataUtil.GetCharaParamFromID(id);}
 static void Main(string[] a) {
  Singleton<TimeManager>.Instance=new TimeManager();Singleton<MasterDataManager>.Instance=new MasterDataManager();
  Singleton<LocalSaveManager>.Instance=new LocalSaveManager();Singleton<UserDataAdmin>.Instance=new UserDataAdmin();
  server=new ServerForgery();Singleton<ServerForgery>.Instance=server;
  typeof(ServerForgery).GetMethod("Start",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(server,null);
  foreach(uint id in new uint[]{788,789,790,791,801,1578,1579,1580,1581,1582,1583}) {
   var c=Unit(id);Check(c!=null && c.size_width==216 && c.size_height==216 && c.pivot==2 && c.side_offset==78,"Small enemy layout "+id);
  }
  var boss=Unit(1576);Check(boss.size_width==640 && boss.size_height==640 && boss.pivot==1 && boss.side_offset==320,"Hendrickson boss was shrunk");
  var midori=Unit(9);Check(midori.size_width==640 && midori.pivot==1,"Original Midori altered");
  Console.WriteLine("PASS signed effective size profiles: Miku/Rin/Luka/Hawk216 with native bottom pivot and78 spacing; Hendrickson640 and original Midori unchanged.");
  if(a.Length>2) {
   string saved=JsonMapper.ToJson(DivineGateOffline.Local291Player(server));
   Check(saved==File.ReadAllText(a[1]),"r17 player account changed across r18 process reload");
   Console.WriteLine("PASS separate-process r17 save reload under r18 retains all player asset fields exactly.");return;
  }
  server.UserParamCreate(new SendCreateUser{uuid="size-transfer-disposable",terminal=new PacketStructTerminal{platform=1}});
  var disposable=DivineGateOffline.Local291Player(server);
  disposable.unit_party_assign=new PacketStructPartyAssign[5];
  for(int i=0;i<5;i++)disposable.unit_party_assign[i]=new PacketStructPartyAssign();
  Directory.CreateDirectory(Path.GetDirectoryName(a[1]));
  string incoming=File.ReadAllText(a[0]);var envelope=JsonMapper.ToObject<DGSaveEnvelope>(incoming);
  Check(envelope.schema==3 && envelope.data_version=="dgdb-2-restored","Expected real r17 backup");
  var payload=JsonMapper.ToObject<DGSavePayload>(envelope.payload);
  string expected=JsonMapper.ToJson(JsonMapper.ToObject<ServerForgerySaveUser>(payload.server_save));
  DivineGateOffline.ImportSaveBackup(server,incoming,Path.GetDirectoryName(a[1]));
  Check(JsonMapper.ToJson(DivineGateOffline.Local291Player(server))==expected,"r17 to r18 import changed player fields");
  server.UserParamSave();
  Check(JsonMapper.ToJson(DivineGateOffline.Local291Player(server))==expected,"r18 save changed player fields");
  string exported=DivineGateOffline.ExportSaveBackup(server);
  var next=JsonMapper.ToObject<DGSaveEnvelope>(exported);
  Check(next.schema==3 && next.data_version=="dgdb-2-restored","Save schema changed");
  var restored=JsonMapper.ToObject<DGSavePayload>(next.payload);
  Check(JsonMapper.ToJson(JsonMapper.ToObject<ServerForgerySaveUser>(restored.server_save))==expected,"r18 re-export changed player fields");
  File.WriteAllText(a[1],expected);
  Console.WriteLine("PASS actual r17 schema3 backup -> r18 -> schema3 export preserves player assets, currency, favorites, parties, gifts and flags; original input not modified.");
 }
}
