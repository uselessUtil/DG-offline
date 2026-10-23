using System;
using System.IO;
using System.Reflection;
using LitJson;
using ServerDataDefine;
using UnityEngine;

public static class VerifyAccountRules
{
    static ServerForgery server;
    static uint serial=1;
    static ServerForgerySaveUser Save { get { return (ServerForgerySaveUser)typeof(ServerForgery).GetField("m_ForgeryPlayerSave",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(server); } }
    static void Check(bool ok,string msg){if(!ok)throw new Exception(msg);}
    static object Call(object obj,string method,params object[] args)
    {return obj.GetType().GetMethod(method,BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance).Invoke(obj,args);}
    static void Set(object obj,string field,object value)
    {obj.GetType().GetField(field,BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance).SetValue(obj,value);}
    static T Api<T>(SERVER_API api,object request,bool success)
    {
        var packet=new PacketData();packet.m_PacketAPI=api;packet.m_PacketUniqueNum=serial++;
        var json=server.CreateRecvForgery(ref packet,JsonMapper.ToJson(request));
        Check(((int)JsonMapper.ToObject(json)["header"]["code"]==(int)API_CODE.API_CODE_SUCCESS)==success,"API outcome "+api);
        return JsonMapper.ToObject<T>(json);
    }
    static PacketStructUnit Add(uint id,uint level)
    {
        long uid=(long)Call(server,"AddUnit",id,level,0u,0u);
        foreach(var u in Save.unit_list)if(u.unique_id==uid)return u;
        throw new Exception("Missing added unit");
    }
    public static void Main(string[] args)
    {
        Singleton<TimeManager>.Instance=new TimeManager();
        Singleton<MasterDataManager>.Instance=new MasterDataManager();
        Singleton<LocalSaveManager>.Instance=new LocalSaveManager();
        Singleton<UserDataAdmin>.Instance=new UserDataAdmin();
        server=new ServerForgery();Singleton<ServerForgery>.Instance=server;Call(server,"Start");
        if(args.Length>0)
        {
            Check(Save.player_rank==250 && Save.unit_total==2500 && Save.have_money==7654321,"Separate-process account defaults regression");
            Console.WriteLine("PASS separate-process reload: higher Rank 250/capacity 2500 and currency preserved.");return;
        }
        var create=new SendCreateUser();create.uuid="rules22";create.terminal=new PacketStructTerminal();
        create.terminal.platform=1;create.terminal.name="Docker rules test";
        var created=Api<RecvCreateUser>(SERVER_API.SERVER_API_USER_CREATE,create,true);
        Api<RecvSelectDefParty>(SERVER_API.SERVER_API_USER_SELECT_DEF_PARTY,new SendSelectDefParty{select_party=1},true);
        Check(Save.player_rank==184 && Save.player_exp==1014655 && Save.party_total==200 && Save.unit_total==2000 && Save.stamina_max==108,"New defaults");
        Check(created.result.player.rank==184 && created.result.player.total_unit==2000,"New account response stale");
        Console.WriteLine("PASS new account: Rank184/EXP1014655/Cost200/stamina108/capacity2000 in save AND API response.");
        Save.player_rank=13;Save.player_exp=1000;Save.unit_total=400;Save.unit_base=35;Save.unit_paid=365;
        Save.have_money=7654321;Save.quest_id=420;Save.have_stone=Save.have_stone_free=98;Save.have_stone_pay=0;
        Save.user_name="Preserved";ServerDataUtil.AddBitFlag(ref Save.flag_quest_clear,86);
        // Seed an actual legacy-format save without invoking the new save hook.
        LocalSaveUtil.SetValueString("SERVER_SAVE",JsonMapper.ToJson(Save));LocalSaveUtil.ExecDataSave();
        string migrationBefore=JsonMapper.ToJson(Save);
        server.UserParamLoad();
        var beforeFields=JsonMapper.ToObject<ServerForgerySaveUser>(migrationBefore);
        var permitted=new[]{"player_rank","player_exp","party_base","party_total","friend_base","friend_total","unit_base","unit_paid","unit_total","stamina_max","stamina_now"};
        foreach(var field in typeof(ServerForgerySaveUser).GetFields(BindingFlags.Public|BindingFlags.Instance))
            if(Array.IndexOf(permitted,field.Name)<0)Check(JsonMapper.ToJson(new object[]{field.GetValue(beforeFields)})==JsonMapper.ToJson(new object[]{field.GetValue(Save)}),"Migration changed unrelated field "+field.Name);
        Check(Save.player_rank==184 && Save.unit_total==2000 && Save.have_money==7654321 && Save.quest_id==420 &&
            Save.have_stone==98 && Save.user_name=="Preserved" && ServerDataUtil.ChkBitFlag(ref Save.flag_quest_clear,86),"Legacy upgrade lost progress");
        Console.WriteLine("PASS legacy load: lower rank/capacity raised; name, coins/chips, clear flags and pending quest preserved.");
        Save.stamina_now=91;server.UserParamSave();server.UserParamLoad();
        Check(Save.stamina_now==91,"Repeated load refilled stamina");
        Console.WriteLine("PASS migration field whitelist and no repeated stamina refill.");
        Save.quest_id=0;
        Save.stamina_max=108;Save.stamina_now=100;Save.stamina_recover=server.GetServerTime()-179;
        server.StaminaAutoRecovery();Check(Save.stamina_now==100,"Recovered early");
        Save.stamina_recover=server.GetServerTime()-180;server.StaminaAutoRecovery();Check(Save.stamina_now==101,"No 3-minute recovery");
        Save.stamina_now=100;Save.stamina_recover=server.GetServerTime()-540;server.StaminaAutoRecovery();Check(Save.stamina_now==103,"Offline recovery catch-up");
        Save.stamina_now=108;Save.stamina_recover=server.GetServerTime()-1800;server.StaminaAutoRecovery();Check(Save.stamina_now==108,"Overfilled stamina");
        Console.WriteLine("PASS stamina: no early point at179s, one at180s, three at540s, full stamina capped.");
        var baseUnit=Add(43,1);var material=Add(55,1);var helper=Add(43,2);var otherCopy=Add(43,3);
        for(int i=0;i<Save.unit_party_assign.Length;i++)Save.unit_party_assign[i]=new PacketStructPartyAssign();
        Save.unit_party_assign[0].unit0_unique_id=baseUnit.unique_id;
        var display=(PacketStructPlayer)Call(server,"CreateForgeryStructPlayer");Singleton<UserDataAdmin>.Instance.m_StructPlayer=display;
        Singleton<UserDataAdmin>.Instance.ConvertPartyAssing();
        var f=DivineGateOffline.WarehouseFriend(helper);
        Check(!ReferenceEquals(helper,f.unit) && f.unit.unique_id==helper.unique_id && f.unit.level==2 && f.friend_state==0,"Support snapshot");
        Check(DivineGateOffline.WarehouseFriend(helper).user_id==f.user_id && DivineGateOffline.WarehouseFriend(otherCopy).user_id!=f.user_id,"Duplicate character identities");
        Check(!DivineGateOffline.WarehouseQuestEligible(baseUnit) && !DivineGateOffline.WarehouseQuestEligible(helper),"Low-star warehouse candidates not filtered");
        var questPage=new MainMenuSeqQuestSelectFriend();var go=EngineSupport.CreateScaleTestObject();EngineSupport.RegisterComponent(go,questPage);
        var view=(WarehouseSupportView)EngineSupport.AddComponentType(go,typeof(WarehouseSupportView));view.warehouse=true;
        var list=DivineGateOffline.QuestSupportList(new TemplateList<MainMenuSeqQuestSelectFriend.SortParamFriend>(),questPage);
        Check(list.m_BufferSize==0,"Low-star warehouse candidates visible");
        Check(Singleton<LocalSaveManager>.Instance.GetLocalSaveUseFriend(f.user_id)!=null,"Own support friendship points not disabled");
        string saveBefore=LocalSaveUtil.GetValueString("SERVER_SAVE","");Singleton<LocalSaveManager>.Instance.SaveFuncUseFriend(f.user_id);
        Check(LocalSaveUtil.GetValueString("SERVER_SAVE","")==saveBefore,"Own support writes cooldown account state");
        var highSupport=Add(127,1);f=DivineGateOffline.WarehouseFriend(highSupport);
        var start=new SendQuestStart();start.quest_id=86;start.helper_user_id=f.user_id;start.helper_unit=f.unit;start.party_current=0;
        int count=Save.unit_list.Length;uint money=Save.have_money,fp=Save.have_friend_pt;
        Api<RecvQuestStart>(SERVER_API.SERVER_API_QUEST_START,start,true);
        Check(Save.unit_list.Length==count && Save.have_friend_pt==fp,"Quest consumed support or awarded points");
        var clear=new SendQuestClear();clear.quest_id=86;
        Api<RecvQuestClear>(SERVER_API.SERVER_API_QUEST_CLEAR,clear,true);
        Check(Save.player_rank>=184 && Save.unit_total>=2000,"Quest clear demoted defaults");
        Console.WriteLine("PASS warehouse quest: own inventory snapshot, per-instance IDs, current-party exclusion, actual quest start/clear, no consumption/FP/cooldown.");
        // Strengthening uses the original friend packet/flow again.
        var blend=new SendUnitBlendBuildUp();blend.unique_id_base=baseUnit.unique_id;blend.unique_id_parts=new[]{material.unique_id};
        blend.helper_user_id=135792468;blend.helper_unit=new PacketStructUnit();blend.helper_unit.Copy(helper);blend.helper_unit.unique_id=0;
        uint expectedExp=(uint)(CharaUtil.GetStatusValue(MasterDataUtil.GetCharaParamFromID(material.id),(int)material.level,4)*1.5f);
        money=Save.have_money;count=Save.unit_list.Length;
        var result=Api<RecvUnitBlendBuildUp>(SERVER_API.SERVER_API_UNIT_BLEND_BUILDUP,blend,true);
        Check(result.result.blend_exp==expectedExp && Save.unit_list.Length==count-1 &&
            Save.have_money==money-100 && helper.level==2,"Original strengthening consume/bonus mismatch");
        Console.WriteLine("PASS original friend strengthening: attribute/species bonus retained, only selected material and gold consumed.");
        string before=JsonMapper.ToJson(Save);
        start.helper_unit=new PacketStructUnit();start.helper_unit.Copy(f.unit);start.helper_unit.unique_id=1234567890123456;
        Api<RecvQuestStart>(SERVER_API.SERVER_API_QUEST_START,start,false);
        Check(before==JsonMapper.ToJson(Save),"Sold/missing quest support mutated save");
        Console.WriteLine("PASS retained warehouse quest ownership: stale support rejected without save changes.");
        Save.player_rank=250;Save.player_exp=2124586;Save.unit_total=2500;Save.unit_base=35;Save.unit_paid=2465;
        server.UserParamSave();server.UserParamLoad();
        Check(Save.player_rank==250 && Save.unit_total==2500,"Higher account demoted");
        Save.have_money=7654321;server.UserParamSave();
    }
}
