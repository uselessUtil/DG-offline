using System;
using System.Linq;
using System.Reflection;
namespace DivineGateGodot;
public sealed partial class QuestRun
{
    void EnemyAct(int idx,MasterDataEnemyActionParam action,bool advance)
    {
        var e=Enemies[idx];SyncBattle();int before=Party.m_PartyTotalHP;
        int hit=EnemyAttackAllDamage(idx,action);
        int survivor;uint passive;
        if(InGameUtil.GetLeaderSkillFunbari(0,Party.m_PartyTotalHP,Party.m_PartyTotalHPMax,before) || InGameUtil.GetLeaderSkillFunbari(4,Party.m_PartyTotalHP,Party.m_PartyTotalHPMax,before) || InGameUtil.PassiveChkFunbari(Party.m_PartyTotalHP,Party.m_PartyTotalHPMax,before,out survivor,out passive))Party.m_PartyTotalHP=1;
        if(advance)actions[idx].StepActionTable();
        Emit("enemy",action?.skill_name??e.name,-1,idx,before-Party.m_PartyTotalHP,e.m_EnemyMasterParamChara.element);
        e.attackFlag=true;StatusAilmentUtil.ActiveAttackFlag(e.m_EnemyAilmentID);StatusAilmentUtil.DelStatusAilment(e.m_EnemyAilmentID,7);
        for(int h=0;h<5;h++)State.Hand[h]=battle.m_BattleHandCardParam[h].m_CardElement;
        if(Party.m_PartyTotalHP>0)Counter(idx,hit);
        State.Message+=$"\n{action?.skill_name??e.name}：{hit}";
    }
    void EnemyReaction(uint skill)
    {
        SyncBattle();EnemyAbility.InitReaction();
        // Use the reference reaction predicate (including its single-enemy
        // early return); do not invent extra reactions absent from r17.
        if(!EnemyAbility.AbilitySpecificActionReaction(skill))return;
        var indices=EnemyAbility.enemyReactIdx;var list=EnemyAbility.enemyReactParam;
        for(int i=0;i<list.Length && Party.m_PartyTotalHP>0;i++)if(list[i]!=null && !Enemies[indices[i]].isDead)EnemyAct(indices[i],list[i],false);
        EnemyAbility.InitReaction();
    }
    static readonly FieldInfo GlobalRand=typeof(RandManager).GetField("m_Rand",BindingFlags.Static|BindingFlags.NonPublic)!;
    static readonly FieldInfo[] RandFields=new[]{"m_RandSeed","m_RandValueX","m_RandValueY","m_RandValueZ","m_RandValueW"}.Select(n=>typeof(Rand).GetField(n,BindingFlags.Instance|BindingFlags.NonPublic)!).ToArray();
    static uint[] ReadRand(Rand r)=>RandFields.Select(f=>(uint)f.GetValue(r)!).ToArray();
    static void WriteRand(Rand r,uint[] state){if(state.Length!=RandFields.Length)return;for(int i=0;i<state.Length;i++)RandFields[i].SetValue(r,state[i]);}
    void SaveRandom(){State.RuleRandom=ReadRand((Rand)GlobalRand.GetValue(null)!);State.EnemyPanelRandom=ReadRand(battle.m_EnemySkillHandCardRemake);State.ActionRandom=actions.Select(a=>ReadRand(a.m_RandomActionTable)).ToArray();}
    void RestoreRandom(){WriteRand((Rand)GlobalRand.GetValue(null)!,State.RuleRandom);WriteRand(battle.m_EnemySkillHandCardRemake,State.EnemyPanelRandom);for(int i=0;i<actions.Length && i<State.ActionRandom.Length;i++)WriteRand(actions[i].m_RandomActionTable,State.ActionRandom[i]);}
}
