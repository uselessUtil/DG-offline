// Execute the complete original footer algorithm with real embedded tables.
using System;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using ServerDataDefine;
using LitJson;
class BadgeUpdateProbe400 {
 static T Uninitialized<T>() {return (T)FormatterServices.GetUninitializedObject(typeof(T));}
 static void Require(bool value,string text) {if(!value)throw new Exception(text);}
 static string Snapshot(object value) {return value==null ? "<null>" : JsonMapper.ToJson(value);}
 static void Field(object obj,string name,object value) {
  obj.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(obj,value);
 }
 static MainMenuPartsFooterChildMenuList Footer() {
  var footer=Uninitialized<MainMenuPartsFooterChildMenuList>();
  var list=new TemplateList<MainMenuPartsFooterChildMenu>();
  foreach(var page in new[]{MAINMENU_SEQ.SEQ_QUEST_SELECT_AREA_STORY,MAINMENU_SEQ.SEQ_QUEST_SELECT_AREA_EVENT,
   MAINMENU_SEQ.SEQ_GACHA_TAB1,MAINMENU_SEQ.SEQ_GACHA_TAB2,MAINMENU_SEQ.SEQ_SHOP_ROOT}) {
   var item=Uninitialized<MainMenuPartsFooterChildMenu>();Field(item,"m_ChangePage",page);
   Field(item,"m_Type",page==MAINMENU_SEQ.SEQ_GACHA_TAB1 || page==MAINMENU_SEQ.SEQ_GACHA_TAB2 ? FOOTER_MENU_TYPE.FOOTER_MENU_SCRATCH :
    page==MAINMENU_SEQ.SEQ_SHOP_ROOT ? FOOTER_MENU_TYPE.FOOTER_MENU_SHOP : FOOTER_MENU_TYPE.FOOTER_MENU_QUEST);
   list.Add(item);
  }
  Field(footer,"m_FooterList",list);return footer;
 }
 static void Main(string[] args) {
  var server=Uninitialized<ServerForgery>();Singleton<ServerForgery>.Instance=server;Singleton<ServerForgery>.HasInstance=true;
  var master=Uninitialized<MasterDataManager>();Singleton<MasterDataManager>.Instance=master;Singleton<MasterDataManager>.HasInstance=true;
  var data=Assembly.Load("DivineGateOffline").GetType("DG400Data");
  data.GetMethod("Apply").Invoke(null,new object[]{server});data.GetMethod("Share").Invoke(null,new object[]{server,master});
  var clock=Uninitialized<TimeManager>();Singleton<TimeManager>.Instance=clock;Singleton<TimeManager>.HasInstance=true;
  clock.m_TimeNow=new DateTime(2026,10,24,19,10,7);
  var events=Uninitialized<TimeEventManager>();Singleton<TimeEventManager>.Instance=events;Singleton<TimeEventManager>.HasInstance=true;
  events.m_TimeEventActiveID=new uint[256];
  var user=Uninitialized<UserDataAdmin>();Singleton<UserDataAdmin>.Instance=user;Singleton<UserDataAdmin>.HasInstance=true;
  user.m_StructPlayer=new PacketStructPlayer{have_money=406,exp=25,flag_quest_clear=new byte[100000],flag_quest_check=new byte[100000]};
  user.m_StructLoginMonthly=new PacketStructLoginMonthly{login_list=new uint[0]};
  var local=Uninitialized<LocalSaveManager>();Singleton<LocalSaveManager>.Instance=local;Singleton<LocalSaveManager>.HasInstance=true;
  if(args.Length>0 && args[0]=="legacy") {
   try {Footer().UpdateNewLabel();}
   catch(IndexOutOfRangeException e) {
    Require(e.StackTrace.Contains("MainMenuPartsFooterChildMenuList.UpdateNewLabel"),"Wrong legacy failure");
    Console.WriteLine("PASS: r12 complete footer update reproduces the logged array exception with an empty monthly-login history.");return;
   }
   throw new Exception("Legacy footer update unexpectedly succeeded");
  }
  var cases=new PacketStructLoginMonthly[]{null,new PacketStructLoginMonthly(),
   new PacketStructLoginMonthly{login_list=new uint[0]},
   new PacketStructLoginMonthly{login_list=new[]{2026102300u,2026102400u}},
   new PacketStructLoginMonthly{login_list=new[]{2014010100u}}};
  int completed=0;
  foreach(var monthly in cases)foreach(var now in new[]{new DateTime(2026,10,24,19,10,7),new DateTime(2026,10,25,0,0,1)}) {
   BadgeProbeIO400.Reset();clock.m_TimeNow=now;user.m_StructLoginMonthly=monthly;
   var dates=monthly==null ? null : monthly.login_list;
   string playerBefore=Snapshot(user.m_StructPlayer);string monthlyBefore=Snapshot(monthly);
   var footer=Footer();footer.UpdateNewLabel();
   uint date=dates!=null && dates.Length>0 ? dates[dates.Length-1] : (uint)(now.Year*1000000+now.Month*10000+now.Day*100);
   int expected=unchecked((int)date+now.Hour);
   Require(local.LoadFuncLogin_NewBadge()==expected,"Badge timestamp changed unexpectedly");
   Require(footer.GetNewFlag(FOOTER_MENU_TYPE.FOOTER_MENU_QUEST),"Quest New! behavior lost");
   Require(Snapshot(user.m_StructPlayer)==playerBefore,"Player/rewards/progress changed");
   Require(Object.ReferenceEquals(user.m_StructLoginMonthly,monthly) && Snapshot(monthly)==monthlyBefore,"Monthly login history fabricated or changed");
   Require(BadgeProbeIO400.Writes.All(k=>k.Contains("BADGE")),"Unexpected non-badge write");
   footer.UpdateNewLabel();Require(local.LoadFuncLogin_NewBadge()==expected,"Repeat update unstable");
   completed++;
  }
  Console.WriteLine("PASS: "+completed+" full footer updates, plus repeats, across null/empty/current/historical login records and midnight; quest New! retained, player rewards/progress and monthly histories unchanged; only badge preferences written.");
  Console.WriteLine("LIMIT: disposable Mono probe with native I/O/in-memory preferences; not Android rendering, result clicks, or a complete quest-clear transaction.");
 }
}
