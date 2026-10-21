using System;
using System.IO;
using System.Reflection;
using LitJson;
using ServerDataDefine;

public static class VerifyCollabGacha
{
    static ServerForgery server;
    static uint serial = 1;
    static ServerForgerySaveUser Save { get { return (ServerForgerySaveUser)typeof(ServerForgery).GetField("m_ForgeryPlayerSave", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(server); } }
    static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
    static T[] Original<T>(string table)
    {
        return JsonMapper.ToObject<T[]>(File.ReadAllText(Path.Combine(Environment.GetEnvironmentVariable("DG_TEST_TABLES"), table + ".json")));
    }
    static T Api<T>(SERVER_API api, object request)
    {
        var packet = new PacketData(); packet.m_PacketAPI = api; packet.m_PacketUniqueNum = serial++;
        string text = server.CreateRecvForgery(ref packet, JsonMapper.ToJson(request));
        Check((int)JsonMapper.ToObject(text)["header"]["code"] == (int)API_CODE.API_CODE_SUCCESS, "API " + api);
        return JsonMapper.ToObject<T>(text);
    }
    public static void Main(string[] args)
    {
        var master = new MasterDataManager(); Singleton<MasterDataManager>.Instance = master;
        Singleton<LocalSaveManager>.Instance = new LocalSaveManager();
        var user = new UserDataAdmin(); Singleton<UserDataAdmin>.Instance = user;
        user.m_StructPlayer = new PacketStructPlayer(); user.m_StructPlayer.flag_unit_get = new byte[200];
        server = new ServerForgery(); Singleton<ServerForgery>.Instance = server;
        typeof(ServerForgery).GetMethod("Start", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(server, null);
        uint[] ids = { 300100, 300200, 300300, 300400 };
        var expected = Original<MasterDataGacha>("MasterDataGacha"); int unlocked = 0;
        for (int i = 0; i < expected.Length; i++)
        {
            var row = expected[i]; var actual = master.m_MasterDataGacha[i];
            if (row.type == 3)
            {
                unlocked++; Check(actual.event_id == 0 && actual.price == 5 && actual.name != "限定スクラッチ", "Pool not open/named");
                row.event_id = 0; row.name = actual.name; row.detail_2 = actual.detail_2;
            }
            Check(JsonMapper.ToJson(row) == JsonMapper.ToJson(actual), "Unexpected pool mutation " + row.fix_id);
        }
        Check(unlocked == 4, "Wrong pool count");
        Check(JsonMapper.ToJson(server.m_MasterDataGachaAssign) == JsonMapper.ToJson(Original<MasterDataGachaAssign>("MasterDataGachaAssign")), "Original weights/levels/pluses changed");
        Check(ReferenceEquals(server.m_MasterDataGacha, master.m_MasterDataGacha), "Menu/API table mismatch");
        Check(DivineGateOffline.CollabPoolID(-1) == 0 && DivineGateOffline.CollabPoolID(4) == 0, "Invalid pool index accepted");
        for (int i = 0; i < 4; i++)
        {
            Check(DivineGateOffline.CollabPoolID(i) == ids[i], "Wrong card/pool mapping");
            var pos = DivineGateOffline.CollabChoicePosition(i);
            Check(Math.Abs(pos.x) + 89 < 320 && pos.y + 107.5 < 293 && pos.y - 107.5 > -350, "Card outside body/title bounds");
            for (int j = 0; j < i; j++)
            {
                var other = DivineGateOffline.CollabChoicePosition(j);
                Check(Math.Abs(pos.x - other.x) >= 178 || Math.Abs(pos.y - other.y) >= 215, "Overlapping card colliders");
            }
        }
        if (args.Length > 0)
        {
            Check(Save != null && Save.have_stone == 0 && Save.have_stone_free == 0 && Save.unit_list.Length == 40, "Reload cards/balance");
            Console.WriteLine("PASS collab reload: all 40 awarded cards and final chip balance persist across processes.");
            return;
        }
        var create = new SendCreateUser(); create.uuid = "selector-test-106";
        create.terminal = new PacketStructTerminal(); create.terminal.platform = 1; create.terminal.name = "Docker selector test";
        Api<RecvCreateUser>(SERVER_API.SERVER_API_USER_CREATE, create);
        Save.have_stone = Save.have_stone_free = 200; Save.have_stone_pay = 0;
        Save.unit_base = Save.unit_total = 400;
        string beforeSelection = JsonMapper.ToJson(Save);
        foreach (uint id in ids)
        {
            Check(DivineGateOffline.SelectLocalCollabPool(id) && MainMenuParam.m_GachaID == id, "Selection routes to wrong pool");
            Check(ReferenceEquals(MainMenuParam.m_GachaPrevUnitGetFlag, user.m_StructPlayer.flag_unit_get), "Collection flags lost");
            Check(MasterDataUtil.GetGachaParamFromID(MainMenuParam.m_GachaID).price == 5, "Wrong confirmation master row");
        }
        foreach (uint invalid in new uint[] { 0, 10, 200100, 999999 })
            Check(!DivineGateOffline.SelectLocalCollabPool(invalid) && MainMenuParam.m_GachaID == 300400, "Invalid selection changed pool");
        Check(JsonMapper.ToJson(Save) == beforeSelection, "Selecting a pool spends chips or changes inventory");
        foreach (uint id in ids)
        {
            Check(DivineGateOffline.SelectLocalCollabPool(id), "Cannot select pool");
            foreach (uint count in new uint[] { 1, 9 })
            {
                uint balance = Save.have_stone; int before = Save.unit_list == null ? 0 : Save.unit_list.Length;
                var request = new SendGachaPlay(); request.gacha_id = MainMenuParam.m_GachaID; request.gacha_ct = count;
                var response = Api<RecvGachaPlay>(SERVER_API.SERVER_API_GACHA_PLAY, request);
                Check(response.result.unit_unique_id.Length == count && Save.have_stone == balance - 5 * count && Save.unit_list.Length == before + count, "Count/cost/inventory mismatch");
                foreach (long uid in response.result.unit_unique_id)
                {
                    PacketStructUnit card = null;
                    foreach (var unit in Save.unit_list) if (unit.unique_id == uid) card = unit;
                    Check(card != null, "Missing awarded card"); bool eligible = false;
                    foreach (var assignment in server.m_MasterDataGachaAssign)
                        if (assignment.gacha_id == id && assignment.active == 2 && assignment.assign_rate > 0 && assignment.assign_chara_id == card.id) eligible = true;
                    Check(eligible, "Card from another pool");
                }
            }
        }
        var failed = new PacketData(); failed.m_PacketAPI = SERVER_API.SERVER_API_GACHA_PLAY; failed.m_PacketUniqueNum = serial++;
        var noChips = new SendGachaPlay(); noChips.gacha_id = 300100; noChips.gacha_ct = 1;
        string beforeFailure = JsonMapper.ToJson(Save);
        string rejected = server.CreateRecvForgery(ref failed, JsonMapper.ToJson(noChips));
        Check((int)JsonMapper.ToObject(rejected)["header"]["code"] != (int)API_CODE.API_CODE_SUCCESS && beforeFailure == JsonMapper.ToJson(Save), "Insufficient balance changed save");
        server.UserParamSave();
        Console.WriteLine("PASS selector data: four permanent named pools, exact original weights/levels/pluses, other pools unchanged; four non-overlapping cards; selection sets correct ID/collection flags without spending.");
        Console.WriteLine("PASS collab API: single + nine draws from each selected pool, 40 eligible cards, original 5-chip cost, insufficient-balance rejection. Android selector and scratch animation remain unverified.");
    }
}
