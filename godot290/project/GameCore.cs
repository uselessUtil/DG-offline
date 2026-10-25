using System;
using System.Linq;
using System.Reflection;
using LitJson;
using ServerDataDefine;

namespace DivineGateGodot;

public sealed class GameCore
{
    public ServerForgery Server {get;private set;}=null!;
    uint serial=1;
    public ServerForgerySaveUser Player=>DivineGateOffline.Local290Player(Server);
    public void Boot(string savePath,string tablesPath)
    {
        PortableEngine.Configure(savePath,tablesPath);
        Singleton<TimeManager>.Instance=new TimeManager();
        Singleton<MasterDataManager>.Instance=new MasterDataManager();
        Singleton<LocalSaveManager>.Instance=new LocalSaveManager();
        Server=new ServerForgery();Singleton<ServerForgery>.Instance=Server;
        typeof(ServerForgery).GetMethod("Start",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(Server,null);
        if(Player==null) {
            string uuid=Guid.NewGuid().ToString("N");
            Api<RecvCreateUser>(SERVER_API.SERVER_API_USER_CREATE,new SendCreateUser{uuid=uuid,terminal=new PacketStructTerminal{platform=1,name="Godot ARM64"}});
            Api<RecvRenameUser>(SERVER_API.SERVER_API_USER_RENAME,new SendRenameUser{user_name="Player"});
        }
    }
    public T Api<T>(SERVER_API api,object request)
    {
        var packet=new PacketData{m_PacketAPI=api,m_PacketUniqueNum=serial++};
        string response=Server.CreateRecvForgery(ref packet,JsonMapper.ToJson(request));
        if(string.IsNullOrEmpty(response))throw new InvalidOperationException("Empty response: "+api);
        var value=JsonMapper.ToObject(response);int code=(int)value["header"]["code"];
        if(code!=(int)API_CODE.API_CODE_SUCCESS)throw new InvalidOperationException(api+" returned "+code);
        return JsonMapper.ToObject<T>(response);
    }
    public void SelectStarter(int party)
    {
        Api<RecvSkipTutorial>(SERVER_API.SERVER_API_TUTORIAL_SKIP,new SendSkipTutorial{select_party=checked((uint)party)});
    }
    public PacketStructUnit[] Draw(uint pool,uint count)
    {
        var result=Api<RecvGachaPlay>(SERVER_API.SERVER_API_GACHA_PLAY,new SendGachaPlay{gacha_id=pool,gacha_ct=count});
        return result.result.unit_unique_id.Select(id=>Player.unit_list.First(u=>u.unique_id==id)).ToArray();
    }
    public uint Claim(int pack)=>DivineGateOffline.ClaimFreeChips(DivineGateOffline.FreeChipProductAt(pack).product_id);
}
