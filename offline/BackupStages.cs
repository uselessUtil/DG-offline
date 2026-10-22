using System;
using LitJson;
using UnityEngine;

public static partial class DivineGateOffline
{
    public static bool BackupStagesApplied;

    private static void RestoreBackupStages(ServerForgery server)
    {
        BackupStagesApplied = false;
        if (!BackupStageData.Enabled) return;
        try
        {
            // Parse all tables before assigning any: a failure leaves the
            // complete original table set intact rather than mixing versions.
            var areas = JsonMapper.ToObject<MasterDataArea[]>(BackupStageData.Table(0));
            var quests = JsonMapper.ToObject<MasterDataQuest[]>(BackupStageData.Table(1));
            var floors = JsonMapper.ToObject<MasterDataQuestFloor[]>(BackupStageData.Table(2));
            var categories = JsonMapper.ToObject<MasterDataCategoryPattern[]>(BackupStageData.Table(3));
            var stars = JsonMapper.ToObject<MasterDataExpectPattern[]>(BackupStageData.Table(4));
            var panels = JsonMapper.ToObject<MasterDataParamPanel[]>(BackupStageData.Table(5));
            var panelGroups = JsonMapper.ToObject<MasterDataParamPanelGroup[]>(BackupStageData.Table(6));
            var enemies = JsonMapper.ToObject<MasterDataParamEnemy[]>(BackupStageData.Table(7));
            var groups = JsonMapper.ToObject<MasterDataEnemyGroup[]>(BackupStageData.Table(8));
            var bonuses = JsonMapper.ToObject<MasterDataAreaAmend[]>(BackupStageData.Table(9));
            var guerrillas = JsonMapper.ToObject<MasterDataGuerrillaBoss[]>(BackupStageData.Table(10));
            if (quests.Length != 371 || areas.Length != 102 || floors.Length != 1000)
                throw new InvalidOperationException("Unexpected backup stage data version");
            server.m_MasterDataArea = areas;
            server.m_MasterDataQuest = quests;
            server.m_MasterDataQuestFloor = floors;
            server.m_MasterDataQuestFloorCategory = categories;
            server.m_MasterDataQuestFloorExpect = stars;
            server.m_MasterDataParamPanel = panels;
            server.m_MasterDataParamPanelGroup = panelGroups;
            server.m_MasterDataParamEnemy = enemies;
            server.m_MasterDataParamEnemyGroup = groups;
            server.m_MasterDataAreaAmend = bonuses;
            server.m_MasterDataGuerrillaBoss = guerrillas;
            BackupStagesApplied = true;
            Debug.Log("[DG offline] Backup stage data applied; existing quest/save IDs preserved.");
        }
        catch (Exception ex)
        {
            error = "Backup stage data failed; using original stage tables.";
            Debug.LogError("[DG offline] " + error + " " + ex.Message);
        }
    }
}
