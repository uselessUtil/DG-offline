using System;
using System.IO;
using System.Reflection;
using LitJson;
using ServerDataDefine;

// Test exact packaged tables, actual field generation and API state transitions.
// Run seed under prototype20, then upgrade under the candidate with the same
// isolated PlayerPrefs file. Never uses an Android or real player save.
public static class VerifyBackupStages
{
    static ServerForgery server;
    static uint serial=1;
    static ServerForgerySaveUser Save { get { return (ServerForgerySaveUser)typeof(ServerForgery)
        .GetField("m_ForgeryPlayerSave",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(server); } }
    static void Check(bool value,string message) { if(!value)throw new Exception(message); }
    static T Api<T>(SERVER_API api,object request)
    {
        var p=new PacketData();p.m_PacketAPI=api;p.m_PacketUniqueNum=serial++;
        string response=server.CreateRecvForgery(ref p,JsonMapper.ToJson(request));
        Check((int)JsonMapper.ToObject(response)["header"]["code"]==(int)API_CODE.API_CODE_SUCCESS,"API failed "+api);
        return JsonMapper.ToObject<T>(response);
    }
    static string Snapshot { get { return Environment.GetEnvironmentVariable("DG_TEST_SAVE_SNAPSHOT"); } }
    static T[] Expected<T>(string table)
    { return JsonMapper.ToObject<T[]>(File.ReadAllText(Path.Combine(Environment.GetEnvironmentVariable("DG_TEST_STAGE_TABLES"),table+".json"))); }
    static void Compare<T>(string table,string field)
    {
        object expected=Expected<T>(table);
        if(table=="MasterDataArea")
            foreach(var area in (MasterDataArea[])expected)
                if(area.type==2 || area.type==3) {area.event_id=0;if(area.type==2)area.pre_area=0;}
        object actual=typeof(ServerForgery).GetField(field).GetValue(server);
        Check(JsonMapper.ToJson(expected)==JsonMapper.ToJson(actual),"Packaged backup table differs: "+table);
        Check(Object.ReferenceEquals(actual,typeof(MasterDataManager).GetField(field).GetValue(Singleton<MasterDataManager>.Instance)),
              "Menu and API tables not shared: "+table);
    }
    static void Seed()
    {
        Check(MasterDataUtil.GetQuestParamFromID(211).quest_stamina==50,"Legacy fixture is not prototype20 stage data");
        var create=new SendCreateUser();create.uuid="backup-stage-upgrade";create.terminal=new PacketStructTerminal();
        create.terminal.platform=1;create.terminal.name="Docker isolated upgrade";
        Api<RecvCreateUser>(SERVER_API.SERVER_API_USER_CREATE,create);
        var rename=new SendRenameUser();rename.user_name="StageUpgrade";
        Api<RecvRenameUser>(SERVER_API.SERVER_API_USER_RENAME,rename);
        var party=new SendSelectDefParty();party.select_party=1;
        Api<RecvSelectDefParty>(SERVER_API.SERVER_API_USER_SELECT_DEF_PARTY,party);
        Save.have_money=123456;Save.have_stone_free=77;Save.have_stone_pay=5;Save.have_stone=82;
        Save.stamina_now=Save.stamina_max=10000;
        ServerDataUtil.AddBitFlag(ref Save.flag_quest_clear,420);
        ServerDataUtil.AddBitFlag(ref Save.flag_quest_clear,86);
        var start=new SendQuestStart();start.quest_id=420;start.helper_unit=new PacketStructUnit();start.helper_unit.id=139;start.helper_unit.level=1;
        Api<RecvQuestStart>(SERVER_API.SERVER_API_QUEST_START,start);
        server.UserParamSave();File.WriteAllText(Snapshot,JsonMapper.ToJson(Save));
        Console.WriteLine("PASS prototype20 seed: named account, party/units, coins/chips, completed quests and in-progress Index quest 420 saved.");
    }
    static void Evolve()
    {
        var add=typeof(ServerForgery).GetMethod("AddUnit",BindingFlags.Instance|BindingFlags.NonPublic);
        long unit=(long)add.Invoke(server,new object[]{43u,10u,0u,0u});
        long material=(long)add.Invoke(server,new object[]{55u,1u,0u,0u});
        Save.have_money=100000;Save.stamina_now=Save.stamina_max=10000;
        var begin=new SendEvolQuestStart();begin.quest_id=7;begin.unique_id_base=unit;begin.unique_id_parts=new long[]{material};
        begin.blend_pattern=36;begin.blend_unit_id=44;begin.helper_unit=new PacketStructUnit();begin.helper_unit.id=1;begin.helper_unit.level=1;
        Api<RecvEvolQuestStart>(SERVER_API.SERVER_API_EVOL_QUEST_START,begin);
        var end=new SendEvolQuestClear();end.quest_id=7;
        Api<RecvEvolQuestClear>(SERVER_API.SERVER_API_EVOL_QUEST_CLEAR,end);
        bool found=false;
        foreach(var u in Save.unit_list)if(u.unique_id==unit)found=u.id==44 && u.level==1;
        Check(found && !Save.evolve_quest_active,"Evolution quest regression");
        Console.WriteLine("PASS actual evolution quest start/clear: 43 Lv10 becomes 44 Lv1 under backup stage settings.");
    }
    public static void Main(string[] args)
    {
        Singleton<MasterDataManager>.Instance=new MasterDataManager();
        Singleton<LocalSaveManager>.Instance=new LocalSaveManager();
        server=new ServerForgery();Singleton<ServerForgery>.Instance=server;
        typeof(ServerForgery).GetMethod("Start",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(server,null);
        if(args.Length>0 && args[0]=="seed") {Seed();return;}
        Check(Save!=null && JsonMapper.ToJson(Save)==File.ReadAllText(Snapshot),"Upgrade changed existing save fields");
        Check(Save.quest_id==420 && ServerDataUtil.ChkBitFlag(ref Save.flag_quest_clear,420),"Index save ID lost");
        Check(MasterDataUtil.GetQuestParamFromID(436)==null && MasterDataUtil.GetQuestParamFromID(420)!=null,"Duplicate or migrated quest ID");
        Console.WriteLine("PASS prototype20-to-candidate upgrade: complete saved account unchanged; in-progress quest and clear flags still use original Index IDs.");
        Compare<MasterDataArea>("MasterDataArea","m_MasterDataArea");
        Compare<MasterDataQuest>("MasterDataQuest","m_MasterDataQuest");
        Compare<MasterDataQuestFloor>("MasterDataQuestFloor","m_MasterDataQuestFloor");
        Compare<MasterDataCategoryPattern>("MasterDataCategoryPattern","m_MasterDataQuestFloorCategory");
        Compare<MasterDataExpectPattern>("MasterDataExpectPattern","m_MasterDataQuestFloorExpect");
        Compare<MasterDataParamPanel>("MasterDataParamPanel","m_MasterDataParamPanel");
        Compare<MasterDataParamPanelGroup>("MasterDataParamPanelGroup","m_MasterDataParamPanelGroup");
        Compare<MasterDataParamEnemy>("MasterDataEnemyChara","m_MasterDataParamEnemy");
        Compare<MasterDataEnemyGroup>("MasterDataEnemyGroup","m_MasterDataParamEnemyGroup");
        Compare<MasterDataAreaAmend>("MasterDataAreaAmend","m_MasterDataAreaAmend");
        Compare<MasterDataGuerrillaBoss>("MasterDataGuerrillaBoss","m_MasterDataGuerrillaBoss");
        Console.WriteLine("PASS all 11 packaged backup tables equal the schema-projected source; original permanent offline area gates retained.");
        uint[] bosses={100170,100270,124210,124300,124390,124480,124570,124660};
        foreach(uint id in bosses)Check(MasterDataUtil.GetEnemyGroupParamFromID(id).chain_id==0,"Unexpected Boss evolution: "+id);
        Check(MasterDataUtil.GetQuestParamFromID(211).quest_stamina==40,"Casino stamina not restored");
        Check(MasterDataUtil.GetQuestParamFromID(238).clear_money==360,"Steins Gate reward not restored");
        Check(server.m_MasterDataParamChara.Length==523 && server.m_MasterDataParamCharaEvol.Length==230,"Player-unit scope changed");
        var pending=new SendQuestClear();pending.quest_id=420;
        Api<RecvQuestClear>(SERVER_API.SERVER_API_QUEST_CLEAR,pending);
        Singleton<SceneGoesParam>.Instance=new SceneGoesParam();
        Singleton<SceneGoesParam>.Instance.m_SceneGoesParamToGameMain=new SceneGoesParamToGameMain();
        int count=0,floorCount=0;
        foreach(var q in server.m_MasterDataQuest)
        {
            if(q.active!=2)continue;
            var field=new FieldManager();
            // Original tutorial stages use their dedicated generator; their
            // sentinel pattern ID 0 is intentionally rejected by normal lookup.
            bool generated=q.fix_id<=6 ? field.CreateFieldAreaTutorial(q.fix_id) : field.CreateFieldArea(q.fix_id,null,null);
            Check(generated,"Field generation failed: "+q.fix_id);
            floorCount+=field.m_FieldFloorMax;
            Save.stamina_now=Save.stamina_max=10000;
            var begin=new SendQuestStart();begin.quest_id=q.fix_id;begin.helper_unit=new PacketStructUnit();begin.helper_unit.id=139;begin.helper_unit.level=1;
            Api<RecvQuestStart>(SERVER_API.SERVER_API_QUEST_START,begin);
            Check(Save.quest_id==q.fix_id && Save.stamina_now==10000-q.quest_stamina,"Stamina mismatch: "+q.fix_id);
            var clear=new SendQuestClear();clear.quest_id=q.fix_id;clear.get_money=(uint)q.clear_money;clear.get_exp=(uint)q.clear_exp;
            Api<RecvQuestClear>(SERVER_API.SERVER_API_QUEST_CLEAR,clear);
            Check(Save.quest_id==0 && ServerDataUtil.ChkBitFlag(ref Save.flag_quest_clear,q.fix_id),"Quest clear failed: "+q.fix_id);
            count++;
        }
        Check(count==364,"Wrong active stage count");
        Console.WriteLine("PASS actual FieldManager generation and local API start/stamina/clear/progress for all "+count+" active quests ("+floorCount+" ground floors).");
        Evolve();
        Console.WriteLine("LIMIT: Unity stand-in; no Android rendering, input, combat animation or full battle simulation.");
    }
}
