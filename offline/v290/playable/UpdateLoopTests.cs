using System;using System.IO;using System.Reflection;using LitJson;using ServerDataDefine;
class UpdateLoop290Tests {
 static ServerForgery server;
 static void Check(bool ok,string s){if(!ok)throw new Exception(s);Console.WriteLine("PASS "+s);}
 static string Saved(){return LocalSaveUtil.GetValueString("SERVER_SAVE","");}
 static void Boot(){
  Singleton<SceneManager>.Instance=new SceneManager();
  Singleton<TimeManager>.Instance=new TimeManager();
  Singleton<MasterDataManager>.Instance=new MasterDataManager();
  Singleton<LocalSaveManager>.Instance=new LocalSaveManager();
  Singleton<MainMenuManager>.Instance=new MainMenuManager();
  Singleton<TitleMenuManager>.Instance=new TitleMenuManager();
  Singleton<ResidentResourceManager>.Instance=new ResidentResourceManager();
  Singleton<PatchManager>.Instance=new PatchManager();DivineGateOffline.SetupPatch(Singleton<PatchManager>.Instance);
  server=new ServerForgery();Singleton<ServerForgery>.Instance=server;
  typeof(ServerForgery).GetMethod("Start",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(server,null);
 }
 static void ResetCheck(){
  var t=typeof(MainMenuSeq);
  t.GetField("m_PatchUpdateDialog",BindingFlags.NonPublic|BindingFlags.Static).SetValue(null,null);
  var f=t.GetField("m_PatchUpdateRequestStep",BindingFlags.NonPublic|BindingFlags.Static);
  f.SetValue(null,Enum.Parse(f.FieldType,"NONE"));
  UpdateUiSupport.Reset();
 }
 static void Update(bool dialog){
  ResetCheck();var page=new MainMenuSeqMasterUpdateChk();page.m_MainMenuSeqStartOK=true;
  page.PageSwitchEventEnableAfter();page.RequestPatchUpdate();
  page.Update();page.Update();
  Check(dialog ? UpdateUiSupport.Dialogs==1 && UpdateUiSupport.MainRoutes==0 : UpdateUiSupport.Dialogs==0 && UpdateUiSupport.MainRoutes==1,
        dialog?"Original common-menu update flow rejects stale marker":"Original common-menu request/wait/check flow completes");
 }
 static void Main(string[] args){
  Boot();var local=Singleton<LocalSaveManager>.Instance;
  if(args.Length==0){
   server.UserParamCreate(new SendCreateUser{uuid="update290-test",terminal=new PacketStructTerminal{platform=1,name="Container"}});
   server.UserParamSave();string before=Saved();
   File.WriteAllText(Environment.GetEnvironmentVariable("DG_EXPECTED_SAVE"),before);
   local.SaveFuncUpdateCounter("old-marker");
   DivineGateOffline.LoadLocalAssets290(null,false);Check(local.LoadFuncUpdateCounter()=="old-marker","Unready title does not acknowledge data");
   var table=server.m_MasterDataQuest;server.m_MasterDataQuest=null;DivineGateOffline.LoadLocalAssets290(null,true);server.m_MasterDataQuest=table;
   Check(local.LoadFuncUpdateCounter()=="old-marker","Missing master data does not acknowledge data");
   Update(true);
   ResetCheck();DivineGateOffline.LoadLocalAssets290(null,true);
   Check(local.LoadFuncUpdateCounter()=="offline290-1","Ready title persists Android-specific marker");
   Check(UpdateUiSupport.ResidentRequests==1 && UpdateUiSupport.TitleRoutes==1,"Title requests resident resources before advancing");
   Check(before==Saved(),"Marker repair leaves SERVER_SAVE byte-for-byte unchanged");
   Update(false);
   local.SaveFuncUpdateCounter("forced-mismatch");Update(true);
   DivineGateOffline.LoadLocalAssets290(null,true);Update(false);
   Singleton<PatchManager>.Instance.m_PatchStructTextAcquired=false;
   ResetCheck();var page=new MainMenuSeqMasterUpdateChk{m_MainMenuSeqStartOK=true};page.PageSwitchEventEnableAfter();
   var f=typeof(MainMenuSeq).GetField("m_PatchUpdateRequestStep",BindingFlags.NonPublic|BindingFlags.Static);
   f.SetValue(null,Enum.Parse(f.FieldType,"WAIT"));page.Update();
   Check(UpdateUiSupport.Dialogs==0 && UpdateUiSupport.MainRoutes==0,"Original WAIT state waits for ready patch data");
   Singleton<PatchManager>.Instance.m_PatchStructTextAcquired=true;
   Check(before==Saved(),"Update checks do not mutate player save");
  }else{
   Check(Saved()==File.ReadAllText(Environment.GetEnvironmentVariable("DG_EXPECTED_SAVE")),"Separate process reload preserves SERVER_SAVE");
   Check(local.LoadFuncUpdateCounter()=="offline290-1","Separate process reload preserves Android update marker");
   Update(false);
  }
 }
}