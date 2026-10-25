using UnityEngine;
public static partial class DivineGateOffline {
 public static string ValidateScratchCapacity291(ServerForgery server,ref PacketData packet,string json) {
  var request=LitJson.JsonMapper.ToObject<ServerDataDefine.SendGachaPlay>(json);
  var save=Local291Player(server);if(request==null || save==null)return null;
  int used=save.unit_list==null?0:save.unit_list.Length;
  int code=0;
  if(request.gacha_ct==0 || request.gacha_ct>9)code=8194;
  else if((ulong)used+request.gacha_ct>save.unit_total)code=(int)ServerDataDefine.API_CODE.API_CODE_WIDE_API_UNIT_EXCESS;
  if(code==0)return null;
  var response=new ServerDataDefine.RecvGachaPlay();
  response.header=new ServerDataDefine.PacketStructHeaderRecv();
  response.header.packet_unique_id=packet.m_PacketUniqueNum;response.header.code=code;
  return LitJson.JsonMapper.ToJson(response);
 }
 public static void SetupFreeChipList291() {
  StoreParam.m_StoreProductEventRenew=false;
  StoreParam.m_StoreProductBase=new StoreProduct[6];
  StoreParam.m_StoreProductChanged=new StoreProduct[6];
  for(int i=0;i<6;i++)StoreParam.m_StoreProductBase[i]=FreeChipProductAt(i);
 }
 public static bool LocalShopBefore291() {
  SetupFreeChipList291();
  return false;
 }
 public static void SetupFreeChipProducts291(StoreCommonUI ui) {
  SetupFreeChipList291();
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
