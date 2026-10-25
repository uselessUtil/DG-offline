// Read-only native master-data export in an isolated managed runtime.
using System;using System.IO;using System.Reflection;using LitJson;
class ExportMasterData {
 static void Main(string[] a) {
  Singleton<TimeManager>.Instance=new TimeManager();Singleton<MasterDataManager>.Instance=new MasterDataManager();
  Singleton<LocalSaveManager>.Instance=new LocalSaveManager();var server=new ServerForgery();Singleton<ServerForgery>.Instance=server;
  typeof(ServerForgery).GetMethod("Start",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(server,null);
  Directory.CreateDirectory(a[0]);
  foreach(var f in typeof(ServerForgery).GetFields(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance)) {
   if(!f.Name.StartsWith("m_MasterData") || !f.FieldType.IsArray)continue;
   string type=f.FieldType.GetElementType().Name;var value=f.GetValue(server);
   if(value==null)continue;
   File.WriteAllText(Path.Combine(a[0],type+".json"),JsonMapper.ToJson(value));
  }
  Console.WriteLine("Exported actual signed APK ServerForgery master arrays; no account created.");
 }
}
