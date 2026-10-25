using System;
using System.Collections.Generic;
using LitJson;
using ServerDataDefine;

[Serializable] public sealed class DG291SkillAdjustment {
 public long unique_id; public uint character_id, before, after;
}
[Serializable] public sealed class DG291MigrationReport {
 public string source_version, source_checksum, target_data;
 public DG291SkillAdjustment[] skill_adjustments;
}
public static partial class DivineGateOffline {
 private static DGSavePayload PrepareMigration291(ServerForgery server,DGSaveEnvelope envelope,DGSavePayload payload) {
  ValidateIntegralSaveNumbers(payload.server_save);
  LegacyShape106(JsonMapper.ToObject(payload.server_save),typeof(ServerForgerySaveUser));
  var save=JsonMapper.ToObject<ServerForgerySaveUser>(payload.server_save);
  if(save==null)throw new InvalidOperationException("Missing account.");
  bool legacy=envelope.game_version!="2.9.1";
  var adjustments=new List<DG291SkillAdjustment>();
  if(save.unit_list!=null)foreach(var unit in save.unit_list) {
   if(unit==null)throw new InvalidOperationException("Missing unit.");
   MasterDataParamChara character=null;
   foreach(var c in server.m_MasterDataParamChara)if(c!=null && c.fix_id==unit.id){character=c;break;}
   if(character==null || unit.id==0)throw new InvalidOperationException("Unit outside fixed content.");
   MasterDataSkillLimitBreak skill=null;
   foreach(var a in server.m_MasterDataSkillLimitBreak)if(a.fix_id==character.skill_limitbreak){skill=a;break;}
   uint max=skill==null?0:(uint)skill.level_max;
   uint sourceMax;
   if(legacy && (!OldSkillMaximum291.TryGetValue(unit.id,out sourceMax) || unit.limitbreak_lv>sourceMax))
    throw new InvalidOperationException("Invalid source skill level.");
   if(unit.limitbreak_lv>max) {
    if(!legacy)throw new InvalidOperationException("Invalid DGDB skill level.");
    adjustments.Add(new DG291SkillAdjustment{unique_id=unit.unique_id,character_id=unit.id,before=unit.limitbreak_lv,after=max});
    unit.limitbreak_lv=max;
   }
  }
  ValidateContentFlags291(server,save.flag_unit_get,false);
  ValidateContentFlags291(server,save.flag_unit_check,false);
  ValidateContentFlags291(server,save.flag_quest_clear,true);
  ValidateContentFlags291(server,save.flag_quest_check,true);
  if(save.present_list!=null)foreach(var gift in save.present_list) {
   if(gift==null)throw new InvalidOperationException("Missing gift.");
   if(gift.present_type==7) {
    MasterDataParamChara c=null;
    foreach(var r in server.m_MasterDataParamChara)if(r.fix_id==gift.present_value0){c=r;break;}
    if(c==null || c.fix_id==0 || gift.present_value1==0 || gift.present_value1>c.level_max)
     throw new InvalidOperationException("Gift outside fixed content.");
   }
  }
  if(legacy) {
   payload.server_save=JsonMapper.ToJson(save);
   payload.migration_report=JsonMapper.ToJson(new DG291MigrationReport{
    source_version=envelope.game_version,source_checksum=envelope.sha256,target_data="dgdb-1",skill_adjustments=adjustments.ToArray()});
  }
  return payload;
 }
 private static void ValidateContentFlags291(ServerForgery server,byte[] flags,bool quests) {
  if(flags==null)return;
  if(flags.Length>20000)throw new InvalidOperationException("Progress flags too large.");
  for(uint i=1;i<flags.Length*8;i++) {
   if((flags[i/8]&(1<<(int)(i%8)))==0)continue;
   bool known=false;
   if(quests) {foreach(var q in server.m_MasterDataQuest)if(q.fix_id==i){known=true;break;}}
   else {foreach(var c in server.m_MasterDataParamChara)if(c.fix_id==i){known=true;break;}}
   if(!known)throw new InvalidOperationException("Progress outside fixed content: "+i);
  }
 }

}
