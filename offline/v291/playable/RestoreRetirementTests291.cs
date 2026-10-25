using System;using System.IO;using System.Reflection;using LitJson;using ServerDataDefine;
class RestoreRetirementTests291 {
 const BindingFlags F=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance;
 static ServerForgery server;static string seed;
 static void Check(bool b,string s){if(!b)throw new Exception(s);}
 static int Probe(string n){return (int)typeof(ServerForgery).Assembly.GetType("RestoreProbe291").GetField(n).GetValue(null);}
 static void SetProbe(string n,int v){typeof(ServerForgery).Assembly.GetType("RestoreProbe291").GetField(n).SetValue(null,v);}
 static ServerForgerySaveUser Player(){return DivineGateOffline.Local291Player(server);}
 static string State(){return JsonMapper.ToJson(Player());}
 static int Phase(TitleMenuSeqRestoreChk p){return (int)typeof(TitleMenuSeqRestoreChk).GetField("m_RestoreSeq",F).GetValue(p);}
 static void SetPlayer(string text){typeof(ServerForgery).GetField("m_ForgeryPlayerSave",F).SetValue(server,JsonMapper.ToObject<ServerForgerySaveUser>(text));}
 static void EmptyCaches(){
  var l=Singleton<LocalSaveManager>.Instance;l.SaveFuncGoesToGameStart(null);l.SaveFuncGoesToGameRestore(null);
  l.SaveFuncGoesToMenuResult(null);l.SaveFuncGoesToMenuRetire(null);
  l.SaveFuncResultVersion();l.SaveFuncRestoreVersion();
 }
 static void Arm(bool evolution,bool resume) {
  SetPlayer(seed);EmptyCaches();var p=Player();p.quest_id=17;p.quest_status=0;
  p.evolve_quest_active=evolution;p.evolve_quest_id=evolution?17u:0u;server.UserParamSave();
  if(resume)Singleton<LocalSaveManager>.Instance.SaveFuncGoesToGameStart(new SceneGoesParamToGameMain{m_QuestMissionID=17,m_QuestEvolve=evolution});
 }
 static void SameExceptRetired(string before) {
  var expected=JsonMapper.ToObject<ServerForgerySaveUser>(before);uint quest=expected.quest_id;
  ServerDataUtil.AddBitFlag(ref expected.flag_quest_check,quest);
  expected.quest_id=expected.quest_status=expected.quest_continue_ct=expected.quest_reset_ct=0;
  expected.quest_build=null;expected.evolve_quest_active=false;
  expected.evolve_quest_id=expected.evolve_quest_status=expected.evolve_pattern=expected.evolve_helper_user_id=expected.evolve_helper_premium=0;
  expected.evolve_unit_base=0;expected.evolve_unit_parts=null;expected.evolve_helper_unit=null;
  Check(State()==JsonMapper.ToJson(expected),"Retirement changed non-quest fields: units/party/currency/stamina/gifts/progress");
 }
 static void Main(string[] args) {
  Singleton<TimeManager>.Instance=new TimeManager();Singleton<MasterDataManager>.Instance=new MasterDataManager();
  Singleton<LocalSaveManager>.Instance=new LocalSaveManager();Singleton<UserDataAdmin>.Instance=new UserDataAdmin();
  Singleton<TitleMenuManager>.Instance=new TitleMenuManager();
  server=new ServerForgery();Singleton<ServerForgery>.Instance=server;
  typeof(ServerForgery).GetMethod("Start",F).Invoke(server,null);
  string directory=Environment.GetEnvironmentVariable("DG_TEST_SAVE_DIR");Directory.CreateDirectory(directory);
  if(args.Length>0 && args[0]=="reload") {
   Check(State()==File.ReadAllText(Path.Combine(directory,"before-crash.json")),"Interrupted retirement not restored before native load");
   Check(!UnityEngine.PlayerPrefs.HasKey("DG291_QUEST_RETIRE_JOURNAL"),"Recovery journal not consumed");
   var page=new TitleMenuSeqRestoreChk();page.RestoreFuncInit();Check(Phase(page)==5,"Recovered orphan not routed to cancellation");
   page.RestoreFuncChkFinishCancel();Check(DivineGateOffline.SaveTransferAllowed(server),"Recovered quest remains blocked");
   SameExceptRetired(File.ReadAllText(Path.Combine(directory,"before-crash.json")));
   Console.WriteLine("PASS interrupted retirement restores exact account on fresh-process native load; orphan retry then retires once and allows backup.");return;
  }
  bool phone=Environment.GetEnvironmentVariable("DG_PHONE_FIXTURE")=="1";
  if(!phone) {
   server.UserParamCreate(new SendCreateUser{uuid="restore-cancel-test",terminal=new PacketStructTerminal{platform=1}});
   var p=Player();p.unit_party_assign=new PacketStructPartyAssign[5];for(int i=0;i<5;i++)p.unit_party_assign[i]=new PacketStructPartyAssign();
   var add=typeof(ServerForgery).GetMethod("AddUnit",F);add.Invoke(server,new object[]{47u,10u,0u,0u});add.Invoke(server,new object[]{59u,1u,0u,0u});
   p.have_money=3780;p.have_stone=p.have_stone_free=35;p.stamina_now=103;server.UserParamSave();
  }
  seed=State();
  if(args.Length>0 && args[0]=="baseline") {
   Arm(true,true);var page=new TitleMenuSeqRestoreChk();page.RestoreFuncChkFinishCancel();
   Check(Player().quest_id==17 && Player().evolve_quest_active && !DivineGateOffline.SaveTransferAllowed(server),"Baseline did not leave quest active");
   page=new TitleMenuSeqRestoreChk();page.RestoreFuncInit();
   Check(Phase(page)==6 && Probe("Retires")==0,"Baseline did not skip orphan restore");
   Console.WriteLine("REPRODUCED r16: decline clears resume cache without original retirement; re-login passes directly to menu; backup remains blocked.");return;
  }
  if(phone) {
   Check(Player().quest_id==17 && Player().evolve_quest_active,"Phone fixture not original orphan");
   var page=new TitleMenuSeqRestoreChk();string before=State();int count=Probe("Retires");
   page.RestoreFuncInit();Check(Phase(page)==5,"Actual phone orphan not routed to retirement");
   page.RestoreFuncChkFinishCancel();SameExceptRetired(before);
   Check(Probe("Retires")==count+1 && DivineGateOffline.SaveTransferAllowed(server),"Actual phone fixture remains blocked");
   string backup=DivineGateOffline.ExportSaveBackup(server);DivineGateOffline.ValidateSaveBackup(server,backup);
   File.WriteAllText(Path.Combine(directory,"phone-copy-repaired.dgbackup.json"),backup);
   Console.WriteLine("PASS actual phone BACKUP COPY: original quest17 retired, full non-quest account identical, units/materials/party/currency/stamina/gifts/clear flags intact; real export and backup validation succeed. No device write.");return;
  }
  foreach(bool evolve in new[]{false,true}) {
   Arm(evolve,true);var page=new TitleMenuSeqRestoreChk();string before=State();int count=Probe("Retires");
   page.RestoreFuncInit();Check(Phase(page)==1 && Probe("Retires")==count && State()==before,"Resumable quest changed before choice");
   page.RestoreFuncChkFinishCancel();SameExceptRetired(before);
   Check(Probe("Retires")==count+1 && DivineGateOffline.SaveTransferAllowed(server),"Declined quest not retired");
   Check(!Singleton<LocalSaveManager>.Instance.LoadFuncGoesToGameStartChk(),"Resume cache retained after success");
   page.RestoreFuncChkFinishCancel();Check(Probe("Retires")==count+1,"Repeated cancellation retired twice");
  }
  Console.WriteLine("PASS normal/evolution decline: original retire API exactly once, full account comparison allows only original quest reset/check flag; resumable path and repeated cancellation preserved.");
  Arm(true,false);var orphan=new TitleMenuSeqRestoreChk();orphan.RestoreFuncInit();Check(Phase(orphan)==5,"Old orphan skipped");
  foreach(string key in new[]{"divinegate_GOES_TO_MENU_RESULT","divinegate_GOES_TO_MENU_RETIRE"}) {
   Arm(true,false);LocalSaveUtil.SetValueString(key,"{}");int count=Probe("Retires");string before=State();
   var pending=new TitleMenuSeqRestoreChk();pending.RestoreFuncInit();
   Check(Phase(pending)==6 && count==Probe("Retires") && State()==before,"Pending settlement/retire state discarded");
  }
  Console.WriteLine("PASS old orphan schedules cancellation; pending settlement/retire flows remain untouched.");
  Arm(true,true);var failing=new TitleMenuSeqRestoreChk();string original=State();int switches=Probe("Switches");
  SetProbe("FailSaves",1);failing.RestoreFuncChkFinishCancel();
  Check(State()==original && LocalSaveUtil.GetValueString("SERVER_SAVE","")==original &&
    Singleton<LocalSaveManager>.Instance.LoadFuncGoesToGameStartChk() && Probe("Switches")==switches,"Failed retirement cleared data or advanced");
  failing.RestoreFuncChkFinishCancel();SameExceptRetired(original);Check(DivineGateOffline.SaveTransferAllowed(server),"Retry failed");
  Console.WriteLine("PASS injected retirement-save failure restores account/disk, retains resume cache, blocks navigation; retry succeeds.");
  Arm(true,false);original=State();File.WriteAllText(Path.Combine(directory,"before-crash.json"),original);
  string journal=JsonMapper.ToJson(new Journal{server_save=original,sha256=DivineGateOffline.SaveChecksum(original),quest_id=17});
  LocalSaveUtil.SetValueString("DG291_QUEST_RETIRE_JOURNAL",journal);
  var partial=JsonMapper.ToObject<ServerForgerySaveUser>(original);partial.quest_id=0;partial.evolve_quest_active=false;
  LocalSaveUtil.SetValueString("SERVER_SAVE",JsonMapper.ToJson(partial));LocalSaveUtil.ExecDataSave();
  Console.WriteLine("FIXTURE interrupted retirement persisted after partial save; next process must restore journal.");
 }
 public class Journal {public string server_save,sha256;public uint quest_id;}
}
