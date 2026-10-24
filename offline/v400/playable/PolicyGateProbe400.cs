using System;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
class PolicyGateProbe400 {
 static T New<T>() {return (T)FormatterServices.GetUninitializedObject(typeof(T));}
 static void Require(bool value,string text) {if(!value)throw new Exception(text);}
 static FieldInfo Static(string name) {return typeof(MainMenuSeq).GetField(name,BindingFlags.NonPublic|BindingFlags.Static);}
 static readonly MethodInfo Update=typeof(MainMenuSeq).GetMethod("PageSwitchUpdate",BindingFlags.NonPublic|BindingFlags.Instance);
 static bool Tick(MainMenuSeq page) {return (bool)Update.Invoke(page,null);}
 static void Reset(int policy,string version) {
  BadgeProbeIO400.Reset();PolicyProbeIO400.Reset();
  BadgeProbeIO400.Values["divinegate_INFORMATION_2nd"]=2;
  BadgeProbeIO400.Values["divinegate_INFORMATION_2nd_VER"]="0";
  if(policy>=0)BadgeProbeIO400.Values["divinegate_INFORMATION_POLICY"]=policy;
  if(version!=null)BadgeProbeIO400.Values["divinegate_INFORMATION_POLICY_VER"]=version;
  BadgeProbeIO400.Values["divinegate_UPDATE_COUNTER"]="offline400-1";
  Static("m_PatchUpdateDialog").SetValue(null,null);
  Static("m_PatchUpdateRequestStep").SetValue(null,EMAINMENU_PATCHUPDATE_REQ.NONE);
 }
 static void Check(MainMenuSeq page) {
  Static("m_PatchUpdateRequestStep").SetValue(null,EMAINMENU_PATCHUPDATE_REQ.CHECK);Tick(page);
 }
 static void Main(string[] args) {
  Singleton<LocalSaveManager>.Instance=New<LocalSaveManager>();Singleton<LocalSaveManager>.HasInstance=true;
  Singleton<PatchManager>.Instance=New<PatchManager>();Singleton<PatchManager>.HasInstance=true;
  Singleton<SceneManager>.Instance=New<SceneManager>();Singleton<SceneManager>.HasInstance=true;
  Singleton<MainMenuManager>.Instance=New<MainMenuManager>();Singleton<MainMenuManager>.HasInstance=true;
  Singleton<TitleMenuManager>.Instance=New<TitleMenuManager>();Singleton<TitleMenuManager>.HasInstance=true;
  var page=New<MainMenuSeq>();page.m_MainMenuSeqStartOK=true;
  if(args.Length>0 && args[0]=="legacy") {
   Reset(1,"0");
   for(int repeat=0;repeat<3;repeat++) {
    Check(page);Require(PolicyProbeIO400.Dialogs.Last()=="POLICY_DIALOG_TITLE:POLICY_UPDATE_DIALOG","Wrong legacy popup");
    var dialog=(Dialog)Static("m_PatchUpdateDialog").GetValue(null);dialog.m_DialogPushButton=EDIALOG_BTN.eDIALOG_BTN_01;Tick(page);
    Require(PolicyProbeIO400.Scenes.Last()==(int)ESCENE.eSCENE_TITLE,"Legacy did not return to title");
   }
   Console.WriteLine("PASS: r14 actual in-menu gate reproduces three repeated policy-update popups and returns to title with unchanged unaccepted policy state.");return;
  }
  foreach(int policy in new[]{-1,0,1,2})foreach(string version in new[]{null,"old-policy","0"}) {
   Reset(policy,version);var before=BadgeProbeIO400.Values.OrderBy(p=>p.Key).Select(p=>p.Key+"="+p.Value).ToArray();
   for(int repeat=0;repeat<3;repeat++) {
    TitleMenuParam.m_PageCheckLogoWait=true;TitleMenuParam.m_PageCheckWaitInput=true;TitleMenuParam.m_PageCheckUserAuthentication=false;
    New<TitleMenuSeqSwitchPage>().Update();
    Require(PolicyProbeIO400.Pages.Last()==(int)TITLEMENU_SEQ.SEQ_USER_AUTHENTICATION,"Title authentication blocked");
    Check(page);Require(Tick(page),"In-menu gate blocked subsequent frame");
   }
   Require(PolicyProbeIO400.PolicyReads==0 && PolicyProbeIO400.Dialogs.Count==0 && PolicyProbeIO400.Scenes.Count==0,"Policy loop remains");
   Require(!BadgeProbeIO400.Writes.Any() && before.SequenceEqual(BadgeProbeIO400.Values.OrderBy(p=>p.Key).Select(p=>p.Key+"="+p.Value)),"Saved agreement/version changed");
  }
  Reset(1,"old-policy");BadgeProbeIO400.Values["divinegate_UPDATE_COUNTER"]="old-data";Check(page);
  Require(PolicyProbeIO400.Dialogs.Single()=="ERROR_MSG_UPDATE_TITLE:ERROR_MSG_UPDATE","Data-update dialog lost");
  var updateDialog=(Dialog)Static("m_PatchUpdateDialog").GetValue(null);updateDialog.m_DialogPushButton=EDIALOG_BTN.eDIALOG_BTN_01;Tick(page);
  Require(PolicyProbeIO400.Scenes.Single()==(int)ESCENE.eSCENE_TITLE,"Data-update return-to-title lost");
  Reset(1,"old-policy");BadgeProbeIO400.Values["divinegate_INFORMATION_2nd"]=1;Check(page);
  Require(PolicyProbeIO400.Dialogs.Single()=="KIYAKU_DIALOG_TITLE:KIYAKU_UPDATE_DIALOG","Existing terms branch changed");
  PolicyProbeIO400.Pages.Clear();TitleMenuParam.m_PageCheckUserAuthentication=false;New<TitleMenuSeqSwitchPage>().Update();
  Require(PolicyProbeIO400.Pages.Single()==(int)TITLEMENU_SEQ.SEQ_INFORMATION_FOX,"Original title terms route lost");
  Console.WriteLine("PASS: 12 saved policy/version combinations across title + in-menu checks, 3 repeats each; no policy reads, popups, scene resets or preference writes. Data mismatch still opens its dialog and returns to title; existing terms gates preserved.");
  Console.WriteLine("LIMIT: disposable Mono test uses ready UI, dialog/scene recording and in-memory preferences; does not test Android login or gameplay.");
 }
}
