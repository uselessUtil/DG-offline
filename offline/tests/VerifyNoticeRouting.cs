using System;
using System.Reflection;
using LitJson;
using ServerDataDefine;

public static class VerifyNoticeRouting
{
    static Type control;
    static ServerForgery server;
    static uint serial=1;
    static ServerForgerySaveUser Save { get { return (ServerForgerySaveUser)typeof(ServerForgery)
        .GetField("m_ForgeryPlayerSave",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(server); } }
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    static MAINMENU_SEQ Step(MainMenuSeqSwitchPage page)
    {
        for(int frame=0;frame<3;frame++)
        {
            control.GetField("Calls").SetValue(null,0);page.Update();
            int calls=(int)control.GetField("Calls").GetValue(null);
            Check(calls<=1,"Multiple outgoing routes");
            if(calls==1)return (MAINMENU_SEQ)(int)control.GetField("Next").GetValue(null);
        }
        throw new Exception("Menu routing stalled");
    }
    static MainMenuSeqSwitchPage Fresh()
    {
        ResidentParam.ParamReset();
        MainMenuParam.m_ResultQuestID=0;MainMenuParam.m_LoginActive=false;MainMenuParam.m_LoginFriendPointGet=0;
        Singleton<UserDataAdmin>.Instance.m_StructPresentList=null;
        Singleton<SceneGoesParam>.Instance.m_SceneGoesParamToMainMenu=null;
        Singleton<SceneGoesParam>.Instance.m_SceneGoesParamToMainMenuRetire=null;
        var page=new MainMenuSeqSwitchPage();
        Check(Step(page)==MAINMENU_SEQ.SEQ_SETUP_SERVER_SEND,"Initial setup skipped");return page;
    }
    static T Api<T>(SERVER_API api,object body)
    {
        var packet=new PacketData();packet.m_PacketAPI=api;packet.m_PacketUniqueNum=serial++;
        var json=server.CreateRecvForgery(ref packet,JsonMapper.ToJson(body));
        Check((int)JsonMapper.ToObject(json)["header"]["code"]==(int)API_CODE.API_CODE_SUCCESS,"API failed "+api);
        return JsonMapper.ToObject<T>(json);
    }
    static void ReceiveHelperReward()
    {
        Save.login_last=server.GetServerTime();Save.login_total=1;Save.login_chain=1;
        uint previous=Save.have_friend_pt;
        var request=new SendLoginPack();request.get_login=1;
        var response=Api<RecvLoginPack>(SERVER_API.SERVER_API_GET_LOGIN_PACK,request);
        var receipt=response.result.result_login_param;
        Check(receipt.friend_help_ct==2 && receipt.friend_point_get==20 && Save.have_friend_pt==previous+20 &&
            receipt.friend_point_now==Save.have_friend_pt,"Actual FP award was not20");
        var cached=new RecvLoginParamValue();cached.login_total=1;cached.login_chain=1;
        cached.friend_point_now=previous;cached.login_bonus=new PacketStructLoginBonus[0];
        Singleton<LocalSaveManager>.Instance.SaveFuncLoginParam(cached);
        Singleton<UserDataAdmin>.Instance.m_StructPlayer=(PacketStructPlayer)typeof(ServerForgery)
            .GetMethod("CreateForgeryStructPlayer",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(server,null);
        ResidentParam.m_StartingFirstLoginBonus=true;
        ServerDataParam.m_RecvPacketLoginPack=response;
        var setup=new MainMenuSeqSetupServerSend();
        typeof(MainMenuSeqSetupServerSend).GetField("m_PacketUniqueID",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(setup,1u);
        var result=(MAINMENU_RESULT)typeof(MainMenuSeqSetupServerSend).GetMethod("ServerSendLoginPack",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(setup,null);
        Check(result==MAINMENU_RESULT.FINISH && MainMenuParam.m_LoginFriendPointGet==20 &&
            MainMenuParam.m_LoginFriendHelpCt==2 && !ResidentParam.m_StartingFirstPresent,"Actual receipt did not re-arm pictured helper notice");
    }
    public static void Main(string[] args)
    {
        control=typeof(MainMenuManager).Assembly.GetType("NoticeRoutingControl");
        Singleton<MasterDataManager>.Instance=new MasterDataManager();
        Singleton<MainMenuManager>.Instance=new MainMenuManager();Singleton<LocalSaveManager>.Instance=new LocalSaveManager();
        Singleton<SceneGoesParam>.Instance=new SceneGoesParam();Singleton<UserDataAdmin>.Instance=new UserDataAdmin();
        Singleton<TimeManager>.Instance=new TimeManager();Singleton<TimeManager>.Instance.m_TimeNow=DateTime.Now;
        Singleton<PatchManager>.Instance=new PatchManager();Singleton<PatchManager>.Instance.m_PatchUpdateTime=Singleton<TimeManager>.Instance.m_TimeNow;
        server=new ServerForgery();Singleton<ServerForgery>.Instance=server;
        typeof(ServerForgery).GetMethod("Start",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(server,null);
        var create=new SendCreateUser();create.uuid="helper-notice-proof";create.terminal=new PacketStructTerminal();
        create.terminal.platform=1;create.terminal.name="isolated UI receipt proof";
        Api<RecvCreateUser>(SERVER_API.SERVER_API_USER_CREATE,create);Save.have_friend_pt=6245;
        bool old=args.Length>0 && args[0]=="baseline";
        PacketStructPresent[][] emptyLists={null,new PacketStructPresent[0],new PacketStructPresent[]{null},
            new[]{new PacketStructPresent()},new[]{null,new PacketStructPresent()}};
        for(int cycle=0;cycle<100;cycle++)
        {
            var page=Fresh();ReceiveHelperReward();
            var rows=old?null:emptyLists[cycle%emptyLists.Length];Singleton<UserDataAdmin>.Instance.m_StructPresentList=rows;
            string snapshot=JsonMapper.ToJson(Save);uint balance=Save.have_friend_pt;
            var route=Step(page);
            Check(route==(old?MAINMENU_SEQ.SEQ_LOGIN_PRESENT:MAINMENU_SEQ.SEQ_QUEST_SELECT_AREA),"Unexpected helper notification route");
            if(cycle==0)Console.WriteLine("SCREENSHOT FIXTURE:6245+20="+balance+" FP; actual post-receipt route="+route);
            Check(Save.have_friend_pt==balance && Singleton<UserDataAdmin>.Instance.m_StructPlayer.have_friend_pt==balance &&
                MainMenuParam.m_LoginFriendPointNow==balance && JsonMapper.ToJson(Save)==snapshot,"Quieting notification removed FP or changed save");
            Check(JsonMapper.ToObject<ServerForgerySaveUser>(LocalSaveUtil.GetValueString("SERVER_SAVE","")).have_friend_pt==balance,"FP not persisted");
            Check(ReferenceEquals(rows,Singleton<UserDataAdmin>.Instance.m_StructPresentList),"Notification filter mutated actual gift list");
            if(!old)
            {
                Check(MainMenuParam.m_LoginFriendPointGet==0 && MainMenuParam.m_LoginFriendHelpCt==0,"Pending helper text not consumed");
                Check(Step(page)==MAINMENU_SEQ.SEQ_QUEST_SELECT_AREA,"Repeat frame opened notice");
            }
        }
        var empty=Fresh();Singleton<UserDataAdmin>.Instance.m_StructPresentList=new PacketStructPresent[0];
        Check(Step(empty)==MAINMENU_SEQ.SEQ_QUEST_SELECT_AREA,"Empty-list route mismatch");
        var placeholders=Fresh();Singleton<UserDataAdmin>.Instance.m_StructPresentList=new[]{null,new PacketStructPresent()};
        Check(Step(placeholders)==(old?MAINMENU_SEQ.SEQ_LOGIN_PRESENT:MAINMENU_SEQ.SEQ_QUEST_SELECT_AREA),"Placeholder-list route mismatch");
        if(old)
        {
            Console.WriteLine("PASS legacy helper-notice regression: actual login awards2 helpers/20FP and receipt re-arms Present Info100 times; placeholders also open an empty notice. FP credited. See runner for baseline provenance.");return;
        }
        var gifts=Fresh();ReceiveHelperReward();var gift=new PacketStructPresent();gift.present_type=3;gift.present_value0=1;
        Singleton<UserDataAdmin>.Instance.m_StructPresentList=new[]{gift};uint fp=Save.have_friend_pt;
        Check(Step(gifts)==MAINMENU_SEQ.SEQ_LOGIN_PRESENT && Save.have_friend_pt==fp,"Real gift notification suppressed or FP lost");
        var resultPage=Fresh();Singleton<SceneGoesParam>.Instance.m_SceneGoesParamToMainMenu=new SceneGoesParamToMainMenu();
        Check(Step(resultPage)==MAINMENU_SEQ.SEQ_RESULT_SERVER_SEND,"Quest-clear result flow skipped");
        Singleton<SceneGoesParam>.Instance.m_SceneGoesParamToMainMenu=null;
        Check(Step(resultPage)==MAINMENU_SEQ.SEQ_QUEST_SELECT_AREA,"After result opens news");
        var retire=Fresh();Singleton<SceneGoesParam>.Instance.m_SceneGoesParamToMainMenuRetire=new SceneGoesParamToMainMenuRetire();
        Check(Step(retire)==MAINMENU_SEQ.SEQ_RESULT_RETIRE,"Retire flow skipped");
        var bonus=Fresh();MainMenuParam.m_LoginActive=true;
        Check(Step(bonus)==MAINMENU_SEQ.SEQ_LOGIN_BONUS,"Earned login reward skipped");
        Console.WriteLine("PASS candidate APK:100 actual2-helper/20FP login receipts return quietly through five empty/sentinel list shapes; awarded/displayed/persisted FP unchanged, pending popup counters consumed, repeat frames quiet.");
        Console.WriteLine("PASS genuine gifts and login rewards retain notifications; quest-clear/retire/setup routes preserved. No Android rendering/touch verified.");
    }
}
