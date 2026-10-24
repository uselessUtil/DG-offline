// Execute the original startup router past the UI-ready/queue boundaries.
using System;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using ServerDataDefine;
using LitJson;
class RouterMonthlyProbe400 {
 static T Uninitialized<T>() {return (T)FormatterServices.GetUninitializedObject(typeof(T));}
 static void Require(bool value,string text) {if(!value)throw new Exception(text);}
 static void Field(object obj,string name,object value) {obj.GetType().GetField(name,BindingFlags.NonPublic|BindingFlags.Instance).SetValue(obj,value);}
 static string Snapshot(object value) {return value==null?"<null>":JsonMapper.ToJson(value);}
 static MainMenuSeqSwitchPage Router() {
  var page=Uninitialized<MainMenuSeqSwitchPage>();
  foreach(string name in new[]{"m_SetupServerSendFinish","m_SetupResultCollaboFinish","m_SetupBeginnerBoostFinish","m_SetupMasterUpdateChkFinish"})Field(page,name,true);
  return page;
 }
 static void Main(string[] args) {
  var server=Uninitialized<ServerForgery>();Singleton<ServerForgery>.Instance=server;Singleton<ServerForgery>.HasInstance=true;
  var master=Uninitialized<MasterDataManager>();Singleton<MasterDataManager>.Instance=master;Singleton<MasterDataManager>.HasInstance=true;
  var adapter=Assembly.Load("DivineGateOffline");var data=adapter.GetType("DG400Data");
  data.GetMethod("Apply").Invoke(null,new object[]{server});data.GetMethod("Share").Invoke(null,new object[]{server,master});
  var manager=Uninitialized<MainMenuManager>();Singleton<MainMenuManager>.Instance=manager;Singleton<MainMenuManager>.HasInstance=true;
  var local=Uninitialized<LocalSaveManager>();Singleton<LocalSaveManager>.Instance=local;Singleton<LocalSaveManager>.HasInstance=true;
  var user=Uninitialized<UserDataAdmin>();Singleton<UserDataAdmin>.Instance=user;Singleton<UserDataAdmin>.HasInstance=true;
  user.m_StructPlayer=new PacketStructPlayer{have_money=406,exp=25};
  ResidentParam.m_StartingFirstTopPage=true;ResidentParam.m_StartingFirstLoginBonus=true;
  user.m_StructLoginMonthly=new PacketStructLoginMonthly{login_list=new uint[0],monthly_list=new MasterDataLoginMonthlyConverted[0]};
  if(args.Length>0 && args[0]=="legacy") {
   try {Router().Update();}
   catch(IndexOutOfRangeException e) {
    Require(e.StackTrace.Contains("MainMenuSeqSwitchPage.Update"),"Wrong legacy exception");
    Console.WriteLine("PASS: r13 original startup router reproduces the logged array exception with empty monthly dates/calendar.");return;
   }
   throw new Exception("Legacy router unexpectedly succeeded");
  }
  var offline=adapter.GetType("DivineGateOffline");var gate=offline.GetMethod("AllowPage400");
  var absent=new PacketStructLoginMonthly[]{null,new PacketStructLoginMonthly(),
   new PacketStructLoginMonthly{login_list=new uint[0],monthly_list=new MasterDataLoginMonthlyConverted[0]},
   new PacketStructLoginMonthly{login_list=new uint[0],monthly_list=new[]{new MasterDataLoginMonthlyConverted()}},
   new PacketStructLoginMonthly{login_list=new[]{2026102400u},monthly_list=new MasterDataLoginMonthlyConverted[0]},
   new PacketStructLoginMonthly{login_list=new[]{2026102400u},monthly_list=new MasterDataLoginMonthlyConverted[1]}};
  foreach(var monthly in absent) {
   BadgeProbeIO400.Reset();RouterProbeIO400.Routes.Clear();user.m_StructLoginMonthly=monthly;
   string before=Snapshot(monthly),player=Snapshot(user.m_StructPlayer);
   var page=Router();page.Update();page.Update();
   Require(RouterProbeIO400.Routes.Count==2 && RouterProbeIO400.Routes.All(p=>p==(int)MAINMENU_SEQ.SEQ_QUEST_SELECT_AREA_STORY),"Unavailable calendar blocked normal quest routing");
   Require(!(bool)gate.Invoke(null,new object[]{MAINMENU_SEQ.SEQ_LOGIN_MONTHLY}),"Unavailable calendar page accepted");
   Require(Snapshot(monthly)==before && Snapshot(user.m_StructPlayer)==player,"Player/calendar state changed");
   Require(!BadgeProbeIO400.Writes.Any(),"Router wrote player/login preferences");
  }
  var usable=new PacketStructLoginMonthly{login_list=new[]{2026102400u},monthly_list=new[]{new MasterDataLoginMonthlyConverted{date=2026102400u}}};
  user.m_StructLoginMonthly=usable;RouterProbeIO400.Routes.Clear();BadgeProbeIO400.Reset();
  Require((bool)gate.Invoke(null,new object[]{MAINMENU_SEQ.SEQ_LOGIN_MONTHLY}),"Usable calendar page blocked");
  Router().Update();Require(RouterProbeIO400.Routes.Single()==(int)MAINMENU_SEQ.SEQ_LOGIN_MONTHLY,"Original unclaimed calendar route lost");
  local.SaveFuncAddLoginLastest(2026102400);RouterProbeIO400.Routes.Clear();
  Router().Update();Require(RouterProbeIO400.Routes.Single()==(int)MAINMENU_SEQ.SEQ_QUEST_SELECT_AREA_STORY,"Already-viewed calendar did not continue");
  // Pending clear handling precedes the calendar gate and must still run.
  var scene=Uninitialized<SceneGoesParam>();Singleton<SceneGoesParam>.Instance=scene;Singleton<SceneGoesParam>.HasInstance=true;
  var result=typeof(SceneGoesParam).GetField("m_SceneGoesParamToMainMenu");
  result.SetValue(scene,Activator.CreateInstance(result.FieldType));
  user.m_StructLoginMonthly=absent[2];RouterProbeIO400.Routes.Clear();
  Router().Update();Require(RouterProbeIO400.Routes.Single()==(int)MAINMENU_SEQ.SEQ_RESULT_SERVER_SEND,"Pending result route skipped");
  Console.WriteLine("PASS: actual startup router continues to story quests for 6 absent/empty calendar cases and repeats; usable unclaimed and viewed calendars preserve original routes; pending quest result still routes to its original result sender; player/history unchanged.");
  Console.WriteLine("LIMIT: disposable Mono native-I/O probe; UI readiness and route queue replaced with recording boundaries, not Android UI, login transport or result processing.");
 }
}
