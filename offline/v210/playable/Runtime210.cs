// Version-specific data setup; original APK and backup remain immutable.
using System;
using System.Collections.Generic;
using UnityEngine;
using ServerDataDefine;
public static partial class DivineGateOffline {
 public static void Ready210(ServerForgery server) {
  DG210Data.Apply(server);
  GroupGachaAssignments210(server);
  DG210Data.Share(server, Singleton<MasterDataManager>.Instance);
  MasterDataUtil.ConvertMasterSkillLimitBreak(ref server.m_MasterDataSkillLimitBreak);
  RegisterLegacySkills210(server);
  OpenLocalEvents(Singleton<MasterDataManager>.Instance);
  OpenLocalCollabPools(Singleton<MasterDataManager>.Instance);
  server.UserParamLoad();
  Debug.Log("[DG210] Backup master data loaded: 595 characters / 399 quest rows.");
 }
 // Rewritten by the Cecil patch to call a narrow private-save accessor.
 public static ServerForgerySaveUser Local210Player(ServerForgery server) { return null; }
 public static void Normalize210(ServerForgery server) {
  ServerForgerySaveUser save = Local210Player(server);
  if (save == null) return;
  if (save.present_list == null) save.present_list = new PacketStructPresent[0];

 }
 // Original 2.1.0 samples every active row between a pool's first/last row.
 // Keep every donor row and its within-pool order, including zero weights.
 public static void GroupGachaAssignments210(ServerForgery server) {
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
 private static void OpenLocalCollabPools(MasterDataManager master) {
  foreach(var pool in master.m_MasterDataGacha) {
   if(pool.type==3 && pool.fix_id>=300100 && pool.fix_id<=300400)pool.event_id=0;
   if(pool.url_img==null)pool.url_img="";
   if(pool.url_web==null)pool.url_web="";
  }
 }
 public static void LoadLocalAssets210(TitleMenuSeqLoadMasterAndAssetBundle page,bool ready) {
  if(!ready || Singleton<ServerForgery>.Instance==null || Singleton<ServerForgery>.Instance.m_MasterDataQuest==null)return;
  Singleton<ResidentResourceManager>.Instance.LoadResidentResourceRequest();
  Singleton<LocalSaveManager>.Instance.SaveFuncAssetBundleInit(LocalSaveManager.ASSETBUNDLE_INIT.ASSETBUNDLE_INIT_CACHE);
  Singleton<TitleMenuManager>.Instance.AddSwitchRequest(TITLEMENU_SEQ.SEQ_SWITCH_PAGE,false);
 }
 public static void SetupFreeChipList210() {
  StoreParam.m_StoreProductEventRenew=false;
  StoreParam.m_StoreProductBase=new StoreProduct[6];
  StoreParam.m_StoreProductChanged=new StoreProduct[6];
  for(int i=0;i<6;i++)StoreParam.m_StoreProductBase[i]=FreeChipProductAt(i);
 }
 public static bool LocalShopBefore210() {
  SetupFreeChipList210();
  return false;
 }
 public static void SetupFreeChipProducts210(StoreCommonUI ui) {
  SetupFreeChipList210();
  for(int i=0;i<6;i++) {
   var product=FreeChipProductAt(i);ui.m_ShopItems[i]=product;
   var root=ui.m_LinkerObject[i];
   if(root==null)continue;
   SetupFreeChipProduct(root,product,null,"");
   foreach(string name in new[]{"StoneDelPrice","StonePrePrice","StoneArrowPrice","StoneDelNum","StoneArrowNum","StonePreNum","StoneRemainDay"}) {
    var node=UnityUtil.GetChildNode(root,name);if(node!=null)node.SetActive(false);
   }
   var tip=UnityUtil.GetChildNode(root,"StoneTip");
   if(tip!=null)UnityUtil.SetUILabelValue(tip,"FREE");
  }
 }
}
