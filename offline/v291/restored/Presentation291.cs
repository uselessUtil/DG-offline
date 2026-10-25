// Used only by the expanded build's fallback request path. RequestNative wins.
using System;using UnityEngine;
using Object=UnityEngine.Object;
public static partial class DivineGateOffline {
 static string RestorationMapName291(string bundle,string name) {
  string r=name;
  if(!(r.StartsWith("map_") && r.Contains("_tex"))) {
   var scene=Singleton<SceneGoesParam>.Instance;
   var p=scene==null?null:scene.m_SceneGoesParamToGameMain;
   var a=p==null?null:MasterDataUtil.GetAreaParamFromID(p.m_QuestAreaID);
   r=a==null?bundle.Replace(".unity3d",""):a.res_map;
  }
  return r.Replace("_tex","");
 }
 static Texture2D RestorationAtlas291(string bundle,string name) {
  return RecoveredTexture("restored-map/"+RestorationMapName291(bundle,name)+"/atlas");
 }
 static GameObject RestorationQuad291(GameObject root,string name,Texture2D texture,float x0,float x1,float z0,float z1,float y) {
  if(texture==null)throw new InvalidOperationException("Missing split map image "+name);
  var go=new GameObject(name);go.transform.parent=root.transform;go.transform.localPosition=Vector3.zero;
  var m=new Mesh();m.vertices=new[]{new Vector3(x0,y,z0),new Vector3(x1,y,z0),new Vector3(x1,y,z1),new Vector3(x0,y,z1)};
  m.uv=new[]{new Vector2(0,0),new Vector2(1,0),new Vector2(1,1),new Vector2(0,1)};
  m.colors=new[]{Color.white,Color.white,Color.white,Color.white};m.triangles=new[]{0,2,1,0,3,2};m.RecalculateNormals();
  go.AddComponent<MeshFilter>().mesh=m;
  var mat=new Material(FlatShader());mat.mainTexture=texture;go.AddComponent<MeshRenderer>().sharedMaterial=mat;
  return go; // Deliberately no Collider: original goal collision remains authoritative.
 }
 static AnimationClip RestorationDoorClip291(string name,bool boss,bool open) {
  var clip=new AnimationClip();clip.name=name;clip.wrapMode=WrapMode.ClampForever;
  clip.SetCurve("NormalDoor",typeof(Transform),"localScale.x",AnimationCurve.Linear(0,boss?0:1,0.3f,boss||open?0:1));
  clip.SetCurve("BossDoor",typeof(Transform),"localScale.x",AnimationCurve.Linear(0,boss?1:0,0.3f,boss&&!open?1:0));
  return clip;
 }
 static GameObject RestorationMap291(string bundle,string name) {
  var root=Template(name);string key="restored-map/"+RestorationMapName291(bundle,name)+"/";
  if(name.ToLowerInvariant().Contains("door")) {
   // Frame stays fixed; the two door skins are independent animated meshes.
   RestorationQuad291(root,"Backdrop",RecoveredTexture(key+"upper"),-2.5f,2.5f,1.916f,3.459f,-0.2f);
   RestorationQuad291(root,"NormalDoor",RecoveredTexture(key+"normal"),-0.45f,0.45f,2.5f,3.5f,-0.15f);
   RestorationQuad291(root,"BossDoor",RecoveredTexture(key+"boss"),-0.45f,0.45f,2.5f,3.5f,-0.14f);
   var clips=new[]{RestorationDoorClip291("dg_fallback_normal_open",false,true),RestorationDoorClip291("dg_fallback_boss_closed",true,false),
                   RestorationDoorClip291("dg_fallback_normal_closed",false,false),RestorationDoorClip291("dg_fallback_boss_open",true,true)};
   var playback=root.AddComponent<Animation>();
   foreach(var clip in clips)playback.AddClip(clip,clip.name);
   var array=root.AddComponent<AnimationArray>();array.m_Clips=clips;
   // Template is under the inactive template root. Awake runs on the clone,
   // after the serialized clip array is populated.
   return root;
  }
  if(name.ToLowerInvariant().Contains("mesh")) {
   var lower=RecoveredTexture(key+"lower");float depth=5f*lower.height/lower.width;
   RestorationQuad291(root,"Floor",lower,-2.5f,2.5f,2.5f-depth,2.5f,-0.1f);
  }
  return root;
 }
}
