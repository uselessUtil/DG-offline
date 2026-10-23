using System;using System.IO;using System.Reflection;using LitJson;using ServerDataDefine;
class LegacySave290 {
 static void Check(bool b,string m){if(!b)throw new Exception(m);}
 static void Main(string[] a){
  Singleton<TimeManager>.Instance=new TimeManager();Singleton<MasterDataManager>.Instance=new MasterDataManager();Singleton<LocalSaveManager>.Instance=new LocalSaveManager();
  var s=new ServerForgery();Singleton<ServerForgery>.Instance=s;
  typeof(ServerForgery).GetMethod("Start",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(s,null);
  var save=(ServerForgerySaveUser)typeof(ServerForgery).GetField("m_ForgeryPlayerSave",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(s);
  if(a[0]=="create"){
   s.UserParamCreate(new SendCreateUser{uuid="r9-ongoing-save",terminal=new PacketStructTerminal{platform=1,name="Docker"}});
   save=(ServerForgerySaveUser)typeof(ServerForgery).GetField("m_ForgeryPlayerSave",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(s);
   foreach(var q in s.m_MasterDataQuest)if(q.fix_id>5 && q.active==2){
    bool exists=false;foreach(var f in s.m_MasterDataQuestFloor)if(f.quest_id==q.fix_id && f.active==2)exists=true;
    if(!exists)continue;save.quest_id=q.fix_id;save.quest_build=ServerForgery.CreateQuestBuild(q.fix_id);break;
   }
   Check(save.quest_build!=null,"No r9 quest");s.UserParamSave();
   File.WriteAllText(a[1]+".save",LocalSaveUtil.GetValueString("SERVER_SAVE",""));
   File.WriteAllText(a[1]+".quest",JsonMapper.ToJson(save.quest_build));
   Console.WriteLine("PASS old r9 APK generated and saved ongoing quest fixture");
  }else{
   Check(File.ReadAllText(a[1]+".save")==LocalSaveUtil.GetValueString("SERVER_SAVE",""),"r10 changed r9 player save");
   Check(File.ReadAllText(a[1]+".quest")==JsonMapper.ToJson(save.quest_build),"r10 changed r9 ongoing quest");
   Check(new FieldManager().CreateFieldArea(save.quest_build),"Cannot restore old floor");
   Check(File.ReadAllText(a[1]+".quest")==JsonMapper.ToJson(save.quest_build),"Reconstruction rerolled old floor");
   Console.WriteLine("PASS r9 -> r10 cross-process existing SERVER_SAVE and ongoing quest byte-for-byte unchanged; field reconstruction succeeds without reroll");
  }
 }
}
