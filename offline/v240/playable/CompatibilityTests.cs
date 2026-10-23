using System;using System.Reflection;
class CompatibilityTests240 {
 static void Check(bool ok,string text){if(!ok)throw new Exception(text);Console.WriteLine("PASS "+text);}
 static object Call(object obj,string name,params object[] args){return obj.GetType().GetMethod(name,BindingFlags.NonPublic|BindingFlags.Instance).Invoke(obj,args);}
 static void Main(){
  DivineGateOffline.QueueBoot240(new ResidentManager());
  Check(Singleton<ServerForgery>.Instance==null,"Bootstrap waits for required managers");
  Singleton<TimeManager>.Instance=new TimeManager();Singleton<MasterDataManager>.Instance=new MasterDataManager();Singleton<LocalSaveManager>.Instance=new LocalSaveManager();
  var server=new ServerForgery();Singleton<ServerForgery>.Instance=server;Call(server,"Start");
  var master=Singleton<MasterDataManager>.Instance;
  Check(master.m_MasterDataSkillBoost!=null && master.m_MasterDataSkillBoost.Length==0,"Absent Boost table is an empty collection");
  Check(master.m_MasterDataQuestRequirement!=null && master.m_MasterDataQuestRequirement.Length==0,"Absent quest-requirement table is an empty collection");
  var page=new MainMenuSeqQuestSelectFriend();int quests=0;
  foreach(var q in server.m_MasterDataQuest)if(q.fix_id!=0){
   if(q.quest_requirement_id!=0 || !(bool)Call(page,"ChkQuestRequirement",q.fix_id))throw new Exception("Legacy quest unexpectedly restricted "+q.fix_id);
   quests++;
  }
  Console.WriteLine("PASS original quest admission accepts "+quests+" legacy rows without fabricated requirements");
  Singleton<BattleManager>.Instance=new BattleManager();var boost=new SkillBoost();int skills=0;
  foreach(var s in server.m_MasterDataSkillActive)if(s.fix_id!=0){
   CheckSkill(s);
   var req=new BattleSkillReq{m_SkillParamFieldNum=-1,m_SkillParamSkillID=s.fix_id};
   if(boost.CheckBoostSkill(req))throw new Exception("Unrequested Boost added "+s.fix_id);
   skills++;
  }
  Console.WriteLine("PASS original Boost predicate leaves "+skills+" legacy active skills unchanged");
  Singleton<TimeEventManager>.Instance=new TimeEventManager();
  var tabs=UnityUtil.GetActiveGachaMaster();
  Check(tabs.Length==4 && tabs[0]!=null && tabs[0].fix_id==100100 && tabs[1]!=null && tabs[1].fix_id==200100 && tabs[2]!=null && tabs[2].fix_id==300400 && tabs[3]==null,"Original pool selector resolves friendship, rare and collaboration tabs");
  foreach(var p in tabs)if(p!=null)Check(!string.IsNullOrEmpty(p.tab_name),"Visible pool "+p.fix_id+" has a tab label");
  var store=new StoreManager();Singleton<StoreManager>.Instance=store;DivineGateOffline.DisableStore(store);
  Check(store.m_UserStoreLock && !store.m_StoreInitOK,"Billing remains locked without obsolete restore field");
  var loader=new TitleMenuSeqLoadStoreProduct();
  var trigger=loader.GetType().GetField("m_WorkStepTriger",BindingFlags.NonPublic|BindingFlags.Instance);
  Call(loader,"ExecStep_000_None");Call(loader,"ExecStep_010_ProductList");
  trigger.SetValue(loader,true);Call(loader,"ExecStep_020_Restore");
  trigger.SetValue(loader,false);Call(loader,"ExecStep_020_Restore");
  Check(loader.GetType().GetField("m_WorkStep",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(loader).ToString()=="STEP_MAX","Original title product loader finishes without waiting for billing");
 }
 static void CheckSkill(MasterDataSkillActive s){if(s.skill_boost_id!=0 || s.skill_critical_odds!=0)throw new Exception("Invented modern skill parameters "+s.fix_id);}
}
