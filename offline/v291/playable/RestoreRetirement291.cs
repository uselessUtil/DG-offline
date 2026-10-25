// Finish a declined/unrecoverable local quest through the original retire API.
using System;
using LitJson;
using ServerDataDefine;
using UnityEngine;

public sealed class DG291QuestRetireJournal
{
    public string server_save;
    public string sha256;
    public uint quest_id;
}
public static partial class DivineGateOffline
{
    private const string RestoreRetireJournal291="DG291_QUEST_RETIRE_JOURNAL";
    private static Dialog restoreRetireError291;
    private static void SetRestorePlayer291(ServerForgery server,ServerForgerySaveUser player)
    { throw new InvalidOperationException("Missing restore player bridge."); }

    public static TitleMenuSeqRestoreChk.RESTORE_SEQ RestoreIdlePhase291()
    {
        var server=Singleton<ServerForgery>.Instance;
        var player=server==null?null:LocalShopPlayer(server);
        var local=Singleton<LocalSaveManager>.Instance;
        // Pending results/retire requests belong to the original menu result
        // flow. Never discard a result or an otherwise resumable quest.
        if(player!=null && (player.quest_id!=0 || player.evolve_quest_active) && local!=null &&
            !local.LoadFuncGoesToGameStartChk() && !local.LoadFuncGoesToGameRestoreChk() &&
            !local.LoadFuncGoesToMenuResultChk() && local.LoadFuncGoesToMenuRetire()==null)
        {
            Debug.Log("[DG291 restore] Abandoned quest has no resume/result data; scheduling original retirement.");
            return TitleMenuSeqRestoreChk.RESTORE_SEQ.SEQ_CHK_FINISH_CANCEL;
        }
        return TitleMenuSeqRestoreChk.RESTORE_SEQ.SEQ_CHK_FINISH_PASS;
    }
    public static void RecoverRestoreRetirement291(ServerForgery server)
    {
        string text=LocalSaveUtil.GetValueString(RestoreRetireJournal291,"");
        if(text.Length==0)return;
        var journal=JsonMapper.ToObject<DG291QuestRetireJournal>(text);
        if(journal==null || journal.server_save==null || journal.sha256!=SaveChecksum(journal.server_save))
            throw new InvalidOperationException("Damaged quest retirement recovery journal.");
        var previous=JsonMapper.ToObject<ServerForgerySaveUser>(journal.server_save);
        if(previous==null || previous.quest_id==0 || previous.quest_id!=journal.quest_id)
            throw new InvalidOperationException("Invalid quest retirement recovery journal.");
        LocalSaveUtil.SetValueString("SERVER_SAVE",journal.server_save);
        LocalSaveUtil.ExecDataSave();
        LocalSaveUtil.ExecDataRemove(RestoreRetireJournal291);
        LocalSaveUtil.ExecDataSave();
        Debug.Log("[DG291 restore] Interrupted retirement restored before account load.");
    }
    // Prefix guard: the original tutorial reset, cache cleanup and navigation
    // execute only after a successful, persisted original backend retirement.
    public static bool CompleteRestoreRetirement291()
    {
        if(restoreRetireError291!=null)
        {
            if(restoreRetireError291.m_DialogPushButton==EDIALOG_BTN.eDIALOG_BTN_NONE)return false;
            restoreRetireError291.Close();restoreRetireError291=null;
        }
        var server=Singleton<ServerForgery>.Instance;
        var player=server==null?null:LocalShopPlayer(server);
        if(player==null)return false;
        if(player.quest_id==0 && !player.evolve_quest_active)return true;
        string before=JsonMapper.ToJson(player);
        try
        {
            if(player.quest_id==0)throw new InvalidOperationException("Evolution quest has no quest ID.");
            // Commit the recovery snapshot before the original API can mutate
            // its in-memory account or PlayerPrefs. It excludes no unit/currency.
            var journal=new DG291QuestRetireJournal{server_save=before,sha256=SaveChecksum(before),quest_id=player.quest_id};
            LocalSaveUtil.SetValueString(RestoreRetireJournal291,JsonMapper.ToJson(journal));
            LocalSaveUtil.ExecDataSave();
            var packet=new PacketData{m_PacketAPI=SERVER_API.SERVER_API_QUEST_RETIRE,m_PacketUniqueNum=0};
            var request=new SendQuestRetire{quest_id=player.quest_id};
            var response=JsonMapper.ToObject<RecvQuestRetire>(server.CreateRecvForgery(ref packet,JsonMapper.ToJson(request)));
            var retired=LocalShopPlayer(server);
            if(response==null || response.header==null || response.header.code!=4096 ||
                retired==null || retired.quest_id!=0 || retired.evolve_quest_active ||
                LocalSaveUtil.GetValueString("SERVER_SAVE","")!=JsonMapper.ToJson(retired))
                throw new InvalidOperationException("Quest retirement was not persisted.");
            LocalSaveUtil.ExecDataRemove(RestoreRetireJournal291);LocalSaveUtil.ExecDataSave();
            Debug.Log("[DG291 restore] Original quest retirement saved; resume cleanup may proceed.");
            return true;
        }
        catch(Exception error)
        {
            // Keep the journal if any persistence operation fails. Startup
            // recovery restores it before UserParamLoad and permits a retry.
            SetRestorePlayer291(server,JsonMapper.ToObject<ServerForgerySaveUser>(before));
            try {LocalSaveUtil.SetValueString("SERVER_SAVE",before);LocalSaveUtil.ExecDataSave();}
            catch(Exception) {}
            Debug.LogError("[DG291 restore] Retirement failed; resume data retained: "+error.GetType().Name);
            if(Singleton<DialogManager>.Instance!=null)
                restoreRetireError291=DialogManager.Open1B("ERROR_MSG_WIDEUSE_TITLE","ERROR_MSG_WIDEUSE","BTN_OK",true);
            return false;
        }
    }
}
