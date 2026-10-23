using System;using System.Reflection;
class SkillTests240 {
 static void Check(bool ok,string text){if(!ok)throw new Exception(text);}
 static int Main(){
  Singleton<TimeManager>.Instance=new TimeManager();Singleton<MasterDataManager>.Instance=new MasterDataManager();Singleton<LocalSaveManager>.Instance=new LocalSaveManager();
  var server=new ServerForgery();Singleton<ServerForgery>.Instance=server;typeof(ServerForgery).GetMethod("Start",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(server,null);
  var game=new InGameManager();Singleton<InGameManager>.Instance=game;game.m_PlayerPartyChara=new CharaOnce[5];game.m_PlayerParty=new CharaParty();
  var owner=new CharaOnce();owner.m_CharaMasterDataParam=new MasterDataParamChara{base_hp_max=10000,element=1,kind=1};owner.m_CharaPow=100;owner.m_CharaHP=5000;
  var enemy=new BattleEnemy();enemy.m_EnemyHP=10000;
  int count=0,damageCount=0,statusCount=0;
  foreach(var row in server.m_MasterDataSkillLimitBreak){
   if(row.fix_id==0)continue;var a=new BattleSkillActivity();var list=new[]{a};
   InGameUtil.ActivityLimitBreakSkill(list,0,-1,row.fix_id);
   Check(a.m_SkillParamSkillID==row.fix_id,"Wrong skill ID");
   for(int i=0;i<5;i++)Check(a.m_Param[i]==row.value_list[i],"Lost old parameter");
   Check(a.m_skill_damage_enable==((row.skill_type==1 || row.skill_type==2)?2:1),"Incorrect damage gate "+row.fix_id);
   if(row.skill_type>=3)Check(a.m_SkillParamTarget!=null && a.m_SkillParamTarget.Length==1,"Missing recovery/status target");
   if(row.skill_type==1 || row.skill_type==2){
    int actual=InGameUtil.GetDamageValue(null,null,owner,null,enemy,false,-1,0,a);int expected=0;
    switch(row.skill_cate){
     case 3:case 8:case 9:expected=row.value0;break;
     case 10:expected=(int)(row.value0*0.01f);break;
     case 11:expected=(int)(10000*row.value0*0.01f);break;
     case 12:expected=200;break;
     case 5:expected=row.value1;break;
     default:expected=row.value0;break;
    }
    Check(actual>=Math.Max(0,expected-1) && actual<=expected+101,"Legacy damage mismatch "+row.fix_id+" category "+row.skill_cate+" got "+actual+" expected base "+expected);
    if(row.skill_cate!=7)Check(actual>0,"Zero damage regression "+row.fix_id);
    if(row.skill_cate==4)Check(a.m_skill_absorb==100,"Absorb lost");
    if(row.skill_cate==6 || row.skill_cate==9)Check(a.m_skill_kickback_fix==int.MaxValue,"HP sacrifice lost");
    damageCount++;
   }
   int category=StatusAilmentUtil.ConvertCategoryToAilment(a.m_Category,a.m_Param);
   if(category!=0){var status=DivineGateOffline.LegacyStatus240(a,100,5000);Check(status.nType==category,"Status category lost");statusCount++;}
   count++;
  }
  var ordinary=new BattleSkillActivity();ordinary.m_skill_power=150;ordinary.m_skill_chk_atk_combo=1;
  Check(InGameUtil.GetDamageValue(null,null,owner,null,enemy,false,-1,0,ordinary)==150,"Original normal-skill path changed");
  Check(count==459,"Incomplete skill coverage");
  Console.WriteLine("PASS "+count+" original skill initialization and parameter preservation; "+damageCount+" damage formulas/gates; "+statusCount+" old status categories; recovery targets, absorb, sacrifice and original ordinary-skill path.");return 0;
 }
}
