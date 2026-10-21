using System;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using LitJson;
using ServerDataDefine;

public static class VerifyFreeChipShop
{
    static ServerForgery server;
    static uint serial = 1;
    static ServerForgerySaveUser Save { get { return (ServerForgerySaveUser)typeof(ServerForgery).GetField("m_ForgeryPlayerSave", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(server); } }
    static void Check(bool test, string message) { if (!test) throw new Exception(message); }
    static void Api(SERVER_API api, object request)
    {
        var packet = new PacketData(); packet.m_PacketAPI = api; packet.m_PacketUniqueNum = serial++;
        var response = JsonMapper.ToObject(server.CreateRecvForgery(ref packet, JsonMapper.ToJson(request)));
        Check((int)response["header"]["code"] == (int)API_CODE.API_CODE_SUCCESS, "API failed: " + api);
    }
    static void SetField(string name, object value)
    {
        typeof(DivineGateOffline).GetField(name, BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, value);
    }
    static void Reject(string id)
    {
        string before = JsonMapper.ToJson(Save);
        bool rejected = false;
        try { DivineGateOffline.ClaimFreeChips(id); } catch (InvalidOperationException) { rejected = true; }
        Check(rejected && before == JsonMapper.ToJson(Save), "Rejected pack changed save: " + id);
    }
    public static void Main(string[] args)
    {
        Singleton<MasterDataManager>.Instance = new MasterDataManager();
        Singleton<LocalSaveManager>.Instance = new LocalSaveManager();
        Singleton<StoreManager>.Instance = new StoreManager();
        Singleton<UserDataAdmin>.Instance = new UserDataAdmin();
        Singleton<UserDataAdmin>.Instance.m_StructPlayer = new PacketStructPlayer();
        server = new ServerForgery(); Singleton<ServerForgery>.Instance = server;
        typeof(ServerForgery).GetMethod("Start", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(server, null);
        uint[] quantities = { 1, 6, 12, 25, 52, 85 };
        var rows = JsonMapper.ToObject<MasterDataStoreProduct[]>(File.ReadAllText(Path.Combine(Environment.GetEnvironmentVariable("DG_TEST_TABLES"), "MasterDataStoreProduct.json")));
        int index = 0;
        foreach (var row in rows)
        {
            if (row.platform != 1 || index >= 6) continue;
            var product = StoreUtil.GetProductFromNum(index);
            Check(row.point == quantities[index] && product.product_num == row.point && product.product_id == row.id, "Original quantity/ID mismatch");
            Check(product.product_active && product.product_price == 0 && product.product_price_format == "FREE", "Paid/inactive product");
            Check(StoreUtil.GetProductFromID(product.product_id).product_num == product.product_num, "ID lookup mismatch");
            index++;
        }
        Check(index == 6 && StoreUtil.GetProductFromNum(-1) == null && StoreUtil.GetProductFromNum(6) == null && StoreUtil.GetProductFromID("invalid") == null, "Invalid product accepted");
        Check(!Singleton<StoreManager>.Instance.m_StoreInitOK, "External billing enabled");
        if (args.Length > 0)
        {
            Check(Save != null && Save.have_stone == 38 && Save.have_stone_free == 35 && Save.have_stone_pay == 3, "Balance lost across process");
            Check(Save.pay_total == 123 && Save.pay_month == 45, "Paid history changed on reload");
            Check(DivineGateOffline.LocalChipShopOpen(), "Shop closed after reload");
            Console.WriteLine("PASS free chip shop reload: exact free/paid balance and paid history survive a separate process.");
            return;
        }
        Check(!DivineGateOffline.LocalChipShopOpen(), "Shop open without account");
        bool noAccount = false;
        try { DivineGateOffline.ClaimFreeChips("jp.gungho.dg.chip1"); } catch (InvalidOperationException) { noAccount = true; }
        Check(noAccount, "Claim without account");
        var create = new SendCreateUser(); create.uuid = "free-chip-test";
        create.terminal = new PacketStructTerminal(); create.terminal.platform = 1; create.terminal.name = "Docker shop test";
        Api(SERVER_API.SERVER_API_USER_CREATE, create);
        Check(DivineGateOffline.LocalChipShopOpen(), "Shop closed with account");
        Save.have_stone_free = 10; Save.have_stone_pay = 3; Save.have_stone = 13;
        Save.pay_total = 123; Save.pay_month = 45;
        string baseline = JsonMapper.ToJson(Save);
        uint total = 13;
        for (int i = 0; i < 6; i++)
        {
            total += quantities[i];
            Check(DivineGateOffline.ClaimFreeChips(StoreUtil.GetProductFromNum(i).product_id) == total, "Wrong grant amount");
            Check(Save.have_stone_free + Save.have_stone_pay == total && Save.have_stone_pay == 3, "Wrong free/paid accounting");
            Check(Save.pay_total == 123 && Save.pay_month == 45, "Paid purchase history changed");
            Check(Singleton<UserDataAdmin>.Instance.m_StructPlayer.have_stone == total && Singleton<UserDataAdmin>.Instance.m_StructPlayer.have_stone_free == Save.have_stone_free, "UI balance stale");
        }
        uint retained = Save.have_stone_free;
        Save.have_stone_free = 10; Save.have_stone = 13;
        Check(JsonMapper.ToJson(Save) == baseline, "Non-chip account fields changed");
        Save.have_stone_free = retained; Save.have_stone = total;
        Reject(null); Reject(""); Reject("jp.gungho.dg.chip30");
        Save.have_stone_free = 996; Save.have_stone_pay = 3; Save.have_stone = 999;
        Reject("jp.gungho.dg.chip1");
        Save.have_stone_free = 995; Save.have_stone = 998;
        Reject("jp.gungho.dg.chip6");
        Check(DivineGateOffline.ClaimFreeChips("jp.gungho.dg.chip1") == 999, "Exact cap refused");
        Save.have_stone_free = uint.MaxValue; Save.have_stone = uint.MaxValue;
        Reject("jp.gungho.dg.chip85");
        Save.have_stone_free = 10; Save.have_stone_pay = 3; Save.have_stone = 13;
        var page = (MainMenuSeqShopBuyTip)FormatterServices.GetUninitializedObject(typeof(MainMenuSeqShopBuyTip));
        var other = (MainMenuSeqShopBuyTip)FormatterServices.GetUninitializedObject(typeof(MainMenuSeqShopBuyTip));
        SetField("freeChipPage", page); SetField("freeChipProduct", "jp.gungho.dg.chip25"); SetField("freeChipCommitted", false);
        MethodInfo commit = typeof(DivineGateOffline).GetMethod("CommitFreeChipPage", BindingFlags.NonPublic | BindingFlags.Static);
        object[] call = { other, null };
        Check(!(bool)commit.Invoke(null, call) && Save.have_stone == 13, "Foreign page claimed");
        call[0] = page;
        Check((bool)commit.Invoke(null, call) && Save.have_stone == 38, "Confirmation failed");
        Check(!(bool)commit.Invoke(null, call) && Save.have_stone == 38, "Repeated confirmation double-claimed");
        SetField("freeChipCommitted", true);
        Check(!(bool)commit.Invoke(null, call) && Save.have_stone == 38, "Cancelled confirmation claimed");
        // A new visit allows another free pack.
        SetField("freeChipCommitted", false); SetField("freeChipProduct", "jp.gungho.dg.chip1");
        Check((bool)commit.Invoke(null, call) && Save.have_stone == 39, "Repeat visit blocked");
        // The original API can spend the new free currency on capacity.
        Save.have_stone_free = 10; Save.have_stone_pay = 0; Save.have_stone = 10;
        uint oldCapacity = Save.unit_total;
        Api(SERVER_API.SERVER_API_STONE_USE_UNIT, new SendStoneUsedUnit());
        Check(Save.have_stone_free == 9 && Save.have_stone == 9 && Save.unit_total == oldCapacity + 5, "Granted free chips cannot be spent");
        Save.have_stone_free = 35; Save.have_stone_pay = 3; Save.have_stone = 38;
        server.UserParamSave();
        Console.WriteLine("PASS free chip shop: exact six original quantities/IDs, FREE prices, no billing, valid account required, free-only grants, immediate UI balance, unchanged paid history/account fields.");
        Console.WriteLine("PASS free chip boundaries: invalid packs, 999 cap/overflow, whole-pack rejection, one grant per confirmation, cancelled/foreign pages, repeat visits, original unit-expansion spending API. Android UI remains unverified.");
    }
}
