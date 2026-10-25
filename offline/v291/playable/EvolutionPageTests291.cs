using System;using System.Reflection;using ServerDataDefine;using LitJson;using UnityEngine;
class EvolutionPageTests291 {
 static readonly BindingFlags fields=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance;
 static object Call(object o,string n,params object[] a){return o.GetType().GetMethod(n,fields).Invoke(o,a);}
 static void Set(object o,string n,object v){o.GetType().GetField(n,fields).SetValue(o,v);}
 static T Get<T>(object o,string n){return (T)o.GetType().GetField(n,fields).GetValue(o);}
 static void Check(bool b,string m){if(!b)throw new Exception(m);}
 static ServerForgery server;static ServerDataManager network;static MainMenuManager menu;
 static GameObject[] links;
 static string Snapshot(){return JsonMapper.ToJson(DivineGateOffline.Local291Player(server))+"\n"+LocalSaveUtil.GetValueString("SERVER_SAVE","");}
 static MainMenuSeqPartyEvolveQuestFix Page(){
  var p=new MainMenuSeqPartyEvolveQuestFix();p.m_MainMenuSeqStartOK=true;
  links=new GameObject[6];for(int i=0;i<links.Length;i++)links[i]=new GameObject("evolve-link-"+i);
  Set(p,"m_LinkerObject",links);
  Input2DManager.Instance=new Input2DManager();
  return p;
 }
 static Dialog Stale(){
  var go=new GameObject("closed-evolve-dialog");var dialog=go.AddComponent<Dialog>();
  go.SetActive(false);dialog.m_DialogPushButton=EDIALOG_BTN.eDIALOG_BTN_NONE;return dialog;
 }
 static LayoutSwitchManager Layout(int id,int state,int input,int finish){
  var layout=new LayoutSwitchManager();Set(layout,"m_WorkSwitchID",id);Set(layout,"m_WorkSwitchState",state);
  Set(layout,"m_SwitchRequestInput",input);Set(layout,"m_WorkSwitchFinish",finish);
  Singleton<DialogManager>.Instance=new DialogManager{m_DialogLayout=layout};return layout;
 }
 static void Init(){
  Singleton<TimeManager>.Instance=new TimeManager();Singleton<MasterDataManager>.Instance=new MasterDataManager();Singleton<LocalSaveManager>.Instance=new LocalSaveManager();
  server=new ServerForgery();Singleton<ServerForgery>.Instance=server;Call(server,"Start");
  server.UserParamCreate(new SendCreateUser{uuid="evolve-page-test",terminal=new PacketStructTerminal{platform=1}});
  long uid=(long)Call(server,"AddUnit",47u,10u,0u,0u),part=(long)Call(server,"AddUnit",59u,1u,0u,0u);
  var save=DivineGateOffline.Local291Player(server);
  save.have_money=5780;save.have_stone_free=37;save.have_stone=37;save.stamina_now=108;server.UserParamSave();
  Singleton<UserDataAdmin>.Instance=new UserDataAdmin{m_StructPlayer=(PacketStructPlayer)Call(server,"CreateForgeryStructPlayer")};
  Singleton<SceneGoesParam>.Instance=new SceneGoesParam();Singleton<SceneManager>.Instance=new SceneManager();
  network=new ServerDataManager();Singleton<ServerDataManager>.Instance=network;Call(network,"Start");
  menu=new MainMenuManager();Singleton<MainMenuManager>.Instance=menu;
  var pages=new MainMenuPageParam[(int)MAINMENU_SEQ.SEQ_MAX];for(int i=0;i<pages.Length;i++)pages[i]=new MainMenuPageParam{m_LinkerState=6};
  var req=new MainMenuSwitchReq[10];for(int i=0;i<req.Length;i++)req[i]=new MainMenuSwitchReq();
  Set(menu,"m_MainMenuPageParam",pages);Set(menu,"m_SwitchRequest",req);
  foreach(var e in server.m_MasterDataParamCharaEvol)if(e.fix_id==38)MainMenuParam.m_EvolMasterData=e;
  foreach(var u in save.unit_list)if(u.unique_id==uid)MainMenuParam.m_EvolQuestUnitBase=u;
  MainMenuParam.m_EvolQuestUnitParts=new PacketStructUnit[3];
  foreach(var u in save.unit_list)if(u.unique_id==part)MainMenuParam.m_EvolQuestUnitParts[0]=u;
  MainMenuParam.m_EvolQuestUnitHelper=Get<TemplateList<PacketStructFriend>>(server,"m_ForgeryFriendHelper")[0];
  MainMenuParam.m_EvolQuestUnitHelper.friend_state=0;
  MainMenuParam.m_EvolQuestSelectAreaAmendStamina=100;MainMenuParam.m_EvolQuestSelectAreaAmendEXP=100;
  MainMenuParam.m_EvolQuestSelectAreaAmendCoin=100;MainMenuParam.m_EvolQuestSelectAreaAmendDrop=100;
  MainMenuParam.m_EvolQuestSelectAreaAmendTicket=100;MainMenuParam.m_QuestStamina=0;foreach(var q in server.m_MasterDataQuest)if(q.fix_id==17)MainMenuParam.m_QuestStamina=(uint)q.quest_stamina;
 }
 public static void Main(string[] args){
  Init();bool baseline=args.Length>0;Layout(0,0,0,0);
  var page=Page();var dialog=Stale();Set(page,"m_DialogCheck",dialog);
  Input2DManager.Instance.m_BackButonInputTrigger=true;string before=Snapshot();
  page.Update();
  if(baseline){
   Check(Get<Dialog>(page,"m_DialogCheck")==dialog && menu.GetSwitchRequest()==null,"Did not reproduce stale-dialog block");
   Call(page,"PageSwitchSetting",false,false);
   Check(Get<Dialog>(page,"m_DialogCheck")==dialog,"Original left-page cleanup changed");
   Check(Snapshot()==before,"Baseline mutated account");
   Console.WriteLine("REPRODUCED original r13: hidden/reused recovery dialog with no result blocks Back and page inputs; leaving page retains its reference; account unchanged.");
   return;
  }
  Check(Get<Dialog>(page,"m_DialogCheck")==null && menu.GetSwitchRequest()==null,"Orphan not released or input not consumed");
  Check(!Get<bool>(page,"m_BlendExec") && Get<uint>(page,"m_PacketUniqueID")==0 && Snapshot()==before,"Orphan recovery authorized/charged departure");
  page.Update();var back=menu.ErsSwitchRequest();
  Check(back!=null && back.m_SwitchRequstNextSeq==(int)MAINMENU_SEQ.SEQ_PARTY_EVOLVE_QUEST,"Back still blocked");

  foreach(var field in typeof(MainMenuSeqPartyEvolveQuestFix).GetFields(fields)){
   if(field.FieldType!=typeof(Dialog))continue;
   page=Page();Set(page,field.Name,Stale());page.Update();
   Check(field.GetValue(page)==null,"Orphan field retained: "+field.Name);
  }
  foreach(int window in new[]{1,2,3}){
   page=Page();Set(page,"m_DialogCheck",Stale());page.Update();
   Input2DManager.Instance.m_TouchObjectTriger=links[window+2];Input2DManager.Instance.m_TouchInputTriger=true;
   page.Update();Check(Get<int>(page,"m_WindowNow")==window,"Tab still blocked "+window);
  }
  // Preserve visible, opening, and closing confirmations; never bypass them.
  foreach(var shape in new[]{new[]{2,0,1,1},new[]{0,4,1,0},new[]{2,2,2,1}}){
   Layout(shape[0],shape[1],shape[2],shape[3]);page=Page();dialog=Stale();Set(page,"m_DialogCheck",dialog);
   Input2DManager.Instance.m_BackButonInputTrigger=true;page.Update();
   Check(Get<Dialog>(page,"m_DialogCheck")==dialog && menu.GetSwitchRequest()==null,"Normal dialog bypassed");
  }
  Layout(0,0,0,0);page=Page();dialog=Stale();dialog.m_LayoutExecOK=true;Set(page,"m_DialogCheck",dialog);page.Update();
  Check(Get<Dialog>(page,"m_DialogCheck")==dialog,"Executing dialog released");
  page=Page();dialog=Stale();dialog.gameObject.SetActive(true);Set(page,"m_DialogCheck",dialog);page.Update();
  Check(Get<Dialog>(page,"m_DialogCheck")==dialog,"Visible dialog released");
  Singleton<DialogManager>.Instance=null;page=Page();dialog=Stale();Set(page,"m_DialogCheck",dialog);page.Update();
  Check(Get<Dialog>(page,"m_DialogCheck")==dialog,"Missing manager assumed closed");

  Layout(0,0,0,0);page=Page();dialog=Stale();Set(page,"m_DialogCheck",dialog);Set(page,"m_PacketUniqueID",123u);
  var packets=Get<PacketData[]>(network,"m_PacketList");packets[0].m_PacketUniqueNum=123;packets[0].m_CommunicateFinish=false;
  page.Update();Check(Get<Dialog>(page,"m_DialogCheck")==dialog && Get<uint>(page,"m_PacketUniqueID")==123,"Pending request changed");
  page=Page();dialog=Stale();Set(page,"m_DialogCheck",dialog);Set(page,"m_BlendExec",true);page.Update();
  Check(Get<Dialog>(page,"m_DialogCheck")==dialog && Get<bool>(page,"m_BlendExec"),"Committed departure changed");
  packets[0].m_PacketUniqueNum=0;

  // Enter the real lifecycle method. Missing page parameters cause its
  // existing return route before presentation setup, after the new reset.
  page=Page();foreach(var field in page.GetType().GetFields(fields))if(field.FieldType==typeof(Dialog))field.SetValue(page,Stale());
  Set(page,"m_IsDialogStamina",true);var recipe=MainMenuParam.m_EvolMasterData;MainMenuParam.m_EvolMasterData=null;
  Call(page,"PageSwitchSetting",true,false);MainMenuParam.m_EvolMasterData=recipe;
  foreach(var field in page.GetType().GetFields(fields))if(field.FieldType==typeof(Dialog))Check(field.GetValue(page)==null,"Entry retained "+field.Name);
  Check(!Get<bool>(page,"m_IsDialogStamina"),"Entry retained recovery flag");menu.ErsSwitchRequest();
  Check(Snapshot()==before,"Cleanup/tab/entry checks changed account");

  // Normal successful recovery confirmation still uses original authorization.
  Singleton<DialogManager>.Instance=null;page=Page();dialog=new Dialog{m_DialogPushButton=EDIALOG_BTN.eDIALOG_BTN_01};Set(page,"m_DialogCheck",dialog);
  page.Update();Check(Get<bool>(page,"m_BlendExec"),"Valid recovery confirmation blocked");page.Update();
  uint packetID=Get<uint>(page,"m_PacketUniqueID");Check(packetID!=0,"No evolution request");
  PacketData request=null;foreach(var p in Get<PacketData[]>(network,"m_PacketList"))if(p.m_PacketUniqueNum==packetID)request=p;
  Check(request!=null && request.m_PacketAPI==SERVER_API.SERVER_API_EVOL_QUEST_START,"Wrong request");
  var response=JsonMapper.ToObject<RecvEvolQuestStart>(server.CreateRecvForgery(ref request,request.m_PacketSendMessage));
  var save=DivineGateOffline.Local291Player(server);
  Check(response.header.code==4096 && response.result.quest!=null && save.evolve_quest_active && save.quest_id==17,"Evolution backend failure");
  Check(save.stamina_now==108-(uint)MainMenuParam.m_QuestStamina && save.have_money==4780 && save.have_stone==37 && save.unit_list.Length==2,"Wrong charge or early material consumption");
  Console.WriteLine("PASS all 9 orphan dialog fields, Back and Leader/Friend/Quest tabs, entry reset, visible/opening/closing confirmations, missing manager, in-flight request and departure protection; recovery itself leaves SERVER_SAVE and all balances/materials unchanged.");
  Console.WriteLine("PASS original recovery confirmation -> EVOL_QUEST_START -> success; quest17, DGDB stamina charge, money5780->4780, chips37 unchanged, materials retained until clear.");
 }
}
