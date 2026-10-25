using System;
using System.IO;
using System.Reflection;
using LitJson;
using ServerDataDefine;

public static class VerifyAcquisitionAndSupports
{
    static ServerForgery server;
    static uint serial=1;
    static ServerForgerySaveUser Save { get { return (ServerForgerySaveUser)typeof(ServerForgery)
        .GetField("m_ForgeryPlayerSave",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(server); } }
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    static object Call(string method,params object[] args)
    {return typeof(ServerForgery).GetMethod(method,BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance).Invoke(server,args);}
    static T Api<T>(SERVER_API api,object request)
    {
        var packet=new PacketData();packet.m_PacketAPI=api;packet.m_PacketUniqueNum=serial++;
        string json=server.CreateRecvForgery(ref packet,JsonMapper.ToJson(request));
        Check((int)JsonMapper.ToObject(json)["header"]["code"]==(int)API_CODE.API_CODE_SUCCESS,"API failed "+api);
        return JsonMapper.ToObject<T>(json);
    }
    static PacketStructUnit Add(uint id)
    {
        long uid=(long)Call("AddUnit",id,1u,0u,0u);
        foreach(var unit in Save.unit_list)if(unit.unique_id==uid)return unit;
        throw new Exception("New unit missing");
    }
    static PacketStructUnit Find(long uid)
    {foreach(var unit in Save.unit_list)if(unit.unique_id==uid)return unit;throw new Exception("Unit missing");}
    static void Display()
    {Singleton<UserDataAdmin>.Instance.m_StructPlayer=(PacketStructPlayer)Call("CreateForgeryStructPlayer");}
    static ulong Key(PacketStructUnit unit)
    {return MainMenuListSort.SortCreateID_GetTime(unit,MasterDataUtil.GetCharaParamFromID(unit.id));}
    public static void Main(string[] args)
    {
        Singleton<TimeManager>.Instance=new TimeManager();
        Singleton<MasterDataManager>.Instance=new MasterDataManager();Singleton<LocalSaveManager>.Instance=new LocalSaveManager();
        Singleton<UserDataAdmin>.Instance=new UserDataAdmin();
        server=new ServerForgery();Singleton<ServerForgery>.Instance=server;Call("Start");
        string expected=Environment.GetEnvironmentVariable("DG_TEST_PREFS")+".units.json";
        if(args.Length>0)
        {
            Check(JsonMapper.ToJson(Save.unit_list)==File.ReadAllText(expected),"Acquisition timestamps/IDs/order changed across process reload");
            Check(JsonMapper.ToJson(Singleton<LocalSaveManager>.Instance.LoadFuncAddFavoriteUnit())==File.ReadAllText(expected+".favorites"),"Favorite unit IDs changed across process");
            Console.WriteLine("PASS separate-process reload: legacy zero/nonzero times and every new card timestamp, ID, level and insertion order preserved.");return;
        }
        var create=new SendCreateUser();create.uuid="acquisition-supports22r4";create.terminal=new PacketStructTerminal();
        create.terminal.platform=1;create.terminal.name="isolated acquisition regression";
        Api<RecvCreateUser>(SERVER_API.SERVER_API_USER_CREATE,create);
        Api<RecvSelectDefParty>(SERVER_API.SERVER_API_USER_SELECT_DEF_PARTY,new SendSelectDefParty{select_party=1});
        var firstNew=Add(43);Check(firstNew.get_time>0,"New unit timestamp missing");
        var oldZero=Add(43);oldZero.get_time=0;
        var oldTimed=Add(55);oldTimed.get_time=server.GetServerTime()-500;
        string oldZeroJson=JsonMapper.ToJson(oldZero),oldTimedJson=JsonMapper.ToJson(oldTimed);
        server.UserParamSave();server.UserParamLoad();oldZero=Find(oldZero.unique_id);oldTimed=Find(oldTimed.unique_id);
        Check(JsonMapper.ToJson(oldZero)==oldZeroJson && JsonMapper.ToJson(oldTimed)==oldTimedJson,"Legacy unit migration changed data");
        string beforeRejected=JsonMapper.ToJson(Save.unit_list);
        Check((long)Call("AddUnit",0u,1u,0u,0u)==0 && (long)Call("AddUnit",999999u,1u,0u,0u)==0 &&
            JsonMapper.ToJson(Save.unit_list)==beforeRejected,"Failed allocation changed existing unit data");
        ulong before=server.GetServerTime();var early=Add(43);var late=Add(55);ulong after=server.GetServerTime();
        Check(early.get_time>=before && early.get_time<=after && late.get_time>=before && late.get_time<=after,"New acquisition time outside real clock bounds");
        // Deterministic same-second fixture, without changing game time or any
        // production method. Real API timestamps are checked separately below.
        early.get_time=late.get_time=before;Display();
        Check(Key(oldZero)<Key(oldTimed) && Key(oldTimed)<Key(early) && Key(early)>Key(late),"Old/new/time or same-second order wrong");
        var saleEarly=new MainMenuSeqPartySale2.SortParamUnit();saleEarly.m_SortNum=Key(early);
        var saleLate=new MainMenuSeqPartySale2.SortParamUnit();saleLate.m_SortNum=Key(late);
        var saleOld=new MainMenuSeqPartySale2.SortParamUnit();saleOld.m_SortNum=Key(oldZero);
        Check(MainMenuSeqPartySale2.SortExec(saleLate,saleOld)<0,"New acquisition is after missing-time legacy card");
        Check(MainMenuSeqPartySale2.SortExec(saleEarly,saleLate)<0,"Actual sale comparator reversed acquisition order");
        Check(MainMenuListSort.SortCreateID_VerForm_GetTime(late,MasterDataUtil.GetCharaParamFromID(late.id),false)>
            MainMenuListSort.SortCreateID_VerForm_GetTime(oldZero,MasterDataUtil.GetCharaParamFromID(oldZero.id),true),"Party priority overlaps timestamp bits");
        var oldSame=Add(29);oldSame.get_time=0;Display();
        Check((Key(oldZero)>Key(oldSame))==(MasterDataUtil.GetCharaParamFromID(oldZero.id).draw_id>MasterDataUtil.GetCharaParamFromID(oldSame.id).draw_id),"Legacy zero-time ID order changed");
        Console.WriteLine("PASS acquisition order: real new-card timestamps; legacy zero/nonzero cards untouched on save/load; actual sale comparator, same-second insertion ties, legacy ID tie order and original party priority verified.");

        Save.have_stone=Save.have_stone_free=1000;Save.have_stone_pay=0;
        foreach(uint pool in new uint[]{200100,300100,300200,300300,300400})
        {
            var draw=new SendGachaPlay();draw.gacha_id=pool;draw.gacha_ct=9;before=server.GetServerTime();
            var result=Api<RecvGachaPlay>(SERVER_API.SERVER_API_GACHA_PLAY,draw);after=server.GetServerTime();
            Check(result.result.unit_unique_id.Length==9,"Nine-draw count changed");Display();
            for(int i=0;i<9;i++)
            {
                var unit=Find(result.result.unit_unique_id[i]);Check(unit.get_time>=before && unit.get_time<=after,"Actual draw timestamp missing");
                if(i>0 && Find(result.result.unit_unique_id[i-1]).get_time==unit.get_time)Check(Key(Find(result.result.unit_unique_id[i-1]))>Key(unit),"Actual nine-draw acquisition order lost");
            }
        }
        Call("AddPresent",7u,43u,1u,"timestamp proof");
        long gift=Save.present_list[Save.present_list.Length-1].serial_id;
        var open=new SendPresentOpen();open.present_serial_id=new[]{gift};before=server.GetServerTime();
        var opened=Api<RecvPresentOpen>(SERVER_API.SERVER_API_PRESENT_OPEN,open);after=server.GetServerTime();
        Check(opened.result.unit_unique_id.Length==1 && Find(opened.result.unit_unique_id[0]).get_time>=before &&
            Find(opened.result.unit_unique_id[0]).get_time<=after,"Gift-card acquisition time missing");
        Console.WriteLine("PASS actual API awards: nine draws from standard + all four collab pools (45 cards), and a unit gift all record clock-bounded acquisition times; multi-draw order retained.");

        uint fourID=0,fiveID=0,sixID=0;
        foreach(var c in server.m_MasterDataParamChara)
        {if(c==null)continue;if(c.rare==3 && fourID==0)fourID=c.fix_id;if(c.rare==4 && fiveID==0)fiveID=c.fix_id;if(c.rare==5 && sixID==0)sixID=c.fix_id;}
        Check(fourID!=0 && fiveID!=0 && sixID!=0,"Rarity fixtures unavailable");
        var four=Add(fourID);var assigned=Add(fiveID);var sameCharacter=Add(fiveID);var six=Add(sixID);
        for(int i=0;i<Save.unit_party_assign.Length;i++)Save.unit_party_assign[i]=new PacketStructPartyAssign();
        Save.unit_party_assign[0].unit0_unique_id=assigned.unique_id;Save.unit_party_current=0;Display();
        Check(!DivineGateOffline.WarehouseQuestEligible(four) && !DivineGateOffline.WarehouseQuestEligible(assigned) &&
            DivineGateOffline.WarehouseQuestEligible(sameCharacter) && DivineGateOffline.WarehouseQuestEligible(six),"Five/six-star or party-instance eligibility wrong");
        var page=new MainMenuSeqQuestSelectFriend();var go=EngineSupport.CreateScaleTestObject();EngineSupport.RegisterComponent(go,page);
        var view=(WarehouseSupportView)EngineSupport.AddComponentType(go,typeof(WarehouseSupportView));view.warehouse=true;
        var original=new TemplateList<MainMenuSeqQuestSelectFriend.SortParamFriend>();
        var list=DivineGateOffline.QuestSupportList(original,page);int expectedCount=0;
        foreach(var unit in Save.unit_list)if(DivineGateOffline.WarehouseQuestEligible(unit))expectedCount++;
        Check(list.m_BufferSize==expectedCount && expectedCount>=2,"Filtered list size wrong");
        for(int i=0;i<list.m_BufferSize;i++)Check(list[i].m_FriendCharaMaster.rare>=4 && list[i].m_FriendStruct.unit.unique_id!=assigned.unique_id,"Disallowed candidate displayed");
        var local=Singleton<LocalSaveManager>.Instance;
        var notFavorite=Add(fiveID);Display();
        local.SaveFuncAddFavoriteUnit(sameCharacter.unique_id,true,false);
        local.SaveFuncAddFavoriteUnit(six.unique_id,true,false);
        local.SaveFuncAddFavoriteUnit(four.unique_id,true,false);
        local.SaveFuncAddFavoriteUnit(assigned.unique_id,true,false);
        Check(DivineGateOffline.WarehouseFavoritesChanged291(view),"Added favorites did not invalidate view");
        string favoriteSaveBefore=LocalSaveUtil.GetValueString("SERVER_SAVE","");
        list=DivineGateOffline.QuestSupportList(original,page);
        Check(!DivineGateOffline.WarehouseFavoritesChanged291(view),"Refreshed favorite snapshot remains dirty");
        int favorites=0;
        for(int i=0;i<list.m_BufferSize;i++) {
            var row=list[i];
            bool expectedFavorite=row.m_FriendStruct.unit.unique_id==sameCharacter.unique_id || row.m_FriendStruct.unit.unique_id==six.unique_id;
            Check(row.m_Favorite==expectedFavorite,"Favorite mapping is not per owned unit instance");
            if(row.m_Favorite)favorites++;
            Check(row.m_FriendStruct.unit.unique_id!=four.unique_id && row.m_FriendStruct.unit.unique_id!=assigned.unique_id,"Favorites bypassed eligibility");
        }
        Check(favorites==2,"Expected favorite candidates missing");
        // Use original mode key builders and the actual patched comparator.
        string[] modes={"Default","Favorite","Element","Kind","Pow","HP","Login","Rank","Level","Rare","Filter"};
        foreach(string mode in modes){
            var key=typeof(MainMenuListSort).GetMethod("SortFriendCreateID_"+mode);
            for(int i=0;i<list.m_BufferSize;i++){
                var row=list[i];
                row.m_SortNum=(ulong)key.Invoke(null,mode=="Favorite"?
                    new object[]{row.m_FriendStruct,row.m_FriendCharaMaster,row.m_Favorite}:
                    new object[]{row.m_FriendStruct,row.m_FriendCharaMaster});
            }
            list.Sort(MainMenuSeqQuestSelectFriend.SortExec);
            for(int i=1;i<list.m_BufferSize;i++){
                var a=list[i-1];var b=list[i];
                Check(a.m_Favorite || !b.m_Favorite,"Favorite is behind normal candidate: "+mode);
                if(a.m_Favorite==b.m_Favorite)Check(a.m_SortNum>=b.m_SortNum,"Within-group original order changed: "+mode);
            }
        }
        var normalA=new MainMenuSeqQuestSelectFriend.SortParamFriend{m_FriendStruct=new PacketStructFriend{user_id=123},m_Favorite=true,m_SortNum=1};
        var normalB=new MainMenuSeqQuestSelectFriend.SortParamFriend{m_FriendStruct=new PacketStructFriend{user_id=456},m_Favorite=false,m_SortNum=100};
        Check(MainMenuSeqQuestSelectFriend.SortExec(normalA,normalB)>0,"Normal friends gained forced favorite priority");
        local.SaveFuncAddFavoriteUnit(sameCharacter.unique_id,false,true);
        Check(DivineGateOffline.WarehouseFavoritesChanged291(view),"Removed favorite did not invalidate view");
        list=DivineGateOffline.QuestSupportList(original,page);
        for(int i=0;i<list.m_BufferSize;i++)if(list[i].m_FriendStruct.unit.unique_id==sameCharacter.unique_id)Check(!list[i].m_Favorite,"Removed favorite remains marked");
        local.SaveFuncAddFavoriteUnit(sameCharacter.unique_id,true,false);
        Check(DivineGateOffline.WarehouseFavoritesChanged291(view),"In-place favorite addition did not invalidate view");
        list=DivineGateOffline.QuestSupportList(original,page);
        Check(favoriteSaveBefore==LocalSaveUtil.GetValueString("SERVER_SAVE",""),"Favorite display changed player save");
        Console.WriteLine("PASS warehouse favorite-first: 11 original sort keys + actual comparator; per-instance favorites; same-group order; add/remove refresh; favorite ineligible cards excluded; normal friends and SERVER_SAVE unchanged.");
        view.warehouse=false;Check(ReferenceEquals(original,DivineGateOffline.QuestSupportList(original,page)),"Normal friend list filtered");
        Save.unit_party_assign[0].unit0_unique_id=0;Display();Check(DivineGateOffline.WarehouseQuestEligible(assigned),"Removed party unit remains hidden");
        // Evolving changes the current master entry, not the original rarity.
        four.id=fiveID;Display();Check(DivineGateOffline.WarehouseQuestEligible(four),"Evolved five-star current form not eligible");
        Check(JsonMapper.ToJson(Find(oldZero.unique_id))==oldZeroJson && JsonMapper.ToJson(Find(oldTimed.unique_id))==oldTimedJson,"Subsequent awards changed legacy cards");
        server.UserParamSave();File.WriteAllText(expected,JsonMapper.ToJson(Save.unit_list));
        File.WriteAllText(expected+".favorites",JsonMapper.ToJson(local.LoadFuncAddFavoriteUnit()));
        Console.WriteLine("PASS warehouse candidates: four-star hidden, five/six-star shown, assigned instance hidden but another copy allowed, current evolved rarity used, normal friends unchanged. Android rendering/touch pending.");
    }
}
