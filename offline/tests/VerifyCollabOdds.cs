using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using LitJson;
using ServerDataDefine;

public class GachaAuditMember { public uint id; public int count; }
public class GachaAuditPool
{
    public uint pool_id;
    public uint total;
    public uint price;
    public int currency_type;
    public GachaAuditMember[] members;
}

public static class VerifyCollabOdds
{
    static ServerForgery server;
    static uint serial=1;
    static ServerForgerySaveUser Save { get { return (ServerForgerySaveUser)typeof(ServerForgery)
        .GetField("m_ForgeryPlayerSave",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(server); } }
    static void Check(bool test,string message){if(!test)throw new Exception(message);}
    static T Api<T>(SERVER_API api,object request)
    {
        var packet=new PacketData();packet.m_PacketAPI=api;packet.m_PacketUniqueNum=serial++;
        string response=server.CreateRecvForgery(ref packet,JsonMapper.ToJson(request));
        Check((int)JsonMapper.ToObject(response)["header"]["code"]==(int)API_CODE.API_CODE_SUCCESS,"API failure "+api);
        return JsonMapper.ToObject<T>(response);
    }
    public static void Main(string[] args)
    {
        Singleton<MasterDataManager>.Instance=new MasterDataManager();
        Singleton<LocalSaveManager>.Instance=new LocalSaveManager();
        server=new ServerForgery();Singleton<ServerForgery>.Instance=server;
        typeof(ServerForgery).GetMethod("Start",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(server,null);
        Check(server.m_MasterDataGacha.Length==46 && server.m_MasterDataGachaAssign.Length==11000,"Unexpected master tables");
        var create=new SendCreateUser();create.uuid="collab-odds-audit";create.terminal=new PacketStructTerminal();
        create.terminal.name="Isolated audit";create.terminal.platform=1;
        Api<RecvCreateUser>(SERVER_API.SERVER_API_USER_CREATE,create);
        foreach(var pool in JsonMapper.ToObject<GachaAuditPool[]>(File.ReadAllText(args[0])))
        {
            uint price=pool.price==0?5:pool.price;
            uint sum=0;foreach(var row in server.m_MasterDataGachaAssign)
                if(row.gacha_id==pool.pool_id && row.active==2)sum+=row.assign_rate;
            Check(sum==pool.total,"Runtime weights changed");
            var counts=new Dictionary<uint,int>();
            for(uint ticket=0;ticket<pool.total;ticket++)
            {
                GachaAuditControl.Ticket=ticket;
                Save.have_stone=999999;Save.have_stone_free=999999;Save.have_stone_pay=0;
                Save.have_friend_pt=999999;
                var request=new SendGachaPlay();request.gacha_id=pool.pool_id;request.gacha_ct=1;
                var result=Api<RecvGachaPlay>(SERVER_API.SERVER_API_GACHA_PLAY,request);
                uint character=(uint)result.result.unit_unique_id[0];
                if(!counts.ContainsKey(character))counts.Add(character,0);
                counts[character]++;
                if(pool.currency_type==1)
                    Check(Save.have_friend_pt==999999-price && Save.have_stone==999999,"Unexpected friendship price");
                else
                    Check(Save.have_stone==999999-price,"Unexpected single-draw price");
            }
            Check(counts.Count==pool.members.Length,"Unexpected pool member");
            foreach(var member in pool.members)
                Check(counts.ContainsKey(member.id) && counts[member.id]==member.count,"Wrong bucket count for "+member.id);
            Console.WriteLine("PASS pool "+pool.pool_id+": all "+pool.total+" tickets, "+pool.members.Length+" characters, exact expected counts; cost "+price+"/draw, currency type "+pool.currency_type+".");
        }
        Console.WriteLine("LIMIT: deterministic-input probe of packaged draw method; unit allocation/storage replaced only in a disposable DLL; not historical official-server odds or Android rendering.");
    }
}
