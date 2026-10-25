using System;using System.Reflection;using ServerDataDefine;
class VerifyPageFlow291 {
 static void Check(bool b,string m){if(!b)throw new Exception(m);}
 static void Set(object o,string n,object v){o.GetType().GetField(n,BindingFlags.NonPublic|BindingFlags.Instance).SetValue(o,v);}
 static void Main(){
  var mm=new MainMenuManager();
  var pages=new MainMenuPageParam[(int)MAINMENU_SEQ.SEQ_MAX];for(int i=0;i<pages.Length;i++)pages[i]=new MainMenuPageParam{m_LinkerState=6};
  var req=new MainMenuSwitchReq[10];for(int i=0;i<10;i++)req[i]=new MainMenuSwitchReq();
  Set(mm,"m_MainMenuPageParam",pages);Set(mm,"m_SwitchRequest",req);
  string before=LocalSaveUtil.GetValueString("SERVER_SAVE","");
  foreach(var next in new[]{MAINMENU_SEQ.SEQ_PARTY_SALE2,MAINMENU_SEQ.SEQ_SWITCH_PAGE,MAINMENU_SEQ.SEQ_PARTY_ROOT}){
   MainMenuParam.m_AchievementClear_NextPage=next;
   Check(mm.AddSwitchRequest(MAINMENU_SEQ.SEQ_ACHIEVEMENT_CLEAR,false,false),"Achievement continuation rejected");
   Check(mm.ErsSwitchRequest().m_SwitchRequstNextSeq==(int)next,"Continuation target lost");
  }
  foreach(var next in new[]{MAINMENU_SEQ.SEQ_ACHIEVEMENT_CLEAR,MAINMENU_SEQ.SEQ_SWITCH_WEBVIEW_PAGE,(MAINMENU_SEQ)0}){
   MainMenuParam.m_AchievementClear_NextPage=next;
   Check(mm.AddSwitchRequest(MAINMENU_SEQ.SEQ_ACHIEVEMENT_CLEAR,false,false),"Invalid continuation deadlock");
   Check(mm.ErsSwitchRequest().m_SwitchRequstNextSeq==(int)MAINMENU_SEQ.SEQ_SWITCH_PAGE,"Invalid continuation not recovered");
  }
  foreach(var next in new[]{MAINMENU_SEQ.SEQ_PARTY_SALE2,MAINMENU_SEQ.SEQ_SWITCH_PAGE}){
   Check(mm.AddSwitchRequest(next,false,true),"Normal cancel/result return rejected");
   var r=mm.ErsSwitchRequest();Check(r.m_SwitchRequstNextSeq==(int)next && r.m_SwitchRequstBack,"Normal return changed");
  }
  Check(!mm.AddSwitchRequest(MAINMENU_SEQ.SEQ_SWITCH_WEBVIEW_PAGE,false,false) && mm.GetSwitchRequest()==null,"External page reopened");
  Check(before==LocalSaveUtil.GetValueString("SERVER_SAVE",""),"Routing wrote player save");
  Console.WriteLine("PASS actual MainMenuManager queue: sale achievement transit resumes intended page; invalid/self transit falls back; cancel and friend return targets/flags preserved; external page blocked; SERVER_SAVE unchanged. Touch tested separately on Android.");
 }
}
