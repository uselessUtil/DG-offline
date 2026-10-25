using System;using System.IO;using System.Reflection;using System.Collections.Generic;using LitJson;using ServerDataDefine;
class LayoutTests291 {
 static ServerForgery server;
 static void Check(bool b,string s){if(!b)throw new Exception(s);}
 static ServerForgerySaveUser Save {get{return (ServerForgerySaveUser)typeof(ServerForgery).GetField("m_ForgeryPlayerSave",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(server);}}
 static void Boot(){
  Singleton<TimeManager>.Instance=new TimeManager();Singleton<MasterDataManager>.Instance=new MasterDataManager();Singleton<LocalSaveManager>.Instance=new LocalSaveManager();
  server=new ServerForgery();Singleton<ServerForgery>.Instance=server;typeof(ServerForgery).GetMethod("Start",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(server,null);
 }
 static void Main(string[] a){
  Boot();
  if(a[0]=="reload") {
   Check(LocalSaveUtil.GetValueString("SERVER_SAVE","")==File.ReadAllText(a[1]+".save"),"Reload rewrote SERVER_SAVE");
   string before=JsonMapper.ToJson(Save.quest_build);
   Check(before==File.ReadAllText(a[1]+".quest"),"Saved packet changed across process");
   var f=new FieldManager();Check(f.CreateFieldArea(Save.quest_build),"Reload field failed");
   Check(before==JsonMapper.ToJson(Save.quest_build),"Field reconstruction rerolled layout");
   Console.WriteLine("PASS separate-process quest packet and SERVER_SAVE unchanged; field rebuilt without reroll");return;
  }
  var categories=new Dictionary<uint,MasterDataCategoryPattern>();foreach(var c in server.m_MasterDataQuestFloorCategory)categories[c.fix_id]=c;
  var expects=new Dictionary<uint,MasterDataExpectPattern>();foreach(var e in server.m_MasterDataQuestFloorExpect)expects[e.fix_id]=e;
  int oracleCount=0;
  using(var r=new BinaryReader(File.OpenRead(a[0])))while(r.BaseStream.Position<r.BaseStream.Length){
   uint c=r.ReadUInt32(),e=r.ReadUInt32(),seed=r.ReadUInt32();var x=new LegacyFloor291(categories[c],expects[e],seed);
   for(int i=0;i<25;i++)Check(x.Expect[i]==r.ReadInt32(),"Original106 expectation mismatch");
   for(int i=0;i<25;i++)Check(x.Types[i]==r.ReadInt32(),"Original106 event mismatch "+c+"/"+e+"/"+seed);
   oracleCount++;
  }
  Check(oracleCount==40320,"Oracle cases missing");Console.WriteLine("PASS "+oracleCount+" layouts exactly match original106 IL (60 categories x21 expectations x32 seeds)");
  int[] histogram=new int[25];int floors=0,quests=0;uint saveQuest=0;PacketStructQuestBuild last=null;
  var patterns=new HashSet<string>();
  string saveBefore=LocalSaveUtil.GetValueString("SERVER_SAVE","");
  for(int repeat=0;repeat<10;repeat++)foreach(var q in server.m_MasterDataQuest){
   if(q.fix_id==0 || q.active!=2 || q.fix_id==1 || q.fix_id==3 || q.fix_id==5)continue;
   var rows=new List<MasterDataQuestFloor>();foreach(var f in server.m_MasterDataQuestFloor)if(f.quest_id==q.fix_id && f.active==2)rows.Add(f);
   if(rows.Count==0)continue;
   var build=ServerForgery.CreateQuestBuild(q.fix_id);Check(build!=null,"No quest");
   var fields=new FieldManager();Check(fields.CreateFieldArea(build),"No fields");
   var ids=new HashSet<int>();
   for(int fi=0;fi<rows.Count;fi++){
    var p=build.panel[fi+1].panel_detail;int keys=0,hidden=0;var types=new int[25];
    for(int i=0;i<25;i++){
     Check(ids.Add(p[i].unique_id),"Duplicate panel identity");
     types[i]=p[i].type;
     if(p[i].type==5){keys++;histogram[i]++;Check(i!=22 && p[i].star==9,"Invalid key");}
     else Check(p[i].star!=9,"False key display");
     if(p[i].star==8)hidden++;
     Check(p[i].star==fields.fieldFloor[fi].fieldCell[i].m_CellDisplay,"Display mapping differs");
     if(p[i].type_unique!=0){
      bool found=false;
      if(p[i].type==3){foreach(var b in build.list_battle)if(b.unique_id==p[i].type_unique)found=true;}
      else if(p[i].type==2 || p[i].type==6){foreach(var b in build.list_item)if(b.unique_id==p[i].type_unique)found=true;}
      else if(p[i].type==4){foreach(var b in build.list_trap)if(b.unique_id==p[i].type_unique)found=true;}
      Check(found,"Broken native event reference");
     }
    }
    Check(keys==1 && p[22].type==1 && p[22].type_unique==0,"Key/start invariant failed");
    Check(hidden<=rows[fi].question_ct,"Too many question masks");
    Check(fields.fieldFloor[fi].GetFieldKeyMax()==1,"Key gate count changed");
    patterns.Add(string.Join(",",Array.ConvertAll(types,v=>v.ToString())));floors++;
   }
   quests++;last=build;saveQuest=q.fix_id;
  }
  Check(histogram[22]==0,"Key on start");
  for(int i=0;i<25;i++)if(i!=22)Check(histogram[i]>0,"Unreachable key cell "+i);
  Check(patterns.Count>100,"Layout lacks diversity");
  Check(saveBefore==LocalSaveUtil.GetValueString("SERVER_SAVE",""),"Builder changed player save");
  Console.WriteLine("PASS quests="+quests+" floors="+floors+" distinct event layouts="+patterns.Count+" key positions="+string.Join(",",Array.ConvertAll(histogram,v=>v.ToString())));
  server.UserParamCreate(new SendCreateUser{uuid="layout291-isolated",terminal=new PacketStructTerminal{platform=1,name="Docker"}});
  Save.quest_id=saveQuest;Save.quest_build=last;server.UserParamSave();
  File.WriteAllText(a[1]+".save",LocalSaveUtil.GetValueString("SERVER_SAVE",""));
  File.WriteAllText(a[1]+".quest",JsonMapper.ToJson(Save.quest_build));
  Console.WriteLine("PASS saved new randomized quest for separate process");
 }
}