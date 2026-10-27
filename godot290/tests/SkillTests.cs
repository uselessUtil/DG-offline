using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Text.Json;
using DivineGateGodot;
using ServerDataDefine;
public static class SkillTests
{
    static void Check(bool ok,string text){if(!ok)throw new Exception(text);Console.WriteLine("PASS "+text);}
    public static void Run()
    {
        var core=new GameCore();string dir="/tmp/dg-godot290/skill-test-"+Guid.NewGuid().ToString("N");
        core.Boot(dir,Environment.GetEnvironmentVariable("DG_TEST_TABLES")!);core.SelectStarter(1);
        var findings=new List<object>();int activated=0,blocked=0;
        var chars=core.Server.m_MasterDataParamChara.Where(c=>c.skill_limitbreak!=0).GroupBy(c=>c.skill_limitbreak).Select(g=>g.First()).ToArray();
        foreach(var ch in chars){
            // Isolated rule fixture, never the device's save. Synthetic party and
            // charged cooldown let each source skill family be exercised directly.
            var team=new PacketStructUnit[5];for(int i=0;i<5;i++)team[i]=new PacketStructUnit{id=ch.fix_id,unique_id=(uint)(i+1),level=1};
            var run=new QuestRun(core);run.Start(1,team);var sk=run.ActiveSkill(0)!;
            if(sk.phase==2 || sk.phase==3){run.Move(17);run.Move(12);Check(run.InBattle,"fixture enters reference battle "+sk.fix_id);}
            var c=run.Team[0];c.AddCharaLimitBreak(999);run.Party.m_PartyTotalHP=Math.Max(2,run.Party.m_PartyTotalHPMax/2);run.Party.m_PartyTotalSP=Math.Max(sk.use_sp,run.Party.m_PartyTotalSPMax/2);
            if(sk.skill_cate==24)run.State.Hand[0]=sk.value_list[0];
            string denied=run.SkillUnavailable(0);
            if(denied=="沒有符合屬性的敵人"){
                Check(sk.skill_cate==5 || sk.skill_cate==10,"element-only skill rejects mismatched enemy "+sk.fix_id);
                int element=sk.value_list[sk.skill_cate==5?0:1];
                run.Enemies[0].m_EnemyMasterParamChara=core.Server.m_MasterDataParamChara.First(x=>x.element==element);
                denied=run.SkillUnavailable(0);Check(denied=="","matching enemy permits skill "+sk.fix_id);
            }
            if(denied!=""){blocked++;findings.Add(new{id=sk.fix_id,category=sk.skill_cate,status="context-blocked",reason=denied});run.Retire();run.Dismiss();continue;}
            int turns=run.State.Turns,sp=run.Party.m_PartyTotalSP;
            if(sk.use_sp>0){run.Party.m_PartyTotalSP=sk.use_sp-1;Check(run.SkillUnavailable(0)=="SP 不足","insufficient SP rejected "+sk.fix_id);run.Party.m_PartyTotalSP=sp;}
            var activity=new[]{new BattleSkillActivity()};InGameUtil.ActivityLimitBreakSkill(activity,0,-1,sk.fix_id);
            int expectedHeal=activity[0].m_Type==5?Math.Min(run.Party.m_PartyTotalHPMax-run.Party.m_PartyTotalHP,(int)InGameUtil.GetHealValue(run.Party,false,activity[0])):-1;
            int expectedTargets=activity[0].m_Type==2 || activity[0].m_Type==4?run.Enemies.Count(e=>!e.isDead && (sk.skill_cate!=5 && sk.skill_cate!=10 || e.m_EnemyMasterParamChara.element==sk.value_list[sk.skill_cate==5?0:1])):-1;
            run.UseActive(0);
            if(expectedHeal>=0)Check(run.Events.First(e=>e.Kind=="heal").Amount==expectedHeal,"reference recovery formula and HP cap "+sk.fix_id);
            if(expectedTargets>=0 && activity[0].m_skill_damage_enable==2)Check(run.Events.Count(e=>e.Kind=="damage" && e.Name==sk.name)==expectedTargets,"all-enemy and element target count "+sk.fix_id);
            Check(c.m_CharaLimitBreak==0,"active resets only its cooldown "+sk.fix_id);
            Check(run.State.Turns==turns && !run.State.RoundActive,"active does not spend a panel turn "+sk.fix_id);
            Check(run.Events.Any(e=>e.Kind=="active"),"active emits presentation event "+sk.fix_id);
            if(sk.skill_cate!=30)Check(run.Party.m_PartyTotalSP==sp-sk.use_sp,"original SP cost "+sk.fix_id);
            if(sk.skill_cate==24)Check(!run.State.Hand.Contains(sk.value_list[0]) || sk.value_list[0]==sk.value_list[1],"element conversion "+sk.fix_id);
            if(sk.skill_cate==46)Check(run.State.Key,"key skill "+sk.fix_id);
            if(sk.use_turn>c.m_CharaLBSLv){int hp=run.Party.m_PartyTotalHP;sp=run.Party.m_PartyTotalSP;bool rejected=false;try{run.UseActive(0);}catch(InvalidOperationException){rejected=true;}Check(rejected && hp==run.Party.m_PartyTotalHP && sp==run.Party.m_PartyTotalSP,"repeat blocked without side effects "+sk.fix_id);}
            run.Save();int savedHp=run.Party.m_PartyTotalHP;var restored=new QuestRun(core);Check(restored.Restore() && restored.Party.m_PartyTotalHP==savedHp && restored.Team[0].m_CharaLimitBreak==0,"active result/cooldown survives restore "+sk.fix_id);
            activated++;findings.Add(new{id=sk.fix_id,category=sk.skill_cate,status="activated-and-restored"});restored.Retire();restored.Dismiss();
        }
        // Save/reload must continue all three original RNG streams, not reseed.
        var party=new PacketStructUnit[5];for(int i=0;i<5;i++)party[i]=core.Player.unit_list[0];
        var replay=new QuestRun(core);replay.Start(1,party);replay.Move(17);replay.Move(12);
        var beforePass=replay.Team.Select(c=>c.m_CharaLimitBreak).ToArray();replay.Resolve();Check(beforePass.SequenceEqual(replay.Team.Select(c=>c.m_CharaLimitBreak)),"PASS without a valid hand does not charge active skills");replay.Save();
        string snapshot=PortableEngine.GetString("godot.quest.v2");
        QuestTests.Fill(replay);string outcome=JsonSerializer.Serialize(replay.State,new JsonSerializerOptions{IncludeFields=true});
        PortableEngine.SetString("godot.quest.v2",snapshot);
        var replay2=new QuestRun(core);Check(replay2.Restore(),"reload before random combat");QuestTests.Fill(replay2);
        Check(outcome==JsonSerializer.Serialize(replay2.State,new JsonSerializerOptions{IncludeFields=true}),"same hand strategy after restore produces identical damage, panels, statuses and RNG state");replay2.Retire();replay2.Dismiss();
        File.WriteAllText("/tmp/dg-godot290/skill-coverage.json",JsonSerializer.Serialize(new{uniqueSkills=chars.Length,activated,blocked,findings},new JsonSerializerOptions{WriteIndented=true}));
        Check(activated==chars.Length && blocked==0,"reference active skill coverage "+activated+" activated / "+blocked+" context blocked");
    }
}
