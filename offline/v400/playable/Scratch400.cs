// Local presentation for the native 4.0.0 scratch list. Pool economics and
// tutorial state remain owned by the original game and original 1.0.6 tables.
using System;
using UnityEngine;
using Object = UnityEngine.Object;
public static partial class DivineGateOffline {
 const string scratchPrefix400="offline400/gacha/";
 static Texture2D scratchTexture400;
 static MasterDataGacha ScratchPool400(string key) {
  if(String.IsNullOrEmpty(key) || !key.StartsWith(scratchPrefix400,StringComparison.Ordinal))return null;
  uint id;
  if(!UInt32.TryParse(key.Substring(scratchPrefix400.Length),out id) || key!=scratchPrefix400+id.ToString())return null;
  var pool=MasterDataUtil.GetGachaParamFromID(id);
  return pool!=null && pool.url_img==key ? pool : null;
 }
 public static string ScratchRequest400(string key,bool forced) {
  return ScratchPool400(key)!=null ? key : String.Empty;
 }
 public static Texture2D ScratchTexture400(string key) {
  if(ScratchPool400(key)==null)return null;
  if(scratchTexture400==null) {
   // A small native UI background, not an unavailable advertising banner.
   scratchTexture400=new Texture2D(2,2);
   scratchTexture400.name="DG400 local scratch card";
   scratchTexture400.SetPixel(0,0,new Color(.08f,.025f,.10f,1));
   scratchTexture400.SetPixel(1,0,new Color(.13f,.04f,.16f,1));
   scratchTexture400.SetPixel(0,1,new Color(.20f,.045f,.24f,1));
   scratchTexture400.SetPixel(1,1,new Color(.09f,.025f,.12f,1));
   scratchTexture400.Apply();
   scratchTexture400.wrapMode=TextureWrapMode.Clamp;
   Object.DontDestroyOnLoad(scratchTexture400);
  }
  return scratchTexture400;
 }
 static void ScratchLabel400(Transform parent,UIFont font,int depth,string name,string text,float y,float size,Color color,int lines) {
  var obj=new GameObject(name);obj.layer=parent.gameObject.layer;
  obj.transform.parent=parent;obj.transform.localPosition=new Vector3(0,y,-1);
  obj.transform.localRotation=Quaternion.identity;obj.transform.localScale=new Vector3(size,size,1);
  var label=obj.AddComponent<UILabel>();label.font=font;label.pivot=UIWidget.Pivot.Center;
  label.depth=depth;label.color=color;label.lineWidth=440;label.maxLineCount=lines;
  label.effectStyle=UILabel.Effect.Outline;label.effectColor=Color.black;label.text=text??String.Empty;
 }
 public static void SetupScratchCard400(MainMenuPartsGachaItem item,MasterDataGacha pool) {
  if(item==null || pool==null || ScratchPool400(pool.url_img)==null)return;
  var image=UnityUtil.GetChildNode(item.gameObject,"URLTexture");
  if(image==null)throw new InvalidOperationException("Scratch card has no URLTexture");
  if(image.transform.Find("OfflineScratchCard")!=null)return;
  var button=UnityUtil.GetChildNode(item.gameObject,"ButtonOK");
  var source=UnityUtil.GetChildNode(button,"ButtonMsg").GetComponent<UILabel>();
  var texture=image.GetComponent<UITexture>();
  if(source==null || source.font==null || texture==null)throw new InvalidOperationException("Scratch card font/texture contract changed");
  var root=new GameObject("OfflineScratchCard");root.layer=image.layer;
  root.transform.parent=image.transform;root.transform.localPosition=new Vector3(0,0,-1);
  root.transform.localRotation=Quaternion.identity;
  var scale=image.transform.localScale;
  root.transform.localScale=new Vector3(1/scale.x,1/scale.y,1);
  int depth=texture.depth+1;
  ScratchLabel400(root.transform,source.font,depth,"Title",pool.name,95,34,Color.yellow,1);
  ScratchLabel400(root.transform,source.font,depth,"Description",pool.detail,22,24,Color.white,3);
  string price=(pool.type==1 ? "友情ポイント" : "チップ")+" : "+pool.price+" / 1回";
  ScratchLabel400(root.transform,source.font,depth,"Price",price,-65,22,Color.white,1);
 }
 public static bool ScratchDetail400(MainMenuPartsGachaItem item) {
  if(item==null || !Singleton<DialogManager>.HasInstance)return false;
  var pool=item.GetMasterData();if(pool==null)return false;
  var status=new MultiDialogStatus();status.SetParamDialog1B(pool.name,pool.detail,"BTN_OK",true);
  status.backkey_callbtn=EDIALOG_BTN.eDIALOG_BTN_01;status.backkey_se=SEID.SE_MENU_RET;
  Singleton<DialogManager>.Instance.DialogRequestOpen(status);
  SoundUtil.PlaySE(SEID.SE_MENU_OK);return true;
 }
}
