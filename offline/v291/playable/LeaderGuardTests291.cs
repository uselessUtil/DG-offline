using System;
using System.IO;
using System.Reflection;
using System.Collections.Generic;

// Calls original battle methods on master data re-extracted from the signed APK.
// Expected element/rate fixtures are written independently from the locked SQL.
class LeaderGuardTests291 {
 static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
 static void Near(float actual,float expected,string message){
  Check(Math.Abs(actual-expected)<0.0001f,message+" expected "+expected+" got "+actual);
 }
 static ServerForgery server;static InGameManager game;static BattleManager battle;
 static Dictionary<uint,int[]> expected=new Dictionary<uint,int[]>();
 static CharaOnce Unit(MasterDataParamChara master){return new CharaOnce{m_CharaMasterDataParam=master};}
 static void Init(){
  Singleton<TimeManager>.Instance=new TimeManager();
  Singleton<MasterDataManager>.Instance=new MasterDataManager();
  Singleton<LocalSaveManager>.Instance=new LocalSaveManager();
  server=new ServerForgery();Singleton<ServerForgery>.Instance=server;
  typeof(ServerForgery).GetMethod("Start",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(server,null);
  Singleton<SceneGoesParam>.Instance=new SceneGoesParam();
  game=new InGameManager();Singleton<InGameManager>.Instance=game;
  game.m_PlayerPartyChara=new CharaOnce[5];game.m_PlayerParty=new CharaParty();
  game.m_PlayerPartyCharaAilment=-1;
  battle=new BattleManager();Singleton<BattleManager>.Instance=battle;
  Singleton<StatusAilmentsManager>.Instance=new StatusAilmentsManager();
 }
 static MasterDataParamChara First(uint skill){
  foreach(var c in server.m_MasterDataParamChara)if(c.fix_id!=0 && c.skill_leader==skill)return c;
  throw new Exception("No owner for "+skill);
 }
 static void Hit(uint leader,uint friend,int element,float fraction){
  // Neutral, unbuffed party isolates guard from elemental affinity/passives.
  for(int i=0;i<5;i++)game.m_PlayerPartyChara[i]=Unit(new MasterDataParamChara{element=1});
  if(leader!=0)game.m_PlayerPartyChara[0].m_CharaMasterDataParam.skill_leader=leader;
  if(friend!=0)game.m_PlayerPartyChara[4].m_CharaMasterDataParam.skill_leader=friend;
  game.m_PlayerParty.m_PartyTotalHP=100000;game.m_PlayerParty.m_PartyTotalHPMax=100000;
  battle.m_EnemyParam=new[]{new BattleEnemy{m_EnemyMasterParamChara=new MasterDataParamChara{element=element},
   m_EnemyMasterParamEnemy=new MasterDataParamEnemy(),m_EnemyAttack=10000,m_EnemyHP=10000,m_EnemyHPMax=10000,m_EnemyAilmentID=-1}};
  var method=typeof(BattleManager).GetMethod("EnemyAttackAllDamage",BindingFlags.NonPublic|BindingFlags.Instance);
  int damage=(int)method.Invoke(battle,new object[]{0,null});
  // Native AvoidErrorMultiple uses floating point; retain native rounding.
  int target=(int)Math.Round(10000*fraction);
  Check(Math.Abs(damage-target)<=2,"Enemy hit "+leader+"/"+friend+" elem "+element+" expected "+target+" got "+damage);
  Check(game.m_PlayerParty.m_PartyTotalHP==100000-damage,"Returned damage differs from actual HP loss");
 }
 public static void Main(string[] args){
  foreach(string line in File.ReadAllLines(args[0])){
   if(line.Length==0)continue;var c=line.Split('\t');
   expected.Add(uint.Parse(c[0]),new[]{int.Parse(c[1]),int.Parse(c[2])});
  }
  Check(expected.Count==23,"Incomplete raw SQL fixture");Init();
  int owners=0,matrix=0;var seen=new HashSet<uint>();
  foreach(var c in server.m_MasterDataParamChara){
   if(c.fix_id==0 || !expected.ContainsKey(c.skill_leader))continue;
   owners++;seen.Add(c.skill_leader);var e=expected[c.skill_leader];
   var skill=MasterDataUtil.GetLeaderSkillParamFromID(c.skill_leader);
   Check(skill.skill_decline_dmg_active==2 && skill.skill_decline_dmg_element==e[0] &&
    skill.skill_decline_dmg_rate==e[1],"Native guard flags "+c.fix_id);
   for(int hp=0;hp<2;hp++){
    game.m_PlayerParty.m_PartyTotalHPMax=10000;game.m_PlayerParty.m_PartyTotalHP=hp==0?10000:5000;
    foreach(int slot in new[]{0,4}){
     game.m_PlayerPartyChara=new CharaOnce[5];game.m_PlayerPartyChara[slot]=Unit(c);
     for(int elem=0;elem<=7;elem++){
      float rate=e[0]==0 || e[0]==elem?e[1]/100f:1f;
      Near(InGameUtil.GetLeaderSkillDamageDecrease(slot,elem),rate,"Guard "+c.fix_id+" slot "+slot+" elem "+elem);matrix++;
     }
    }
   }
  }
  Check(owners==76 && seen.Count==23,"Affected owner count changed");
  // Every unchanged LS must retain its prior native-flag behavior.
  int controls=0;
  foreach(var c in server.m_MasterDataParamChara){
   if(c.fix_id==0 || c.skill_leader==0 || expected.ContainsKey(c.skill_leader))continue;
   var s=MasterDataUtil.GetLeaderSkillParamFromID(c.skill_leader);
   game.m_PlayerPartyChara=new CharaOnce[5];game.m_PlayerPartyChara[0]=Unit(c);
   game.m_PlayerParty.m_PartyTotalHP=5000;game.m_PlayerParty.m_PartyTotalHPMax=10000;
   for(int elem=0;elem<=7;elem++){
    float rate=s.skill_decline_dmg_active==2 && (s.skill_decline_dmg_element==0 || s.skill_decline_dmg_element==elem)?s.skill_decline_dmg_rate/100f:1f;
    Near(InGameUtil.GetLeaderSkillDamageDecrease(0,elem),rate,"Unchanged guard control "+c.fix_id);controls++;
   }
  }
  // Composite guards must still traverse their existing attack-buff chains.
  foreach(uint id in new uint[]{201000,201400,202100}){
   var owner=First(id);game.m_PlayerPartyChara=new CharaOnce[5];game.m_PlayerPartyChara[0]=Unit(owner);
   int element=id==201000?3:id==201400?1:5;float rate=id==202100?2.5f:2f;
   Near(InGameUtil.GetLeaderSkillDamageRate(0,Unit(new MasterDataParamChara{element=element})),rate,"Composite attack chain "+id);
   Near(InGameUtil.GetLeaderSkillDamageRate(0,Unit(new MasterDataParamChara{element=2})),1f,"Composite unrelated element "+id);
  }
  // Original enemy attack path, actual returned damage and party HP changes.
  int hits=0;
  foreach(var pair in expected)for(int elem=1;elem<=7;elem++){
   var e=pair.Value;float rate=e[0]==0 || e[0]==elem?e[1]/100f:1f;
   Hit(pair.Key,0,elem,rate);Hit(0,pair.Key,elem,rate);hits+=2;
  }
  Hit(118700,118700,2,0.75f*0.75f); // same-element leader/friend multiply
  Hit(118700,119100,2,0.75f);      // unrelated friend's guard does not apply
  Hit(118800,202100,2,0.50f*0.80f);// elemental + all-element guard
  Hit(200400,210080,7,0.85f*0.90f);// all-element guards multiply
  Console.WriteLine("PASS 76 owners / 23 LS; "+matrix+" leader/friend/HP/element cases; "+controls+" unchanged controls; three composite attack chains; "+(hits+4)+" actual enemy hits and matching HP loss, including double guards.");
 }
}
