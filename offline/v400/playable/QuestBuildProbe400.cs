using System;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using System.Collections.Generic;
using System.Linq;
using LitJson;
class QuestBuildProbe400 {
 static void Main(string[] args) {
  var server=(ServerForgery)FormatterServices.GetUninitializedObject(typeof(ServerForgery));
  Singleton<ServerForgery>.Instance=server;
  Singleton<ServerForgery>.HasInstance=true;
  var master=(MasterDataManager)FormatterServices.GetUninitializedObject(typeof(MasterDataManager));
  Singleton<MasterDataManager>.Instance=master;
  Singleton<MasterDataManager>.HasInstance=true;
  if(args.Length<2 || args[1]!="tables") {
   var embedded=Assembly.Load("DivineGateOffline").GetType("DG400Data");
   embedded.GetMethod("Apply").Invoke(null,new object[]{server});
   embedded.GetMethod("Share").Invoke(null,new object[]{server,master});
  } else {
  var decode=typeof(JsonMapper).GetMethods().SingleGenericJson();
  foreach(var f in typeof(ServerForgery).GetFields()) {
   if(!f.FieldType.IsArray || !f.FieldType.GetElementType().Name.StartsWith("MasterData"))continue;
   var name=f.FieldType.GetElementType().Name;
   if(name=="MasterDataParamEnemy")name="MasterDataEnemyChara";
   if(name=="MasterDataEvent")name="MasterDataEventBase";
   var path=Path.Combine(args[0],name+".json");
   var data=decode.MakeGenericMethod(f.FieldType).Invoke(null,new object[]{File.Exists(path)?File.ReadAllText(path):"[]"});
   f.SetValue(server,data);
   var mf=typeof(MasterDataManager).GetField(f.Name);
   if(mf!=null && mf.FieldType==f.FieldType)mf.SetValue(master,data);
   var mp=typeof(MasterDataManager).GetProperty(f.Name);
   if(mp!=null && mp.PropertyType==f.FieldType)mp.SetValue(master,data,null);
  }
  }
  var build=typeof(ServerForgery).GetMethod("CreateQuestBuild",BindingFlags.Static|BindingFlags.Public);
  var lookup=typeof(ServerForgery).GetMethod("ServerFogery_GetEnemyGroupFromID",BindingFlags.NonPublic|BindingFlags.Static);
  foreach(var group in server.m_MasterDataParamEnemyGroup.Where(g=>g.fix_id!=0))
   if(!Object.ReferenceEquals(lookup.Invoke(null,new object[]{group.fix_id}),group))throw new Exception("Wrong enemy group "+group.fix_id);
  if(lookup.Invoke(null,new object[]{0u})!=null || lookup.Invoke(null,new object[]{uint.MaxValue})!=null)throw new Exception("Unknown group must not select row zero");
  int count=0;var failures=new List<string>();
  foreach(var quest in server.m_MasterDataQuest.Where(q=>q.fix_id!=0 && q.active==2)) {
   uint id=quest.fix_id;
   try {
    var result=(ServerDataDefine.PacketStructQuestBuild)build.Invoke(null,new object[]{id});
    if(result==null)throw new Exception("NULL quest build");
    if(new uint[]{2,4,6}.Contains(id)) {
     uint expected=id==2?27u:id==4?30u:33u;
     if(!result.list_battle.Any(b=>b.enemy_list!=null && b.enemy_list.Contains(expected)))throw new Exception("Wrong tutorial boss");
    }
    if(result.list_battle.Any(b=>b.enemy_list!=null && b.enemy_list.Any(e=>e!=0 && !server.m_MasterDataParamEnemy.Any(p=>p.fix_id==e))))throw new Exception("Missing battle enemy");
    count++;
   } catch(Exception e){failures.Add("Quest "+id+": "+(e.InnerException??e));}
  }
  foreach(var failure in failures)Console.WriteLine(failure);
  if(failures.Count!=0)throw new Exception("Quest build failures: "+failures.Count);
  Console.WriteLine("PASS: "+count+" active original quests built from embedded data; all enemy-group lookups matched IDs; evolution tutorials 2/4/6 contain original bosses.");
  Console.WriteLine("LIMIT: disposable Mono probe with engine I/O stubs, not Android UI or gameplay verification.");
 }
}
static class ProbeReflection {
 public static MethodInfo SingleGenericJson(this MethodInfo[] methods) {
  foreach(var m in methods)if(m.Name=="ToObject" && m.IsGenericMethod && m.GetParameters().Length==1 && m.GetParameters()[0].ParameterType==typeof(string))return m;
  throw new Exception("Missing JsonMapper generic parser");
 }
}
