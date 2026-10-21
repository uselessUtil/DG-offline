using System;
using System.Reflection;
using LitJson;
using ServerDataDefine;

public static class LocalApiTests
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
        Singleton<MasterDataManager>.Instance = new MasterDataManager();
        Singleton<LocalSaveManager>.Instance = new LocalSaveManager();
        server = new ServerForgery(); Singleton<ServerForgery>.Instance = server;
        typeof(ServerForgery).GetMethod("Start", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(server, null);
        Check(server.m_MasterDataParamChara.Length == 523 && server.m_MasterDataQuest.Length == 371, "Embedded master tables load through original LitJson");
        Check(new LocalSaveManager().LoadFuncServerAddressIP() == "", "API address is empty (local routing)");
        Check(!PatchUtil.CheckAppUpdate(), "No forced application update");
        if (args.Length > 0 && args[0] == "reload")
        {
            Check(Save != null && Save.user_name == "OfflineTest", "Account survives a separate process");
            Check(Save.quest_id == 0 && Save.unit_list.Length >= 3, "Cleared quest and party survive reload");
            return;
        }
        var create = new SendCreateUser(); create.uuid = "offline-test-106";
        create.terminal = new PacketStructTerminal(); create.terminal.platform = 1; create.terminal.name = "Docker test";
        var created = Api<RecvCreateUser>(SERVER_API.SERVER_API_USER_CREATE, create);
        Check(created.result.player.user.user_id == 123456789, "Local account creation");
        var rename = new SendRenameUser(); rename.user_name = "OfflineTest";
        Api<RecvRenameUser>(SERVER_API.SERVER_API_USER_RENAME, rename);
        var party = new SendSelectDefParty(); party.select_party = 1;
        Api<RecvSelectDefParty>(SERVER_API.SERVER_API_USER_SELECT_DEF_PARTY, party);
        Check(Save.unit_list != null && Save.unit_list.Length >= 3, "Starter party assigned");
        var auth = new SendUserAuthentication(); auth.uuid = create.uuid;
        Api<RecvUserAuthentication>(SERVER_API.SERVER_API_USER_AUTHENTICATION, auth);
        Api<RecvMasterDataAll>(SERVER_API.SERVER_API_MASTER_DATA_GET_ALL, new SendGetMasterDataAll());
        var login = new SendLoginPack(); login.get_login = 1; login.get_helper = 1; login.get_friend = 1; login.get_present = 1;
        Api<RecvLoginPack>(SERVER_API.SERVER_API_GET_LOGIN_PACK, login);
        var start = new SendQuestStart(); start.quest_id = 1; start.helper_unit = new PacketStructUnit();
        start.helper_unit.id = 139; start.helper_unit.level = 1;
        Api<RecvQuestStart>(SERVER_API.SERVER_API_QUEST_START, start);
        Check(Save.quest_id == 1, "Tutorial quest starts and persists");
        var clear = new SendQuestClear(); clear.quest_id = 1; clear.get_money = 2000; clear.get_exp = 14;
        Api<RecvQuestClear>(SERVER_API.SERVER_API_QUEST_CLEAR, clear);
        Check(Save.quest_id == 0 && Save.have_money > 0, "Tutorial quest clear grants rewards");
        server.UserParamSave();
    }
}
