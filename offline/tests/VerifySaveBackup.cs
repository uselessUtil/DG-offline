using System;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using LitJson;
using ServerDataDefine;

public static class VerifySaveBackup
{
    static ServerForgery server;
    static ServerForgerySaveUser Save {get{return (ServerForgerySaveUser)typeof(ServerForgery).GetField("m_ForgeryPlayerSave",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(server);}}
    static void Check(bool value,string message){if(!value)throw new Exception(message);}
    static object Call(string name,params object[] args){return typeof(ServerForgery).GetMethod(name,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic).Invoke(server,args);}
    static string Account(){return JsonMapper.ToJson(Save);}
    static string Rewrap(DGSavePayload payload)
    {
        var envelope=JsonMapper.ToObject<DGSaveEnvelope>(DivineGateOffline.ExportSaveBackup(server));
        envelope.payload=JsonMapper.ToJson(payload);envelope.sha256=DivineGateOffline.SaveChecksum(envelope.payload);return JsonMapper.ToJson(envelope);
    }
    static void Rejected(string text,string directory,string description)
    {
        string before=Account(),prefs=LocalSaveUtil.GetValueString("divinegate_FAVOTIRE_UNIT","");bool rejected=false;
        int count=Directory.Exists(directory)?Directory.GetFiles(directory).Length:0;
        try{DivineGateOffline.ImportSaveBackup(server,text,directory);}catch(Exception){rejected=true;}
        Check(rejected && Account()==before && prefs==LocalSaveUtil.GetValueString("divinegate_FAVOTIRE_UNIT","") &&
            (Directory.Exists(directory)?Directory.GetFiles(directory).Length:0)==count,"Rejected import changed data: "+description);
    }
    public static void Main(string[] args)
    {
        string directory=Environment.GetEnvironmentVariable("DG_TEST_SAVE_DIR");
        Singleton<MasterDataManager>.Instance=new MasterDataManager();Singleton<LocalSaveManager>.Instance=new LocalSaveManager();
        Singleton<UserDataAdmin>.Instance=new UserDataAdmin();server=new ServerForgery();Singleton<ServerForgery>.Instance=server;Call("Start");
        if(args.Length>0)
        {
            Check(Account()==File.ReadAllText(Path.Combine(directory,"expected-account.json")),"Crash recovery lost account data");
            Check(LocalSaveUtil.GetValueString("divinegate_FAVOTIRE_UNIT","")==File.ReadAllText(Path.Combine(directory,"expected-protected.json")),"Crash recovery lost protected units");
            Check(LocalSaveUtil.GetValueInt("divinegate_SORT_UNIT_SALE",0)==6 && !File.Exists(Path.Combine(directory,"restore-journal.json")),"Crash recovery did not restore settings/delete journal");
            Check(Singleton<LocalSaveManager>.Instance.LoadFuncAddFavoriteUnit().m_BufferSize==1,"Fresh process cannot load protected-unit list");
            Console.WriteLine("PASS crash/reload: actual startup load repairs a partially written import from the journal, restoring account, protected units and sort settings before the game sees them.");return;
        }
        foreach(string value in new[]{"","abc",new string('a',55),new string('b',56),new string('c',64),"備份測試⚡"})
        {
            byte[] digest=SHA256.Create().ComputeHash(Encoding.UTF8.GetBytes(value));
            Check(DivineGateOffline.SaveChecksum(value)==BitConverter.ToString(digest).Replace("-","").ToLowerInvariant(),"SHA-256 mismatch");
        }
        var create=new SendCreateUser();create.uuid="backup-test";create.terminal=new PacketStructTerminal();create.terminal.platform=1;create.terminal.name="Docker save roundtrip";
        var packet=new PacketData();packet.m_PacketAPI=SERVER_API.SERVER_API_USER_CREATE;packet.m_PacketUniqueNum=1;
        string response=server.CreateRecvForgery(ref packet,JsonMapper.ToJson(create));Check((int)JsonMapper.ToObject(response)["header"]["code"]==(int)API_CODE.API_CODE_SUCCESS,"Create account failed");
        long first=(long)Call("AddUnit",335u,1u,3u,4u),second=(long)Call("AddUnit",55u,2u,0u,0u);
        Save.unit_list[0].get_time=0;Save.user_name="備份測試⚡";Save.player_rank=250;Save.have_money=1234567;
        Save.have_stone=Save.have_stone_free=98;Save.have_stone_pay=0;Save.have_friend_pt=6245;
        ServerDataUtil.AddBitFlag(ref Save.flag_quest_clear,86);
        for(int i=0;i<Save.unit_party_assign.Length;i++)Save.unit_party_assign[i]=new PacketStructPartyAssign();
        Save.unit_party_assign[0].unit0_unique_id=first;
        var favorite=new TemplateList<long>();favorite.Add(first);
        LocalSaveUtil.SetValueString("divinegate_FAVOTIRE_UNIT",JsonMapper.ToJson(favorite));
        var option=new LocalSaveOption();option.m_OptionBGM=0;option.m_OptionSE=1;option.m_OptionVoice=1;option.m_OptionGuide=1;
        LocalSaveUtil.SetValueString("divinegate_OPTION",JsonMapper.ToJson(option));
        LocalSaveUtil.SetValueInt("divinegate_SORT_UNIT_SALE",6);LocalSaveUtil.SetValueString("divinegate_USER_UUID",Save.uuid);
        string a=DivineGateOffline.ExportSaveBackup(server);var payload=DivineGateOffline.ValidateSaveBackup(server,a);
        string expected=Account();Directory.CreateDirectory(directory);File.WriteAllText(Path.Combine(directory,"sample.dgbackup.json"),a);
        Save.user_name="Account B";Save.have_money=7654321;Save.have_stone=Save.have_stone_free=30;
        favorite=new TemplateList<long>();favorite.Add(second);LocalSaveUtil.SetValueString("divinegate_FAVOTIRE_UNIT",JsonMapper.ToJson(favorite));
        string b=DivineGateOffline.ExportSaveBackup(server),expectedB=Account();
        var envelope=JsonMapper.ToObject<DGSaveEnvelope>(a);envelope.sha256="bad";Rejected(JsonMapper.ToJson(envelope),directory,"checksum");
        envelope=JsonMapper.ToObject<DGSaveEnvelope>(a);envelope.game_version="2.1.0";Rejected(JsonMapper.ToJson(envelope),directory,"foreign version");
        envelope=JsonMapper.ToObject<DGSaveEnvelope>(a);envelope.schema=999;Rejected(JsonMapper.ToJson(envelope),directory,"future schema");
        Rejected(a.Substring(0,a.Length/2),directory,"truncated JSON");Rejected(new string('x',DivineGateOffline.SaveBackupLimit+1),directory,"oversized input");
        var invalid=JsonMapper.ToObject<DGSavePayload>(JsonMapper.ToJson(payload));var bad=JsonMapper.ToObject<ServerForgerySaveUser>(invalid.server_save);
        bad.unit_list[1].unique_id=bad.unit_list[0].unique_id;invalid.server_save=JsonMapper.ToJson(bad);Rejected(Rewrap(invalid),directory,"duplicate unit ID");
        invalid=JsonMapper.ToObject<DGSavePayload>(JsonMapper.ToJson(payload));bad=JsonMapper.ToObject<ServerForgerySaveUser>(invalid.server_save);
        bad.unit_party_assign[0].unit0_unique_id=12345;invalid.server_save=JsonMapper.ToJson(bad);Rejected(Rewrap(invalid),directory,"invalid party reference");
        invalid=JsonMapper.ToObject<DGSavePayload>(JsonMapper.ToJson(payload));bad=JsonMapper.ToObject<ServerForgerySaveUser>(invalid.server_save);
        bad.login_last=ulong.MaxValue;invalid.server_save=JsonMapper.ToJson(bad);Rejected(Rewrap(invalid),directory,"unrepresentable login date");
        invalid=JsonMapper.ToObject<DGSavePayload>(JsonMapper.ToJson(payload));invalid.preferences[0].key="divinegate_API_TEST_IP";Rejected(Rewrap(invalid),directory,"unexpected settings key");
        Save.quest_id=86;Rejected(a,directory,"current account in quest");bool blocked=false;try{DivineGateOffline.ExportSaveBackup(server);}catch(Exception){blocked=true;}
        Check(blocked,"Export allowed during quest");Save.quest_id=0;
        Console.WriteLine("PASS backup validation: SHA-256 matches framework vectors including Unicode; checksum/version/schema/truncation/size/numeric overflow/duplicate IDs/party references/unexpected preferences rejected without touching account or files; active quest export/import blocked.");
        DivineGateOffline.ImportSaveBackup(server,a,directory);
        Check(Account()==expected,"Roundtrip changed account, unit identity/time, currency, flags or party");
        Check(LocalSaveUtil.GetValueString("divinegate_FAVOTIRE_UNIT","")==JsonMapper.ToJson(payload.preferences[4].present?JsonMapper.ToObject<TemplateList<long>>(payload.preferences[4].text):null),"Protected units not restored");
        Check(JsonMapper.ToObject<LocalSaveOption>(LocalSaveUtil.GetValueString("divinegate_OPTION","")).m_OptionBGM==0,"Sound options lost");
        string[] backups=Directory.GetFiles(directory,"before-import-*.json");Check(backups.Length==1,"No pre-import backup");
        Check(DivineGateOffline.ValidateSaveBackup(server,DivineGateOffline.ReadSaveFile(backups[0])).server_save==expectedB,"Pre-import recovery snapshot wrong");
        DivineGateOffline.ImportSaveBackup(server,b,directory);Check(Account()==expectedB,"Cannot restore recovery account");
        Check(!File.Exists(Path.Combine(directory,"restore-journal.json")),"Successful import left journal behind");
        Console.WriteLine("PASS roundtrip: Unicode name, Rank250, currency/FP, owned unit IDs/levels/pluses/legacy zero and new timestamps, clear flags, party, protected units and sound/sort settings restored; previous account backed up and restorable.");
        File.WriteAllText(Path.Combine(directory,"expected-account.json"),Account());
        File.WriteAllText(Path.Combine(directory,"expected-protected.json"),LocalSaveUtil.GetValueString("divinegate_FAVOTIRE_UNIT",""));
        DivineGateOffline.WriteSaveFile(Path.Combine(directory,"restore-journal.json"),b);
        LocalSaveUtil.SetValueString("SERVER_SAVE",payload.server_save);LocalSaveUtil.SetValueInt("divinegate_SORT_UNIT_SALE",42);
        LocalSaveUtil.SetValueString("divinegate_FAVOTIRE_UNIT",payload.preferences[4].text);LocalSaveUtil.ExecDataSave();
        Console.WriteLine("FIXTURE: interrupted import persisted after partial preference writes; next process must recover automatically.");
    }
}
