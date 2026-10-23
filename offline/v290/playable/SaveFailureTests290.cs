using System;using System.Reflection;using LitJson;using ServerDataDefine;
class SaveFailureTests290 {
 static void Main() {
  Singleton<TimeManager>.Instance=new TimeManager();Singleton<MasterDataManager>.Instance=new MasterDataManager();Singleton<LocalSaveManager>.Instance=new LocalSaveManager();
  var server=new ServerForgery();Singleton<ServerForgery>.Instance=server;
  typeof(ServerForgery).GetMethod("Start",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(server,null);
  server.UserParamCreate(new SendCreateUser{uuid="save-failure290",terminal=new PacketStructTerminal{platform=1}});
  server.UserParamSave();
  var save=DivineGateOffline.Local290Player(server);
  string before=JsonMapper.ToJson(save),disk=LocalSaveUtil.GetValueString("SERVER_SAVE","");
  var flag=typeof(ServerForgery).Assembly.GetType("SaveFailureControl290").GetField("Fail");flag.SetValue(null,true);
  bool caught=false;try{DivineGateOffline.ClaimFreeChips("jp.gungho.dg.chip25");}catch(Exception){caught=true;}
  if(!caught || before!=JsonMapper.ToJson(save) || disk!=LocalSaveUtil.GetValueString("SERVER_SAVE",""))throw new Exception("Failed save modified chip balance or stored account");
  flag.SetValue(null,false);
  if(DivineGateOffline.ClaimFreeChips("jp.gungho.dg.chip25")!=25)throw new Exception("Retry after failure wrong");
  Console.WriteLine("PASS injected save failure rolls back full in-memory account and leaves disk unchanged; retry grants exactly one pack. Fault injection only in disposable test DLL.");
 }
}
