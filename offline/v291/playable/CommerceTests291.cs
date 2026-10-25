using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using LitJson;
using ServerDataDefine;

class CommerceTests291
{
    static ServerForgery server;
    static uint serial=1;
    static ServerForgerySaveUser Save {get{return DivineGateOffline.Local291Player(server);}}
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);Console.WriteLine("PASS "+message);}
    static T Api<T>(SERVER_API api,object request){
        var p=new PacketData();p.m_PacketAPI=api;p.m_PacketUniqueNum=serial++;
        string raw=server.CreateRecvForgery(ref p,JsonMapper.ToJson(request));
        if((int)JsonMapper.ToObject(raw)["header"]["code"]!=(int)API_CODE.API_CODE_SUCCESS)throw new Exception(raw);
        return JsonMapper.ToObject<T>(raw);
    }
    static void Main(string[] args){
        Singleton<TimeManager>.Instance=new TimeManager();
        Singleton<MasterDataManager>.Instance=new MasterDataManager();
        Singleton<LocalSaveManager>.Instance=new LocalSaveManager();
        server=new ServerForgery();Singleton<ServerForgery>.Instance=server;
        typeof(ServerForgery).GetMethod("Start",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(server,null);
        var rawRows=JsonMapper.ToObject<MasterDataGachaAssign[]>(File.ReadAllText(Path.Combine(Environment.GetEnvironmentVariable("DG_TEST_TABLES"),"MasterDataGachaAssign.json")));
        var expected=new Dictionary<uint,List<MasterDataGachaAssign>>();
        foreach(var row in rawRows){if(!expected.ContainsKey(row.gacha_id))expected.Add(row.gacha_id,new List<MasterDataGachaAssign>());expected[row.gacha_id].Add(row);}
        var seen=new HashSet<uint>();uint previous=uint.MaxValue;
        foreach(var row in server.m_MasterDataGachaAssign){
            if(row.gacha_id!=previous){Check(!seen.Contains(row.gacha_id),"Pool contiguous "+row.gacha_id);seen.Add(row.gacha_id);previous=row.gacha_id;}
            if(MasterDataUtil.GetCharaParamFromID(row.assign_chara_id)==null)throw new Exception("Missing pool character "+row.assign_chara_id);
        }
        if(args.Length>0){
            Check(Save!=null && Save.user_name=="Commerce291" && Save.unit_list.Length>=70,"Awarded cards and account survive a separate process");
            return;
        }
        var create=new SendCreateUser();create.uuid="commerce-291";create.terminal=new PacketStructTerminal();create.terminal.platform=1;
        Api<RecvCreateUser>(SERVER_API.SERVER_API_USER_CREATE,create);
        Api<RecvSelectDefParty>(SERVER_API.SERVER_API_USER_SELECT_DEF_PARTY,new SendSelectDefParty{select_party=1});
        Api<RecvRenameUser>(SERVER_API.SERVER_API_USER_RENAME,new SendRenameUser{user_name="Commerce291"});
        var start=new SendQuestStart();start.quest_id=1;start.helper_unit=new PacketStructUnit();start.helper_unit.id=139;start.helper_unit.level=1;
        Api<RecvQuestStart>(SERVER_API.SERVER_API_QUEST_START,start);
        Api<RecvQuestClear>(SERVER_API.SERVER_API_QUEST_CLEAR,new SendQuestClear{quest_id=1,get_money=2000,get_exp=14});
        Check(Save.have_stone==5,"Tutorial quest clear provides its original five-chip reward");
        var tutorial=Api<RecvGachaPlay>(SERVER_API.SERVER_API_GACHA_PLAY,new SendGachaPlay{gacha_id=10,gacha_ct=1});
        Check(Save.have_stone==0 && tutorial.result.unit_unique_id.Length==1,"Tutorial reward pays for the actual first scratch without shop access or an extra grant");
        Save.have_stone=Save.have_stone_free=999;Save.have_stone_pay=0;Save.have_friend_pt=10000;
        Save.unit_base=Save.unit_total=2000;
        int total=0;
        foreach(uint id in new uint[]{10,100100,200100,300100,300200,300300,300400}){
            var pool=MasterDataUtil.GetGachaParamFromID(id);
            foreach(uint count in new uint[]{1,9}){
                uint chips=Save.have_stone,fp=Save.have_friend_pt;int before=Save.unit_list.Length;
                var response=Api<RecvGachaPlay>(SERVER_API.SERVER_API_GACHA_PLAY,new SendGachaPlay{gacha_id=id,gacha_ct=count});
                Check(response.result.unit_unique_id.Length==count && Save.unit_list.Length==before+count,"Pool "+id+" awards "+count+" cards");
                Check(Save.have_stone==chips-(pool.type==1?0:pool.price*count) && Save.have_friend_pt==fp-(pool.type==1?pool.price*count:0),"Expected currency deducted for pool "+id);
                foreach(long uid in response.result.unit_unique_id){
                    PacketStructUnit card=null;foreach(var unit in Save.unit_list)if(unit.unique_id==uid)card=unit;
                    bool eligible=false;
                    foreach(var row in server.m_MasterDataGachaAssign)
                        if(row.gacha_id==id && row.active==2 && row.assign_rate>0 && card!=null && row.assign_chara_id==card.id)eligible=true;
                    if(!eligible)throw new Exception("Wrong-pool card "+id);
                }
                total+=(int)count;
            }
        }
        Check(total==70,"70 tutorial/friendship/standard/collab awards remain inside their requested pools");
        Save.have_stone=Save.have_stone_free=0;
        string beforeFailure=JsonMapper.ToJson(Save);
        var p=new PacketData();p.m_PacketAPI=SERVER_API.SERVER_API_GACHA_PLAY;p.m_PacketUniqueNum=serial++;
        string rejected=server.CreateRecvForgery(ref p,JsonMapper.ToJson(new SendGachaPlay{gacha_id=200100,gacha_ct=1}));
        Check((int)JsonMapper.ToObject(rejected)["header"]["code"]!=(int)API_CODE.API_CODE_SUCCESS && beforeFailure==JsonMapper.ToJson(Save),"Insufficient chips reject without changing inventory or save");
        var realUnits=Save.unit_list;
        Save.unit_list=new PacketStructUnit[Save.unit_total];
        for(int i=0;i<Save.unit_list.Length;i++)Save.unit_list[i]=realUnits[0];
        Save.have_stone=Save.have_stone_free=100;beforeFailure=JsonMapper.ToJson(Save);
        rejected=server.CreateRecvForgery(ref p,JsonMapper.ToJson(new SendGachaPlay{gacha_id=200100,gacha_ct=1}));
        Check((int)JsonMapper.ToObject(rejected)["header"]["code"]!=(int)API_CODE.API_CODE_SUCCESS && beforeFailure==JsonMapper.ToJson(Save),"Full inventory rejects scratch without spending");
        Save.unit_list=realUnits;
        foreach(uint invalid in new uint[]{0,10}) {
            beforeFailure=JsonMapper.ToJson(Save);
            rejected=server.CreateRecvForgery(ref p,JsonMapper.ToJson(new SendGachaPlay{gacha_id=200100,gacha_ct=invalid}));
            Check((int)JsonMapper.ToObject(rejected)["header"]["code"]!=(int)API_CODE.API_CODE_SUCCESS && beforeFailure==JsonMapper.ToJson(Save),"Invalid draw count rejected without mutation: "+invalid);
        }

        // 2.9.1's page must not wait for product events or billing initialization.
        var page=new MainMenuSeqShopRoot();
        StoreParam.m_StoreProductEventRenew=true;
        Check(!page.PageSwitchEventEnableBefore() && !StoreParam.m_StoreProductEventRenew,"Shop opens without a product-event or SDK wait");
        for(int i=0;i<6;i++)Check(StoreParam.m_StoreProductBase[i].product_price==0 && StoreParam.m_StoreProductChanged[i]==null,"Local FREE product "+i+" has no timed paid replacement");
        server.UserParamSave();
    }
}
