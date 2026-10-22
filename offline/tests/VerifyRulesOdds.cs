using System;
using System.IO;
using System.Collections.Generic;
using System.Reflection;
using LitJson;
using ServerDataDefine;

public static class VerifyRulesOdds
{
    static ServerForgery server;
    static Type control;
    static uint serial=1;
    static Dictionary<uint,bool> five=new Dictionary<uint,bool>();
    static ServerForgerySaveUser Save { get { return (ServerForgerySaveUser)typeof(ServerForgery).GetField("m_ForgeryPlayerSave",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(server); } }
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    static void Set(string name,uint value){control.GetField(name).SetValue(null,value);}
    static uint Get(string name){return (uint)control.GetField(name).GetValue(null);}
    static void Ticket(uint a,uint b){Set("First",a);Set("Second",b);Set("Calls",0);}
    static MasterDataGachaAssign Decode(List<MasterDataGachaAssign> rows,uint ticket)
    {
        Check(ticket>0,"Zero ticket reintroduces first-bucket bias");
        foreach(var row in rows){if(ticket<=row.assign_rate)return row;ticket-=row.assign_rate;}
        throw new Exception("Ticket outside pool");
    }
    static void ApiBoundary(SendGachaPlay request,uint first,uint second,MasterDataGachaAssign expected)
    {
        Ticket(first,second);Save.have_stone=Save.have_stone_free=1000;Save.have_stone_pay=0;
        var packet=new PacketData();packet.m_PacketAPI=SERVER_API.SERVER_API_GACHA_PLAY;packet.m_PacketUniqueNum=serial++;
        string json=server.CreateRecvForgery(ref packet,JsonMapper.ToJson(request));
        var result=JsonMapper.ToObject<RecvGachaPlay>(json);
        Check(result.header.code==(int)API_CODE.API_CODE_SUCCESS,"Actual draw rejected");
        Check(result.result.unit_unique_id[0]==expected.assign_chara_id && Get("Level")==expected.assign_level &&
            Get("Pow")==expected.plus_pow && Get("HP")==expected.plus_hp,"Draw/variant boundary mismatch");
        Check(Save.have_stone==995,"Actual cost changed");
        foreach(uint id in result.result.blank_unit_id)Check(five.ContainsKey(id),"Invalid blank scratch unit");
    }
    public static void Main()
    {
        Singleton<MasterDataManager>.Instance=new MasterDataManager();Singleton<LocalSaveManager>.Instance=new LocalSaveManager();
        server=new ServerForgery();Singleton<ServerForgery>.Instance=server;
        typeof(ServerForgery).GetMethod("Start",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(server,null);
        control=typeof(ServerForgery).Assembly.GetType("RulesOddsControl");
        foreach(var c in server.m_MasterDataParamChara)if(c!=null)five[c.fix_id]=c.rare==4;
        var create=new SendCreateUser();create.uuid="rules-odds";create.terminal=new PacketStructTerminal();create.terminal.platform=1;create.terminal.name="Probe";
        var createPacket=new PacketData();createPacket.m_PacketAPI=SERVER_API.SERVER_API_USER_CREATE;createPacket.m_PacketUniqueNum=serial++;
        server.CreateRecvForgery(ref createPacket,JsonMapper.ToJson(create));
        var old=JsonMapper.ToObject<MasterDataGachaAssign[]>(File.ReadAllText(Path.Combine(Environment.GetEnvironmentVariable("DG_TEST_TABLES"),"MasterDataGachaAssign.json")));
        foreach(uint pool in new uint[]{200100,300100,300200,300300,300400})
        {
            var source=new List<MasterDataGachaAssign>();var rows=new List<MasterDataGachaAssign>();
            foreach(var row in old)if(row.gacha_id==pool && row.active==2 && row.assign_rate>0)source.Add(row);
            foreach(var row in server.m_MasterDataGachaAssign)if(row.gacha_id==pool && row.active==2)rows.Add(row);
            uint total=0,oldFive=0,newTotal=0;var standardUnits=new Dictionary<uint,bool>();
            for(int i=0;i<source.Count;i++)
            {
                total+=source[i].assign_rate;
                if(five[source[i].assign_chara_id])oldFive+=(uint)((long)source[i].assign_rate+(i==0?1:0)-(i==source.Count-1?1:0));
            }
            foreach(var row in rows){newTotal+=row.assign_rate;if(five[row.assign_chara_id])standardUnits[row.assign_chara_id]=true;}
            if(pool==200100)Check(standardUnits.Count==42,"Standard five-star roster incomplete");
            var request=new SendGachaPlay();request.gacha_id=pool;request.gacha_ct=1;
            uint actualFive=0;
            for(uint t=0;t<total;t++)
            {
                Ticket(t,0);var selected=Decode(rows,DivineGateOffline.ScratchTicket(0,newTotal,server,request));
                if(five[selected.assign_chara_id])actualFive++;
            }
            Check(actualFive==3*oldFive,"Not exactly tripled relative to original actual sampler");
            var poolMaster=MasterDataUtil.GetGachaParamFromID(pool);
            Check(poolMaster.detail_2.Contains("3x original local pool") && !poolMaster.detail_2.Contains("2x"),"Pool description has stale multiplier");
            foreach(bool isFive in new[]{true,false})
            {
                uint weight=0;foreach(var row in rows)if(five[row.assign_chara_id]==isFive)weight+=row.assign_rate;
                var counts=new Dictionary<uint,uint>();
                uint phase=isFive?0:total-1;
                for(uint t=0;t<weight;t++)
                {
                    Ticket(phase,t);var selected=Decode(rows,DivineGateOffline.ScratchTicket(0,newTotal,server,request));
                    Check(five[selected.assign_chara_id]==isFive,"Crossed rarity category");
                    if(!counts.ContainsKey(selected.fix_id))counts.Add(selected.fix_id,0);counts[selected.fix_id]++;
                }
                uint start=0;
                foreach(var row in rows)
                {
                    if(five[row.assign_chara_id]!=isFive || row.assign_rate==0)continue;
                    Check(counts[row.fix_id]==row.assign_rate,"Conditional weight/variant changed");
                    ApiBoundary(request,phase,start,row);ApiBoundary(request,phase,start+row.assign_rate-1,row);
                    start+=row.assign_rate;
                }
            }
            Console.WriteLine("PASS pool "+pool+": EXACT five-star probability "+oldFive+"/"+total+" -> "+actualFive+"/"+total+
                " ("+(100.0*actualFive/total).ToString("F6")+"%); "+standardUnits.Count+" five-star units; every conditional weight, plus variant and actual draw boundary verified.");
        }
        Console.WriteLine("LIMIT: deterministic disposable probe, not a Monte Carlo estimate; unit allocation intercepted only here, original payment/draw code executes. Android animations unverified.");
    }
}
