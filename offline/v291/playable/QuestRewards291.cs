using System;
using System.Collections.Generic;
using ServerDataDefine;

public static partial class DivineGateOffline {
 // The local backend previously tested rate < roll. Rates are per 10,000.
 public static bool QuestDrop291(MasterDataParamEnemy enemy,uint roll) {
  if(enemy==null || enemy.drop_unit_id==0 || enemy.drop_unit_rate<=0)return false;
  return roll<(uint)Math.Min(10000,enemy.drop_unit_rate);
 }
 // Relative weights, not independent percent chances. One drop gets at most
 // one plus point. The result is saved in the native quest packet at generation.
 public static void QuestDropPlus291(PacketStructQuestBuildDrop drop,MasterDataParamEnemy enemy) {
  if(drop==null || enemy==null)throw new ArgumentNullException();
  int pow=enemy.drop_plus_pow,hp=enemy.drop_plus_hp,none=enemy.drop_plus_none;
  if(pow<0 || hp<0 || none<0)throw new InvalidOperationException("Negative drop plus weight");
  drop.plus_pow=0;drop.plus_hp=0;
  long total=(long)pow+hp+none;
  if(total==0)return;
  if(total>uint.MaxValue)throw new InvalidOperationException("Drop plus weight overflow");
  uint ticket=RandManager.GetRand(0,(uint)total);
  if(ticket<(uint)pow)drop.plus_pow=1;
  else if(ticket<(long)pow+hp)drop.plus_hp=1;
 }
 public static MasterDataEnemyGroup QuestInvasion291(MasterDataEnemyGroup original,MasterDataGuerrillaBoss invasion,int star,LegacyFloor291 floor) {
  if(star!=7 || invasion==null || invasion.active!=2 || invasion.enemy_rate<=0)return original;
  var candidates=PermanentInvasionCandidates291(invasion.quest_id);
  if(candidates.Count==0)return original;
  // Schedule duplicates never increase probability. Different enemies share
  // the original total chance, then receive equal conditional chances.
  if(floor==null)throw new InvalidOperationException("Missing invasion floor seed");
  uint ticket=floor.InvasionTicket(candidates.Count);
  if(ticket>=(uint)(Math.Min(100,invasion.enemy_rate)*candidates.Count))return original;
  invasion=candidates[(int)(ticket%(uint)candidates.Count)];
  var group=MasterDataUtil.GetEnemyGroupParamFromID(invasion.enemy_group_id);
  if(group==null)throw new InvalidOperationException("Missing invasion enemy group "+invasion.enemy_group_id);
  if(floor==null)throw new InvalidOperationException("Missing invasion floor seed");
  return group;
 }
 // Ignore only dates, as explicitly requested for permanent offline invasions.
 // Keep active flags, quest scope, player group and prerequisite clear checks.
 static List<MasterDataGuerrillaBoss> PermanentInvasionCandidates291(uint questID) {
  var result=new List<MasterDataGuerrillaBoss>();
  var quest=MasterDataUtil.GetQuestParamFromID(questID);
  var master=Singleton<MasterDataManager>.Instance;
  if(quest==null || quest.active!=2 || master==null || master.m_MasterDataGuerrillaBoss==null)return result;
  int userGroup=SystemUtil.GetUserGroup();var seen=new HashSet<uint>();
  var server=Singleton<ServerForgery>.Instance;
  var save=server==null?null:Local291Player(server);
  foreach(var r in master.m_MasterDataGuerrillaBoss) {
   if(r==null || r.active!=2 || r.quest_id!=questID || (r.user_group!=0 && r.user_group!=userGroup))continue;
   if(r.quest_id_must!=0 && (save==null || !ServerDataUtil.ChkBitFlag(ref save.flag_quest_clear,r.quest_id_must)))continue;
   if(seen.Add(r.enemy_group_id))result.Add(r);
  }
  return result;
 }
 public static MasterDataGuerrillaBoss PermanentInvasion291(uint questID) {
  var rows=PermanentInvasionCandidates291(questID);
  return rows.Count==0?null:rows[0];
 }
 public static void ValidatePermanentInvasions291(ServerForgery server) {
  var rates=new Dictionary<uint,int>();
  foreach(var r in server.m_MasterDataGuerrillaBoss) {
   if(r==null || r.active!=2)continue;
   var quest=MasterDataUtil.GetQuestParamFromID(r.quest_id);
   if(quest==null || quest.active!=2)continue;
   int prior;
   if(r.enemy_rate<0 || r.enemy_rate>100)throw new InvalidOperationException("Invalid invasion chance");
   if(rates.TryGetValue(r.quest_id,out prior) && prior!=r.enemy_rate)
    throw new InvalidOperationException("Conflicting invasion rates require an explicit mapping: "+r.quest_id);
   rates[r.quest_id]=r.enemy_rate;
   if(MasterDataUtil.GetEnemyGroupParamFromID(r.enemy_group_id)==null)
    throw new InvalidOperationException("Missing permanent invasion group "+r.enemy_group_id);
  }
 }
 public static uint[] PermanentInvasionPickup291(uint[] original,uint questID) {
  if(original==null)return null;
  var candidates=PermanentInvasionCandidates291(questID);
  if(candidates.Count==0)return original;
  var units=new HashSet<uint>();foreach(uint id in original)units.Add(id);
  var list=new TemplateList<uint>();list.Alloc(256);
  foreach(var r in candidates) {
   var group=MasterDataUtil.GetEnemyGroupParamFromID(r.enemy_group_id);
   if(group==null)throw new InvalidOperationException("Missing permanent pickup group");
   MasterDataUtil.GetQuestPickupUnitLoop(group,ref list);
  }
  var additional=list.ToArray();
  if(additional!=null)foreach(uint id in additional)if(id!=0)units.Add(id);
  var result=new List<uint>(units);result.Sort();return result.ToArray();
 }

}
