using System;
using System.Reflection;
using System.Collections.Generic;
using ServerDataDefine;

class NativeBuffTests290 {
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
 static void Apply(BattleSkillActivity a){apply.Invoke(null,new object[]{a,2280,3450});}
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
  Init();bool baseline=args.Length>0 && args[0]=="baseline";
  var master=MasterDataUtil.GetCharaParamFromID(342);
  Check(master.name=="光才カルネアデス" && master.skill_limitbreak==102000,"Reported character changed");
  var owner=new CharaOnce{m_CharaMasterDataParam=master,m_CharaPow=2280,m_CharaHP=3450};
  var target=new CharaOnce{m_CharaMasterDataParam=MasterDataUtil.GetCharaParamFromID(217)};
  int defence=0;foreach(var row in server.m_MasterDataParamEnemy)if(row.fix_id==100620)defence=(int)row.status_def;
  Check(defence==2000 && target.m_CharaMasterDataParam.name=="ダークエッグ","Reported enemy changed");
  var attack=new BattleSkillActivity{m_SkillType=ESKILLTYPE.eACTIVE,m_Element=4,m_skill_power=100,
   m_skill_chk_atk_affinity=2,m_skill_chk_atk_leader=2,m_skill_chk_atk_passive=2,
   m_skill_chk_atk_ailment=2,m_skill_chk_atk_combo=1};
  int before=Attack(owner,target,attack);
  Apply(Activity(102000));int after=Attack(owner,target,attack);
  if(baseline) {
   Near(Rate(4,master.kind),0.01f,"Old incorrect light multiplier");
   Check(before==4560 && after==45 && AfterDefence(after,defence,4)==1,"Old reported one damage not reproduced");
   Console.WriteLine("REPRODUCED r12 APK: 2280 ATK, light vs Dark Egg DEF 2000; raw 4560 -> 45; applied 2560 -> 1. Light buff incorrectly uses 1%.");
   return;
  }
  Near(Rate(4,master.kind),1.5f,"Reported light buff multiplier");
  Check(before==4560 && after==6840 && AfterDefence(before,defence,4)==2560 && AfterDefence(after,defence,4)==4840,"Correct native damage sequence");
  // A genuinely weak attack must still respect Dark Egg's original high DEF.
  Check(AfterDefence(300,defence,4)==1,"Legitimate high defence bypassed");
  states.UpdateTurnOnce(EAILMENT_UPDATE_TIMING.eBATTLE,game.m_PlayerPartyCharaAilment);
  Check(Attack(owner,target,attack)==before,"One-turn buff did not expire");
  Console.WriteLine("PASS native light buff: raw 4560 -> 6840, DEF 2000 applied 2560 -> 4840; expires after original 1 turn. Low damage still respects original DEF.");
  int count=0,matrix=0,durations=0,random=0,referenced=0;
  foreach(var row in server.m_MasterDataSkillLimitBreak) {
   if(row.skill_cate!=17 && row.skill_cate!=18 && row.skill_cate!=43)continue;
   count++;
   var a=Activity(row.fix_id);
   int ignored;
   Check(!DivineGateOffline.LegacyDamage290(null,null,owner,target,null,false,game.m_PlayerPartyCharaAilment,0,a,out ignored),"Buff still enters legacy damage");
   Check(!DivineGateOffline.LegacyAilment290(a,2280,3450),"Buff still enters legacy state application");
   Check(a.m_skill_damage_enable==1 && a.m_skill_power==0 && a.m_statusAilment_target==1,"Buff damage/target fields");
   int upper=row.value0==row.value1?row.value0+1:row.value1;
   for(int life=row.value0;life<upper;life++) {
    Reset();a.m_Life=life;DivineGateOffline.LegacyActivity290(new[]{a});
    var state=MasterDataUtil.GetMasterDataStatusAilmentParam(a.m_statusAilment[0]);
    Check(state!=null && state.duration==life && state.param01==(row.skill_cate==17?row.value2:row.value3),"Native state reference/value/duration");
    Check(state.name==row.name && state.detail==row.detail,"Native state text changed");
    Apply(a);
    Check(states.GetAilmentChara(game.m_PlayerPartyCharaAilment).cAilment[9].nMasterDataStatusAilmentID==state.fix_id,"Native apply did not set master reference");
    for(int elem=1;elem<=7;elem++)for(int kind=1;kind<=8;kind++) {
     float expected=(row.skill_cate==17 || (row.skill_cate==18 && row.value2==elem) ||
      (row.skill_cate==43 && row.value2==kind)) ? state.param01*0.01f : 1f;
     Near(Rate(elem,kind),expected,"Scope "+row.fix_id+" element "+elem+" kind "+kind);
     matrix++;
    }
    // Same-state reapplication is rejected by the native manager, not stacked.
    float prior=Rate(4,4);Apply(a);Near(Rate(4,4),prior,"Same buff stacked");
    for(int turn=1;turn<=life;turn++) {
     states.UpdateTurnOnce(EAILMENT_UPDATE_TIMING.eBATTLE,game.m_PlayerPartyCharaAilment);
     if(turn<life)Near(Rate(row.skill_cate==18?row.value2:4,row.skill_cate==43?row.value2:4),
       state.param01*0.01f,"Early expiry "+row.fix_id);
    }
    Near(Rate(4,4),1f,"Expired buff remains");durations++;
   }
   if(row.value0!=row.value1) {
    var seen=new HashSet<int>();
    for(int i=0;i<128;i++) {
     var roll=Activity(row.fix_id);
     Check(roll.m_Life>=row.value0 && roll.m_Life<row.value1,"Changed original exclusive RNG upper bound");
     Check(MasterDataUtil.GetMasterDataStatusAilmentParam(roll.m_statusAilment[0]).duration==roll.m_Life,"RNG state reference");
     seen.Add(roll.m_Life);random++;
    }
    Check(seen.Count==row.value1-row.value0,"Random duration coverage");
   }
   foreach(var character in server.m_MasterDataParamChara)
    if(character.skill_limitbreak==row.fix_id)referenced++;
  }
  Check(count==39,"Missing native buff coverage");
  // Global and attribute buffs retain the original independent multiplicative factors.
  Reset();Apply(Activity(101920));Apply(Activity(102000));Near(Rate(4,4),2.25f,"Global and light composition");
  Near(Rate(2,4),1.5f,"Light buff leaked to fire");
  // Switching/dropping a state uses the original native cleanup.
  states.ClearAllChara();Near(Rate(4,4),1f,"Clear did not restore damage");
  Console.WriteLine("PASS all "+count+" buffs, "+referenced+" character references, "+matrix+" element/kind scope cases, "+durations+" lifetimes and "+random+" original RNG rolls; native application, no duplicate stacking, composition, expiry and cleanup.");
 }
}
