using System;
using System.Linq;
using System.Collections.Generic;
using DivineGateGodot;
using ServerDataDefine;

public static class QuestTests
{
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);Console.WriteLine("PASS "+message);}
    public static void Fill(QuestRun run)
    {
        // A deterministic player strategy: only use the actual five visible
        // hand panels. Never edit HP, enemy stats, drops, or grant a win.
        for(int n=0;n<25;n++){
            double best=double.NegativeInfinity;int hand=-1,field=-1;
            for(int h=0;h<5;h++)for(int f=0;f<5;f++)if(run.State.Fields[f].Count<5){
                var before=run.Skills();run.State.Fields[f].Add(run.State.Hand[h]);var after=run.Skills();
                double score=after.Count-before.Count;
                if(run.State.Hand[h]==7 && run.Party.m_PartyTotalHP<run.Party.m_PartyTotalHPMax*.65)score+=score>0?8:1;
                score+=run.State.Fields[f].Count(e=>e==run.State.Hand[h])*.09;
                if(run.State.Hand[h]==2)score+=.18;
                run.State.Fields[f].RemoveAt(run.State.Fields[f].Count-1);
                if(score>best){best=score;hand=h;field=f;}
            }
            if(hand<0)break;run.Place(hand,field);
        }
        run.Resolve();
    }
    public static int NextStep(int from,int to)
    {
        if(from%5<to%5)return from+1;if(from%5>to%5)return from-1;
        return from+(from<to?5:-5);
    }
    public static void Complete(QuestRun run)
    {
        for(int step=0;step<1000 && !run.Finished;step++){
            if(run.State.Phase=="defeat")throw new Exception("Test player defeated: "+run.Quest.fix_id+" "+run.State.Message);
            if(run.InBattle){Fill(run);continue;}
            int target=run.State.Key?2:Array.FindIndex(run.Panels,p=>p.type==5);
            if(target<0)throw new Exception("Missing key");
            if(run.State.Position!=target)run.Move(NextStep(run.State.Position,target));else run.Door();
        }
        Check(run.State.Phase=="cleared","real route, key, normal battles, boss and quest clear "+run.Quest.fix_id);
    }
    static GameCore Boot(string dir){var c=new GameCore();c.Boot(dir,Environment.GetEnvironmentVariable("DG_TEST_TABLES") ?? throw new InvalidOperationException("Set DG_TEST_TABLES to the verified r17 tables directory"));return c;}
    public static void Main()
    {
        string dir="/tmp/dg-godot290/quest-test-"+Guid.NewGuid().ToString("N");
        var core=Boot(dir);core.SelectStarter(1);
        var team=new PacketStructUnit[5];for(int i=0;i<4;i++)team[i]=core.Player.unit_list[i];team[4]=core.Player.unit_list[0];
        var run=new QuestRun(core);run.Start(1,team);
        Check(run.State.Floor==1,"reserved floor zero is skipped");
        int pos=run.State.Position;run.Move(0);Check(run.State.Position==pos,"nonadjacent move is rejected");
        run.Door();Check(!run.InBattle,"door cannot be used remotely or without key");
        run.Move(17);int hp=run.Party.m_PartyTotalHP,sp=run.Party.m_PartyTotalSP;uint stamina=core.Player.stamina_now;
        core=Boot(dir);run=new QuestRun(core);Check(run.Restore(),"reload pending quest");
        Check(run.State.Position==17 && run.Party.m_PartyTotalHP==hp && run.Party.m_PartyTotalSP==sp,"position, HP and SP persist");
        Check(core.Player.stamina_now==stamina,"resume does not charge stamina");
        // Exercise a genuine battle interrupted in the middle of panel input.
        while(!run.InBattle){
            int target=run.State.Key?2:Array.FindIndex(run.Panels,p=>p.type==5);
            if(run.State.Position!=target)run.Move(NextStep(run.State.Position,target));else run.Door();
        }
        run.Place(0,0);run.Tick(.75);run.Save();
        var hand=(int[])run.State.Hand.Clone();var enemyHp=run.Enemies.Select(e=>e.m_EnemyHP).ToArray();
        double seconds=run.State.Seconds;int placed=run.State.Fields[0][0];
        core=Boot(dir);run=new QuestRun(core);Check(run.Restore(),"reload active battle");
        Check(run.State.Hand.SequenceEqual(hand) && run.State.Fields[0].SequenceEqual(new[]{placed}),"actual hand and placed panels persist");
        Check(run.State.RoundActive && run.State.Seconds==seconds && run.Enemies.Select(e=>e.m_EnemyHP).SequenceEqual(enemyHp),"battle countdown and enemy HP persist");
        int turns=run.State.Turns;run.Tick(seconds+.1);
        Check(run.State.Turns==turns+1 && !run.State.RoundActive,"countdown resolves exactly one battle round");
        uint money=core.Player.have_money;int units=core.Player.unit_list.Length;
        Complete(run);
        Check(core.Player.quest_id==0,"server quest order closed after victory");
        Check(core.Player.have_money==money+run.State.Money+run.Quest.clear_money,"only earned money awarded");
        Check(core.Player.unit_list.Length>=units+run.State.Drops.Count,"actual battle drops enter warehouse");
        money=core.Player.have_money;uint exp=core.Player.player_exp;
        core=Boot(dir);run=new QuestRun(core);Check(run.Restore() && run.Finished,"result screen persists across restart");
        run.Door();Check(core.Player.have_money==money && core.Player.player_exp==exp,"completed quest cannot award twice");run.Dismiss();
        var regular=core.Server.m_MasterDataQuest.Where(q=>q.area_id==100100 && q.active==2).OrderBy(q=>q.fix_id).First();
        Console.WriteLine("REGULAR "+regular.fix_id+" "+regular.quest_name);
        run=new QuestRun(core);stamina=core.Player.stamina_now;run.Start(regular.fix_id,team);
        Check(core.Player.stamina_now==stamina-regular.quest_stamina,"normal quest charges original stamina cost exactly once");
        Complete(run);run.Dismiss();
        run=new QuestRun(core);run.Start(1,team);run.Retire();Check(core.Player.quest_id==0 && run.Finished,"retirement closes order without rewards");run.Dismiss();
        run=new QuestRun(core);run.Start(regular.fix_id,team);
        while(!run.InBattle){int target=run.State.Key?2:Array.FindIndex(run.Panels,p=>p.type==5);if(run.State.Position!=target)run.Move(NextStep(run.State.Position,target));else run.Door();}
        money=core.Player.have_money;
        for(int n=0;n<10000 && run.InBattle;n++)run.Resolve();
        Check(run.State.Phase=="defeat","passing without attacks eventually reaches real defeat");
        run.Retire();run.Dismiss();Check(core.Player.have_money==money && core.Player.quest_id==0,"defeat retirement awards nothing and permits another quest");
        Console.WriteLine("PASS quest integration completed");
    }
}
