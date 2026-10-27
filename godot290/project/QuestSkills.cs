using System;
using System.Collections.Generic;
using System.Linq;
namespace DivineGateGodot;

// Rule adapter for the reference r17 BattleUtil / InGameSkillWindow. Rendering
// consumes immutable events; it never applies damage, spends SP, or awards loot.
public sealed partial class QuestRun
{
    public readonly List<BattleEvent> Events=new();
    public MasterDataSkillLimitBreak? ActiveSkill(int owner)=>owner>=0 && owner<5 && Team[owner]!=null?MasterDataUtil.GetLimitBreakSkillParamFromID(Team[owner].m_CharaMasterDataParam.skill_limitbreak):null;
    public int SkillTurns(int owner){var sk=ActiveSkill(owner);return sk==null?0:Math.Max(0,sk.use_turn-Team[owner].m_CharaLBSLv-Team[owner].m_CharaLimitBreak);}
    public string SkillUnavailable(int owner)
    {
        var s=ActiveSkill(owner);if(s==null)return "此角色沒有主動技能";
        if(State.Phase!="map" && !InBattle)return "目前無法使用";
        if(State.RoundActive)return "放牌開始後無法使用";
        if(s.phase!=(InBattle?2:1) && s.phase!=3)return InBattle?"僅限探索時使用":"僅限戰鬥時使用";
        if(SkillTurns(owner)>0)return $"還需 {SkillTurns(owner)} 回合";
        if(Party.m_PartyTotalSP<s.use_sp)return "SP 不足";
        if(StatusAilmentUtil.GetSkillNotUse(0))return "技能被封印";
        if((s.status_ailment_target==1 || s.status_ailment_target==3 || s.status_ailment_target==5) && InGameUtil.ChkLimitBreakAilmentStatusPlayer(owner))return "已有相同狀態";
        if(InBattle && (s.status_ailment_target==2 || s.status_ailment_target==4 || s.status_ailment_target==5) && InGameUtil.ChkLimitbreakAilmentStatusEnemy(owner))return "敵人已有相同狀態";
        if(s.skill_cate==48 && !InGameUtil.ChkLimitbreakAilmentStatusCondition(owner))return "沒有可解除的狀態";
        if((s.skill_cate==6 || s.skill_cate==9) && Party.m_PartyTotalHP<=1)return "HP 不足";
        if((s.skill_cate==28 || s.skill_cate==29) && Party.m_PartyTotalHP>=Party.m_PartyTotalHPMax)return "HP 已滿";
        if(s.skill_cate==30 && Party.m_PartyTotalSP>=Party.m_PartyTotalSPMax)return "SP 已滿";
        if(s.skill_cate==24 && InBattle && !State.Hand.Contains(s.value_list[0]))return "手牌沒有可轉換的屬性";
        if((s.skill_cate==5 || s.skill_cate==10) && !InGameUtil.ChkSpecificElementEnemy(s.value_list[s.skill_cate==5?0:1],State.Target))return "沒有符合屬性的敵人";
        if(s.skill_cate==23)for(int e=1;e<8;e++){var v=new int[5];v[3]=e;if(StatusAilmentUtil.ChkStatusAilmentPlayer(StatusAilmentUtil.ConvertCategoryToAilment(s.skill_cate,v)))return "已有面板固定效果";}
        return "";
    }
    public void UseActive(int owner)
    {
        string reason=SkillUnavailable(owner);if(reason!="")throw new InvalidOperationException(reason);
        var sk=ActiveSkill(owner)!;Events.Clear();SyncBattle();
        var array=new[]{new BattleSkillActivity()};InGameUtil.ActivityLimitBreakSkill(array,owner,-1,sk.fix_id);
        InGameUtil.DelLimitBreakSkillCost(owner);
        ApplySkill(array[0],sk.name,false,0);
        State.Message=sk.name;
        if(InBattle && !Enemies.All(e=>e.isDead))EnemyReaction(sk.fix_id);
        if(Party.m_PartyTotalHP<=0){State.Phase="defeat";Emit("defeat","GAME OVER");}
        else if(InBattle && Enemies.All(e=>e.isDead))WinBattle();
        Save();
    }
    void SyncBattle()
    {
        SyncFieldCards();battle.m_BattleActive=InBattle;battle.m_TargetEnemyCurrent=State.Target;
        battle.m_EnemyParam=Enemies;battle.m_EnemyTotalNum=Enemies.Length;
        for(int i=0;i<5;i++)battle.m_BattleHandCardParam[i]=new BattleCard{m_CardElement=State.Hand[i],m_CardIndex=i};
    }
    void Emit(string kind,string name,int owner=-1,int target=-1,int amount=0,int element=0,int effect=0)
    {
        Events.Add(new BattleEvent{Kind=kind,Name=name,Owner=owner,Target=target,Amount=amount,Element=element,Effect=effect,
            EnemyIds=Enemies.Select(e=>e.m_EnemyMasterParamChara.fix_id).ToArray(),EnemyHp=Enemies.Select(e=>e.m_EnemyHP).ToArray(),EnemyMax=Enemies.Select(e=>e.m_EnemyHPMax).ToArray(),PartyHp=Party.m_PartyTotalHP});
    }
    int[] Targets(BattleSkillActivity a)
    {
        var valid=Enumerable.Range(0,Enemies.Length).Where(i=>!Enemies[i].isDead).ToArray();
        if(a.m_SkillType==ESKILLTYPE.eLIMITBREAK && (a.m_Category==5 || a.m_Category==10))valid=valid.Where(i=>Enemies[i].m_EnemyMasterParamChara.element==a.m_Param[a.m_Category==5?0:1]).ToArray();
        if(a.m_Type==2 || a.m_Type==4)return valid;
        if(a.m_Type!=1)return Array.Empty<int>();
        if(valid.Contains(State.Target))return new[]{State.Target};
        int damaged=valid.Where(i=>Enemies[i].m_EnemyHP<Enemies[i].m_EnemyHPMax).OrderBy(i=>Enemies[i].m_EnemyHP).FirstOrDefault(-1);
        if(damaged>=0)return new[]{damaged};
        foreach(float threshold in new[]{1f,.5f,0f}){int pick=valid.FirstOrDefault(i=>InGameUtil.GetSkillElementRate(a.m_Element,Enemies[i].m_EnemyMasterParamChara.element)>threshold,-1);if(pick>=0)return new[]{pick};}
        return valid.Take(1).ToArray();
    }
    int Hurt(int target,int amount,int element,string name,int owner,int effect=0)
    {
        var e=Enemies[target];e.DelHP(amount,1,false);State.MaxDamage=Math.Max(State.MaxDamage,(uint)Math.Max(0,amount));
        Emit("damage",name,owner,target,amount,element,effect);return amount;
    }
    void Heal(int amount,string name,int owner=-1)
    {
        int before=Party.m_PartyTotalHP;Party.RecoveryHP(amount,false);Emit("heal",name,owner,-1,Party.m_PartyTotalHP-before,7);
    }
    int ApplySkill(BattleSkillActivity a,string name,bool boost,int hands,bool prepared=false)
    {
        SyncBattle();Emit(a.m_SkillType==ESKILLTYPE.eLIMITBREAK?"active":"skill",name,a.m_SkillParamOwnerNum,-1,0,a.m_Element,a.m_Effect);
        int[] targets=Targets(a);int owner=a.m_SkillParamOwnerNum;int total=0;
        if(!prepared)a.m_SkillParamTarget=targets.Select(i=>new BattleSkillTarget{m_TargetNum=i,m_TargetType=1}).ToArray();
        if(a.m_Type==5){Heal(prepared?a.m_SkillParamTarget[0].m_SkillValue:(int)InGameUtil.GetHealValue(Party,boost,a),name,owner);}
        if(a.m_skill_damage_enable==2 && a.m_Type!=5){
            foreach(var t in a.m_SkillParamTarget??Array.Empty<BattleSkillTarget>()){
                int i=t.m_TargetNum;if(i<0 || i>=Enemies.Length || Enemies[i].isDead)continue;
                var e=Enemies[i];int damage=t.m_SkillValue;
                if(!prepared){var target=new CharaOnce();target.CharaSetupFromParamEnemy(e.m_EnemyMasterParamEnemy);damage=InGameUtil.GetDamageValue(Team[0],Team[4],Team[owner],target,e,boost,0,hands,a);}
                damage=InGameUtil.DamageReduce(damage,a.m_Element,e.m_EnemyAilmentID,e.m_EnemyDefense,a.m_skill_chk_def_defence!=2,a.m_skill_chk_def_ailment!=2);
                if(a.m_SkillType==ESKILLTYPE.eLIMITBREAK && a.m_Category==7 && !EnemyAbility.ChkAbilityInvalid(e.m_EnemyMasterParamEnemy,7) && RandManager.GetRand(0,100)<=a.m_Param[0])damage=e.m_EnemyHP;
                total+=Hurt(i,damage,a.m_Element,name,owner,a.m_Effect);
                if(a.m_skill_absorb!=0)Heal((int)InGameUtil.AvoidErrorMultiple(damage,InGameUtil.GetDBRevisionValue(a.m_skill_absorb)),name,owner);
                if(a.m_SkillType==ESKILLTYPE.eACTIVE){InGameUtil.PassiveChkHandsStatusAilment(hands,owner,i);}
            }
        }
        if(a.m_SkillType==ESKILLTYPE.eLIMITBREAK){
            switch(a.m_Category){
                case 24:for(int i=0;i<5;i++)if(State.Hand[i]==a.m_Param[0])State.Hand[i]=a.m_Param[1];break;
                case 25:for(int i=0;i<5;i++)State.Hand[i]=DrawPanel();break;
                case 23:if(a.m_Param[0]==2)for(int i=0;i<5;i++)State.Hand[i]=a.m_Param[3];break;
                case 40:State.Boost=(int)Random(5);break;
                case 41:for(int i=0;i<5;i++)if(a.m_Param[i]!=0 && a.m_Param[i]!=8)State.Hand[i]=a.m_Param[i];break;
                case 44:InGameUtil.LBSCostSkillProc(a);break;
                case 45:State.Position=a.m_Param[0]==2?(int)Random(4)*5+(int)Random(4):a.m_Param[2]*5+a.m_Param[1];break;
                case 46:State.Key=true;break;
                case 47:StatusAilmentUtil.DelAllStatusAilment(0);game.m_PanelDarkEnable=false;break;
                case 48:InGameUtil.ClearAilmentStatusCondition(a);break;
                case 30:Party.RecoverySP(a.m_Param[0],false,false);break;
            }
            if(a.m_skill_kickback_fix!=0)Party.DamageHP(Math.Min(a.m_skill_kickback_fix,Party.m_PartyTotalHP-1),true,false);
            if(a.m_skill_kickback!=0)Party.DamageHP(Math.Min((int)InGameUtil.AvoidErrorMultiple(Party.m_PartyTotalHPMax,InGameUtil.GetDBRevisionValue(a.m_skill_kickback)),Party.m_PartyTotalHP-1),true,false);
        }
        if(a.m_SkillType==ESKILLTYPE.eBOOST){
            switch(a.m_Category){
                case 3:Party.RecoverySP(a.m_Param[0],false,false);break;
                case 4:for(int i=0;i<5;i++)if(a.m_Param[i]!=8)State.Hand[i]=a.m_Param[i]!=0?a.m_Param[i]:(int)RandManager.GetRand(1,8);break;
                case 5:var map=new[]{0,a.m_Param[5],a.m_Param[0],a.m_Param[1],a.m_Param[3],a.m_Param[4],a.m_Param[2],a.m_Param[6]};for(int i=0;i<5;i++)if(map[i]!=8)State.Hand[i]=map[State.Hand[i]]!=0?map[State.Hand[i]]:(int)RandManager.GetRand(1,8);break;
                // Reference SkillBoost methods for categories 6-9 are empty.
            }
        }
        ApplyAilments(a,owner>=0?Team[owner].m_CharaPow:0,owner>=0?Team[owner].m_CharaHP:Party.m_PartyTotalHPMax);
        return total;
    }
    void ApplyBoost(List<BattleSkillActivity> normal)
    {
        battle.m_SkillRequestActive.m_SkillActivityArray=normal.ToArray();battle.m_SkillBoost.ActivityBoostSkill();
        if(battle.m_SkillBoost.BoostReqInputNum<=0)return;
        foreach(var a in battle.m_SkillBoost.SkillRequestBoost.m_SkillActivityArray)if(a!=null)ApplySkill(a,"BOOST",false,0);
        battle.m_SkillBoost.ResetSkillRequestBoost();
    }
    void FollowAttacks()
    {
        foreach(int owner in new[]{0,4})if(Team[owner]!=null && InGameUtil.CheckLaeaderSkill(owner)){
            var s=MasterDataUtil.GetLeaderSkillParamFromID(Team[owner].m_CharaMasterDataParam.skill_leader);if(s==null || s.skill_follow_atk_active!=2)continue;
            var a=new BattleSkillActivity{m_SkillParamOwnerNum=owner,m_SkillType=ESKILLTYPE.eLEADER,m_Type=2,m_Element=s.skill_follow_atk_element,m_Effect=s.skill_follow_atk_effect};
            a.m_SkillParamTarget=Enumerable.Range(0,Enemies.Length).Where(i=>!Enemies[i].isDead).Select(i=>{var t=new CharaOnce();t.CharaSetupFromParamEnemy(Enemies[i].m_EnemyMasterParamEnemy);return new BattleSkillTarget{m_TargetNum=i,m_SkillValue=InGameUtil.GetDamageFollow(Team[0],Team[4],Team[owner],t,Enemies[i].m_EnemyAilmentID)};}).ToArray();
            ApplySkill(a,s.name,false,0,true);
        }
        for(int owner=0;owner<5;owner++)if(Team[owner]!=null){
            var s=MasterDataUtil.GetPassiveSkillParamFromID(Team[owner].m_CharaMasterDataParam.skill_passive);if(s==null || s.skill_type!=11)continue;
            var a=new BattleSkillActivity{m_SkillParamOwnerNum=owner,m_SkillType=ESKILLTYPE.ePASSIVE,m_Type=2,m_Element=s.skill_param_00,m_Effect=s.skill_param_02};
            a.m_SkillParamTarget=Enumerable.Range(0,Enemies.Length).Where(i=>!Enemies[i].isDead).Select(i=>{var t=new CharaOnce();t.CharaSetupFromParamEnemy(Enemies[i].m_EnemyMasterParamEnemy);return new BattleSkillTarget{m_TargetNum=i,m_SkillValue=InGameUtil.GetPassiveDamageFollow(Team[owner],t,Enemies[i].m_EnemyAilmentID)};}).ToArray();
            ApplySkill(a,s.name,false,0,true);
        }
    }
    void Counter(int enemy,int damage)
    {
        for(int owner=0;owner<5;owner++)if(Team[owner]!=null){
            var array=new BattleSkillActivity[5];
            if(InGameUtil.PassiveChkSkillCounter(array,owner,damage,enemy) || InGameUtil.PassiveChkSkillCounterHeal(array,owner,damage,enemy))foreach(var a in array)if(a!=null)ApplySkill(a,"COUNTER",false,0,true);
        }
    }
    void PlayerTurnStart()
    {
        if(!StatusAilmentUtil.ChkStatusAilmentCondition(0,1)){int heal=StatusAilmentUtil.GetHealValue(0);if(heal!=0)Heal(heal,"REGEN");}
        int poison=(int)StatusAilmentUtil.GetPoisonDamage(0);if(poison!=0){Party.DamageHP(poison,true,false);Emit("player-hit","POISON",-1,-1,poison);}
        ailments.UpdateTurnOnce(EAILMENT_UPDATE_TIMING.eBATTLE,0);
    }
    static readonly System.Reflection.FieldInfo CardField=typeof(InGameCard).GetField("fieldCell",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!;
    void SyncFieldCards()
    {
        game.m_inGameCard=new InGameCard[25];
        for(int i=0;i<25;i++){var p=Panels[i];var c=new InGameCard();CardField.SetValue(c,new FieldCell{m_CellEventType=p.type,m_CellEventParam=p.type_unique,cellEnable=!State.Open[i]});game.m_inGameCard[i]=c;}
    }
    public bool Dark=>StatusAilmentUtil.GetDark(0);
    public string StatusText(int id)=>string.Join(" / ",ailments.m_StatusAilmentChara[id].cAilment.Where(a=>a.bUsed).Select(a=>$"{MasterDataUtil.GetMasterDataStatusAilmentParam(a.nMasterDataStatusAilmentID)?.name??a.nType.ToString()} {a.nLife}"));
}
public sealed class BattleEvent
{
    public string Kind="",Name="";
    public int Owner=-1,Target=-1,Amount,Element,Effect,PartyHp;
    public uint[] EnemyIds=Array.Empty<uint>();
    public int[] EnemyHp=Array.Empty<int>(),EnemyMax=Array.Empty<int>();
}
