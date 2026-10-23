// Version-specific data setup; original APK and backup remain immutable.
using System;
using System.Collections.Generic;
using UnityEngine;
using ServerDataDefine;
public static partial class DivineGateOffline {
 public static void Ready109(ServerForgery server) {
  DG109Data.Apply(server);
  GroupGachaAssignments109(server);
  DG109Data.Share(server, Singleton<MasterDataManager>.Instance);
  MasterDataUtil.ConvertMasterSkillLimitBreak(ref server.m_MasterDataSkillLimitBreak);
  OpenLocalEvents(Singleton<MasterDataManager>.Instance);
  OpenLocalCollabPools(Singleton<MasterDataManager>.Instance);
  server.UserParamLoad();
  Debug.Log("[DG109] Backup master data loaded: 595 characters / 399 quest rows.");
 }
 // Rewritten by the Cecil patch to call a narrow private-save accessor.
 public static ServerForgerySaveUser Local109Player(ServerForgery server) { return null; }
 public static void Normalize109(ServerForgery server) {
  ServerForgerySaveUser save = Local109Player(server);
  if (save == null) return;
  if (save.present_list == null) save.present_list = new PacketStructPresent[0];
  save.monthly_present = null;
 }
 // Original 1.0.9 samples every active row between a pool's first/last row.
 // Keep every donor row and its within-pool order, including zero weights.
 public static void GroupGachaAssignments109(ServerForgery server) {
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
 public static void SetupFreeChipList109() {
  StoreParam.m_StoreProductEventRenew=false;
  StoreParam.m_StoreProductBase=new StoreProduct[6];
  StoreParam.m_StoreProductChanged=new StoreProduct[6];
  for(int i=0;i<6;i++)StoreParam.m_StoreProductBase[i]=FreeChipProductAt(i);
 }
 public static bool LocalShopBefore109() {
  SetupFreeChipList109();
  return false;
 }
 public static void SetupFreeChipProducts109(StoreCommonUI ui) {
  SetupFreeChipList109();
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
