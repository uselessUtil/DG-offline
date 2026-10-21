using System;
using System.IO;
using System.Reflection;
using LitJson;
using ServerDataDefine;

// Disposable managed experiment: original game DLL plus a native-engine stub.
// Missing tables remain null. No successful responses or game handlers are patched.
public static class ProbeComparison
{
    static ServerForgery server;
    static uint serial = 1;
    static ServerForgerySaveUser Save
    {
        get { return (ServerForgerySaveUser)typeof(ServerForgery).GetField("m_ForgeryPlayerSave", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(server); }
    }
    static bool HasCharacter(uint id)
    {
        foreach (var row in server.m_MasterDataParamChara) if (row.fix_id == id) return true;
        return false;
    }
    static void Stage(string name, Action action)
    {
        try { action(); Console.WriteLine("PASS_STAGE\t" + name); }
        catch (Exception ex)
        {
            while (ex is TargetInvocationException && ex.InnerException != null) ex = ex.InnerException;
            Console.WriteLine("FAIL_STAGE\t" + name + "\t" + ex.GetType().Name + "\t" + ex.Message);
            Console.WriteLine(ex.StackTrace);
        }
    }
    static object Parse(string filename, Type type)
    {
        foreach (var mapper in typeof(JsonMapper).GetMethods(BindingFlags.Public | BindingFlags.Static))
            if (mapper.Name == "ToObject" && mapper.IsGenericMethodDefinition &&
                mapper.GetParameters().Length == 1 && mapper.GetParameters()[0].ParameterType == typeof(string))
                return mapper.MakeGenericMethod(type).Invoke(null, new object[] { File.ReadAllText(filename) });
        throw new Exception("Original JSON mapper not found");
    }
    static void Api(SERVER_API api, object request)
    {
        var packet = new PacketData(); packet.m_PacketAPI = api; packet.m_PacketUniqueNum = serial++;
        var response = server.CreateRecvForgery(ref packet, JsonMapper.ToJson(request));
        var json = JsonMapper.ToObject(response);
        var code = (int)json["header"]["code"];
        Console.WriteLine("API_CODE\t" + api + "\t" + code);
        if (code != (int)API_CODE.API_CODE_SUCCESS) throw new Exception("API error " + code);
        if (json["result"] == null) throw new Exception("Success response has no result");
    }
    public static void Main(string[] args)
    {
        Singleton<MasterDataManager>.Instance = new MasterDataManager();
        Singleton<LocalSaveManager>.Instance = new LocalSaveManager();
        server = new ServerForgery(); Singleton<ServerForgery>.Instance = server;
        var request = new SendCreateUser(); request.uuid = "comparison-not-real-account";
        request.terminal = new PacketStructTerminal(); request.terminal.platform = 1;
        request.terminal.name = "isolated managed comparison";
        try
        {
            server.UserParamCreate(request);
            throw new Exception("Unexpected creation without master data");
        }
        catch (NullReferenceException) { Console.WriteLine("EXPECTED_FAIL\tbaseline_no_tables_UserParamCreate\tNullReferenceException"); }
        int loaded = 0, rawFailures = 0;
        foreach (var field in typeof(ServerForgery).GetFields())
        {
            if (!field.Name.StartsWith("m_MasterData")) continue;
            var element = field.FieldType.GetElementType();
            var filename = Path.Combine(args[0], element.Name + ".json");
            var raw = Path.Combine(args[1], element.Name + ".json");
            if (!File.Exists(filename)) { Console.WriteLine("MISSING\t" + element.Name); continue; }
            try { Parse(raw, field.FieldType); }
            catch (Exception ex)
            {
                while (ex is TargetInvocationException && ex.InnerException != null) ex = ex.InnerException;
                rawFailures++;
                Console.WriteLine("RAW_REJECT\t" + element.Name + "\t" + ex.Message);
            }
            var table = Parse(filename, field.FieldType);
            field.SetValue(server, table);
            var masterField = typeof(MasterDataManager).GetField(field.Name);
            if (masterField != null && masterField.FieldType == field.FieldType)
                masterField.SetValue(Singleton<MasterDataManager>.Instance, table);
            loaded++;
        }
        Console.WriteLine("LOADED\t" + loaded + "\tRAW_FAILURES\t" + rawFailures);
        Stage("106_rank_UserParamCreate", () => {
            server.UserParamCreate(request);
            var save = Save;
            if (save.stamina_max != 20 || save.unit_base != 20) throw new Exception("Unexpected rank conversion");
        });
        Stage("106_ConvertMasterSkillLimitBreak", () => {
            MasterDataUtil.ConvertMasterSkillLimitBreak(ref server.m_MasterDataSkillLimitBreak);
        });
        Stage("106_CreateUser_API", () => {
            Api(SERVER_API.SERVER_API_USER_CREATE, request);
            if (Save.stamina_max != 20) throw new Exception("Created player has wrong rank data");
            foreach (var gift in Save.present_list)
                if (gift.present_type == 7 && !HasCharacter(gift.present_value0))
                    Console.WriteLine("UNRESOLVED_GIFT_UNIT\t" + gift.present_value0);
        });
        Stage("106_SelectDefaultParty_API", () => {
            var req = new SendSelectDefParty(); req.select_party = 1;
            Api(SERVER_API.SERVER_API_USER_SELECT_DEF_PARTY, req);
            if (Save.unit_list == null || Save.unit_list.Length < 3) throw new Exception("No starter units");
            foreach (var unit in Save.unit_list)
                if (!HasCharacter(unit.id)) throw new Exception("Starter unit not in migrated master data: " + unit.id);
            Console.WriteLine("STARTER_UNITS\t" + Save.unit_list.Length);
        });
        Stage("106_LoginPack_API", () => {
            var req = new SendLoginPack(); req.get_login = 1; req.get_helper = 1; req.get_friend = 1; req.get_present = 1;
            Api(SERVER_API.SERVER_API_GET_LOGIN_PACK, req);
        });
        Stage("106_memory_save_roundtrip", () => {
            Save.user_name = "MigrationProbe";
            int units = Save.unit_list.Length;
            server.UserParamSave();
            if (!server.UserParamLoad() || Save.user_name != "MigrationProbe" || Save.unit_list.Length != units)
                throw new Exception("In-process JSON save roundtrip failed");
        });
        Console.WriteLine("NOT_TESTED\tAndroid, rendering, battle, complete client login, on-device or cross-process save/reload, authentic newer-version data");
    }
}
