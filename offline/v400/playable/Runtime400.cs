// Version-specific data setup; original APK and backup remain immutable.
using System;
using System.Collections.Generic;
using UnityEngine;
using ServerDataDefine;
public static partial class DivineGateOffline {
 // The memorial backend's lookup always returns row zero. With the original
 // 1.0.6 sentinel row this produces an empty enemy pool and a null dereference.
 public static MasterDataEnemyGroup EnemyGroup400(uint id) {
  if(id==0 || !Singleton<ServerForgery>.HasInstance)return null;
  var server=Singleton<ServerForgery>.Instance;
  if(server==null || server.m_MasterDataParamEnemyGroup==null)return null;
  foreach(var group in server.m_MasterDataParamEnemyGroup)
   if(group!=null && group.fix_id==id)return group;
  return null;
 }
 static ResidentManager pendingResident400;
 public static void QueueBoot400(ResidentManager resident) { pendingResident400=resident;Tick400(); }
 public static void Tick400() {
  if(pendingResident400!=null && Singleton<ServerForgery>.Instance==null &&
     Singleton<MasterDataManager>.Instance!=null && Singleton<TimeManager>.Instance!=null &&
     Singleton<LocalSaveManager>.Instance!=null)Bootstrap(pendingResident400);
  PumpNativeAssets();

 }
 public static void Ready400(ServerForgery server) {
  DG400Data.Apply(server);
  GroupGachaAssignments400(server);


  DG400Data.Share(server, Singleton<MasterDataManager>.Instance);
  MasterDataUtil.ConvertMasterSkillLimitBreak(ref server.m_MasterDataSkillLimitBreak);
  RegisterLegacySkills400(server);
  OpenLocalEvents(Singleton<MasterDataManager>.Instance);

  server.UserParamLoad();
  Debug.Log("[DG400] Original 1.0.6 master data loaded: 522 characters / 371 quest rows.");
 }
 // Rewritten by the Cecil patch to call a narrow private-save accessor.
 public static ServerForgerySaveUser Local400Player(ServerForgery server) { return null; }
 public static void Normalize400(ServerForgery server) {
  ServerForgerySaveUser save = Local400Player(server);
  if (save == null) return;
  if (save.present_list == null) save.present_list = new PacketStructPresent[0];

 }
 // Original 4.0.0 samples every active row between a pool's first/last row.
 // Keep every donor row and its within-pool order, including zero weights.
 public static void GroupGachaAssignments400(ServerForgery server) {
  var groups = new Dictionary<uint,List<MasterDataGachaAssign>>();
  var order = new List<uint>();
  foreach(var row in server.m_MasterDataGachaAssign) {
   if(!groups.ContainsKey(row.gacha_id)) {
    groups.Add(row.gacha_id,new List<MasterDataGachaAssign>());order.Add(row.gacha_id);
   }
   groups[row.gacha_id].Add(row);
  }
  var result = new List<MasterDataGachaAssign>();
  foreach(uint id in order)result.AddRange(groups[id]);
  server.m_MasterDataGachaAssign=result.ToArray();
 }
 // 4.0.0 uses the Android-specific counter key; the old shared runtime does not.
 public static string PatchText400(string key) {
  if(key=="DATA_UPDATE_COUNTER_ANDROID")return "offline400-1";
  return PatchText(key);
 }
 public static void LoadLocalAssets400(TitleMenuSeqLoadMasterAndAssetBundle page,bool ready) {
  if(!ready || Singleton<ServerForgery>.Instance==null || Singleton<ServerForgery>.Instance.m_MasterDataQuest==null)return;
  Singleton<ResidentResourceManager>.Instance.LoadResidentResourceRequest();
  // Match original ExecSeq_Max: acknowledge the data loaded by this title flow.
  // This repairs an empty/stale marker without changing SERVER_SAVE.
  Singleton<LocalSaveManager>.Instance.SaveFuncUpdateCounter(Singleton<PatchManager>.Instance.GetPatchText("DATA_UPDATE_COUNTER_ANDROID"));
  Singleton<LocalSaveManager>.Instance.SaveFuncAssetBundleInit(LocalSaveManager.ASSETBUNDLE_INIT.ASSETBUNDLE_INIT_CACHE);
  Singleton<TitleMenuManager>.Instance.AddSwitchRequest(TITLEMENU_SEQ.SEQ_SWITCH_PAGE,false);
 }

 public static bool AllowPage400(MAINMENU_SEQ page) {
  string name=page.ToString();
  if(name=="SEQ_SWITCH_WEBVIEW_PAGE" || name.Contains("ACHIEVEMENT") || name.Contains("LOGIN_SCRATCH") || name.Contains("INFORMATION") || name.Contains("LINK") || name.Contains("LIMIT_OVER") || name.Contains("SHOP_BUY_TIP"))return false;
  return true;
 }
}
