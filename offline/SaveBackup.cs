using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Runtime.InteropServices;
using LitJson;
using ServerDataDefine;
using UnityEngine;

[Serializable] public sealed class DGSavePreference
{ public string key; public int kind; public bool present; public string text; public int number; }
[Serializable] public sealed class DGSavePayload
{ public string server_save; public DGSavePreference[] preferences; }
[Serializable] public sealed class DGSaveEnvelope
{ public string format; public int schema; public string game_version; public string prototype;
  public string created_utc; public string payload; public string sha256; }

public static partial class DivineGateOffline
{
    public const int SaveBackupLimit = 4 * 1024 * 1024;
    private static bool saveImportBusy;
    private const string BackupDirectoryKey = "DG_OFFLINE_BACKUP_DIRECTORY";
    private static readonly string[] saveStringKeys = {
        "divinegate_USER_ID","divinegate_USER_UUID","divinegate_SAVE_VERSION","divinegate_RESTORE_VER",
        "divinegate_FAVOTIRE_UNIT","divinegate_FAVOTIRE_FRIEND","divinegate_FRIEND_USE","divinegate_OPTION" };
    private static readonly string[] saveIntKeys = {
        "divinegate_TUTORIAL_GROUP","divinegate_INFORMATION_2nd","divinegate_QUALITY",
        "divinegate_SORT_PARTY_FORM","divinegate_SORT_BUILD_UNIT","divinegate_SORT_BUILD_FRIEND",
        "divinegate_SORT_EVOLVE_UNIT","divinegate_SORT_EVOLVE_FRIEND","divinegate_SORT_UNIT_SALE",
        "divinegate_SORT_UNIT_LIST","divinegate_SORT_FRIEND_LIST","divinegate_SORT_FRIEND_WAIT_ME",
        "divinegate_SORT_FRIEND_WAIT_HIM","divinegate_SORT_QUEST_FRIEND" };
    private static readonly string[] saveTransientKeys = {
        "divinegate_GOES_TO_GAME_RESTORE","divinegate_GOES_TO_GAME_START","divinegate_GOES_TO_MENU_RESULT",
        "divinegate_GOES_TO_MENU_RETIRE","divinegate_QUEST_FP_ACTIVE","divinegate_LOGINPARAM",
        "divinegate_INGAME_CONTINUE","divinegate_INGAME_RESET","divinegate_SCRATCH" };

