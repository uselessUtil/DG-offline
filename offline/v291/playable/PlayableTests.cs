using System;
using System.Reflection;
using LitJson;
using ServerDataDefine;

public static class Playable291Tests
{
    static uint serial = 1;
    static ServerForgery server;
    static ServerForgerySaveUser Save { get { return (ServerForgerySaveUser)typeof(ServerForgery).GetField("m_ForgeryPlayerSave", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(server); } }
    static void Check(bool test, string message) { if (!test) throw new Exception(message); Console.WriteLine("PASS " + message); }
    static T Api<T>(SERVER_API api, object request)
    {
        var packet = new PacketData(); packet.m_PacketAPI = api; packet.m_PacketUniqueNum = serial++;
        string response = server.CreateRecvForgery(ref packet, JsonMapper.ToJson(request));
        if (response == null) throw new Exception("No response for " + api);
        var json = JsonMapper.ToObject(response);
        Check((int)json["header"]["code"] == (int)API_CODE.API_CODE_SUCCESS, api + " success");
        return JsonMapper.ToObject<T>(response);
    }
    public static void Main(string[] args)
    {
        Singleton<TimeManager>.Instance = new TimeManager();
        Singleton<MasterDataManager>.Instance = new MasterDataManager();
        Singleton<LocalSaveManager>.Instance = new LocalSaveManager();
        server = new ServerForgery(); Singleton<ServerForgery>.Instance = server;
        typeof(ServerForgery).GetMethod("Start", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(server, null);
        Check(server.m_MasterDataParamChara.Length == 596 && server.m_MasterDataQuest.Length == 399, "Embedded master tables load through original LitJson");
        Singleton<GameTextManager>.Instance=new GameTextManager();
        foreach(var row in server.m_MasterDataTextDefinition) {
            string text=Singleton<GameTextManager>.Instance.GetText(row.text_key);
            Check(text==row.text && text.IndexOf("???")<0,"Missing-text fallback resolves through original GetText: "+row.text_key);
        }
        Check(Singleton<GameTextManager>.Instance.GetText("BTN_TUTORIALSKIP_NO")=="受ける","First dialog left button means receive tutorial");
        Check(Singleton<GameTextManager>.Instance.GetText("BTN_TUTORIALSKIP_CONFIRM_NO")=="戻る","Second dialog right button returns without skipping");
        Check(string.Format(Singleton<GameTextManager>.Instance.GetText("AILMENT_DETAIL_TURN"),3)=="残り 3 ターン","Status turn placeholder formats correctly");
        Check(new LocalSaveManager().LoadFuncServerAddressIP() == "", "API address is empty (local routing)");
        Check(!PatchUtil.CheckAppUpdate(), "No forced application update");
        if (args.Length > 0 && args[0] == "reload")
        {
            Check(Save != null && Save.user_name == "OfflineTest", "Account survives a separate process");
            Check(Save.quest_id == 0 && Save.unit_list.Length >= 3, "Cleared quest and party survive reload");
            Check(Save.flag_quest_clear[0] != 0,"Tutorial clear flag survives process restart");
            return;
        }
        var create = new SendCreateUser(); create.uuid = "offline-test-291";
        create.terminal = new PacketStructTerminal(); create.terminal.platform = 1; create.terminal.name = "Docker test";
        var created = Api<RecvCreateUser>(SERVER_API.SERVER_API_USER_CREATE, create);
        Check(created.result.player.user.user_id > 0, "Local account creation");
        var rename = new SendRenameUser(); rename.user_name = "OfflineTest";
        Api<RecvRenameUser>(SERVER_API.SERVER_API_USER_RENAME, rename);
        var party = new SendSelectDefParty(); party.select_party = 1;
        Api<RecvSelectDefParty>(SERVER_API.SERVER_API_USER_SELECT_DEF_PARTY, party);
        Check(Save.unit_list != null && Save.unit_list.Length >= 3, "Starter party assigned");
        var auth = new SendUserAuthentication(); auth.uuid = create.uuid;
        Api<RecvUserAuthentication>(SERVER_API.SERVER_API_USER_AUTHENTICATION, auth);
        Api<RecvMasterDataAll>(SERVER_API.SERVER_API_MASTER_DATA_GET_ALL, new SendGetMasterDataAll{hash=new PacketStructMasterHash[0]});
        var types=(EMASTERDATA_SERVER[])Enum.GetValues(typeof(EMASTERDATA_SERVER));var hashes=new PacketStructMasterHash[types.Length];for(int i=0;i<hashes.Length;i++)hashes[i]=new PacketStructMasterHash{type=(uint)types[i],hash="request"};
        var all=Api<RecvMasterDataAll>(SERVER_API.SERVER_API_MASTER_DATA_GET_ALL,new SendGetMasterDataAll{hash=hashes});
        Check(all.result.master_array_chara.Length==596 && all.result.master_array_quest.Length==399,"Full client master-data request includes core tables");
        var login = new SendLoginPack(); login.get_login = 1; login.get_helper = 1; login.get_friend = 1; login.get_present = 1;
        Api<RecvLoginPack>(SERVER_API.SERVER_API_GET_LOGIN_PACK, login);
        Check(Save.have_stone==0,"Login does not fabricate tutorial chips");
        var start = new SendQuestStart(); start.quest_id = 1; start.helper_unit = new PacketStructUnit();
        start.helper_unit.id = 139; start.helper_unit.level = 1;
        Api<RecvQuestStart>(SERVER_API.SERVER_API_QUEST_START, start);
        Check(Save.quest_id == 1, "Tutorial quest starts and persists");
        var clear = new SendQuestClear(); clear.quest_id = 1; clear.get_money = 2000; clear.get_exp = 14;
        Api<RecvQuestClear>(SERVER_API.SERVER_API_QUEST_CLEAR, clear);
        Check(Save.quest_id == 0 && Save.have_money > 0, "Tutorial quest clear grants rewards");
        Check(server.m_MasterDataGacha.Length==7 && server.m_MasterDataGachaAssign.Length>0,"Seven local scratch pools enabled");
        Check(MasterDataUtil.GetGachaParamFromID(10)!=null,"Original tutorial scratch pool resolves");
        Check(Save.have_stone==5,"Original tutorial quest awards exactly five chips");
        int unitsBefore=Save.unit_list.Length;
        var scratch=Api<RecvGachaPlay>(SERVER_API.SERVER_API_GACHA_PLAY,new SendGachaPlay{gacha_id=10,gacha_ct=1});
        Check(Save.have_stone==0 && Save.unit_list.Length==unitsBefore+1 && scratch.result.unit_unique_id.Length==1,"Original five chips complete one tutorial scratch");
        var tutorial=new TutorialManager();Singleton<TutorialManager>.Instance=tutorial;
        var groups=new TutorialGroup[27];
        groups[(int)TUTORIAL_GROUP.GROUP_MAINMENU_SCRATCH]=new TutorialGroup(TUTORIAL_GROUP.GROUP_MAINMENU_SCRATCH,200u);
        typeof(TutorialManager).GetField("m_TutorialGroup",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(tutorial,groups);
        foreach(MAINMENU_SEQ page in Enum.GetValues(typeof(MAINMENU_SEQ))) {
            string name=page.ToString();
            if(name.Contains("GACHA"))Check(DivineGateOffline.AllowPage291(page),"Tutorial scratch page remains reachable: "+name);
            if(name.Contains("ACHIEVEMENT") || name.Contains("LOGIN_SCRATCH") || name.Contains("INFORMATION"))
                Check(!DivineGateOffline.AllowPage291(page),"Unsupported page disabled: "+name);
        }
        groups[(int)TUTORIAL_GROUP.GROUP_MAINMENU_SCRATCH].m_GroupFinish=true;
        foreach(MAINMENU_SEQ page in Enum.GetValues(typeof(MAINMENU_SEQ)))
            if(page.ToString().Contains("GACHA"))Check(DivineGateOffline.AllowPage291(page),"Ordinary scratch available after tutorial: "+page);
        Check(Save.present_list != null && Save.present_list.Length==0,"New account has no developer demonstration gifts");
        Check(typeof(ServerForgery).GetMethod("CreateLoginMonthlyList",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(server,null)==null,"Unsupported monthly login is disabled");
        int characters=0;
        foreach(var c in server.m_MasterDataParamChara) {
            if(c.fix_id==0)continue;
            for(int kind=0;kind<4;kind++) {
                CharaUtil.GetStatusValue(c,c.level_min,kind);
                CharaUtil.GetStatusValue(c,c.level_max,kind);
            }
            characters++;
        }
        Check(characters==595,"All 595 backup characters evaluate growth/status at both level bounds");
        int questCount=0, floors=0;
        var build=typeof(ServerForgery).GetMethod("CreateQuestBuild",BindingFlags.Static|BindingFlags.Public);
        foreach(var q in server.m_MasterDataQuest) {
            if(q.fix_id==0 || q.active!=2)continue;
            bool hasFloor=false;
            foreach(var f in server.m_MasterDataQuestFloor) if(f.quest_id==q.fix_id){hasFloor=true;floors++;}
            if(!hasFloor)continue;
            try { Check(build.Invoke(server,new object[]{q.fix_id})!=null,"Quest build "+q.fix_id); questCount++; }
            catch(TargetInvocationException ex){throw new Exception("Quest build failed "+q.fix_id,ex.InnerException);}
        }
        Check(questCount>350,"Active quests with floors build through original enemy/panel/drop generation: "+questCount);
        server.UserParamSave();
    }
}
