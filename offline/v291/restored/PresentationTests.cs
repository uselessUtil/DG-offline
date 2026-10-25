using System;using System.Reflection;using UnityEngine;
class PresentationTests {
 static void Check(bool b,string s){if(!b)throw new Exception(s);}
 static void Main(){
  var type=typeof(DivineGateOffline);
  var method=type.GetMethod("RestorationDoorClip291",BindingFlags.NonPublic|BindingFlags.Static);
  foreach(bool boss in new[]{false,true})foreach(bool open in new[]{false,true}){
   DoorCurveProbe.Values.Clear();DoorCurveProbe.Paths.Clear();method.Invoke(null,new object[]{"test",boss,open});
   Check(DoorCurveProbe.Values.Count==2 && DoorCurveProbe.Paths[0]=="NormalDoor" && DoorCurveProbe.Paths[1]=="BossDoor","Unsplit door paths");
   var normal=DoorCurveProbe.Values[0];var rare=DoorCurveProbe.Values[1];
   Check(normal[0]==0 && normal[2]==0.3f && rare[2]==0.3f,"Animation timing");
   Check(normal[1]==(boss?0:1) && normal[3]==(boss||open?0:1),"Normal open/closed curve");
   Check(rare[1]==(boss?1:0) && rare[3]==(boss&&!open?1:0),"Boss open/closed curve");
  }
  var lookup=type.GetMethod("NativePackage",BindingFlags.NonPublic|BindingFlags.Static);
  Check(lookup.Invoke(null,new object[]{"map_0012_tex","map_0012_tex",typeof(Texture2D)})!=null,"Original map0012 texture route lost");
  Check(lookup.Invoke(null,new object[]{"chara_001","chara_prefab_001",typeof(GameObject)})!=null,"Original character model route lost");
  Check(lookup.Invoke(null,new object[]{"chara_237","chara_prefab_237",typeof(GameObject)})==null,"Missing character unexpectedly mapped to unrelated native model");
  Console.WriteLine("PASS signed adapter: independent normal/boss open/closed curves (0.3s); original map0012 and character001 native routes win, absent character237 selects PNG fallback. Engine animation rendering remains device-only.");
 }
}
