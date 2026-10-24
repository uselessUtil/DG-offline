using System;
using System.IO;
using UnityEngine;

public static partial class DivineGateOffline
{
    private const string SaveBridgeClass="jp.gungho.dg.offline106.SaveTransferActivity";
    private static bool savePickerPending;
    private static bool saveRestartRequired;
    private static string saveTransferStatus="Choose Export or Import. Backups include units, parties, progress and protected-unit settings.";
    private static string NativeBackupDirectory()
    {
        using(AndroidJavaClass player=new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
        using(AndroidJavaObject activity=player.GetStatic<AndroidJavaObject>("currentActivity"))
        using(AndroidJavaClass bridge=new AndroidJavaClass(SaveBridgeClass))
            return bridge.CallStatic<string>("directory",activity);
    }
    private static void SaveTransferMessage(string message)
    {
        saveTransferStatus=message;
        try {
            using(AndroidJavaClass player=new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using(AndroidJavaObject activity=player.GetStatic<AndroidJavaObject>("currentActivity"))
            using(AndroidJavaClass bridge=new AndroidJavaClass(SaveBridgeClass))bridge.CallStatic("message",activity,message);
        } catch(Exception e){Debug.LogError("[DG backup] "+e.Message);}
    }
    private static bool PollSaveTransfer()
    {
        try {
            string directory=LocalSaveUtil.GetValueString(BackupDirectoryKey,"");
            string statusFile=directory.Length==0?"":Path.Combine(directory,"result.txt");
            string result;
            if(statusFile.Length>0 && File.Exists(statusFile))
            { result=ReadSaveFile(statusFile);File.Delete(statusFile); }
            else
            {
                if(!savePickerPending)return false;
                using(AndroidJavaClass bridge=new AndroidJavaClass(SaveBridgeClass))result=bridge.CallStatic<string>("poll");
            }
            if(result==null || result.Length==0)return false;
            savePickerPending=false;
            if(result=="import-confirmed")
            {
                directory=NativeBackupDirectory();
                ImportSaveBackup(Singleton<ServerForgery>.Instance,ReadSaveFile(Path.Combine(directory,"import.json")),directory);
                File.Delete(Path.Combine(directory,"import.json"));
                saveRestartRequired=true;
                SaveTransferMessage("Save imported. Reopen the game to continue.");Application.Quit();return true;
            }
            if(result=="exported")SaveTransferMessage("Save exported successfully.");
            else if(result=="cancelled")SaveTransferMessage("Cancelled. Current save unchanged.");
            else SaveTransferMessage(result.StartsWith("error:")?result.Substring(6):result);
            return true; // Consume the returning frame's pre-pause touch.
        } catch(Exception e){savePickerPending=false;SaveTransferMessage("Save transfer failed: "+e.Message);return true;}
        return false;
    }
    public static bool UpdateSaveTransferUI(MainMenuSeqOthers page)
    {
        if(saveRestartRequired || PollSaveTransfer())return true;
        GameObject root=UnityUtil.GetChildNode(page.gameObject,"OthersWindow_150_Transfer");
        if(root==null)return false;
        GameObject entry=UnityUtil.GetChildNode(page.gameObject,"ListElement150");
        if(entry!=null)
        {
            foreach(LabelSetter setter in entry.GetComponentsInChildren<LabelSetter>(true)){setter.m_LabelTextKey="";setter.enabled=false;}
            UnityUtil.SetUILabelValue(UnityUtil.GetChildNode(entry,"ElementLabel"),"Save Backup");
        }
        foreach(LabelSetter setter in root.GetComponentsInChildren<LabelSetter>(true)){setter.m_LabelTextKey="";setter.enabled=false;}
        UnityUtil.SetUILabelValue(UnityUtil.GetChildNode(root,"PartsTitle"),"Save Backup");
        UnityUtil.SetUILabelValue(UnityUtil.GetChildNode(root,"PartsDetail"),
            "Export: choose a file location.\nImport: choose a backup and confirm replacement.\nFinish the current quest first.\n\n"+saveTransferStatus);
        if(root.activeInHierarchy)MainMenuFixLayerLinker.UnderMsgRequest("Local save backup: export / import files.");
        GameObject export=UnityUtil.GetChildNode(root,"ButtonTransferOrder"),import=UnityUtil.GetChildNode(root,"ButtonTransferExec");
        if(export!=null)UnityUtil.SetUILabelValue(UnityUtil.GetChildNode(export,"ButtonMsg"),"Export Save");
        if(import!=null)UnityUtil.SetUILabelValue(UnityUtil.GetChildNode(import,"ButtonMsg"),"Import Save");
        return false;
    }
    public static void SaveTransferInput(MainMenuSeqOthers page)
    {
        if(savePickerPending || Input2DManager.Instance==null || !Input2DManager.Instance.m_TouchObjectReleaseEqualTriger)return;
        if(Singleton<DialogManager>.Instance!=null && Singleton<DialogManager>.Instance.CheckDialogActive())return;
        GameObject root=UnityUtil.GetChildNode(page.gameObject,"OthersWindow_150_Transfer");if(root==null)return;
        GameObject clicked=Input2DManager.Instance.m_TouchObjectRelease;
        if(clicked==null)return;
        bool export=clicked==UnityUtil.GetChildNode(root,"ButtonTransferOrder");
        if(!export && clicked!=UnityUtil.GetChildNode(root,"ButtonTransferExec"))return;
        try {
            RequireSaveTransfer(Singleton<ServerForgery>.Instance);
            string directory=NativeBackupDirectory();
            LocalSaveUtil.SetValueString(BackupDirectoryKey,directory);LocalSaveUtil.ExecDataSave();
            if(export)WriteSaveFile(Path.Combine(directory,"export.json"),ExportSaveBackup(Singleton<ServerForgery>.Instance));
            using(AndroidJavaClass player=new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using(AndroidJavaObject activity=player.GetStatic<AndroidJavaObject>("currentActivity"))
            using(AndroidJavaClass bridge=new AndroidJavaClass(SaveBridgeClass))bridge.CallStatic("open",activity,export);
            savePickerPending=true;saveTransferStatus="Waiting for file selection...";
        } catch(Exception e){SaveTransferMessage("Save transfer failed: "+e.Message);}
    }
}
