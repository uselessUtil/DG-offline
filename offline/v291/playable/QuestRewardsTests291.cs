using System;using System.IO;using System.Reflection;using System.Collections.Generic;using LitJson;using ServerDataDefine;
class QuestRewardsTests291 {
 static ServerForgery server;static uint serial=1;
 static ServerForgerySaveUser Save{get{return DivineGateOffline.Local291Player(server);}}
 static FieldInfo rng=typeof(RandManager).GetField("m_Rand",BindingFlags.NonPublic|BindingFlags.Static);
 static MethodInfo battle=typeof(ServerForgery).GetMethod("CreateQuestBuildBattle",BindingFlags.NonPublic|BindingFlags.Static);
 static void Check(bool v,string s){if(!v)throw new Exception(s);}
 static uint[] Seeds(int max,int draw=1) {
  var a=new uint[max];var seen=new bool[max];int left=max;var r=new Rand();
  for(uint s=0;left>0;s++){r.SetRandSeed(s);uint t=0;for(int j=0;j<draw;j++)t=r.GetRand(0,(uint)max);if(!seen[t]){seen[t]=true;a[t]=s;left--;}}
  return a;
 }
 static void Seed(uint s){var r=new Rand();r.SetRandSeed(s);rng.SetValue(null,r);}
 static T Api<T>(SERVER_API api,object send) {
  var packet=new PacketData{m_PacketAPI=api,m_PacketUniqueNum=serial++};
  var raw=server.CreateRecvForgery(ref packet,JsonMapper.ToJson(send));var j=JsonMapper.ToObject(raw);
  Check((int)j["header"]["code"]==(int)API_CODE.API_CODE_SUCCESS,"API failed "+api+": "+raw);return JsonMapper.ToObject<T>(raw);
 }
 static void Init(){
  Singleton<TimeManager>.Instance=new TimeManager();Singleton<MasterDataManager>.Instance=new MasterDataManager();Singleton<LocalSaveManager>.Instance=new LocalSaveManager();
  server=new ServerForgery();Singleton<ServerForgery>.Instance=server;
  typeof(ServerForgery).GetMethod("Start",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(server,null);
 }
 static PacketStructQuestBuildDrop[] Battle(MasterDataEnemyGroup g) {
  var bs=new TemplateList<PacketStructQuestBuildBattle>();bs.Alloc(10);var ds=new TemplateList<PacketStructQuestBuildDrop>();ds.Alloc(10);
  battle.Invoke(null,new object[]{1,bs,ds,g});
  Check(bs.m_BufferSize==1 && bs[0].enemy_list.Length==1,"Single enemy fixture");return ds.ToArray()??new PacketStructQuestBuildDrop[0];
 }
 static LegacyFloor291 Floor(uint seed) {
  return new LegacyFloor291(new MasterDataCategoryPattern{cate0=24,cate5=1},new MasterDataExpectPattern{level1=25},seed);
 }
 static string ExpectedFile{get{return Environment.GetEnvironmentVariable("DG_TEST_PREFS")+".quest.json";}}
 static void Reload(){
  string expected=File.ReadAllText(ExpectedFile);Check(Save!=null && JsonMapper.ToJson(Save.quest_build)==expected,"Quest/drop/invasion packet changed on restart");
  var first=Save.quest_build.list_drop[0];Check(first.plus_hp+first.plus_pow==1,"Saved plus missing");
  int before=Save.unit_list.Length;
  var clear=new SendQuestClear{quest_id=Save.quest_id,get_unit=new[]{new PacketStructUnitGet{id=first.unit_id,level=1,add_hp=(uint)first.plus_hp,add_pow=(uint)first.plus_pow}}};
  var result=Api<RecvQuestClear>(SERVER_API.SERVER_API_QUEST_CLEAR,clear);
  Check(result.result.get_unit.Length==1 && Save.unit_list.Length==before+1,"Native clear did not grant drop");
  PacketStructUnit unit=null;foreach(var u in Save.unit_list)if(u.unique_id==result.result.get_unit[0])unit=u;Check(unit!=null,"Granted unit not found");Check(unit.id==first.unit_id && unit.add_hp==first.plus_hp && unit.add_pow==first.plus_pow,"Drop plus changed during native award");
  Console.WriteLine("PASS cross-process native quest packet preserved; original clear API awards saved drop plus exactly once.");
 }
 static void Main(string[] args){
  Init();if(args.Length>0){Reload();return;}
  var enemy=MasterDataUtil.GetEnemyParamFromID(103920);string original=JsonMapper.ToJson(enemy);
  var group=new MasterDataEnemyGroup{fix_id=999999,fix=2,enemy_id_1=enemy.fix_id};
  var seeds=Seeds(10000);
  enemy.drop_plus_pow=enemy.drop_plus_hp=0;enemy.drop_plus_none=1;
  foreach(int rate in new[]{0,1,500,1000,3333,5000,10000}) {
   enemy.drop_unit_rate=rate;int n=0;
   for(int r=0;r<10000;r++){Seed(seeds[r]);if(Battle(group).Length>0)n++;}
   Check(n==rate,"Native drop chance "+rate+" got "+n);
   Console.WriteLine("PASS actual native battle drop "+rate+"/10000 => "+n+"/10000");
  }
  enemy.drop_unit_rate=10000;
  var weights=new HashSet<string>();int cases=0;
  foreach(var e in server.m_MasterDataParamEnemy) {
   string key=e.drop_plus_pow+","+e.drop_plus_hp+","+e.drop_plus_none;if(!weights.Add(key))continue;
   int p=e.drop_plus_pow,h=e.drop_plus_hp,z=e.drop_plus_none,total=p+h+z;
   enemy.drop_plus_pow=p;enemy.drop_plus_hp=h;enemy.drop_plus_none=z;
   if(total==0){Check(Battle(group)[0].plus_pow==0,"Zero plus weights");continue;}
   // Native battle consumes chance draw first; enumerate all second-draw tickets.
   var ss=Seeds(total,2);int cp=0,ch=0,cz=0;
   for(int i=0;i<total;i++){
    Seed(ss[i]);var d=Battle(group)[0];Check(d.plus_pow+d.plus_hp<=1,"Double plus");if(d.plus_pow>0)cp++;else if(d.plus_hp>0)ch++;else cz++;
   }
   Check(cp==p && ch==h && cz==z,"Plus weights changed "+key+" got "+cp+","+ch+","+cz);cases++;
  }
  // Restore original enemy object fields through reflection after fixture tests.
  var restored=JsonMapper.ToObject<MasterDataParamEnemy>(original);foreach(var f in typeof(MasterDataParamEnemy).GetFields())f.SetValue(enemy,f.GetValue(restored));
  Console.WriteLine("PASS "+cases+" distinct real DGDB plus-weight combinations through original battle/packet creation.");
  var master=Singleton<MasterDataManager>.Instance;
  var noInvasionPickup=new uint[]{1,2};Check(DivineGateOffline.PermanentInvasionPickup291(noInvasionPickup,17)==noInvasionPickup,"No-invasion pickup list changed");
  int permanent=0;var quests=new List<uint>();
  foreach(var r in server.m_MasterDataGuerrillaBoss)if(r.active==2 && MasterDataUtil.GetQuestParamFromID(r.quest_id)!=null && MasterDataUtil.GetQuestParamFromID(r.quest_id).active==2 && !quests.Contains(r.quest_id))quests.Add(r.quest_id);
  foreach(uint q in quests)if(MasterDataUtil.GetGuerrillaBossParamFromQuestID(q)!=null)permanent++;
  Check(permanent==79,"Permanent quest coverage "+permanent);
  Console.WriteLine("PASS all79 eligible quest IDs resolve permanent invasions despite archived2014 dates.");
  var originals=master.m_MasterDataGuerrillaBoss;
  var row=new MasterDataGuerrillaBoss{active=2,quest_id=315,enemy_group_id=120730,enemy_rate=20,timing_start=2014010100,timing_end=2014010200};
  master.m_MasterDataGuerrillaBoss=new[]{row};
  var ordinary=MasterDataUtil.GetEnemyGroupParamFromID(100220);
  foreach(int rate in new[]{0,8,13,20,100}) {
   row.enemy_rate=rate;var ss=Seeds(100);int n=0;
   for(int i=0;i<100;i++){var f=Floor(ss[i]);if(DivineGateOffline.QuestInvasion291(ordinary,row,7,f)!=ordinary)n++;Check(DivineGateOffline.QuestInvasion291(ordinary,row,6,f)==ordinary,"Invasion below7stars");}
   Check(n==rate,"Invasion chance "+rate+" got "+n);Console.WriteLine("PASS invasion "+rate+"/100; 0 and100 endpoints; only7-star cells.");
  }
  row.enemy_rate=20;row.user_group=SystemUtil.GetUserGroup()%5+1;Check(MasterDataUtil.GetGuerrillaBossParamFromQuestID(315)==null,"User group ignored");row.user_group=0;
  row.active=1;Check(MasterDataUtil.GetGuerrillaBossParamFromQuestID(315)==null,"Inactive invasion enabled");row.active=2;
  row.quest_id_must=399;Check(MasterDataUtil.GetGuerrillaBossParamFromQuestID(315)==null,"Missing prerequisite accepted");row.quest_id_must=0;
  Check(MasterDataUtil.GetGuerrillaBossParamFromQuestID(1)==null,"Unlisted/tutorial quest gained invasion");
  master.m_MasterDataGuerrillaBoss=originals;
  int conflicts=0;
  foreach(uint q in quests) {
   var unique=new List<MasterDataGuerrillaBoss>();var seen=new HashSet<uint>();
   foreach(var r in originals)if(r.quest_id==q && r.active==2 && seen.Add(r.enemy_group_id))unique.Add(r);
   if(unique.Count<2)continue;conflicts++;
   var ss=Seeds(100*unique.Count);var counts=new Dictionary<uint,int>();int success=0;
   for(int ticket=0;ticket<ss.Length;ticket++) {
    var f=Floor(ss[ticket]);var chosen=DivineGateOffline.QuestInvasion291(ordinary,unique[0],7,f);
    Check(chosen==DivineGateOffline.QuestInvasion291(ordinary,unique[0],7,f),"Same-floor invasion rerolled");
    if(chosen!=ordinary){success++;if(!counts.ContainsKey(chosen.fix_id))counts[chosen.fix_id]=0;counts[chosen.fix_id]++;}
   }
   Check(success==20*unique.Count && counts.Count==unique.Count,"Total conflict chance changed "+q);
   foreach(var r in unique)Check(counts[r.enemy_group_id]==20,"Conditional group chance changed "+q);
   var pickup=MasterDataUtil.GetQuestPickupUnit(q,unique[0]);var ids=new HashSet<uint>();foreach(uint id in pickup)ids.Add(id);
   foreach(var r in unique){var list=new TemplateList<uint>();list.Alloc(256);MasterDataUtil.GetQuestPickupUnitLoop(MasterDataUtil.GetEnemyGroupParamFromID(r.enemy_group_id),ref list);foreach(uint id in list.ToArray())if(id!=0)Check(ids.Contains(id),"Missing invasion pickup");}
  }
  Check(conflicts==14,"Conflict coverage");Console.WriteLine("PASS all14 collision quests: exact20% total, equal distinct-group chances, repeated schedules do not add weight; pickup lists include all candidates.");
  Api<RecvCreateUser>(SERVER_API.SERVER_API_USER_CREATE,new SendCreateUser{uuid="reward-fixture",terminal=new PacketStructTerminal{platform=1,name="Docker"}});
  Api<RecvSelectDefParty>(SERVER_API.SERVER_API_USER_SELECT_DEF_PARTY,new SendSelectDefParty{select_party=1});
  master.m_MasterDataGuerrillaBoss=new[]{row};row.quest_id_must=399;
  Check(MasterDataUtil.GetGuerrillaBossParamFromQuestID(315)==null,"Uncleared prerequisite accepted");
  ServerDataUtil.AddBitFlag(ref Save.flag_quest_clear,399);
  Check(MasterDataUtil.GetGuerrillaBossParamFromQuestID(315)!=null,"Cleared prerequisite rejected");
  row.quest_id_must=0;master.m_MasterDataGuerrillaBoss=originals;
  // Exercise full original quest builder with a controlled7-star floor.
  var floorOld=server.m_MasterDataQuestFloor;var catOld=server.m_MasterDataQuestFloorCategory;var expectOld=server.m_MasterDataQuestFloorExpect;
  var invasionOld=master.m_MasterDataGuerrillaBoss;
  var invEnemy=MasterDataUtil.GetEnemyParamFromID(MasterDataUtil.GetEnemyGroupParamFromID(120730).enemy_id_1);
  string invOriginal=JsonMapper.ToJson(invEnemy);
  invEnemy.drop_unit_rate=10000;invEnemy.drop_plus_pow=1;invEnemy.drop_plus_hp=1;invEnemy.drop_plus_none=0;
  server.m_MasterDataQuestFloor=new[]{new MasterDataQuestFloor{fix_id=999999,active=2,quest_id=315,pattern_category=999999,pattern_expect=999999,enemy_group_id_7=100220,boss_group_id=100220}};
  server.m_MasterDataQuestFloorCategory=new[]{new MasterDataCategoryPattern{fix_id=999999,cate1=24,cate5=1}};
  server.m_MasterDataQuestFloorExpect=new[]{new MasterDataExpectPattern{fix_id=999999,level7=25}};
  master.m_MasterDataQuestFloor=server.m_MasterDataQuestFloor;
  row.enemy_rate=100;master.m_MasterDataGuerrillaBoss=new[]{row};
  var built=ServerForgery.CreateQuestBuild(315);int invasions=0,keys=0;var dropIds=new HashSet<int>();
  foreach(var p in built.panel[1].panel_detail) {
   if(p.type==5)keys++;
   if(p.type!=3)continue;
   PacketStructQuestBuildBattle encounter=null;foreach(var b in built.list_battle)if(b.unique_id==p.type_unique)encounter=b;
   Check(encounter!=null && encounter.enemy_list[0]==invEnemy.fix_id,"Full builder did not use invasion");invasions++;
   foreach(int id in encounter.drop_list)if(id>0)Check(dropIds.Add(id),"Duplicate drop identifier");
  }
  Check(invasions==23 && keys==1 && built.list_drop.Length>=23,"Controlled floor coverage");
  foreach(var d in built.list_drop)Check(d.plus_hp+d.plus_pow<=1,"Full builder invalid plus");
  // Boss group stays ordinary; no invasion hook on boss call.
  PacketStructQuestBuildBattle boss=null;foreach(var b in built.list_battle)if(b.unique_id==built.boss[1])boss=b;
  Check(boss!=null && boss.enemy_list[0]!=invEnemy.fix_id,"Boss replaced by invasion");
  Check(built.list_drop[0].plus_hp+built.list_drop[0].plus_pow==1,"Forced plus was lost");
  master.m_MasterDataQuestFloor=floorOld;server.m_MasterDataQuestFloor=floorOld;server.m_MasterDataQuestFloorCategory=catOld;server.m_MasterDataQuestFloorExpect=expectOld;master.m_MasterDataGuerrillaBoss=invasionOld;
  restored=JsonMapper.ToObject<MasterDataParamEnemy>(invOriginal);foreach(var f in typeof(MasterDataParamEnemy).GetFields())f.SetValue(invEnemy,f.GetValue(restored));
  Save.quest_id=315;Save.quest_build=built;server.UserParamSave();File.WriteAllText(ExpectedFile,JsonMapper.ToJson(built));
  Console.WriteLine("PASS full native builder: 23 invasion cells, ordinary boss, one key, unique drop identifiers and saved plus; ready for process reload.");
 }
}
