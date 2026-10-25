using System;using System.IO;using System.Reflection;
class RejectNewBackup {
 static void Main(string[] args) {
  Singleton<TimeManager>.Instance=new TimeManager();Singleton<MasterDataManager>.Instance=new MasterDataManager();Singleton<LocalSaveManager>.Instance=new LocalSaveManager();
  var server=new ServerForgery();Singleton<ServerForgery>.Instance=server;
  typeof(ServerForgery).GetMethod("Start",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(server,null);
  bool rejected=false;try{DivineGateOffline.ValidateSaveBackup(server,File.ReadAllText(args[0]));}catch{rejected=true;}
  if(!rejected)throw new Exception("Old release accepted DGDB backup.");
  Console.WriteLine("PASS actual 2.9.0 r17 rejects new 2.9.1 schema2 backup.");
 }
}
