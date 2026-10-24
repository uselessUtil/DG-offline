// Run only in the disposable managed probe; never added to the APK.
using System;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using ServerDataDefine;
class GachaDateProbe400 {
 static T Uninitialized<T>() { return (T)FormatterServices.GetUninitializedObject(typeof(T)); }
 static void Require(bool value,string message) { if(!value)throw new Exception(message); }
 static void Main(string[] args) {
  var server=Uninitialized<ServerForgery>();
  Singleton<ServerForgery>.Instance=server;Singleton<ServerForgery>.HasInstance=true;
  var master=Uninitialized<MasterDataManager>();
  Singleton<MasterDataManager>.Instance=master;Singleton<MasterDataManager>.HasInstance=true;
  var embedded=Assembly.Load("DivineGateOffline").GetType("DG400Data");
  embedded.GetMethod("Apply").Invoke(null,new object[]{server});
  embedded.GetMethod("Share").Invoke(null,new object[]{server,master});
  var clock=Uninitialized<TimeManager>();
  Singleton<TimeManager>.Instance=clock;Singleton<TimeManager>.HasInstance=true;
  clock.m_TimeNow=new DateTime(2026,10,24,16,21,31);
  var events=Uninitialized<TimeEventManager>();
  Singleton<TimeEventManager>.Instance=events;Singleton<TimeEventManager>.HasInstance=true;
  events.m_TimeEventActiveID=new uint[256];
  var user=Uninitialized<UserDataAdmin>();
  Singleton<UserDataAdmin>.Instance=user;Singleton<UserDataAdmin>.HasInstance=true;
  user.m_StructPlayer=new PacketStructPlayer{have_friend_pt=200,have_stone=5};

  if(args.Length>0 && args[0]=="legacy") {
   try { MasterDataUtil.GetTopPriorityActiveGachaMasterList(); }
   catch(ArgumentOutOfRangeException e) {
    Require(e.StackTrace.Contains("TimeManager.CheckWithinTime"),"Wrong legacy failure");
    Console.WriteLine("PASS: r9 embedded data reproduces DateTime ArgumentOutOfRangeException in the original gacha-menu lookup.");
    return;
   }
   throw new Exception("Legacy menu lookup unexpectedly succeeded");
  }
  // Control: the absent legacy date fields really trigger the original DLL,
  // rather than merely checking a reimplementation of its date arithmetic.
  bool reproduced=false;
  try { MasterDataUtil.CheckActiveGachaMaster(new MasterDataGacha{fix_id=100100,type=1}); }
  catch(ArgumentOutOfRangeException) { reproduced=true; }
  Require(reproduced,"Missing-date control did not reproduce");
  Require(server.m_MasterDataGacha.Length==7,"Pool count changed");
  foreach(var date in new[]{new DateTime(2026,10,24),new DateTime(2030,1,1)}) {
   clock.m_TimeNow=date;
   foreach(var pool in server.m_MasterDataGacha) {
    Require(pool.timing_public==1970010100u && pool.timing_start==1970010100u && pool.timing_end==0,"Wrong projected dates");
    Require(clock.CheckWithinTime(pool.timing_start,pool.timing_end),"Invalid pool dates "+pool.fix_id);
   }
   var top=MasterDataUtil.GetTopPriorityActiveGachaMasterList();
   Require(top[0].fix_id==100100 && top[1].fix_id==200100 && top[2]==null,"Wrong inactive-event menu");
   Require(MasterDataUtil.GetActiveGachaMaster(1).Single().fix_id==100100,"Friend pool missing");
   Require(MasterDataUtil.GetActiveGachaMaster(2).Single().fix_id==200100,"Rare pool missing");
   var count=MasterDataUtil.GetGachaCountMasterList();
   Require(count[0]==1 && count[1]==1,"Draw price/count changed");
  }
  foreach(var pool in server.m_MasterDataGacha.Where(p=>p.event_id!=0))
   events.m_TimeEventActiveID[events.m_TimeEventActiveInputed++]=pool.event_id;
  Require(MasterDataUtil.GetActiveGachaMaster(3).Length==4,"Active collab gates changed");
  Require(MasterDataUtil.GetTopPriorityActiveGachaMasterList()[2].fix_id==300400,"Collab priority changed");
  var tutorial=MasterDataUtil.GetGachaParamFromID(10u);
  Require(tutorial!=null && tutorial.type==2 && tutorial.price==5,"Tutorial pool contract changed");
  Require(MasterDataUtil.ConvertGachaMasterToTabNum(tutorial,0)==1,"Tutorial tab changed");
  Require(!MasterDataUtil.CheckActiveGachaMaster(tutorial),"Tutorial pool leaked into normal menu");
  Require(server.m_MasterDataGachaAssign.Any(p=>p.gacha_id==10 && p.active==2 && p.assign_rate>0),"Tutorial pool empty");
  Console.WriteLine("PASS: missing-date exception reproduced; all 7 embedded pool dates valid; original priority/list/count lookups passed at 2026 and 2030; event gates and tutorial ID 10 retained.");
  Console.WriteLine("LIMIT: disposable Mono engine-I/O probe, not Android clicks, scratching, or tutorial completion.");
 }
}