    public static bool SaveTransferAllowed(ServerForgery server)
    {
        ServerForgerySaveUser p=server==null?null:LocalShopPlayer(server);
        return p!=null && p.quest_id==0 && !p.evolve_quest_active;
    }
    private static void RequireSaveTransfer(ServerForgery server)
    { if(!SaveTransferAllowed(server))throw new InvalidOperationException("Return to the main menu and finish the current quest first."); }
    private static DGSavePreference CapturePreference(string key,int kind)
    {
        DGSavePreference p=new DGSavePreference();p.key=key;p.kind=kind;p.present=PlayerPrefs.HasKey(key);
        p.text=kind==1?LocalSaveUtil.GetValueString(key,""):"";
        p.number=kind==0?LocalSaveUtil.GetValueInt(key,0):0;return p;
    }
    private static DGSavePayload CaptureSavePayload(ServerForgery server)
    {
        List<DGSavePreference> prefs=new List<DGSavePreference>();
        foreach(string key in saveStringKeys)prefs.Add(CapturePreference(key,1));
        foreach(string key in saveIntKeys)prefs.Add(CapturePreference(key,0));
        foreach(string key in saveTransientKeys)prefs.Add(CapturePreference(key,1));
        DGSavePayload p=new DGSavePayload();p.server_save=JsonMapper.ToJson(LocalShopPlayer(server));p.preferences=prefs.ToArray();return p;
    }
    private static string WrapSave(DGSavePayload payload)
    {
        DGSaveEnvelope e=new DGSaveEnvelope();e.format="dg-offline-save";e.schema=1;e.game_version="1.0.6";
        e.prototype=Build;e.created_utc=DateTime.Now.ToUniversalTime().ToString("s")+"Z";e.payload=JsonMapper.ToJson(payload);e.sha256=SaveChecksum(e.payload);
        string text=JsonMapper.ToJson(e);if(Encoding.UTF8.GetByteCount(text)>SaveBackupLimit)throw new InvalidOperationException("Backup exceeds 4 MiB.");return text;
    }
    public static string ExportSaveBackup(ServerForgery server)
    { RequireSaveTransfer(server);server.UserParamSave();return WrapSave(CaptureSavePayload(server)); }
    private static TemplateList<T> ValidateBackupList<T>(string text)
    {
        TemplateList<T> list=JsonMapper.ToObject<TemplateList<T>>(text);
        if(list==null || list.m_BufferSize<0 || list.m_BufferSize>20000 ||
            (list.m_Buffer==null?list.m_BufferSize!=0:list.m_BufferSize>list.m_Buffer.Length))
            throw new InvalidOperationException("Invalid collection settings.");return list;
    }
    private static void ValidateIntegralSaveNumbers(string text)
    {
        // Inspect the raw text: old LitJson turns an overflowing integer into
        // an ordinary zero even in its generic JSON tree, losing the evidence.
        bool quoted=false;
        for(int i=0;i<text.Length;i++)
        {
            char c=text[i];
            if(quoted){if(c=='\\')i++;else if(c=='"')quoted=false;continue;}
            if(c=='"'){quoted=true;continue;}
            if(c!='-' && (c<'0' || c>'9'))continue;
            int start=i;
            while(i<text.Length && ((text[i]>='0' && text[i]<='9') || text[i]=='-' || text[i]=='+' ||
                text[i]=='.' || text[i]=='e' || text[i]=='E'))i++;
            long number;
            if(!long.TryParse(text.Substring(start,i-start),out number))throw new InvalidOperationException("Invalid numeric value in backup.");
            i--;
        }
    }
    public static DGSavePayload ValidateSaveBackup(ServerForgery server,string text)
    {
        if(text==null || Encoding.UTF8.GetByteCount(text)>SaveBackupLimit)throw new InvalidOperationException("Invalid backup size.");
        DGSaveEnvelope e=JsonMapper.ToObject<DGSaveEnvelope>(text);
        if(e==null || e.format!="dg-offline-save" || e.schema!=1 || e.game_version!="1.0.6" || e.payload==null ||
            e.sha256!=SaveChecksum(e.payload))throw new InvalidOperationException("Incompatible or damaged backup.");
        ValidateIntegralSaveNumbers(e.payload);
        DGSavePayload p=JsonMapper.ToObject<DGSavePayload>(e.payload);
        if(p==null || p.server_save==null || p.preferences==null)throw new InvalidOperationException("Incomplete backup.");
        ValidateIntegralSaveNumbers(p.server_save);
        ServerForgerySaveUser s=JsonMapper.ToObject<ServerForgerySaveUser>(p.server_save);
        if(s==null || s.player_rank==0 || s.player_rank>server.m_MasterDataUserRank.Length || s.user_name==null ||
            s.user_name.Length>256 || s.uuid==null || s.uuid.Length>1024 || s.quest_id!=0 || s.evolve_quest_active ||
            s.unit_party_assign==null || s.unit_party_assign.Length!=5 || s.unit_party_current<0 || s.unit_party_current>=5 ||
            s.unit_total>100000 || s.login_last>uint.MaxValue || s.stamina_recover>uint.MaxValue ||
            (s.unit_list!=null && s.unit_list.Length>20000))
            throw new InvalidOperationException("Invalid account or an unfinished quest in backup.");
        Dictionary<long,bool> units=new Dictionary<long,bool>();
        if(s.unit_list!=null)foreach(PacketStructUnit u in s.unit_list)
        {
            if(u==null)throw new InvalidOperationException("Null unit in backup.");
            MasterDataParamChara c=null;foreach(MasterDataParamChara row in server.m_MasterDataParamChara)if(row!=null && row.fix_id==u.id){c=row;break;}
            if(u==null || u.unique_id<=0 || units.ContainsKey(u.unique_id) || c==null || u.level==0 || u.level>c.level_max || u.get_time>uint.MaxValue)
                throw new InvalidOperationException("Invalid or duplicate unit in backup.");
            units.Add(u.unique_id,true);
        }
        foreach(PacketStructPartyAssign party in s.unit_party_assign)
        {
            if(party==null)throw new InvalidOperationException("Missing party data.");
            foreach(long id in new long[]{party.unit0_unique_id,party.unit1_unique_id,party.unit2_unique_id,party.unit3_unique_id})
                if(id!=0 && !units.ContainsKey(id))throw new InvalidOperationException("Party references a missing unit.");
        }
        Dictionary<string,int> keys=new Dictionary<string,int>();
        foreach(string key in saveStringKeys)keys.Add(key,1);foreach(string key in saveIntKeys)keys.Add(key,0);
        foreach(string key in saveTransientKeys)keys.Add(key,1);
        if(p.preferences.Length!=keys.Count)throw new InvalidOperationException("Incomplete settings backup.");
        foreach(DGSavePreference pref in p.preferences)
        {
            int kind;if(pref==null || pref.key==null || !keys.TryGetValue(pref.key,out kind) || kind!=pref.kind || pref.text==null)
                throw new InvalidOperationException("Invalid settings key or type.");
            keys.Remove(pref.key);
            if(!pref.present || pref.kind!=1 || pref.text.Length==0)continue;
            if(pref.key=="divinegate_OPTION" || pref.key=="divinegate_FAVOTIRE_UNIT" ||
                pref.key=="divinegate_FAVOTIRE_FRIEND" || pref.key=="divinegate_FRIEND_USE")ValidateIntegralSaveNumbers(pref.text);
            if(pref.key=="divinegate_OPTION" && JsonMapper.ToObject<LocalSaveOption>(pref.text)==null)
                throw new InvalidOperationException("Invalid sound options.");
            if(pref.key=="divinegate_FAVOTIRE_UNIT")ValidateBackupList<long>(pref.text);
            if(pref.key=="divinegate_FAVOTIRE_FRIEND")ValidateBackupList<uint>(pref.text);
            if(pref.key=="divinegate_FRIEND_USE")ValidateBackupList<LocalSaveFriendUse>(pref.text);
        }
        return p;
    }
    private static void ApplySavePayload(DGSavePayload p,bool imported)
    {
        LocalSaveUtil.SetValueString("SERVER_SAVE",p.server_save);
        foreach(DGSavePreference pref in p.preferences)
        {
            if(pref.present)
            { if(pref.kind==1)LocalSaveUtil.SetValueString(pref.key,pref.text);else LocalSaveUtil.SetValueInt(pref.key,pref.number); }
            else LocalSaveUtil.ExecDataRemove(pref.key);
        }
        if(imported)foreach(string key in saveTransientKeys)LocalSaveUtil.ExecDataRemove(key);
        LocalSaveUtil.ExecDataSave();
    }
    // PatchRules binds this to libc.rename through P/Invoke metadata. The
    // archived mscorlib omits DllImportAttribute's managed constructor.
    private static int RenameSaveFile(string from,string to)
    { throw new InvalidOperationException("Missing atomic rename binding."); }
    public static void WriteSaveFile(string path,string text)
    {
        string temporary=path+".tmp";
        using(FileStream stream=new FileStream(temporary,FileMode.Create,FileAccess.Write,FileShare.None))
        { byte[] bytes=new UTF8Encoding(false).GetBytes(text);stream.Write(bytes,0,bytes.Length);stream.Flush(); }
        if(RenameSaveFile(temporary,path)!=0)throw new IOException("Cannot commit backup file: "+Marshal.GetLastWin32Error());
    }
    public static string ReadSaveFile(string path)
    {
        using(FileStream stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read))
        {
            if(stream.Length>SaveBackupLimit)throw new InvalidOperationException("Backup exceeds 4 MiB.");
            using(StreamReader reader=new StreamReader(stream,new UTF8Encoding(false,true)))return reader.ReadToEnd();
        }
    }
    public static void ImportSaveBackup(ServerForgery server,string text,string directory)
    {
        RequireSaveTransfer(server);DGSavePayload incoming=ValidateSaveBackup(server,text);
        Directory.CreateDirectory(directory);
        string journal=Path.Combine(directory,"restore-journal.json");
        if(File.Exists(journal)){RecoverSaveJournal(server,directory);server.UserParamLoad();}
        string old=ExportSaveBackup(server);DGSavePayload rollback=ValidateSaveBackup(server,old);
        string backup=Path.Combine(directory,"before-import-"+DateTime.Now.ToUniversalTime().Ticks.ToString()+".json");
        WriteSaveFile(backup,old);WriteSaveFile(journal,old);
        LocalSaveUtil.SetValueString(BackupDirectoryKey,directory);LocalSaveUtil.ExecDataSave();
        saveImportBusy=true;
        try
        {
            ApplySavePayload(incoming,true);
            if(!server.UserParamLoad())throw new InvalidOperationException("Imported account failed to load.");
            File.Delete(journal);
        }
        catch
        {
            ApplySavePayload(rollback,false);server.UserParamLoad();File.Delete(journal);throw;
        }
        finally { saveImportBusy=false; }
    }
    public static void RecoverSaveJournal(ServerForgery server,string directory)
    {
        if(saveImportBusy || directory==null || directory.Length==0)return;
        string path=Path.Combine(directory,"restore-journal.json");if(!File.Exists(path))return;
        DGSavePayload previous=ValidateSaveBackup(server,ReadSaveFile(path));
        saveImportBusy=true;
        try { ApplySavePayload(previous,false);File.Delete(path); }
        finally { saveImportBusy=false; }
    }
    public static void BeforeSaveAccountLoad(ServerForgery server)
    { RecoverSaveJournal(server,LocalSaveUtil.GetValueString(BackupDirectoryKey,"")); }

    // SHA-256 without adding a modern crypto assembly to stripped Unity 4.1.
    private static uint Ror(uint x,int n){return (x>>n)|(x<<(32-n));}
    public static string SaveChecksum(string text)
    {
        uint[] k={0x428a2f98,0x71374491,0xb5c0fbcf,0xe9b5dba5,0x3956c25b,0x59f111f1,0x923f82a4,0xab1c5ed5,
            0xd807aa98,0x12835b01,0x243185be,0x550c7dc3,0x72be5d74,0x80deb1fe,0x9bdc06a7,0xc19bf174,
            0xe49b69c1,0xefbe4786,0x0fc19dc6,0x240ca1cc,0x2de92c6f,0x4a7484aa,0x5cb0a9dc,0x76f988da,
            0x983e5152,0xa831c66d,0xb00327c8,0xbf597fc7,0xc6e00bf3,0xd5a79147,0x06ca6351,0x14292967,
            0x27b70a85,0x2e1b2138,0x4d2c6dfc,0x53380d13,0x650a7354,0x766a0abb,0x81c2c92e,0x92722c85,
            0xa2bfe8a1,0xa81a664b,0xc24b8b70,0xc76c51a3,0xd192e819,0xd6990624,0xf40e3585,0x106aa070,
            0x19a4c116,0x1e376c08,0x2748774c,0x34b0bcb5,0x391c0cb3,0x4ed8aa4a,0x5b9cca4f,0x682e6ff3,
            0x748f82ee,0x78a5636f,0x84c87814,0x8cc70208,0x90befffa,0xa4506ceb,0xbef9a3f7,0xc67178f2};
        uint[] h={0x6a09e667,0xbb67ae85,0x3c6ef372,0xa54ff53a,0x510e527f,0x9b05688c,0x1f83d9ab,0x5be0cd19};
        byte[] input=Encoding.UTF8.GetBytes(text),data=new byte[((input.Length+72)/64)*64];
        Array.Copy(input,data,input.Length);data[input.Length]=128;ulong bits=(ulong)input.Length*8;
        for(int i=0;i<8;i++)data[data.Length-1-i]=(byte)(bits>>(8*i));
        uint[] w=new uint[64];
        unchecked { for(int offset=0;offset<data.Length;offset+=64)
        {
            for(int i=0;i<16;i++){int p=offset+i*4;w[i]=((uint)data[p]<<24)|((uint)data[p+1]<<16)|((uint)data[p+2]<<8)|data[p+3];}
            for(int i=16;i<64;i++)w[i]=w[i-16]+(Ror(w[i-15],7)^Ror(w[i-15],18)^(w[i-15]>>3))+w[i-7]+(Ror(w[i-2],17)^Ror(w[i-2],19)^(w[i-2]>>10));
            uint a=h[0],b=h[1],c=h[2],d=h[3],e=h[4],f=h[5],g=h[6],v=h[7];
            for(int i=0;i<64;i++)
            {
                uint t=v+(Ror(e,6)^Ror(e,11)^Ror(e,25))+((e&f)^(~e&g))+k[i]+w[i];
                uint s=(Ror(a,2)^Ror(a,13)^Ror(a,22))+((a&b)^(a&c)^(b&c));
                v=g;g=f;f=e;e=d+t;d=c;c=b;b=a;a=t+s;
            }
            h[0]+=a;h[1]+=b;h[2]+=c;h[3]+=d;h[4]+=e;h[5]+=f;h[6]+=g;h[7]+=v;
        }}
        const string hex="0123456789abcdef";char[] result=new char[64];
        for(int i=0;i<8;i++)for(int j=0;j<8;j++)result[i*8+j]=hex[(int)((h[i]>>(28-j*4))&15)];return new string(result);
    }
}
