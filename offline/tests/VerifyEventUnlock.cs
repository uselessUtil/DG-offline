using System;
using System.IO;
using System.Reflection;
using LitJson;
using ServerDataDefine;

public static class VerifyEventUnlock
{
    static void Check(bool test, string message) { if (!test) throw new Exception(message); }
    static T[] Original<T>(string name)
    {
        string folder = Environment.GetEnvironmentVariable("DG_TEST_STAGE_TABLES");
        string file = string.IsNullOrEmpty(folder) ? "" : Path.Combine(folder, name + ".json");
        if (!File.Exists(file)) file = Path.Combine(Environment.GetEnvironmentVariable("DG_TEST_TABLES"), name + ".json");
        return JsonMapper.ToObject<T[]>(File.ReadAllText(file));
    }
    static uint serial = 1;
    static ServerForgery server;
    static ServerForgerySaveUser Save { get { return (ServerForgerySaveUser)typeof(ServerForgery).GetField("m_ForgeryPlayerSave", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(server); } }
    static void Api(SERVER_API api, object request)
    {
        var packet = new PacketData(); packet.m_PacketAPI = api; packet.m_PacketUniqueNum = serial++;
        var response = JsonMapper.ToObject(server.CreateRecvForgery(ref packet, JsonMapper.ToJson(request)));
        Check((int)response["header"]["code"] == (int)API_CODE.API_CODE_SUCCESS, "API failed: " + api);
    }
    public static void Main()
    {
        var master = new MasterDataManager(); Singleton<MasterDataManager>.Instance = master;
        Singleton<LocalSaveManager>.Instance = new LocalSaveManager();
        server = new ServerForgery(); Singleton<ServerForgery>.Instance = server;
        typeof(ServerForgery).GetMethod("Start", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(server, null);
        var baseline = Original<MasterDataArea>("MasterDataArea");
        Check(ReferenceEquals(master.m_MasterDataArea, server.m_MasterDataArea), "Local API and menu must share area rows");
        Check(baseline.Length == master.m_MasterDataArea.Length, "Area count changed");
        int events = 0, temples = 0;
        for (int i = 0; i < baseline.Length; i++)
        {
            var expected = baseline[i]; var actual = master.m_MasterDataArea[i];
            if (expected.type == 2 || expected.type == 3)
            {
                expected.event_id = 0;
                Check(new TimeEventManager().ChkEventActive(actual.event_id), "Area still depends on schedule/group");
                if (expected.type == 2) { events++; expected.pre_area = 0; } else temples++;
            }
            Check(JsonMapper.ToJson(expected) == JsonMapper.ToJson(actual), "Unexpected area mutation " + expected.fix_id);
        }
        Check(events == 49 && temples == 6, "Wrong unlocked area count");
        Check(JsonMapper.ToJson(Original<MasterDataQuest>("MasterDataQuest")) == JsonMapper.ToJson(master.m_MasterDataQuest), "Quest rewards/stamina/active/once modified");
        Check(JsonMapper.ToJson(Original<MasterDataEvent>("MasterDataEvent")) == JsonMapper.ToJson(master.m_MasterDataEvent), "Unrelated global schedules modified");
        int playable = 0, reserved = 0, story = 0;
        foreach (var quest in master.m_MasterDataQuest)
        {
            var area = MasterDataUtil.GetAreaParamFromID(quest.area_id);
            bool expected = area != null && area.type == 2 && quest.active == 2;
            Check(DivineGateOffline.EventQuestPlayable(quest, false) == expected, "Wrong uncleared quest access " + quest.fix_id);
            Check(DivineGateOffline.EventQuestPlayable(quest, true), "Existing access revoked");
            if (expected) playable++;
            if (area != null && area.type == 2 && quest.active != 2) reserved++;
            if (area != null && area.type == 1) story++;
        }
        Check(playable == 132 && reserved == 7 && story > 0, "Wrong mission coverage");
        Check(!DivineGateOffline.EventQuestPlayable(null, false), "Null quest granted access");
        var create = new SendCreateUser(); create.uuid = "event-test-106";
        create.terminal = new PacketStructTerminal(); create.terminal.platform = 1; create.terminal.name = "Docker event test";
        Api(SERVER_API.SERVER_API_USER_CREATE, create);
        var party = new SendSelectDefParty(); party.select_party = 1;
        Api(SERVER_API.SERVER_API_USER_SELECT_DEF_PARTY, party);
        string saveBefore = JsonMapper.ToJson(Save);
        typeof(DivineGateOffline).GetMethod("OpenLocalEvents", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] { master });
        Check(saveBefore == JsonMapper.ToJson(Save), "Unlock changed save/progress/rewards");
        foreach (var quest in master.m_MasterDataQuest)
        {
            if (!DivineGateOffline.EventQuestPlayable(quest, false)) continue;
            Save.stamina_now = Save.stamina_max = 10000;
            var start = new SendQuestStart(); start.quest_id = quest.fix_id;
            start.helper_unit = new PacketStructUnit(); start.helper_unit.id = 139; start.helper_unit.level = 1;
            Api(SERVER_API.SERVER_API_QUEST_START, start);
            Check(Save.quest_id == quest.fix_id && Save.stamina_now == 10000 - quest.quest_stamina, "Start or stamina mismatch " + quest.fix_id);
            var clear = new SendQuestClear(); clear.quest_id = quest.fix_id;
            Api(SERVER_API.SERVER_API_QUEST_CLEAR, clear);
            Check(Save.quest_id == 0 && ServerDataUtil.ChkBitFlag(ref Save.flag_quest_clear, quest.fix_id), "Clear did not persist " + quest.fix_id);
        }
        Console.WriteLine("PASS events: 49 areas, 132 difficulties, 6 evolution temples permanent; 7 reserves remain inactive; configured stage data and global schedules verified.");
        Console.WriteLine("PASS actual local API: all 132 event quests start, charge configured stamina, clear and set progress. Unlock itself leaves save unchanged. Android gameplay remains unverified.");
    }
}
