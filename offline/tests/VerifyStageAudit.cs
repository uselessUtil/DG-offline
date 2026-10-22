// Run against the exact packaged game DLL and the existing mocked Unity engine.
// This probes real group creation and post-defeat routing, not Android rendering.
using System;
using System.IO;
using System.Reflection;
using LitJson;

public class StagePhaseCase
{
    public uint quest_id;
    public int floor_index;
    public uint[] apk_groups;
    public uint[] backup_groups;
}

public static class VerifyStageAudit
{
    static void Check(bool value, string message)
    { if (!value) throw new Exception(message); }

    static int FixedGroups(BattleReqManager queue, MasterDataEnemyGroup[] groups)
    {
        Singleton<MasterDataManager>.Instance.m_MasterDataParamEnemyGroup = groups;
        int checkedGroups = 0;
        foreach (var group in groups)
        {
            if (group.fix != 2) continue;
            queue.BattleRequestClear();
            queue.BattleRequestAdd(2,0,group.fix_id,1,2,3,group.chain_id,group.chain_turn_offset,true,false,0);
            uint[] actual = new uint[7]; bool[] drops = new bool[7];
            int count = InGameUtil.CreateEnemyList(queue.GetBattleRequest(), ref actual, ref drops);
            uint[] slots = {group.enemy_id_1,group.enemy_id_2,group.enemy_id_3,group.enemy_id_4,
                            group.enemy_id_5,group.enemy_id_6,group.enemy_id_7};
            int position = 0;
            foreach (uint enemy in slots)
            {
                if (enemy == 0) continue;
                Check(actual[position++] == enemy, "Fixed enemy order mismatch for " + group.fix_id);
            }
            Check(count == position, "Fixed group count mismatch for " + group.fix_id);
            checkedGroups++;
        }
        return checkedGroups;
    }

    static void Route(BattleReqManager queue, InGameManager game, uint[] phases)
    {
        Check(phases.Length > 0, "No initial boss");
        queue.BattleRequestClear();
        var initial = MasterDataUtil.GetEnemyGroupParamFromID(phases[0]);
        queue.BattleRequestAdd(2,0,initial.fix_id,1,2,3,initial.chain_id,initial.chain_turn_offset,true,false,0);
        var end = typeof(InGameManager).GetMethod("StepFuncBattleEnd",BindingFlags.NonPublic|BindingFlags.Instance);
        for (int i=1;i<phases.Length;i++)
        {
            queue.GetBattleRequest().m_BattleRequestAttached=true;
            Check(end.Invoke(game,null).ToString()=="eINGAME_STEP_BATTLE_EVOL_START", "Phase did not invoke evolution");
            var next=queue.GetBattleRequest();
            Check(next.m_BattleEnemyGroupID==phases[i] && next.m_BattleEnemyBoss && next.m_BattleEnemyChain,
                  "Unexpected next Boss group");
        }
        Check(queue.GetBattleRequest().m_BattleEnemyChainGroupID==0, "Final boss still has a phase");
    }

    public static void Main(string[] args)
    {
        Singleton<MasterDataManager>.Instance=new MasterDataManager();
        Singleton<LocalSaveManager>.Instance=new LocalSaveManager();
        var server=new ServerForgery();Singleton<ServerForgery>.Instance=server;
        typeof(ServerForgery).GetMethod("Start",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(server,null);
        var queue=new BattleReqManager();Singleton<BattleReqManager>.Instance=queue;
        typeof(BattleReqManager).GetMethod("Start",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(queue,null);
        Singleton<BattleManager>.Instance=new BattleManager();
        var game=new InGameManager();
        var apk=JsonMapper.ToObject<MasterDataEnemyGroup[]>(File.ReadAllText(args[0]));
        var backup=JsonMapper.ToObject<MasterDataEnemyGroup[]>(File.ReadAllText(args[1]));
        var cases=JsonMapper.ToObject<StagePhaseCase[]>(File.ReadAllText(args[2]));
        Console.WriteLine("PASS real CreateEnemyList fixed groups: APK="+FixedGroups(queue,apk)+", backup="+FixedGroups(queue,backup));
        foreach(var c in cases)
        {
            Singleton<MasterDataManager>.Instance.m_MasterDataParamEnemyGroup=apk;
            Route(queue,game,c.apk_groups);
            Singleton<MasterDataManager>.Instance.m_MasterDataParamEnemyGroup=backup;
            Route(queue,game,c.backup_groups);
            Console.WriteLine("PASS quest "+c.quest_id+" floor "+c.floor_index+": APK phases="+c.apk_groups.Length+", backup phases="+c.backup_groups.Length);
        }
        Console.WriteLine("LIMIT: phase routing/group creation only; Unity mocked, no Android battle/animation/rendering execution.");
    }
}
