using System;
using System.Reflection;
using System.IO;
using LitJson;
using System.Collections.Generic;
using ServerDataDefine;

class NativeDgdbTests {
 static ServerForgery server;
 static InGameManager game;
 static StatusAilmentsManager states;
 static MethodInfo apply=typeof(BattleUtil).GetMethod("SkillUpdate_StatusAilment",BindingFlags.Static|BindingFlags.NonPublic);
 static void Check(bool ok,string text){if(!ok)throw new Exception(text);}
 static void Near(float a,float b,string text){Check(Math.Abs(a-b)<0.0001f,text+" expected "+b+" got "+a);}
 static void Init() {
  Singleton<TimeManager>.Instance=new TimeManager();
  Singleton<MasterDataManager>.Instance=new MasterDataManager();
  Singleton<LocalSaveManager>.Instance=new LocalSaveManager();
  server=new ServerForgery();Singleton<ServerForgery>.Instance=server;
  typeof(ServerForgery).GetMethod("Start",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(server,null);
  game=new InGameManager();Singleton<InGameManager>.Instance=game;
  game.m_PlayerPartyChara=new CharaOnce[5];game.m_PlayerParty=new CharaParty();
  var battle=new BattleManager();Singleton<BattleManager>.Instance=battle;
  battle.m_EnemyParam=new BattleEnemy[1];
  states=new StatusAilmentsManager();Singleton<StatusAilmentsManager>.Instance=states;
  for(int i=0;i<states.m_StatusAilmentChara.Length;i++) {
   var c=new StatusAilmentChara();c.cAilment=new StatusAilment[10];
   for(int j=0;j<c.cAilment.Length;j++)c.cAilment[j]=new StatusAilment();
   states.m_StatusAilmentChara[i]=c;
  }
  Reset();
 }
 static void Reset(){states.ClearAllChara();game.m_PlayerPartyCharaAilment=states.ReqAddChara(true);}
 static BattleSkillActivity Activity(uint id) {
  var a=new BattleSkillActivity();InGameUtil.ActivityLimitBreakSkill(new[]{a},0,-1,id);return a;
 }
 static void Apply(BattleSkillActivity a){InGameUtil.CreateTargetPlayer(ref a);apply.Invoke(null,new object[]{a,2280,3450});}
 static float Rate(int elem,int kind) {
  int id=game.m_PlayerPartyCharaAilment;
  return states.GetAilmentChara(id).fOffenceRate*
   states.GetAilmentChara(id).fOffecceRateElem[elem]*
   states.GetAilmentChara(id).fOffenceRateKind[kind];
 }
 static int Attack(CharaOnce owner,CharaOnce target,BattleSkillActivity attack) {
  return InGameUtil.GetDamageValue(null,null,owner,target,null,false,game.m_PlayerPartyCharaAilment,0,attack);
 }
 static int AfterDefence(int damage,int defence,int elem) {
  // BattleEnemy.DelHP applies a one-point minimum after original DamageReduce.
  return Math.Max(1,InGameUtil.DamageReduce(damage,elem,-1,defence,false,false));
 }
 public static void Main(string[] args) {
  Init();
  var master=MasterDataUtil.GetCharaParamFromID(342);
  var owner=new CharaOnce{m_CharaMasterDataParam=master,m_CharaPow=2280,m_CharaHP=3450};
  var target=new CharaOnce{m_CharaMasterDataParam=MasterDataUtil.GetCharaParamFromID(217)};
  int defence=0;foreach(var r in server.m_MasterDataParamEnemy)if(r.fix_id==100620)defence=(int)r.status_def;
  var attack=new BattleSkillActivity{m_SkillType=ESKILLTYPE.eACTIVE,m_Element=4,m_skill_power=100,
   m_skill_chk_atk_affinity=2,m_skill_chk_atk_leader=2,m_skill_chk_atk_passive=2,m_skill_chk_atk_ailment=2,m_skill_chk_atk_combo=1};
  int before=Attack(owner,target,attack);Apply(Activity(102000));int after=Attack(owner,target,attack);
  Near(Rate(4,4),1.5f,"Carneades multiplier");
  Check(before==4560 && after==6840 && AfterDefence(after,defence,4)>1,"Dark Egg damage regression");
  Apply(Activity(102000));Near(Rate(4,4),1.5f,"Duplicate buff stacked");
  states.UpdateTurnOnce(EAILMENT_UPDATE_TIMING.eBATTLE,game.m_PlayerPartyCharaAilment);
  Near(Rate(4,4),1f,"Carneades native expiry");
  Console.WriteLine("PASS Carneades/Dark Egg native damage "+before+" -> "+after+"; actual DEF "+defence+"; duplicate and expiry correct.");
  int all=0,damage=0,status=0,matrix=0;
  var rawOwner=new CharaOnce{m_CharaMasterDataParam=new MasterDataParamChara{element=1,kind=1,base_hp_max=10000},m_CharaPow=100,m_CharaHP=5000};
  var enemy=new BattleEnemy{m_EnemyHP=10000};
  foreach(var row in server.m_MasterDataSkillLimitBreak) {
   if(row.fix_id==0)continue;
   Reset();var a=Activity(row.fix_id);
   Check(a.m_skill_damage_enable==row.skill_damage_enable && a.m_skill_power==row.skill_power &&
    a.m_skill_power_fix==row.skill_power_fix && a.m_skill_power_hp_rate==row.skill_power_hp_rate &&
    a.m_statusAilment_target==row.status_ailment_target,"Native fields changed "+row.fix_id);
   for(int i=0;i<5;i++)Check(a.m_Param[i]==row.value_list[i],"Lost native parameter");
   for(int i=0;i<4;i++) {
    int id=(int)typeof(MasterDataSkillLimitBreak).GetField("status_ailment"+(i+1)).GetValue(row);
    Check(a.m_statusAilment[i]==id,"State reference changed");
    if(id!=0)Check(MasterDataUtil.GetMasterDataStatusAilmentParam(id)!=null,"Missing state");
   }
   if(row.skill_damage_enable==2) {
    int actual=InGameUtil.GetDamageValue(null,null,rawOwner,null,enemy,false,-1,0,a);
    int expected=row.skill_power+row.skill_power_fix+row.skill_power_hp_rate*100;
    Check(Math.Abs(actual-expected)<=2,"DGDB native damage "+row.fix_id+" expected "+expected+" got "+actual);damage++;
   }
   if(row.status_ailment_target==1 && row.status_ailment1!=0) {
    var state=MasterDataUtil.GetMasterDataStatusAilmentParam(row.status_ailment1);
    if(state.category>=12 && state.category<=19 || state.category>=34 && state.category<=41) {
     Apply(a);
     for(int e=1;e<=7;e++)for(int k=1;k<=8;k++) {
      int category=state.category;bool applies=category==12 || (category==13 && e==2) ||
       (category==14 && e==3) || (category==15 && e==4) || (category==16 && e==5) ||
       (category==17 && e==6) || (category==18 && e==7) || (category==19 && e==1) || category==33+k;
      Near(Rate(e,k),applies?state.param01*0.01f:1f,"DGDB target "+row.fix_id);matrix++;
     }
     for(int turn=0;turn<state.duration;turn++)states.UpdateTurnOnce(EAILMENT_UPDATE_TIMING.eBATTLE,game.m_PlayerPartyCharaAilment);
     Near(Rate(4,4),1f,"State did not expire "+row.fix_id);status++;
    }
   }
   all++;
  }
  Check(all==253 && damage>100 && status>30,"Incomplete native skill coverage");
  var mikasa=Activity(203120);Check(mikasa.m_skill_power==1500 && mikasa.m_skill_damage_enable==2 && mikasa.m_skill_chk_def_defence==1,"Mikasa attack lost");
  Reset();Apply(mikasa);Near(Rate(3,1),1.3f,"Mikasa water buff");
  var armin=Activity(203130);Check(armin.m_Category==41 && armin.m_Param[2]==1 && armin.m_Param[3]==1 && armin.m_Param[4]==1,"Armin right three panels");
  Reset();Apply(armin);Near(Rate(6,1),1.3f,"Armin wind buff");
  Reset();Apply(Activity(204280));Near(Rate(2,1),1.25f,"Uiharu buff");
  for(int i=0;i<2;i++)states.UpdateTurnOnce(EAILMENT_UPDATE_TIMING.eBATTLE,game.m_PlayerPartyCharaAilment);
  Near(Rate(2,1),1.25f,"Uiharu early expiry");states.UpdateTurnOnce(EAILMENT_UPDATE_TIMING.eBATTLE,game.m_PlayerPartyCharaAilment);Near(Rate(2,1),1f,"Uiharu expiry");
  Reset();Apply(Activity(204490));Near(Rate(2,1),1.75f,"Akane buff");
  foreach(var b in server.m_MasterDataSkillBoost)Check(b.param_list!=null && b.param_list.Length==16,"Boost not converted");
  // Preserve and exercise the 2.9.1 cost-condition fix with duplicate NS IDs.
  MasterDataSkillActive normal=null;foreach(var n in server.m_MasterDataSkillActive)if(n.skill_element==2 && n.cost1!=0){normal=n;break;}
  Check(normal!=null,"No fire normal skill");
  var condition=typeof(InGameUtil).GetMethod("GetLeaderSkillDamageUPConditionCost",BindingFlags.Static|BindingFlags.NonPublic);
  var leader=new MasterDataSkillLeader{skill_value_00=1,skill_value_01=1,skill_value_02=1,skill_value_03=1,skill_value_15=200};
  var battle=Singleton<BattleManager>.Instance;
  Func<int,float> evaluate=count=>{
   battle.m_SkillRequestActive.m_SkillActivityArray=new BattleSkillActivity[count];
   for(int i=0;i<count;i++)battle.m_SkillRequestActive.m_SkillActivityArray[i]=new BattleSkillActivity{m_SkillType=ESKILLTYPE.eACTIVE,m_SkillParamSkillID=normal.fix_id};
   return (float)condition.Invoke(null,new object[]{leader});
  };
  Near(evaluate(1),1f,"One skill satisfied duplicate conditions");
  Near(evaluate(2),2f,"Repeated native NS cost condition failed");
  battle.m_SkillRequestActive.m_SkillActivityArray=new[]{new BattleSkillActivity{m_SkillType=ESKILLTYPE.eLIMITBREAK,m_SkillParamSkillID=normal.fix_id}};
  Near((float)condition.Invoke(null,new object[]{leader}),1f,"AS counted as normal skill");
  Console.WriteLine("PASS "+all+" native AS, "+damage+" damage cases, "+status+" status lifetimes, "+matrix+" element/kind cases; composite attack/buff, panel parameters, Boost conversion and original 2.9.1 duplicate-cost conditions.");
 }
}
