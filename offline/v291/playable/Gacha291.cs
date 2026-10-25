using System;
using UnityEngine;
using ServerDataDefine;
using Object=UnityEngine.Object;
public static partial class DivineGateOffline {
 static bool collabMode291;
 static readonly System.Collections.Generic.Dictionary<uint,uint> poolArt291=new System.Collections.Generic.Dictionary<uint,uint>();
 static readonly uint[] collabPools291={300100,300200,300300,300400};
 static readonly string[] collabNames291={"FAIRY TAIL","進撃の巨人","ケリ姫スイーツ","とある魔術の禁書目録"};
 static GameObject[] GachaLinks291(MainMenuSeqGachaRootNew page){throw new Exception("Bridge");}
 static int GachaWindow291(MainMenuSeqGachaRootNew page){throw new Exception("Bridge");}
 static void RefreshGacha291(MainMenuSeqGachaRootNew page){throw new Exception("Bridge");}
 public static void ConfigurePools291(ServerForgery server) {
  poolArt291.Clear();
  foreach(var pool in server.m_MasterDataGacha) {
   foreach(var row in server.m_MasterDataGachaAssign)if(row.gacha_id==pool.fix_id && row.active==2 && row.assign_rate>0){poolArt291[pool.fix_id]=row.assign_chara_id;break;}
   pool.event_id=0;pool.url_img="";pool.url_web="";pool.change_title="";
   if(pool.fix_id==100100){pool.tab_name="友情";pool.priority_show=0;}
   if(pool.fix_id==200100){pool.tab_name="レア";pool.priority_show=1;}
   for(int i=0;i<4;i++)if(pool.fix_id==collabPools291[i]){
    pool.name=collabNames291[i];pool.tab_name=new[]{"FT","進撃","ケリ姫","魔禁"}[i];pool.priority_show=4-i;
   }
  }
 }
 public static MasterDataGacha[] LocalGachaTabs291() {
  var tabs=new MasterDataGacha[4];
  if(SupportTutorialActive()){tabs[1]=MasterDataUtil.GetGachaParamFromID(10);return tabs;}
  if(collabMode291)for(int i=0;i<4;i++)tabs[i]=MasterDataUtil.GetGachaParamFromID(collabPools291[i]);
  else {tabs[0]=MasterDataUtil.GetGachaParamFromID(100100);tabs[1]=MasterDataUtil.GetGachaParamFromID(200100);}
  return tabs;
 }
 public static bool UpdateGachaMode291(MainMenuSeqGachaRootNew page) {
  RenderLocalGacha291(page);
  if(SupportTutorialActive() || MainMenuParam.m_IsDialog || Singleton<DialogManager>.Instance.CheckDialogActive())return false;
  var input=Input2DManager.Instance;var links=GachaLinks291(page);
  bool back=input.m_BackButonInputTrigger && collabMode291;
  if(!back && !(input.m_TouchObjectReleaseEqualTriger && input.m_TouchObjectRelease==links[0]))return false;
  collabMode291=back?false:!collabMode291;
  MainMenuParam.m_GachaPlayed=false;MainMenuParam.m_GachaWindowType=-1;
  RefreshGacha291(page);SoundUtil.PlaySE(SEID.SE_MENU_OK);return true;
 }
 public static void RenderLocalGacha291(MainMenuSeqGachaRootNew page) {
  var links=GachaLinks291(page);int window=GachaWindow291(page);
  if(links==null || window<0 || window>3 || links[6+window]==null)return;
  var pool=LocalGachaTabs291()[window];if(pool==null)return;
  Singleton<LoadingManager>.Instance.RequestLoadingFinish(LOADING_TYPE.SCRATCH);
  UnityUtil.SetObjectEnabled(links[26],true);
  var player=Singleton<UserDataAdmin>.Instance.m_StructPlayer;
  uint count=(pool.type==1?player.have_friend_pt:player.have_stone)/pool.price;
  UnityUtil.SetObjectEnabled(links[24],count>0);
  if(count>0)UnityUtil.SetUILabelValue(links[25],count);

  // Downloaded campaign artwork is absent. Reuse an owned old portrait and
  // native label; never request a server banner or invent an official banner.
  var picture=links[6+window];picture.SetActive(true);
  uint id=poolArt291[pool.fix_id];
  var character=MasterDataUtil.GetCharaParamFromID(id);
  Texture2D texture=character==null?null:CharacterTexture("chara_"+character.res_chara_id_str);
  if(picture.renderer!=null && texture!=null)picture.renderer.material.SetTexture("_MainTex",texture);
  picture.transform.localPosition=new Vector3(0,-500,0);
  picture.transform.localScale=new Vector3(400,300,1);
  var parent=picture.transform.parent;
  var detail=UnityUtil.GetChildNode(parent.gameObject,"DGLocalPoolText");
  if(detail==null) {
   detail=Object.Instantiate(links[10+window]) as GameObject;detail.name="DGLocalPoolText";
   detail.transform.parent=parent;detail.transform.localRotation=Quaternion.identity;
   detail.transform.localPosition=new Vector3(0,-290,-160);detail.transform.localScale=new Vector3(21,21,1);
   foreach(var setter in detail.GetComponentsInChildren<LabelSetter>(true)){setter.m_LabelTextKey="";setter.enabled=false;}
   var label=detail.GetComponent<UILabel>();label.pivot=UIWidget.Pivot.Center;label.lineWidth=560;label.maxLineCount=0;
   BindCollabWidgets(detail,parent.gameObject.layer);
  }
  ScratchOdds odds;
  string info=pool.fix_id==10?"チュートリアル":pool.type==1?"友情ポイント "+pool.price+" / 1回":"1回 5枚・最大9連";
  if(scratchOdds.TryGetValue(pool.fix_id,out odds)){
   uint hundredths=(uint)(10000.0*FiveStarMultiplier*odds.five/odds.total+0.5);
   info="★5確率 "+(hundredths/100)+"."+(hundredths%100<10?"0":"")+(hundredths%100)+"%（3倍）\n"+info;
  }
  UnityUtil.SetUILabelValue(detail,pool.name+"\n"+info);
  UnityUtil.SetObjectEnabled(links[0],!SupportTutorialActive());
  var buttonLabel=UnityUtil.GetChildNode(links[0],"ButtonMsg");
  if(buttonLabel!=null)UnityUtil.SetUILabelValue(buttonLabel,collabMode291?"通常に戻る":"コラボを選択");
 }
 // Warehouse source control uses the same proven NGUI registration/reset.
 private static void BindCollabWidgets(GameObject root,int layer) {
  root.layer=layer;var widget=root.GetComponent<UIWidget>();
  if(widget!=null){widget.enabled=true;widget.alpha=1;widget.ParentHasChanged();widget.CreatePanel();}
  for(int i=0;i<root.transform.GetChildCount();i++)BindCollabWidgets(root.transform.GetChild(i).gameObject,layer);
 }
 private static void ResetCollabCardAnimation(GameObject card) {
  var button=card.GetComponent<UIButtonScale>();if(button==null || !button.enabled)return;
  card.SendMessage("OnHover",false,SendMessageOptions.DontRequireReceiver);
  if(button.tweenTarget==null)return;var tween=button.tweenTarget.GetComponent<TweenScale>();
  if(tween==null)return;tween.enabled=false;button.tweenTarget.localScale=tween.to;tween.from=tween.to;
 }
}
