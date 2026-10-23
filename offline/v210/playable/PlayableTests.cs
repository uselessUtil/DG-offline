using System;
using System.Reflection;
using LitJson;
using ServerDataDefine;

public static class Playable210Tests
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
        Check(new LocalSaveManager().LoadFuncServerAddressIP() == "", "API address is empty (local routing)");
        Check(!PatchUtil.CheckAppUpdate(), "No forced application update");
        if (args.Length > 0 && args[0] == "reload")
        {
            Check(Save != null && Save.user_name == "OfflineTest", "Account survives a separate process");
            Check(Save.quest_id == 0 && Save.unit_list.Length >= 3, "Cleared quest and party survive reload");
            Check(Save.flag_quest_clear[0] != 0,"Tutorial clear flag survives process restart");
            return;
        }
        var create = new SendCreateUser(); create.uuid = "offline-test-210";
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
        var start = new SendQuestStart(); start.quest_id = 1; start.helper_unit = new PacketStructUnit();
        start.helper_unit.id = 139; start.helper_unit.level = 1;
        Api<RecvQuestStart>(SERVER_API.SERVER_API_QUEST_START, start);
        Check(Save.quest_id == 1, "Tutorial quest starts and persists");
        var clear = new SendQuestClear(); clear.quest_id = 1; clear.get_money = 2000; clear.get_exp = 14;
        Api<RecvQuestClear>(SERVER_API.SERVER_API_QUEST_CLEAR, clear);
        Check(Save.quest_id == 0 && Save.have_money > 0, "Tutorial quest clear grants rewards");
        Check(server.m_MasterDataGacha.Length==46 && server.m_MasterDataGachaAssign.Length==11000,"Donor scratch tables load alongside the unchanged core backup tables");
        Check(MasterDataUtil.GetGachaParamFromID(10)!=null,"Original tutorial scratch pool resolves");
        Check(Save.present_list != null,"Present list is initialized without fabricated gifts");
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
