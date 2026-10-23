using System;using System.IO;using System.Reflection;using System.Collections.Generic;using LitJson;
class KeyFloorTests210 {
 static void Check(bool ok,string text){if(!ok)throw new Exception(text);}
 static int Main(string[] args){
  bool baseline=args.Length>0 && args[0]=="baseline";
  Singleton<TimeManager>.Instance=new TimeManager();Singleton<MasterDataManager>.Instance=new MasterDataManager();Singleton<LocalSaveManager>.Instance=new LocalSaveManager();
  var server=new ServerForgery();Singleton<ServerForgery>.Instance=server;
  typeof(ServerForgery).GetMethod("Start",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(server,null);
  var source=JsonMapper.ToObject<MasterDataCategoryPattern[]>(File.ReadAllText(Environment.GetEnvironmentVariable("DG_KEY_SOURCE")));
  var originalKeys=new Dictionary<uint,int>();foreach(var p in source){Check(p.cate0+p.cate1+p.cate2+p.cate3+p.cate4==24,"Unexpected legacy 24-cell pattern");originalKeys[p.fix_id]=1;}
  if(!baseline)foreach(var p in server.m_MasterDataQuestFloorCategory){Check(p.cate5==1,"Legacy implicit key not restored");}
  int quests=0,floors=0,mismatches=0,keyFloors=0,missingKeys=0,opens=0;
  var init=typeof(InGameManager).GetMethod("StepFuncInitFloorCreate",BindingFlags.NonPublic|BindingFlags.Instance);
  foreach(var q in server.m_MasterDataQuest){
   if(q.fix_id==0 || q.active!=2 || q.fix_id==1 || q.fix_id==3 || q.fix_id==5)continue;
   var required=new List<MasterDataQuestFloor>();foreach(var f in server.m_MasterDataQuestFloor)if(f.active==2 && f.quest_id==q.fix_id)required.Add(f);
   if(required.Count==0)continue;
   var build=ServerForgery.CreateQuestBuild(q.fix_id);Check(build!=null,"No generated quest "+q.fix_id);
   var fields=new FieldManager();Singleton<FieldManager>.Instance=fields;Check(fields.CreateFieldArea(build),"Field creation failed");
   var game=new InGameManager();Singleton<InGameManager>.Instance=game;game.m_FieldFloor=fields.fieldFloor;
   Check(fields.fieldFloor.Length==required.Count,"Floor count changed");
   for(int i=0;i<required.Count;i++){
    int expected=originalKeys[required[i].pattern_category],actual=fields.fieldFloor[i].GetFieldKeyMax();
    int keyPanels=0;foreach(var panel in build.panel[i+1].panel_detail)if(panel.type==5)keyPanels++;
    if(actual!=expected || keyPanels!=expected)mismatches++;
    if(expected>0){keyFloors++;if(actual==0)missingKeys++;}
    game.m_QuestFloor=i;init.Invoke(game,null);
    bool shouldOpen=expected==0;
    if(game.m_AcquireKey!=shouldOpen)opens++;
    if(!baseline){
     Check(actual==expected && keyPanels==expected,"Quest "+q.fix_id+" floor "+i+" expected "+expected+" keys, actual "+actual);
     Check(game.m_AcquireKey==shouldOpen,"Incorrect initial door state "+q.fix_id+" / "+i);
     if(actual>0){
      // All key tiles must be consumed; with one remaining key the door stays locked.
      var cells=fields.fieldFloor[i].fieldCell;int remaining=actual;
      foreach(var cell in cells)if(cell.m_CellDisplay==9){
       Check(fields.fieldFloor[i].ChkFieldKey(),"Key gate vanished early");cell.cellEnable=false;remaining--;
       Check(fields.fieldFloor[i].ChkFieldKey()==(remaining>0),"Incorrect remaining-key gate");
      }
      Check(fields.fieldFloor[i].GetFieldKey()==actual,"Collected-key count mismatch");
      init.Invoke(game,null);Check(game.m_AcquireKey,"Door did not become accessible after collecting all keys");
     }
    }
    floors++;
   }
   quests++;
  }
  // The tutorial has a hard-coded layout and must not be rewritten by this data fix.
  foreach(uint id in new uint[]{1,3,5}){
   var b=ServerForgery.CreateQuestBuild(id);var f=new FieldFloor();Check(f.SetupFloor(1,b),"Tutorial setup failed");Check(f.GetFieldKeyMax()==1,"Tutorial key changed");
  }
  Console.WriteLine((baseline?"BASELINE":"PASS")+" quests="+quests+" floors="+floors+" source_key_floors="+keyFloors+" mismatched_key_counts="+mismatches+" key_floors_without_keys="+missingKeys+" wrong_initial_door_state="+opens+"; original floor-init method executed; tutorial keys preserved.");
  if(baseline)Check(missingKeys>0 && opens>0,"Baseline did not reproduce missing keys/open door");
  return 0;
 }
}
