using System;using System.IO;using System.Reflection;using System.Collections.Generic;using System.Runtime.Serialization;using UnityEngine;
class UiAtlasTests {
 static void Check(bool ok,string s){if(!ok)throw new Exception(s);}
 static UIAtlas Atlas(string name){
  var a=(UIAtlas)FormatterServices.GetUninitializedObject(typeof(UIAtlas));
  typeof(UIAtlas).GetField("sprites",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(a,new List<UIAtlas.Sprite>());
  EngineSupport.BindComponentOwner(a,"DG offline atlas "+name);return a;
 }
 static void Main(string[] args){
  var oldArea=Atlas("AtlasAreaIcon_000_020");var newArea=Atlas("AtlasAreaIcon_021_040");
  Check(oldArea.GetSprite("area_icon_4068")==null,"Missing area forged in atlas0");
  Check(newArea.GetSprite("area_icon_4068")!=null,"New area not found in second atlas");
  var unknown=oldArea.GetSprite("area_icon_9999");Check(unknown==null,"Unknown area got arbitrary tile");
  var p0=Atlas("AtlasPanelIcon");var p1=Atlas("AtlasPanelIcon_2");
  Check(p0.GetSprite("hagaren_key")==null && p1.GetSprite("hagaren_key")!=null,"Missing key stopped native search");
  Check(p0.GetSprite("panel_b_1")!=null,"Original star panel missing");
  Check(p1.GetSprite("panel_b_1")==null,"Foreign star panel forged in later atlas");
  var chara=Atlas("AtlasCharaIcon_000_199");
  Check(chara.GetSprite("chara_icon_810")==null && chara.GetSprite("chara_icon_009")!=null,"Character group search changed");
  Singleton<SceneObjReferGameMain>.Instance=new SceneObjReferGameMain();
  Singleton<SceneObjReferGameMain>.Instance.m_AtlasObjPanel=new[]{p0,p1};
  Check(InGameUtil.GetPanelAtlas("hagaren_key")==p1,"Original GetPanelAtlas picked wrong texture");
  Check(InGameUtil.GetPanelAtlas("panel_b_1")==p0,"Original star panel atlas changed");
  int uv=0;
  foreach(string line in File.ReadAllLines(args[0])){
   var parts=line.Split('\t');var a=Atlas(parts[0]);var sprite=a.GetSprite(parts[1]);
   Check(sprite!=null,"Expected sprite missing "+line);
   var expected=parts[2].Split(',');
   Check(sprite.inner.x==int.Parse(expected[0]) && sprite.inner.y==int.Parse(expected[1]) &&
         sprite.inner.width==int.Parse(expected[2]) && sprite.inner.height==int.Parse(expected[3]),"Sprite pixel rect changed");
   uv++;
  }
  Singleton<TimeManager>.Instance=new TimeManager();Singleton<MasterDataManager>.Instance=new MasterDataManager();
  Singleton<LocalSaveManager>.Instance=new LocalSaveManager();var server=new ServerForgery();Singleton<ServerForgery>.Instance=server;
  typeof(ServerForgery).GetMethod("Start",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(server,null);
  foreach(var row in server.m_MasterDataParamPanel) {
   foreach(string name in new[]{InGameUtil.GetTrapSpriteName(row.fix_id),InGameUtil.GetItemSpriteName(row.fix_id)})
    if(!string.IsNullOrEmpty(name))Check(InGameUtil.GetPanelAtlas(name)!=null,"Missing actual native panel result "+row.fix_id+" / "+name);
  }
  Check(InGameUtil.GetPanelAtlas("panel_Heart_SP")!=null && InGameUtil.GetPanelAtlas("heal_sp")!=null,"SP tile missing");
  Check(DivineGateOffline.HudCrop291(9,0).width==0,"Original Midori crop changed");
  int crops=0;
  foreach(string line in File.ReadAllLines(args[1])){
   uint id=uint.Parse(line);for(int slot=0;slot<5;slot++){
    Rect rect=DivineGateOffline.HudCrop291(id,slot);
    Check(rect.x>=0 && rect.y>=0 && rect.xMax<=1.00001f && rect.yMax<=1.00001f && rect.width>0.01f && rect.width<=1.00001f,"Unsafe HUD crop "+id);
    crops++;
   }
  }
  var mami=DivineGateOffline.HudCrop291(810,0);
  Check(mami.y>.55f && mami.yMax>.8f && mami.x<.5f && mami.xMax>.5f,"Mami face crop regressed to legs");
  Console.WriteLine("PASS actual signed UIAtlas.GetSprite and InGameUtil.GetPanelAtlas: missing names continue native multi-atlas search; original sprites retained; "+uv+" exact pixel rectangles.");
  Console.WriteLine("PASS battle HUD: "+crops+" crops inside texture bounds; original595 IDs untouched; Mami810 face region replaces borrowed leg crop. No account data or battle calculations touched.");
 }
}
