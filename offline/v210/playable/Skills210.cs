// Old-backup skill semantics at the 2.1.0 activity boundary. No new skill values invented.
using System;
using System.Collections.Generic;
using UnityEngine;
public static partial class DivineGateOffline {
 static readonly Dictionary<uint,bool> legacySkills210=new Dictionary<uint,bool>();
 public static void RegisterLegacySkills210(ServerForgery server) {
  legacySkills210.Clear();foreach(var s in server.m_MasterDataSkillLimitBreak)if(s.fix_id!=0)legacySkills210[s.fix_id]=true;
 }
 static bool IsLegacy210(BattleSkillActivity a){return a!=null && a.m_SkillType==ESKILLTYPE.eLIMITBREAK && legacySkills210.ContainsKey(a.m_SkillParamSkillID);}
 public static void LegacyActivity210(BattleSkillActivity[] activities) {
  if(activities==null || activities.Length==0)return;
  var a=activities[0];if(!IsLegacy210(a))return;
  int c=a.m_Category;var p=a.m_Param;
  a.m_skill_damage_enable=(a.m_Type==1 || a.m_Type==2)?2:1;
  a.m_skill_chk_atk_affinity=2;a.m_skill_chk_def_defence=2;a.m_skill_chk_def_ailment=2;
  a.m_skill_absorb=c==4?100:0;
  a.m_skill_kickback_fix=(c==6 || c==9)?int.MaxValue:0;
  if(c==7 || c==11 || ((c==3 || c==8 || c==9) && p[1]==2) || (c==10 && p[2]==2))a.m_skill_chk_def_defence=1;
  // Old utility generated a player target for recovery and non-damage actions.
  if(a.m_Type>=3)InGameUtil.CreateTargetPlayer(ref a);
 }
 public static bool LegacyDamage210(CharaOnce leader,CharaOnce friend,CharaOnce owner,CharaOnce target,BattleEnemy enemy,bool boost,int ailment,int count,BattleSkillActivity a,out int damage) {
  damage=0;if(!IsLegacy210(a))return false;
  if(owner==null)return true;
  int c=a.m_Category;var p=a.m_Param;float affinity=target==null?1f:InGameUtil.GetSkillElementRate(a.m_Element,target.m_CharaMasterDataParam.element);
  float value;
  if(c==3 || c==8 || c==9){damage=(int)InGameUtil.AvoidErrorMultiple(p[0],affinity);return true;}
  if(c==10){damage=(int)InGameUtil.AvoidErrorMultiple(InGameUtil.GetDBRevisionValue(p[0]),affinity);return true;}
  if(c==11){damage=enemy==null?0:(int)InGameUtil.AvoidErrorMultiple(enemy.m_EnemyHP,InGameUtil.GetDBRevisionValue(p[0]));return true;}
  if(c==12){value=InGameUtil.AvoidErrorDivision(owner.m_CharaHP,owner.m_CharaMasterDataParam.base_hp_max);damage=(int)InGameUtil.AvoidErrorDivision(10000f,InGameUtil.AvoidErrorMultiple(value,100f));return true;}
  float rate=InGameUtil.GetDBRevisionValue(c==5?p[1]:p[0]);
  if(c!=4 && c!=5 && c!=6)rate+=InGameUtil.GetDBRevisionValue((int)RandManager.GetRand(0,(uint)Math.Min(100,Math.Max(0,p[1]))));
  value=InGameUtil.AvoidErrorMultiple(owner.m_CharaPow,rate);
  value=InGameUtil.AvoidErrorMultiple(value,affinity);
  if(c!=4 && c!=5 && c!=6)value=InGameUtil.AvoidErrorMultiple(value,boost?1.5f:1f);
  float l=1f;
  l=InGameUtil.AvoidErrorMultiple(l,InGameUtil.GetLeaderSkillDamageRate(0,owner));l=InGameUtil.AvoidErrorMultiple(l,InGameUtil.GetLeaderSkillDamageRate(4,owner));
  l=InGameUtil.AvoidErrorMultiple(l,InGameUtil.GetLeaderSkillDamageUPColor(0));l=InGameUtil.AvoidErrorMultiple(l,InGameUtil.GetLeaderSkillDamageUPColor(4));
  l=InGameUtil.AvoidErrorMultiple(l,InGameUtil.GetLeaderSkillDamageUPHands(0));l=InGameUtil.AvoidErrorMultiple(l,InGameUtil.GetLeaderSkillDamageUPHands(4));
  value=InGameUtil.AvoidErrorMultiple(value,l);
  float passive=InGameUtil.AvoidErrorMultiple(InGameUtil.PassiveChkSkillDamageRate(owner),InGameUtil.PassiveChkKindKillerAtk(owner));value=InGameUtil.AvoidErrorMultiple(value,passive);
  float status=StatusAilmentUtil.GetOffenceRate(ailment);status=InGameUtil.AvoidErrorMultiple(status,StatusAilmentUtil.GetOffenceElementRate(ailment,owner.m_CharaMasterDataParam.element));status=InGameUtil.AvoidErrorMultiple(status,StatusAilmentUtil.GetOffenceKindRate(ailment,owner.m_CharaMasterDataParam.kind));
  value=InGameUtil.AvoidErrorMultiple(value,status);
  if(c!=4 && c!=5 && c!=6)value=InGameUtil.AvoidErrorMultiple(value,InGameUtil.GetSkillCountRate(count));
  damage=(int)value;return true;
 }
 public static bool LegacyAilment210(BattleSkillActivity a,int attack,int hp) {
  if(!IsLegacy210(a))return false;
  int category=StatusAilmentUtil.ConvertCategoryToAilment(a.m_Category,a.m_Param);
  if(category==0)return true;
  var game=Singleton<InGameManager>.Instance;var battle=Singleton<BattleManager>.Instance;
  if(game==null || battle==null)return true;
  bool enemies=a.m_Type==3 || a.m_Type==4;
  if(a.m_Category==15 && battle.m_EnemyParam!=null)foreach(var enemy in battle.m_EnemyParam)if(enemy!=null && !enemy.attackFlag)return true;
  var status=LegacyStatus210(a,attack,hp);
  if(enemies) {
   if(battle.m_EnemyParam==null)return true;
   for(int i=0;i<battle.m_EnemyParam.Length;i++) {
    var enemy=battle.m_EnemyParam[i];if(enemy==null)continue;
    if(a.m_Category==15){enemy.AddTurn((int)RandManager.GetRand((uint)a.m_Param[0],(uint)a.m_Param[1]));if(battle.m_EnemyTurnCount!=null && i<battle.m_EnemyTurnCount.Length && battle.m_EnemyTurnCount[i]!=null)battle.m_EnemyTurnCount[i].text="NEXT "+enemy.m_EnemyTurn;}
    StatusAilmentUtil.AddStatusAilment(enemy.m_EnemyAilmentID,status);
   }
  } else StatusAilmentUtil.AddStatusAilment(game.m_PlayerPartyCharaAilment,status);
  return true;
 }
 public static StatusAilment LegacyStatus210(BattleSkillActivity a,int attack,int hp) {
  var s=new StatusAilment();bool enemy=a.m_Type==3 || a.m_Type==4;
  s.SetupData(2,StatusAilmentUtil.ConvertCategoryToAilment(a.m_Category,a.m_Param),2,a.m_Category==15?1:2,a.m_Category==15?0:a.m_Life,enemy?attack:0,enemy?hp:0,a.m_Param);
  return s;
 }
}
