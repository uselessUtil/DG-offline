using System;using System.IO;using System.Reflection;using LitJson;using ServerDataDefine;
class ExportPriorSizeFixture {
 static void Main(string[] a) {
  Singleton<TimeManager>.Instance=new TimeManager();Singleton<MasterDataManager>.Instance=new MasterDataManager();
  Singleton<LocalSaveManager>.Instance=new LocalSaveManager();Singleton<UserDataAdmin>.Instance=new UserDataAdmin();
  var server=new ServerForgery();Singleton<ServerForgery>.Instance=server;
  typeof(ServerForgery).GetMethod("Start",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(server,null);
  server.UserParamCreate(new SendCreateUser{uuid="size-transfer-disposable",terminal=new PacketStructTerminal{platform=1}});
  var disposable=DivineGateOffline.Local291Player(server);
  disposable.unit_party_assign=new PacketStructPartyAssign[5];
  for(int i=0;i<5;i++)disposable.unit_party_assign[i]=new PacketStructPartyAssign();
  string source=File.ReadAllText(a[0]);var envelope=JsonMapper.ToObject<DGSaveEnvelope>(source);
  if(envelope.schema!=3 || envelope.data_version!="dgdb-2-restored")throw new Exception("Not current disposable fixture");
  var input=JsonMapper.ToObject<DGSavePayload>(envelope.payload);
  string expected=JsonMapper.ToJson(JsonMapper.ToObject<ServerForgerySaveUser>(input.server_save));
  DivineGateOffline.ImportSaveBackup(server,source,Path.GetDirectoryName(a[1]));
  if(JsonMapper.ToJson(DivineGateOffline.Local291Player(server))!=expected)throw new Exception("Actual r17 import changed account");
  string exported=DivineGateOffline.ExportSaveBackup(server);
  var output=JsonMapper.ToObject<DGSavePayload>(JsonMapper.ToObject<DGSaveEnvelope>(exported).payload);
  if(JsonMapper.ToJson(JsonMapper.ToObject<ServerForgerySaveUser>(output.server_save))!=expected)throw new Exception("Actual r17 export changed account");
  File.WriteAllText(a[1],exported);
  Console.WriteLine("PASS current disposable schema3 fixture -> actual r17 import/re-export retains exact player assets.");
 }
}
