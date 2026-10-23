// Test-only UI sinks. Compiled and used only in container /tmp, never in an APK.
using System;using System.Runtime.Serialization;
public static class UpdateUiSupport {
 public static int Dialogs,MainRoutes,TitleRoutes,ResidentRequests;
 public static string TitleKey,MessageKey;
 public static Dialog Open(string title,string message,string button,bool wall) {
  Dialogs++;TitleKey=title;MessageKey=message;
  return (Dialog)FormatterServices.GetUninitializedObject(typeof(Dialog));
 }
 public static bool MainRoute(MAINMENU_SEQ seq,bool fast,bool back) {
  if(seq!=MAINMENU_SEQ.SEQ_SWITCH_PAGE)throw new Exception("Unexpected main route "+seq);
  MainRoutes++;return true;
 }
 public static bool TitleRoute(TITLEMENU_SEQ seq,bool fast) {
  if(seq!=TITLEMENU_SEQ.SEQ_SWITCH_PAGE)throw new Exception("Unexpected title route "+seq);
  TitleRoutes++;return true;
 }
 public static void Resident(){ResidentRequests++;}
 public static void Reset(){Dialogs=MainRoutes=TitleRoutes=ResidentRequests=0;TitleKey=MessageKey=null;}
}
