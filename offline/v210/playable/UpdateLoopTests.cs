using System;using System.IO;using System.Reflection;using LitJson;using ServerDataDefine;
class UpdateLoopTests {
 static ServerForgery server;static uint serial=1;
 static void Check(bool ok,string what){if(!ok)throw new Exception(what);Console.WriteLine("PASS "+what);}
 static T Api<T>(SERVER_API api,object req) {
  var packet=new PacketData{m_PacketAPI=api,m_PacketUniqueNum=serial++};
  string response=server.CreateRecvForgery(ref packet,JsonMapper.ToJson(req));
  Check(response!=null && (int)JsonMapper.ToObject(response)["header"]["code"]==(int)API_CODE.API_CODE_SUCCESS,"API "+api);
  return JsonMapper.ToObject<T>(response);
 }
 static string Saved(){return LocalSaveUtil.GetValueString("SERVER_SAVE","");}
 static void Boot() {
  Singleton<TimeManager>.Instance=new TimeManager();
  Singleton<MasterDataManager>.Instance=new MasterDataManager();
  Singleton<LocalSaveManager>.Instance=new LocalSaveManager();
  Singleton<MainMenuManager>.Instance=new MainMenuManager();
  Singleton<TitleMenuManager>.Instance=new TitleMenuManager();
  Singleton<ResidentResourceManager>.Instance=new ResidentResourceManager();
  Singleton<PatchManager>.Instance=new PatchManager();
  DivineGateOffline.SetupPatch(Singleton<PatchManager>.Instance);
  server=new ServerForgery();Singleton<ServerForgery>.Instance=server;
  typeof(ServerForgery).GetMethod("Start",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(server,null);
 }
 static void Title(bool ready){DivineGateOffline.LoadLocalAssets210(null,ready);}
 static void Update(bool dialog) {
  UpdateUiSupport.Reset();
  var page=new MainMenuSeqMasterUpdateChk();page.m_MainMenuSeqStartOK=true;
  page.Update();
  if(dialog)Check(UpdateUiSupport.Dialogs==1 && UpdateUiSupport.MainRoutes==0 && UpdateUiSupport.TitleKey=="ERROR_MSG_UPDATE_TITLE" && UpdateUiSupport.MessageKey=="ERROR_MSG_UPDATE","Original update check opens update dialog");
  else Check(UpdateUiSupport.Dialogs==0 && UpdateUiSupport.MainRoutes==1,"Original update check enters next menu");
 }
 static void CreateAndClear() {
  Api<RecvCreateUser>(SERVER_API.SERVER_API_USER_CREATE,new SendCreateUser{uuid="update-loop-fixture",terminal=new PacketStructTerminal{platform=1,name="Container fixture"}});
  Api<RecvRenameUser>(SERVER_API.SERVER_API_USER_RENAME,new SendRenameUser{user_name="UpdateRecoveryTest"});
  int count=0;
  foreach(var q in server.m_MasterDataQuest)if(q.area_id==210040 && q.active==2) {
   Check(ServerForgery.CreateQuestBuild(q.fix_id)!=null,"Index quest builds "+q.fix_id);
   Api<RecvQuestStart>(SERVER_API.SERVER_API_QUEST_START,new SendQuestStart{quest_id=q.fix_id,helper_unit=new PacketStructUnit{id=139,level=1}});
   Api<RecvQuestClear>(SERVER_API.SERVER_API_QUEST_CLEAR,new SendQuestClear{quest_id=q.fix_id,get_money=100,get_exp=10});
   count++;
  }
  Check(count>0,"Cleared Index collaboration quests="+count);
 }
 static int Main(string[] args) {
  string mode=args[0],expected=Environment.GetEnvironmentVariable("DG_EXPECTED_SAVE");
  Boot();
  if(mode=="seed-baseline" || mode=="fresh") {CreateAndClear();File.WriteAllText(expected,Saved());}
  string before=File.ReadAllText(expected);
  Check(before==Saved(),"Existing account, units, rewards and clear flags loaded byte-for-byte");
  var local=Singleton<LocalSaveManager>.Instance;
  if(mode=="reload") {
   Check(local.LoadFuncUpdateCounter()=="offline210-1","Counter persists across separate process");
   Update(false);Title(true);Check(before==Saved(),"Restart/title preserve SERVER_SAVE bytes");return 0;
  }
  if(mode=="seed-baseline") {
   Check(local.LoadFuncUpdateCounter()=="","r2 counter is absent");
   Check(Singleton<PatchManager>.Instance.GetPatchText("DATA_UPDATE_COUNTER_ANDROID")=="0","r2 Android counter falls through to 0");
   Title(true);Update(true);Title(true);Update(true);
   Check(local.LoadFuncUpdateCounter()=="" && Saved()==before,"r2 title reload repeats mismatch and preserves blocked fixture");
   return 0;
  }
  string old=local.LoadFuncUpdateCounter();
  Title(false);Check(local.LoadFuncUpdateCounter()==old,"Unready title does not acknowledge data");
  var quests=server.m_MasterDataQuest;server.m_MasterDataQuest=null;Title(true);server.m_MasterDataQuest=quests;
  Check(local.LoadFuncUpdateCounter()==old,"Missing master tables do not acknowledge data");
  UpdateUiSupport.Reset();Title(true);
  Check(UpdateUiSupport.ResidentRequests==1 && UpdateUiSupport.TitleRoutes==1,"Local title flow requests resident assets and advances");
  Check(local.LoadFuncUpdateCounter()=="offline210-1","Loaded data counter saved using Android key");
  Check(Saved()==before,"Counter repair preserves all SERVER_SAVE bytes");
  Update(false);
  // A genuine mismatch still opens the original dialog; no blanket bypass.
  local.SaveFuncUpdateCounter("stale-fixture");Update(true);
  Title(true);Update(false);
  Singleton<PatchManager>.Instance.m_PatchStructTextAcquired=false;
  UpdateUiSupport.Reset();new MainMenuSeqMasterUpdateChk{m_MainMenuSeqStartOK=true}.Update();
  Check(UpdateUiSupport.Dialogs==0 && UpdateUiSupport.MainRoutes==0,"Original check still waits for patch data readiness");
  Singleton<PatchManager>.Instance.m_PatchStructTextAcquired=true;
  Check(Saved()==before,"All recovery checks preserve player progress bytes");
  return 0;
 }
}
