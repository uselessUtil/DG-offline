using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using ServerDataDefine;

namespace DivineGateGodot;

// Godot owns input/rendering. The r17 assembly remains the authority for data,
// character growth, leader modifiers, affinity, hands multipliers and rewards.
public sealed partial class QuestRun
{
    const string SaveKey="godot.quest.v2";
    static readonly JsonSerializerOptions Json=new(){IncludeFields=true};
    readonly GameCore core;
    public RunState State {get;private set;}=new();
    public CharaOnce[] Team {get;private set;}=new CharaOnce[5];
    public CharaParty Party {get;private set;}=new();
    public BattleEnemy[] Enemies {get;private set;}=Array.Empty<BattleEnemy>();
    InGameManager game=null!;
    BattleManager battle=null!;
    StatusAilmentsManager ailments=null!;
    EnemyActionTableControl[] actions=Array.Empty<EnemyActionTableControl>();
    public MasterDataQuest Quest=>core.Server.m_MasterDataQuest.First(q=>q.fix_id==State.QuestId);
    public MasterDataArea Area=>core.Server.m_MasterDataArea.First(a=>a.fix_id==Quest.area_id);
    public PacketStructQuestBuildPanelDetail[] Panels=>State.Build.panel[State.Floor].panel_detail;
    public bool InBattle=>State.Phase=="battle";
    public bool Finished=>State.Phase=="cleared" || State.Phase=="retired";
    public QuestRun(GameCore owner){core=owner;}
    public bool Restore()
    {
        string saved=PortableEngine.GetString(SaveKey);
        if(string.IsNullOrEmpty(saved))return false;
        State=JsonSerializer.Deserialize<RunState>(saved,Json)!;
        if(State.Phase=="retired")return false;
        if(core.Player.quest_id!=State.QuestId && State.Phase!="cleared" && State.Phase!="clear-ready")return false;
        Initialize();
        Party.m_PartyTotalHP=State.Hp;Party.m_PartyTotalSP=State.Sp;
        if(InBattle)LoadEnemies(false);
        RestoreRandom();SyncBattle();
        if(State.Phase=="clear-ready")CommitClear();
        return true;
    }
    public void Start(uint questId,PacketStructUnit[] team)
    {
        if(core.Player.quest_id!=0)throw new InvalidOperationException("已有進行中的關卡，請先繼續或撤退。");
        if(team.Length!=5 || team[0]==null)throw new InvalidOperationException("請先選擇隊長。");
        var q=core.Server.m_MasterDataQuest.First(x=>x.fix_id==questId);
        if(core.Player.stamina_now<q.quest_stamina)throw new InvalidOperationException("體力不足，請先回復體力。");
        var result=core.Api<RecvQuestStart>(SERVER_API.SERVER_API_QUEST_START,new SendQuestStart{quest_id=questId,party_current=core.Player.unit_party_current,helper_unit=team[4]});
        State=new RunState{QuestId=questId,Build=result.result.quest,Units=team,Floor=1,Phase="map",Rng=(uint)DateTime.UtcNow.Ticks|1};
        Initialize();NewFloor();Save();
    }
    void Initialize()
    {
        game=new InGameManager();Singleton<InGameManager>.Instance=game;
        battle=new BattleManager();Singleton<BattleManager>.Instance=battle;
        Singleton<SceneGoesParam>.Instance=new SceneGoesParam{
            m_SceneGoesParamToGameMain=new SceneGoesParamToGameMain{m_QuestAreaID=Quest.area_id,m_QuestMissionID=Quest.fix_id},
            m_SceneGoesParamToGameMainBuild=new SceneGoesParamToGameMainBuild{m_QuestBuild=State.Build}};
        Team=new CharaOnce[5];
        for(int i=0;i<5;i++)if(State.Units[i]!=null){var u=State.Units[i];Team[i]=new CharaOnce();Team[i].CharaSetupFromID(u.id,(int)u.level,(int)u.limitbreak_lv,(int)u.add_pow,(int)u.add_hp);Team[i].m_CharaLimitBreak=State.Cooldowns[i];}
        Party=new CharaParty();game.m_PlayerPartyChara=Team;game.m_PlayerParty=Party;
        game.m_QuestAreaID=Quest.area_id;game.m_QuestMissionID=Quest.fix_id;game.m_QuestFloor=State.Floor;
        Party.PartySetup(Team);
        ailments=new StatusAilmentsManager();Singleton<StatusAilmentsManager>.Instance=ailments;
        ailments.m_StatusAilmentChara=State.Ailments??Enumerable.Range(0,13).Select(_=>new StatusAilmentChara{cAilment=Enumerable.Range(0,10).Select(_=>new StatusAilment()).ToArray()}).ToArray();
        game.m_PlayerPartyCharaAilment=0;ailments.m_StatusAilmentChara[0].bUsed=true;
        battle.CreateSkillList();
        if(Party.m_PartyTotalHPMax<=0)throw new InvalidOperationException("隊伍 HP 為 0。");
    }
    public void Save()
    {
        State.Hp=Party.m_PartyTotalHP;State.Sp=Party.m_PartyTotalSP;
        State.EnemyHp=Enemies.Select(e=>e.m_EnemyHP).ToArray();State.EnemyTurns=Enemies.Select(e=>e.m_EnemyTurn).ToArray();
        for(int i=0;i<5;i++)State.Cooldowns[i]=Team[i]?.m_CharaLimitBreak??0;
        State.Ailments=ailments.m_StatusAilmentChara;
        SaveRandom();
        State.EnemyAttacked=Enemies.Select(e=>e.attackFlag).ToArray();
        State.ActionTables=actions.Select(a=>a.m_CurrentActionTableID).ToArray();State.ActionSteps=actions.Select(a=>a.m_ActionStep).ToArray();
        PortableEngine.SetString(SaveKey,JsonSerializer.Serialize(State,Json));PortableEngine.Save();
    }
    uint Random(uint max){uint n=State.Rng;n^=n<<13;n^=n>>17;n^=n<<5;State.Rng=n;return max==0?0:n%max;}
    void NewFloor()
    {
        State.Position=22;State.Open=new bool[25];State.Open[22]=true;State.Key=false;State.Boss=false;State.BossDefeated=false;
        State.Phase="map";State.Message="探索相鄰格子，取得鑰匙後到上方的門。";Enemies=Array.Empty<BattleEnemy>();
        game.m_QuestFloor=State.Floor;actions=Array.Empty<EnemyActionTableControl>();SyncBattle();
    }
    public bool CanMove(int cell)=>State.Phase=="map" && cell>=0 && cell<25 && Math.Abs(cell%5-State.Position%5)+Math.Abs(cell/5-State.Position/5)==1;
    public void Move(int cell)
    {
        if(!CanMove(cell))return;
        if(Party.m_PartyTotalSP>0)Party.DamageSP(1);
        else Party.DamageHP((int)(Party.m_PartyTotalHPMax*.2f),true,false);
        Events.Clear();State.Position=cell;State.Message="";
        if(!StatusAilmentUtil.ChkStatusAilmentCondition(0,1)){int heal=StatusAilmentUtil.GetHealValue(0);if(heal>0)Heal(heal,"REGEN");}
        int poison=(int)StatusAilmentUtil.GetPoisonDamage(0);if(poison>0){Party.DamageHP(poison,true,false);Emit("player-hit","POISON",-1,-1,poison);}
        ailments.UpdateTurn(EAILMENT_UPDATE_TIMING.eFIELD);game.m_PanelDarkEnable=Dark;
        SyncFieldCards();int healOwner=5;
        int moveHeal=(int)InGameUtil.GetHealValue(Party,InGameUtil.GetLeaderSkillHealMove(0),false)+(int)InGameUtil.GetHealValue(Party,InGameUtil.GetLeaderSkillHealMove(4),false)+(int)InGameUtil.GetHealValue(Party,InGameUtil.PassiveChkHealHPMove(ref healOwner),false);
        if(moveHeal>0)Heal(moveHeal,"REGEN");
        if(!State.Open[cell]){
            State.Open[cell]=true;var p=Panels[cell];State.Visited.Add(p.unique_id);
            switch(p.type){
                case 5:State.Key=true;State.Message="KEY GET！前往上方的門。";break;
                case 3:BeginBattle(p.type_unique,false);break;
                case 4:var t=State.Build.list_trap.FirstOrDefault(x=>x.unique_id==p.type_unique);if(t!=null)PanelEffect(t.trap_id,0,true);break;
                case 2:case 6:var item=State.Build.list_item.FirstOrDefault(x=>x.unique_id==p.type_unique);if(item!=null)PanelEffect(item.item_id,item.param_0,false);break;
            }
        }
        if(Party.m_PartyTotalHP<=0)State.Phase="defeat";
        Save();
    }
    void PanelEffect(uint id,int amount,bool trap)
    {
        var p=MasterDataUtil.GetPanelParamFromID(id);if(p==null)return;int value=trap?p.effective_value:amount;
        State.Message=p.name;
        if(trap && Team.Where(c=>c!=null).Any(c=>InGameUtil.PassiveChkPassTrap(c,p.trap_type))){State.Message+="：迴避";return;}
        switch(p.effective_type){
            case 2:Party.DamageHP(value,false,false);break;
            case 3:Party.DamageHP(value,false,false);break; // Random displacement is a separate field animation milestone.
            case 8:Party.RecoveryHP(value,false);break;
            case 9:Party.RecoveryHP((int)InGameUtil.AvoidErrorMultiple(Party.m_PartyTotalHPMax,InGameUtil.GetDBRevisionValue(value)),false);break;
            case 10:Party.m_PartyTotalSP=Math.Min(Party.m_PartyTotalSPMax,Party.m_PartyTotalSP+value);break;
            case 11:Party.DamageSP(value);break;
            case 20:var dark=new StatusAilment();dark.SetupData(1,StatusAilmentUtil.ConvertCategoryToAilment(34,null),2,1,value,0,0,null);StatusAilmentUtil.AddStatusAilment(0,dark);game.m_PanelDarkEnable=true;break;
            case 21:var poison=new StatusAilment();var param=new int[12];param[0]=value;poison.SetupData(1,StatusAilmentUtil.ConvertCategoryToAilment(13,null),2,2,5,Party.m_PartyTotalHPMax,Party.m_PartyTotalHPMax,param);StatusAilmentUtil.AddStatusAilment(0,poison);break;
            case 23:StatusAilmentUtil.AddStatusAilment(0,value,Party.m_PartyTotalHPMax,Party.m_PartyTotalHPMax);break;
            case 13:case 14:State.Money+=(uint)Math.Max(0,amount);break;
            case 15:State.Money=(uint)(State.Money*Math.Max(0,100-value)/100);break;
        }
    }
    public void Door()
    {
        if(State.Phase!="map" || State.Position!=2)return;
        if(!State.Key){State.Message="還沒有取得鑰匙。";Save();return;}
        int boss=State.Build.boss[State.Floor];
        if(boss>0 && !State.BossDefeated){BeginBattle(boss,true);Save();return;}
        if(State.Floor+1<State.Build.panel.Length){State.Floor++;NewFloor();Save();}
        else{State.Phase="clear-ready";Save();CommitClear();}
    }
    void BeginBattle(int id,bool boss)
    {
        State.BattleId=id;State.Boss=boss;State.Phase="battle";State.Target=-1;
        State.Fields=Enumerable.Range(0,5).Select(_=>new List<int>()).ToArray();State.Hand=new int[5];
        State.RoundActive=false;State.Seconds=5;State.Boost=(int)Random(5);
        LoadEnemies(true);for(int i=0;i<5;i++)State.Hand[i]=DrawPanel();
        State.Message=boss?"BOSS BATTLE":"BATTLE";Emit("battle",State.Message);
        for(int i=0;i<Enemies.Length && Party.m_PartyTotalHP>0;i++){var first=actions[i].GetActionFirstAttack();if(first!=null)EnemyAct(i,first,false);}
        if(Party.m_PartyTotalHP<=0){State.Phase="defeat";Emit("defeat","GAME OVER");}
        else if(Enemies.All(e=>e.isDead))WinBattle();else PlayerTurnStart();
    }
    void LoadEnemies(bool fresh)
    {
        var group=State.Build.list_battle.First(x=>x.unique_id==State.BattleId);
        Enemies=group.enemy_list.Where(x=>x!=0).Select((id,i)=>{
            var e=MasterDataUtil.GetEnemyParamFromID(id);
            if(e==null)throw new InvalidOperationException("找不到敵人資料 "+id);
            if(fresh)ailments.ClearChara(i+1);ailments.m_StatusAilmentChara[i+1].bUsed=true;
            return new BattleEnemy{m_EnemyMasterParamEnemy=e,m_EnemyMasterParamChara=MasterDataUtil.GetCharaParamFromID(e.chara_id),m_EnemyHP=fresh?e.status_hp:State.EnemyHp[i],m_EnemyHPMax=e.status_hp,m_EnemyAttack=e.status_pow,m_EnemyDefense=e.status_def,m_EnemyTurn=fresh?Math.Max(1,e.status_turn):State.EnemyTurns[i],m_EnemyTurnMax=Math.Max(1,e.status_turn),m_EnemyAilmentID=i+1,attackFlag=!fresh && i<State.EnemyAttacked.Length && State.EnemyAttacked[i]};
        }).ToArray();
        battle.m_EnemyParam=Enemies;battle.m_EnemyTotalNum=Enemies.Length;
        actions=Enumerable.Range(0,Enemies.Length).Select(i=>{var a=new EnemyActionTableControl();a.Setup(i);if(!fresh && i<State.ActionTables.Length){a.m_CurrentActionTableID=State.ActionTables[i];a.m_ActionStep=State.ActionSteps[i];}return a;}).ToArray();
        if(Enemies.Length==0)throw new InvalidOperationException("戰鬥沒有敵人。");SyncBattle();
    }
    int DrawPanel()
    {
        int[] weights={0,Area.cost0,Area.cost1,Area.cost2,Area.cost4,Area.cost5,Area.cost3,Area.cost6};
        if(!StatusAilmentUtil.GetHandCardDefault(0))for(int i=1;i<8;i++)weights[i]=Math.Max(0,(int)(weights[i]*InGameUtil.PassiveChkHandCardChanceUPElement(i)));
        int total=weights.Sum();if(total<=0)throw new InvalidOperationException("區域面板機率為 0。");
        int r=(int)Random((uint)total);for(int i=1;i<8;i++){r-=weights[i];if(r<0)return TransformPanel(i);}return TransformPanel(7);
    }
    int TransformPanel(int e)
    {
        e=InGameUtil.GetLeaderSkillTransformCard(0,e);e=InGameUtil.GetLeaderSkillTransformCard(4,e);
        int owner=5;e=InGameUtil.PassiveChkChangeHandElem(e,ref owner);int fix=StatusAilmentUtil.GetPanelElemFix(0);return fix!=0?fix:e;
    }
    public void Place(int hand,int field)
    {
        if(!InBattle || hand<0 || hand>=5 || field<0 || field>=5 || State.Fields[field].Count>=5)return;
        if(!State.RoundActive){State.RoundActive=true;State.Seconds=Math.Max(1,5+InGameUtil.GetLeaderSkillCountDown(0)+InGameUtil.GetLeaderSkillCountDown(4)+InGameUtil.PassiveChkCountDown()+StatusAilmentUtil.GetCountDown(0));}
        State.Fields[field].Add(State.Hand[hand]);State.Hand[hand]=DrawPanel();Save();
    }
    public void Tick(double delta){if(InBattle && State.RoundActive){State.Seconds=Math.Max(0,State.Seconds-delta);if(State.Seconds<=0)Resolve();}}
    public List<(MasterDataSkillActive Skill,int Owner,int Field)> Skills()
    {
        var result=new List<(MasterDataSkillActive,int,int)>();
        for(int field=0;field<5;field++){
            int[] counts=new int[8];foreach(int e in State.Fields[field])counts[e]++;
            var skills=core.Server.m_MasterDataSkillActive.Where(s=>s.always==2).Select(s=>(s,-1)).ToList();
            for(int c=0;c<5;c++)if(Team[c]?.m_CharaMasterDataParam!=null){var ch=Team[c].m_CharaMasterDataParam;foreach(uint id in new[]{ch.skill_active0,ch.skill_active1}){var sk=MasterDataUtil.GetActiveSkillParamFromID(id);if(sk!=null)skills.Add((sk,c));}}
            foreach(var owner in skills.GroupBy(x=>x.Item2)){
                int[] left=(int[])counts.Clone();
                foreach(var pair in owner.OrderByDescending(x=>new[]{x.Item1.cost1,x.Item1.cost2,x.Item1.cost3,x.Item1.cost4,x.Item1.cost5}.Count(e=>e>0))){var sk=pair.Item1;int[] need=new int[8];foreach(int e in new[]{sk.cost1,sk.cost2,sk.cost3,sk.cost4,sk.cost5})if(e>0 && e<8)need[e]++;
                    while(need.Sum()>0 && Enumerable.Range(1,7).All(e=>left[e]>=need[e])){result.Add((sk,pair.Item2,field));for(int e=1;e<8;e++)left[e]-=need[e];}}
            }
        }
        return result;
    }
    public void Resolve()
    {
        if(!InBattle)return;
        Events.Clear();State.RoundActive=false;State.Seconds=5;SyncBattle();
        var skills=Skills();int hands=skills.Count;State.MaxCombo=Math.Max(State.MaxCombo,(uint)hands);
        battle.m_AttackFixReqInputNum=hands;battle.m_ActiveSkillElement=new bool[8];
        foreach(var s in skills)battle.m_ActiveSkillElement[s.Skill.skill_element]=true;
        Emit("hands",$"{hands} HANDS",-1,-1,hands);
        int totalDamage=0;var normal=new List<BattleSkillActivity>();
        foreach(var entry in skills){
            var sk=entry.Skill;bool boost=entry.Field==State.Boost;
            var a=new BattleSkillActivity{m_SkillParamOwnerNum=entry.Owner,m_SkillParamFieldID=entry.Field,m_SkillParamSkillID=sk.fix_id,m_SkillType=ESKILLTYPE.eACTIVE,m_Element=sk.skill_element,m_Type=sk.skill_type,m_Effect=sk.effect,m_skill_power=sk.skill_active==2?sk.skill_value:0,m_skill_chk_atk_combo=2,m_bBonusBoost=boost,m_nAttackReqNum=hands,m_bCritical=Random(100)<sk.skill_critical_odds};
            a.m_Param[0]=sk.skill_value;a.m_Param[1]=sk.skill_value_rand;
            if(sk.skill_type==5)a.m_skill_damage_enable=1;
            if(entry.Owner>=0 || sk.skill_type==5)totalDamage+=ApplySkill(a,sk.name,boost,hands);
            normal.Add(a);
        }
        ApplyBoost(normal);if(hands>0)FollowAttacks();
        State.Message=$"{hands} HANDS / {totalDamage} DAMAGE";
        foreach(var e in Enemies.Where(e=>!e.isDead)){
            int i=Array.IndexOf(Enemies,e);
            if(!StatusAilmentUtil.ChkStatusAilmentCondition(e.m_EnemyAilmentID,1))e.AddHP(StatusAilmentUtil.GetHealValue(e.m_EnemyAilmentID));
            int poison=(int)StatusAilmentUtil.GetPoisonDamage(e.m_EnemyAilmentID);if(poison>0)Hurt(i,poison,0,"POISON",-1);
        }
        SyncBattle();
        foreach(var e in Enemies.Where(e=>!e.isDead)){
            e.DelTurn(1);if(e.m_EnemyTurn>0)continue;
            int idx=Array.IndexOf(Enemies,e);actions[idx].SelectActionTable();var action=actions[idx].GetCurrentActionParam();
            EnemyAct(idx,action,true);e.m_EnemyTurn=e.m_EnemyTurnMax;
            if(Party.m_PartyTotalHP<=0)break;
        }
        for(int i=0;i<5;i++)State.Hand[i]=battle.m_BattleHandCardParam[i].m_CardElement;
        if(hands>0 && Party.m_PartyTotalHP>0)foreach(var c in Team)if(c!=null)c.AddCharaLimitBreak(1);
        if(Enemies.All(e=>e.attackFlag))foreach(var e in Enemies)e.ClearOnAttack();
        State.Turns++;
        int ownerHeal=5;int regen=(int)InGameUtil.GetHealValue(Party,InGameUtil.GetLeaderSkillHealBattle(0),false)+(int)InGameUtil.GetHealValue(Party,InGameUtil.GetLeaderSkillHealBattle(4),false)+(int)InGameUtil.GetHealValue(Party,InGameUtil.PassiveChkHealHPBattle(ref ownerHeal),false);
        if(regen>0 && Party.m_PartyTotalHP>0)Heal(regen,"REGEN");
        foreach(var e in Enemies)ailments.UpdateTurnOnce(EAILMENT_UPDATE_TIMING.eBATTLE,e.m_EnemyAilmentID);
        State.Fields=Enumerable.Range(0,5).Select(_=>new List<int>()).ToArray();State.Boost=(int)Random(5);
        if(Party.m_PartyTotalHP<=0){State.Phase="defeat";Emit("defeat","GAME OVER");}
        else if(Enemies.All(e=>e.isDead))WinBattle();
        else PlayerTurnStart();
        Save();
    }
    void WinBattle()
    {
        var group=State.Build.list_battle.First(x=>x.unique_id==State.BattleId);
        foreach(var e in Enemies){State.Money+=(uint)Math.Max(0,e.m_EnemyMasterParamEnemy.acquire_money);State.Exp+=(uint)Math.Max(0,e.m_EnemyMasterParamEnemy.acquire_exp);}
        foreach(int id in group.drop_list??Array.Empty<int>()){
            var drop=State.Build.list_drop?.FirstOrDefault(d=>d.unique_id==id);if(drop==null || drop.unit_id==0)continue;
            var enemy=Enemies.FirstOrDefault(e=>e.m_EnemyMasterParamEnemy.drop_unit_id==drop.unit_id);
            State.Drops.Add(new PacketStructUnitGet{id=drop.unit_id,level=(uint)Math.Max(1,enemy?.m_EnemyMasterParamEnemy.drop_unit_level??1),add_pow=(uint)Math.Max(0,drop.plus_pow),add_hp=(uint)Math.Max(0,drop.plus_hp)});
        }
        if(group.chain>0 && State.Build.list_battle.Any(x=>x.unique_id==group.chain)){BeginBattle(group.chain,State.Boss);return;}
        Emit("win","BATTLE WIN");
        if(State.Boss)State.BossDefeated=true;
        State.Phase="map";Enemies=Array.Empty<BattleEnemy>();actions=Array.Empty<EnemyActionTableControl>();SyncBattle();State.Message="BATTLE WIN"+(State.Boss?"！點門前往下一層／完成關卡。":"");
    }
    void CommitClear()
    {
        if(State.Phase!="clear-ready")return;
        if(core.Player.quest_id==State.QuestId){
            core.Api<RecvQuestClear>(SERVER_API.SERVER_API_QUEST_CLEAR,new SendQuestClear{quest_id=State.QuestId,get_money=State.Money+(uint)Math.Max(0,Quest.clear_money),get_exp=State.Exp+(uint)Math.Max(0,Quest.clear_exp),get_unit=State.Drops.ToArray(),panel_unique=State.Visited.ToArray(),achievement_combo=State.MaxCombo,achievement_damage=State.MaxDamage});
        }else if(core.Player.quest_id!=0)throw new InvalidOperationException("關卡結算與存檔不一致。");
        State.Phase="cleared";State.Message="QUEST CLEAR";Save();
    }
    public void Retire(){if(core.Player.quest_id==State.QuestId)core.Api<RecvQuestRetire>(SERVER_API.SERVER_API_QUEST_RETIRE,new SendQuestRetire{quest_id=State.QuestId});State.Phase="retired";Save();}
    void AddHealEnemyHP(int index,int amount){if(index>=0 && index<Enemies.Length && !Enemies[index].isDead)Enemies[index].AddHP(amount);}
    public void Dismiss(){if(!Finished)throw new InvalidOperationException("關卡尚未結束。");PortableEngine.DeleteKey(SaveKey);PortableEngine.Save();}
}

public sealed class RunState
{
    public uint QuestId,Rng=1,Money,Exp,MaxCombo,MaxDamage;
    public int Floor,Position=22,Hp,Sp,BattleId,Target=-1,Boost,Turns;
    public bool Key,Boss,BossDefeated,RoundActive;
    public string Phase="map",Message="";
    public double Seconds=5;
    public PacketStructQuestBuild Build=null!;
    public PacketStructUnit[] Units=new PacketStructUnit[5];
    public bool[] Open=new bool[25],EnemyAttacked=Array.Empty<bool>();
    public int[] Hand=new int[5],EnemyHp=Array.Empty<int>(),EnemyTurns=Array.Empty<int>(),Cooldowns=new int[5];
    public int[] ActionTables=Array.Empty<int>(),ActionSteps=Array.Empty<int>();
    public StatusAilmentChara[]? Ailments;
    public uint[] RuleRandom=Array.Empty<uint>(),EnemyPanelRandom=Array.Empty<uint>();
    public uint[][] ActionRandom=Array.Empty<uint[]>();
    public List<int>[] Fields=Enumerable.Range(0,5).Select(_=>new List<int>()).ToArray();
    public List<int> Visited=new();
    public List<PacketStructUnitGet> Drops=new();
}
