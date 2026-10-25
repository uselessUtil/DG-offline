using System;using System.IO;using System.Reflection;using LitJson;using ServerDataDefine;
class ImportLegacy106Tests {
 static ServerForgery server;
 static void Check(bool b,string s){if(!b)throw new Exception(s);}
 static string State(){return DivineGateOffline.ExportSaveBackup(server);}
 static string Wrapped(DGSaveEnvelope e,DGSavePayload p){e.payload=JsonMapper.ToJson(p);e.sha256=DivineGateOffline.SaveChecksum(e.payload);return JsonMapper.ToJson(e);}
 static void Reject(string text,string dir,string why) {
  string before=State();int count=Directory.GetFiles(dir).Length;bool failed=false;
  try{DivineGateOffline.ImportSaveBackup(server,text,dir);}catch{failed=true;}
  Check(failed && JsonMapper.ToObject<DGSaveEnvelope>(State()).payload==JsonMapper.ToObject<DGSaveEnvelope>(before).payload && Directory.GetFiles(dir).Length==count,"Rejected legacy import modified current account: "+why);
 }
 static void Main(string[] args) {
  Singleton<TimeManager>.Instance=new TimeManager();Singleton<MasterDataManager>.Instance=new MasterDataManager();
  Singleton<LocalSaveManager>.Instance=new LocalSaveManager();Singleton<UserDataAdmin>.Instance=new UserDataAdmin();
  server=new ServerForgery();Singleton<ServerForgery>.Instance=server;
  typeof(ServerForgery).GetMethod("Start",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(server,null);
  string dir=args[1];Directory.CreateDirectory(dir);
  if(args.Length>2 && args[2]=="reload") {
   Check(JsonMapper.ToObject<DGSavePayload>(JsonMapper.ToObject<DGSaveEnvelope>(State()).payload).server_save==File.ReadAllText(Path.Combine(dir,"expected-server.json")),"Legacy import changed on process reload");
   Check(Singleton<LocalSaveManager>.Instance.LoadFuncAddFavoriteUnit().m_BufferSize==2,"Favorites not usable on reload");
   Check(Singleton<LocalSaveManager>.Instance.LoadFuncSortTypeUnitList()==6,"Acquisition sort not retained");
   Check(Singleton<LocalSaveManager>.Instance.LoadFuncRestoreVersionCheck(),"Native restore version gate rejected migrated save");
   Console.WriteLine("PASS migrated 1.0.6 account survives new process; native favorites, acquisition sort and restore version gate load correctly.");return;
  }
  server.UserParamCreate(new SendCreateUser{uuid="target-account",terminal=new PacketStructTerminal{platform=1}});
  var target=DivineGateOffline.Local291Player(server);target.user_name="Target before import";target.have_ticket=77;
  target.unit_party_assign=new PacketStructPartyAssign[5];for(int i=0;i<5;i++)target.unit_party_assign[i]=new PacketStructPartyAssign();
  LocalSaveUtil.SetValueInt("divinegate_TUTORIAL_SKIP",1);LocalSaveUtil.SetValueString("divinegate_FILTER_UNIT_LIST","target-filter");
  LocalSaveUtil.SetValueString("divinegate_SCRATCH","target-transient");
  // A syntactically valid target filter is needed for rollback validation.
  LocalSaveUtil.SetValueString("divinegate_FILTER_UNIT_LIST",JsonMapper.ToJson(new LocalSaveFilter{m_FilterElements=new[]{1,1,1,1,1,1,1},m_FilterPriority=0}));
  string text=File.ReadAllText(args[0]),before=State();
  var e=JsonMapper.ToObject<DGSaveEnvelope>(text);var p=JsonMapper.ToObject<DGSavePayload>(e.payload);
  string old=p.server_save;
  e.prototype="future106";Reject(Wrapped(e,p),dir,"unrecognized source revision");e.prototype="1.0.6 offline prototype 22r7";
  var bad=JsonMapper.ToObject<DGSave106Account>(old);
  bad.unit_list[0].id=595;p.server_save=JsonMapper.ToJson(bad);Reject(Wrapped(e,p),dir,"foreign unit ID");
  bad=JsonMapper.ToObject<DGSave106Account>(old);bad.quest_id=420;p.server_save=JsonMapper.ToJson(bad);Reject(Wrapped(e,p),dir,"unfinished source quest");
  bad=JsonMapper.ToObject<DGSave106Account>(old);ServerDataUtil.AddBitFlag(ref bad.flag_quest_clear,999);p.server_save=JsonMapper.ToJson(bad);Reject(Wrapped(e,p),dir,"unknown quest progress");
  bad=JsonMapper.ToObject<DGSave106Account>(old);bad.present_list[5].present_value0=999;p.server_save=JsonMapper.ToJson(bad);Reject(Wrapped(e,p),dir,"unknown gifted unit");
  bad=JsonMapper.ToObject<DGSave106Account>(old);bad.unit_list[1].unique_id=bad.unit_list[0].unique_id;p.server_save=JsonMapper.ToJson(bad);Reject(Wrapped(e,p),dir,"duplicate owned unit");
  p.server_save=old.Substring(0,old.Length-1)+",\"future_field\":1}";Reject(Wrapped(e,p),dir,"unknown source field");
  p.server_save=old.Replace("\"have_money\":12345678","\"have_money\":4294967296");Reject(Wrapped(e,p),dir,"unsigned overflow");
  p.server_save=old;string savedKey=p.preferences[0].key;p.preferences[0].key="divinegate_TUTORIAL_SKIP";Reject(Wrapped(e,p),dir,"target-only source preference");p.preferences[0].key=savedKey;
  DivineGateOffline.ImportSaveBackup(server,text,dir);
  string after=State();var converted=JsonMapper.ToObject<DGSavePayload>(JsonMapper.ToObject<DGSaveEnvelope>(after).payload);
  File.WriteAllText(Path.Combine(dir,"migrated.dgbackup.json"),after);
  File.WriteAllText(Path.Combine(dir,"expected-server.json"),converted.server_save);
  Check(DivineGateOffline.Local291Player(server).have_ticket==0,"Inherited target ticket balance");
  Check(!UnityEngine.PlayerPrefs.HasKey("divinegate_TUTORIAL_SKIP") && !UnityEngine.PlayerPrefs.HasKey("divinegate_FILTER_UNIT_LIST") &&
        !UnityEngine.PlayerPrefs.HasKey("divinegate_SCRATCH"),"Inherited target settings or old battle/scratch transient");
  string[] recovery=Directory.GetFiles(dir,"before-import-*.json");Check(recovery.Length==1,"Missing migration recovery");
  Check(DivineGateOffline.ValidateSaveBackup(server,File.ReadAllText(recovery[0])).server_save==
        JsonMapper.ToObject<DGSavePayload>(JsonMapper.ToObject<DGSaveEnvelope>(before).payload).server_save,"Wrong pre-migration backup");
  Console.WriteLine("PASS legacy validation rejects unknown revision/IDs/fields/progress/gifts, active quests, duplicate units, overflow and target-only preferences before account/file mutation; migration clears target-only state and retains exact pre-import backup.");
 }
}
