using System;
using ServerDataDefine;

// Layout port of original 1.0.6 FieldManager.CreateFieldArea. The original Rand
// algorithm is identical in 2.9.1. Saved packets never run this generator again.
public sealed class LegacyFloor291 {
 public readonly int[] Expect;
 public readonly int[] Types;
 public readonly uint InvasionRoll;
 readonly uint invasionSeed;
 public uint InvasionTicket(int count) { if(count<=0 || count>10000)throw new InvalidOperationException("Invalid invasion candidates");var r=new Rand();r.SetRandSeed(invasionSeed);return r.GetRand(0,(uint)(100*count)); }
 readonly Rand random;
 public LegacyFloor291(MasterDataCategoryPattern category, MasterDataExpectPattern expect, uint seed) {
  if(category==null || expect==null)throw new InvalidOperationException("Missing legacy floor pattern");
  int[] counts={category.cate0,category.cate2,category.cate1,category.cate3,category.cate4,1};
  int[] kinds={1,2,3,4,6,5};
  int total=0;foreach(int n in counts){if(n<0)throw new InvalidOperationException("Negative category");total+=n;}
  if(total!=25 || category.cate5!=1)throw new InvalidOperationException("Expected legacy 24+1 layout");
  Expect=new int[25];Types=new int[25];
  random=new Rand();random.SetRandSeed(seed);
  // 1.0.6 reseeds the invasion RNG with the floor seed at each eligible cell:
  // all seven-star enemy cells on a floor share this first draw.
  var invasionRandom=new Rand();invasionRandom.SetRandSeed(seed);
  InvasionRoll=invasionRandom.GetRand(0,100);invasionSeed=seed;
  int[] stars={expect.level1,expect.level2,expect.level3,expect.level4,expect.level5,expect.level6,expect.level7};
  int pos=0;
  for(int s=0;s<7;s++) {
   if(stars[s]<0 || pos+stars[s]>25)throw new InvalidOperationException("Invalid expectation count");
   for(int n=0;n<stars[s];n++)Expect[pos++]=s+1;
  }
  while(pos<25)Expect[pos++]=1;
  Swap100(Expect);
  pos=0;for(int c=0;c<counts.Length;c++)for(int n=0;n<counts[c];n++)Types[pos++]=kinds[c];
  Swap100(Types);
  // Original correction of high-expectation ordinary vs low-expectation events.
  for(int i=0;i<25;i++)if(Types[i]==1 && Expect[i]>2)
   for(int j=0;j<25;j++)if(Types[j]!=1 && Expect[j]<=2){Swap(Types,i,j);break;}
  // Original starting square is 22. Move its key away, then make it ordinary.
  // As in 1.0.6, a non-key event count can decrease by one at the start.
  while(Types[22]==5)Swap(Types,(int)random.GetRand(0,25),22);
  Types[22]=1;
 }
 static void Swap(int[] a,int i,int j){int v=a[i];a[i]=a[j];a[j]=v;}
 void Swap100(int[] a){for(int i=0;i<100;i++)Swap(a,(int)random.GetRand(0,25),(int)random.GetRand(0,25));}
 public void Apply(PacketStructQuestBuildPanelDetail[] panels) {
  if(panels==null || panels.Length!=25)throw new InvalidOperationException("Expected 25 panels");
  for(int i=0;i<25;i++){panels[i].type=Types[i];panels[i].star=Types[i]==5?9:Expect[i];}
 }
 public void Finish(PacketStructQuestBuildPanelDetail[] panels,uint questionCount) {
  // Mask AFTER the real star has selected the 2.9.1 enemy/item/trap group.
  // Old QUESTION display maps to dummy=8, actual key=9 in the new packet.
  bool[] hidden=new bool[25];
  for(uint n=0;n<questionCount;n++)for(int attempt=0;attempt<100;attempt++) {
   int p=(int)random.GetRand(0,25);
   if(Types[p]!=5 && !hidden[p] && Expect[p]!=6){hidden[p]=true;break;}
  }
  for(int i=0;i<25;i++) {
   if(Types[i]==5){panels[i].star=9;panels[i].risk=0;}
   else if(hidden[i])panels[i].star=8;
   else if(Types[i]==6)panels[i].star=10;
  }
 }
}
public static partial class DivineGateOffline {
 public static LegacyFloor291 ApplyLegacyFloor291(PacketStructQuestBuildPanelDetail[] panels,MasterDataCategoryPattern category,MasterDataExpectPattern expect) {
  var layout=new LegacyFloor291(category,expect,RandManager.GetRand());layout.Apply(panels);return layout;
 }
 public static void FinishLegacyFloor291(LegacyFloor291 layout,PacketStructQuestBuildPanelDetail[] panels,MasterDataQuestFloor floor) {
  layout.Finish(panels,floor.question_ct);
 }
}
