using System;using System.IO;using System.Reflection;using System.Collections.Generic;
using LitJson;using ServerDataDefine;using UnityEngine;

class DropLevelTests291 {
 static ServerForgery server;static InGameManager game;static uint serial=1;
 static ServerForgerySaveUser Save{get{return DivineGateOffline.Local291Player(server);}}
 static string FileBase{get{return Environment.GetEnvironmentVariable("DG_TEST_PREFS");}}
 static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
 static T Api<T>(SERVER_API api,object send){
  var p=new PacketData{m_PacketAPI=api,m_PacketUniqueNum=serial++};
  var raw=server.CreateRecvForgery(ref p,JsonMapper.ToJson(send));
  Check((int)JsonMapper.ToObject(raw)["header"]["code"]==(int)API_CODE.API_CODE_SUCCESS,"API "+api+": "+raw);
  return JsonMapper.ToObject<T>(raw);
 }
 static void Init(){
  Singleton<TimeManager>.Instance=new TimeManager();Singleton<MasterDataManager>.Instance=new MasterDataManager();
  Singleton<LocalSaveManager>.Instance=new LocalSaveManager();server=new ServerForgery();Singleton<ServerForgery>.Instance=server;
  typeof(ServerForgery).GetMethod("Start",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(server,null);
  Singleton<SceneGoesParam>.Instance=new SceneGoesParam();Singleton<StatusAilmentsManager>.Instance=null;
  game=new InGameManager();Singleton<InGameManager>.Instance=game;game.m_QuestFloor=1;
 }
 static InGameQuestData Collector(int count){
  var q=new InGameQuestData();Singleton<InGameQuestData>.Instance=q;
  q.m_AcquireUnit=new InGameAcquireUnit[count];q.m_AcquireMoney=new InGameAcquireMoney[count+1];
  for(int i=0;i<count;i++)q.m_AcquireUnit[i]=new InGameAcquireUnit();
  for(int i=0;i<q.m_AcquireMoney.Length;i++)q.m_AcquireMoney[i]=new InGameAcquireMoney();return q;
 }
 static PacketStructQuestBuildDrop Drop(MasterDataParamEnemy e,int plus){
  return new PacketStructQuestBuildDrop{unique_id=1,unit_id=e.drop_unit_id,floor=1,plus_pow=plus};
 }
 static void Kill(MasterDataParamEnemy enemy,PacketStructQuestBuildDrop drop){
  var b=new BattleManager();Singleton<BattleManager>.Instance=b;
  b.m_BattleRequest=new BattleReq{m_QuestBuild=new PacketStructQuestBuild{list_drop=drop==null?new PacketStructQuestBuildDrop[0]:new[]{drop}}};
  b.m_EnemyParam=new[]{new BattleEnemy{m_EnemyHP=0,m_EnemyMasterParamEnemy=enemy,m_EnemyDrop=drop==null?0:drop.unique_id}};
  b.m_EnemyObjectCharaPos=new[]{EngineSupport.CreateScaleTestObject()};EngineSupport.SetActiveState(b.m_EnemyObjectCharaPos[0],true);
  b.m_EnemyObjectHP=new GameObject[1];
  var death=typeof(BattleManager).GetMethod("DeadEnemy",BindingFlags.NonPublic|BindingFlags.Instance);
  death.Invoke(b,null);
  // Original object-disable/drop guard still prevents duplicate collection.
  death.Invoke(b,null);
 }
 static int Level(MasterDataParamEnemy e){
  var u=MasterDataUtil.GetCharaParamFromID(e.drop_unit_id);
  return Math.Max(u.level_min,Math.Min(u.level_max,e.drop_unit_level));
 }
 static PacketStructUnitGet[] Gets(InGameAcquireUnit[] list){
  var result=new List<PacketStructUnitGet>();
  for(int i=0;i<list.Length;i++)if(list[i]!=null && list[i].m_UnitID!=0)
   result.Add(new PacketStructUnitGet{id=list[i].m_UnitID,level=(uint)list[i].m_UnitLevel,
    add_hp=(uint)list[i].m_UnitAddHP,add_pow=(uint)list[i].m_UnitAddPow});
  return result.ToArray();
 }
 static PacketStructUnit Owned(long id){
  foreach(var u in Save.unit_list)if(u.unique_id==id)return u;return null;
 }
 static void CheckGrant(long uid,InGameAcquireUnit a){
  var u=Owned(uid);Check(u!=null,"Missing awarded unit");
  int level=a.m_UnitLevel==0?1:a.m_UnitLevel;
  Check(u.id==a.m_UnitID && u.level==level && u.add_pow==a.m_UnitAddPow && u.add_hp==a.m_UnitAddHP,"Award level/pluses differ");
  Check(u.exp==CharaUtil.GetStatusValue(MasterDataUtil.GetCharaParamFromID(u.id),level,3),"EXP does not match awarded level");
 }
 static void Reload(){
  Check(Save!=null,"Missing isolated account");
  Check(File.ReadAllText(FileBase+".account")==LocalSaveUtil.GetValueString("SERVER_SAVE",""),"Startup rewrote old account");
  Check(File.ReadAllText(FileBase+".quest")==JsonMapper.ToJson(Save.quest_build),"Startup rewrote/rerolled quest");
  var restored=Singleton<LocalSaveManager>.Instance.LoadFuncGoesToGameRestore();
  Check(restored!=null && JsonMapper.ToJson(restored.m_AcquireUnit)==File.ReadAllText(FileBase+".acquired"),"Native restore lost levels");
  var old=new Dictionary<long,string>();foreach(var u in Save.unit_list)old[u.unique_id]=JsonMapper.ToJson(u);
  var clear=Api<RecvQuestClear>(SERVER_API.SERVER_API_QUEST_CLEAR,new SendQuestClear{quest_id=1,get_unit=Gets(restored.m_AcquireUnit)});
  Check(clear.result.get_unit.Length==4 && Save.unit_list.Length==old.Count+4,"Normal clear award count");
  for(int i=0;i<4;i++)CheckGrant(clear.result.get_unit[i],restored.m_AcquireUnit[i]);
  foreach(var p in old)Check(JsonMapper.ToJson(Owned(p.Key))==p.Value,"Existing owned unit changed");
  Console.WriteLine("PASS native restore across process preserves acquired levels/pluses and exact pre-clear SERVER_SAVE/quest; normal clear grants levels and matching EXP. Legacy pre-update level0 remains original Lv1 behavior.");
  // Exercise the unchanged evolution settlement with a valid base/material state.
  var add=typeof(ServerForgery).GetMethod("AddUnit",BindingFlags.NonPublic|BindingFlags.Instance,null,
   new[]{typeof(uint),typeof(uint),typeof(uint),typeof(uint)},null);
  long baseID=(long)add.Invoke(server,new object[]{47u,10u,0u,0u});
  long partID=(long)add.Invoke(server,new object[]{59u,1u,0u,0u});
  MasterDataParamCharaEvol route=null;foreach(var r in server.m_MasterDataParamCharaEvol)if(r.fix_id==38)route=r;
  Check(route!=null && route.unit_id_pre==47,"Evolution fixture route changed");
  var enemy=MasterDataUtil.GetEnemyParamFromID(100750);var q=Collector(1);Kill(enemy,Drop(enemy,1));
  Save.quest_id=route.quest_id;Save.evolve_quest_id=route.quest_id;Save.evolve_quest_active=true;
  Save.evolve_pattern=route.fix_id;Save.evolve_unit_base=baseID;Save.evolve_unit_parts=new[]{partID};
  var evo=Api<RecvEvolQuestClear>(SERVER_API.SERVER_API_EVOL_QUEST_CLEAR,new SendEvolQuestClear{quest_id=route.quest_id,get_unit=Gets(q.m_AcquireUnit)});
  Check(evo.result.get_unit.Length==1,"Evolution drop missing");CheckGrant(evo.result.get_unit[0],q.m_AcquireUnit[0]);
  Check(Owned(baseID).id==route.unit_id_after && Owned(partID)==null,"Native evolution/material consumption changed");
  server.UserParamSave();File.WriteAllText(FileBase+".awarded",JsonMapper.ToJson(Save.unit_list));
  Console.WriteLine("PASS original evolution clear grants drop at DGDB level/EXP, evolves the base and consumes its material normally.");
 }
 static void Main(string[] args){
  Init();
  if(args.Length>0){
   if(args[0]=="reload"){Reload();return;}
   Check(File.ReadAllText(FileBase+".awarded")==JsonMapper.ToJson(Save.unit_list),"Cross-process awarded inventory changed");
   Console.WriteLine("PASS another process retains awarded levels, EXP, plus values and evolution result.");return;
  }
  Api<RecvCreateUser>(SERVER_API.SERVER_API_USER_CREATE,new SendCreateUser{uuid="drop-level-r10-test",terminal=new PacketStructTerminal{platform=1,name="Docker"}});
  Api<RecvSelectDefParty>(SERVER_API.SERVER_API_USER_SELECT_DEF_PARTY,new SendSelectDefParty{select_party=1});
  string before=JsonMapper.ToJson(Save);int cases=0,normalized=0;var levels=new HashSet<int>();
  foreach(var e in server.m_MasterDataParamEnemy){
   if(e.drop_unit_id==0)continue;
   var q=Collector(2);var drop=Drop(e,cases%2);string original=JsonMapper.ToJson(drop);Kill(e,drop);
   var a=q.m_AcquireUnit[0];int expected=Level(e);
   Check(a.m_UnitID==e.drop_unit_id && a.m_UnitLevel==expected && a.m_UnitAddPow==drop.plus_pow && a.m_UnitAddHP==drop.plus_hp,"Death path level/pluses "+e.fix_id);
   Check(q.m_AcquireUnit[1].m_UnitID==0,"Duplicate death granted twice");
   Check(JsonMapper.ToJson(drop)==original,"Native drop packet rewritten");
   if(expected!=e.drop_unit_level)normalized++;levels.Add(expected);cases++;
  }
  Check(cases==2144 && normalized==9 && levels.Count==10,"Incomplete imported level coverage");
  Check(JsonMapper.ToJson(Save)==before,"Collecting drops changed account before settlement");
  var none=Collector(1);Kill(server.m_MasterDataParamEnemy[0],null);Check(none.m_AcquireUnit[0].m_UnitID==0,"No-drop gained unit");
  var two=Collector(2);MasterDataParamEnemy first=null,second=null;
  foreach(var a in server.m_MasterDataParamEnemy)foreach(var b in server.m_MasterDataParamEnemy)
   if(first==null && a.drop_unit_id==b.drop_unit_id && a.drop_unit_rate>0 && b.drop_unit_rate>0 && Level(a)!=Level(b)){first=a;second=b;}
  Check(first!=null,"No different-level same-character fixture");
  Kill(first,Drop(first,0));Kill(second,Drop(second,1));
  Check(two.m_AcquireUnit[0].m_UnitLevel==Level(first) && two.m_AcquireUnit[1].m_UnitLevel==Level(second),"Level looked up by character instead of enemy");
  bool rejected=false;try{DivineGateOffline.QuestDropLevel291(new BattleEnemy{m_EnemyMasterParamEnemy=first},new PacketStructQuestBuildDrop{unit_id=0});}
  catch(InvalidOperationException){rejected=true;}Check(rejected,"Invalid drop context accepted");
  Console.WriteLine("PASS 2144 actual enemy death paths, effective Lv1..10, nine explicit range normalizations, no duplicate/empty drops or pre-clear account/packet mutation; same character at different enemy levels remains distinct.");
  var high=MasterDataUtil.GetEnemyParamFromID(100750);var king=MasterDataUtil.GetEnemyParamFromID(127290);
  var collect=Collector(100);Kill(high,Drop(high,1));Kill(second,Drop(second,0));Kill(king,Drop(king,0));
  // An old collected record has no enemy identity; do not guess or upgrade it.
  collect.m_AcquireUnit[3]=new InGameAcquireUnit{m_UnitID=high.drop_unit_id,m_UnitLevel=0,m_UnitFloor=1};
  Check(collect.m_AcquireUnit[0].m_UnitLevel==2 && collect.m_AcquireUnit[2].m_UnitLevel==1,"Known level fixtures");
  Save.quest_id=1;Save.quest_build=ServerForgery.CreateQuestBuild(1);server.UserParamSave();
  Singleton<LocalSaveManager>.Instance.SaveFuncGoesToGameRestore(new SceneGoesParamToGameMainRestore{m_QuestMissionID=1,m_AcquireUnit=collect.m_AcquireUnit});
  File.WriteAllText(FileBase+".account",LocalSaveUtil.GetValueString("SERVER_SAVE",""));
  File.WriteAllText(FileBase+".quest",JsonMapper.ToJson(Save.quest_build));
  File.WriteAllText(FileBase+".acquired",JsonMapper.ToJson(collect.m_AcquireUnit));
  Console.WriteLine("PASS acquired level/plus records saved with native restore serializer and unchanged native quest packet; ready for process reload.");
 }
}
