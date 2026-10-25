// Version-specific data setup; original APK and backup remain immutable.
using System;
using System.Collections.Generic;
using UnityEngine;
using ServerDataDefine;
public static partial class DivineGateOffline {
 static ResidentManager pendingResident291;
 public static void QueueBoot291(ResidentManager resident) { pendingResident291=resident;Tick291(); }
 public static void Tick291() {
  if(pendingResident291!=null && Singleton<ServerForgery>.Instance==null &&
     Singleton<MasterDataManager>.Instance!=null && Singleton<TimeManager>.Instance!=null &&
     Singleton<LocalSaveManager>.Instance!=null)Bootstrap(pendingResident291);
  PumpNativeAssets();
  AuditNative291();
 }
 static AssetBundleResource auditDoor291, auditCharacter291;
 static bool auditQueued291, auditDone291;
 public static void AuditNative291() {
  if(auditDone291 || Singleton<ServerForgery>.Instance==null || Singleton<MasterDataManager>.Instance==null)return;
  if(!auditQueued291) {
   var area=MasterDataUtil.GetAreaParamFromID(10);
   var character=MasterDataUtil.GetCharaParamFromID(10);
   if(area==null || character==null)return;
   auditDoor291=RequestNative("",area.mesh_door,default(ASSETBUNDLE_TAG),typeof(GameObject));
   auditCharacter291=RequestNative("","chara_prefab_"+character.res_chara_id_str,default(ASSETBUNDLE_TAG),typeof(GameObject));
   auditQueued291=true;
  }
  if(auditDoor291==null || auditCharacter291==null) {auditDone291=true;Debug.LogError("[DG291 audit] Native routes missing");return;}
  if(auditDoor291.m_ResultStep==ASSETBUNDLE_STEP.LOAD_LOADING || auditCharacter291.m_ResultStep==ASSETBUNDLE_STEP.LOAD_LOADING)return;
  auditDone291=true;
  var door=auditDoor291.m_ResultObject as GameObject;
  var body=auditCharacter291.m_ResultObject as GameObject;
  int clips=0;bool sampledMotion=false;
  if(door!=null) {
   foreach(AnimationArray a in door.GetComponentsInChildren<AnimationArray>(true))
    if(a.m_Clips!=null)foreach(AnimationClip clip in a.m_Clips)
     if(clip!=null) {
      clips++;
      if(clip.name.IndexOf("door_open")<0 || clip.length<=0)continue;
      // Test the real clip on a temporary off-screen clone, never a player's map.
      var probe=UnityEngine.Object.Instantiate(door,new Vector3(10000,10000,10000),Quaternion.identity) as GameObject;
      var playback=probe.GetComponent<Animation>();
      if(playback[clip.name]==null)playback.AddClip(clip,clip.name);
      playback.Play(clip.name);playback[clip.name].time=0;playback.Sample();
      var nodes=probe.GetComponentsInChildren<Transform>(true);
      var positions=new Vector3[nodes.Length];var rotations=new Quaternion[nodes.Length];
      for(int i=0;i<nodes.Length;i++){positions[i]=nodes[i].localPosition;rotations[i]=nodes[i].localRotation;}
      playback[clip.name].time=clip.length*0.5f;playback.Sample();
      for(int i=0;i<nodes.Length;i++)if(positions[i]!=nodes[i].localPosition || rotations[i]!=nodes[i].localRotation)sampledMotion=true;
      UnityEngine.Object.Destroy(probe);
     }
  }
  bool nativeBody=body!=null && body.GetComponent<DGNativeCharacterMarker>()!=null;
  if(clips>0 && sampledMotion && nativeBody)Debug.Log("[DG291 audit] Native tutorial door clips="+clips+"; sampled door motion=True; native evolved character=True; no player scene or save writes.");
  else Debug.LogError("[DG291 audit] Native content failed: door clips="+clips+"; sampled motion="+sampledMotion+"; character="+nativeBody);

 }
 public static void Ready291(ServerForgery server) {
  DG291Data.Apply(server);
  GroupGachaAssignments291(server);
  ConfigurePools291(server);
  ConfigureScratch(server);
  DG291Data.Share(server, Singleton<MasterDataManager>.Instance);
  ValidatePermanentInvasions291(server);
  MasterDataUtil.ConvertMasterSkillLimitBreak(ref server.m_MasterDataSkillLimitBreak);
  MasterDataUtil.ConvertMasterSkillBoost(ref server.m_MasterDataSkillBoost);
  MasterDataUtil.ConvertMasterEnemyAbility(ref server.m_MasterDataEnemyAbility);
  RegisterLegacySkills291(server);
  OpenLocalEvents(Singleton<MasterDataManager>.Instance);

  server.UserParamLoad();
  Debug.Log("[DG291 DGDB] Fixed-content master data loaded: 595 characters / 399 quest rows.");
 }
 // Rewritten by the Cecil patch to call a narrow private-save accessor.
 public static ServerForgerySaveUser Local291Player(ServerForgery server) { return null; }
 public static void Normalize291(ServerForgery server) {
  ServerForgerySaveUser save = Local291Player(server);
  if (save == null) return;
  if (save.present_list == null) save.present_list = new PacketStructPresent[0];

 }
 // Original 2.9.1 samples every active row between a pool's first/last row.
 // Keep every donor row and its within-pool order, including zero weights.
 public static void GroupGachaAssignments291(ServerForgery server) {
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
 // 2.9.1 uses the Android-specific counter key; the old shared runtime does not.
 public static string PatchText291(string key) {
  if(key=="DATA_UPDATE_COUNTER_ANDROID")return "offline291-dgdb-1";
  return PatchText(key);
 }
 public static void LoadLocalAssets291(TitleMenuSeqLoadMasterAndAssetBundle page,bool ready) {
  if(!ready || Singleton<ServerForgery>.Instance==null || Singleton<ServerForgery>.Instance.m_MasterDataQuest==null)return;
  Singleton<ResidentResourceManager>.Instance.LoadResidentResourceRequest();
  // Match original ExecSeq_Max: acknowledge the data loaded by this title flow.
  // This repairs an empty/stale marker without changing SERVER_SAVE.
  Singleton<LocalSaveManager>.Instance.SaveFuncUpdateCounter(Singleton<PatchManager>.Instance.GetPatchText("DATA_UPDATE_COUNTER_ANDROID"));
  Singleton<LocalSaveManager>.Instance.SaveFuncAssetBundleInit(LocalSaveManager.ASSETBUNDLE_INIT.ASSETBUNDLE_INIT_CACHE);
  Singleton<TitleMenuManager>.Instance.AddSwitchRequest(TITLEMENU_SEQ.SEQ_SWITCH_PAGE,false);
 }

 public static MAINMENU_SEQ ResolvePage291(MAINMENU_SEQ page) {
  if(page!=MAINMENU_SEQ.SEQ_ACHIEVEMENT_CLEAR)return page;
  // Unsupported achievement presentation is a transit page, not a dead end.
  // Clear only its in-memory pending UI notifications; no player/save writes.
  ResidentParam.m_AchievementClear=null;ResidentParam.m_AchievementNew=null;
  var next=MainMenuParam.m_AchievementClear_NextPage;
  if(next==MAINMENU_SEQ.SEQ_ACHIEVEMENT_CLEAR || !AllowPage291(next) || next==0)
   return MAINMENU_SEQ.SEQ_SWITCH_PAGE;
  return next;
 }
 public static bool AllowPage291(MAINMENU_SEQ page) {
  string name=page.ToString();
  if(name=="SEQ_SWITCH_WEBVIEW_PAGE" || name.Contains("ACHIEVEMENT") || name.Contains("LOGIN_SCRATCH") || name.Contains("INFORMATION"))return false;
  return true;
 }
}
