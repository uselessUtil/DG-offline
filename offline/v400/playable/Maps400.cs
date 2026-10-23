// Rebuild archived map meshes and legacy AnimationClips with Unity 5 APIs.
// The scene retains its original camera, GoalCollision and field input logic.
using System;
using System.IO;
using System.Text;
using UnityEngine;

public static partial class DivineGateOffline {
 static string MapString400(BinaryReader r) {
  int count=r.ReadInt32();if(count<0||count>4096)throw new InvalidOperationException("Map string size");
  byte[] bytes=new byte[count];for(int i=0;i<count;i++)bytes[i]=r.ReadByte();
  return Encoding.UTF8.GetString(bytes);
 }
 static Vector3 MapVector400(BinaryReader r){return new Vector3(r.ReadSingle(),r.ReadSingle(),r.ReadSingle());}
 static Material MapMaterial400(string bundle,string name) {
  Shader shader=Shader.Find("Unlit/Colored Mult Diffuse Opaque");
  if(shader==null)throw new InvalidOperationException("Missing original opaque map shader");
  Material material=new Material(shader);material.name="DG400 restored map material";
  material.SetColor("_Color",Color.white);material.mainTexture=MapPartTexture(bundle,name,"lower");
  return material;
 }
 static GameObject Map400(string bundle,string name) {
  byte[] data=MapData400.Read(name);
  if(data==null)return Map(bundle,name); // Non-map requests such as unavailable stage particles.
  var stream=new MemoryStream(data);var r=new BinaryReader(stream);
  if(r.ReadInt32()!=4001)throw new InvalidOperationException("Map data version");
  int count=r.ReadInt32();if(count<1||count>128)throw new InvalidOperationException("Map node count");
  GameObject[] nodes=new GameObject[count];
  Material material=MapMaterial400(bundle,name);
  for(int n=0;n<count;n++) {
   string nodeName=MapString400(r);int parent=r.ReadInt32(),layer=r.ReadInt32();bool active=r.ReadInt32()!=0;
   if(parent>=n || (n==0?parent!=-1:parent<0))throw new InvalidOperationException("Map hierarchy");
   GameObject node=n==0?Template(name):new GameObject(nodeName);nodes[n]=node;
   if(n>0)node.transform.parent=nodes[parent].transform;
   node.layer=layer;node.transform.localPosition=MapVector400(r);
   node.transform.localRotation=new Quaternion(r.ReadSingle(),r.ReadSingle(),r.ReadSingle(),r.ReadSingle());
   node.transform.localScale=MapVector400(r);node.SetActive(active);
   int vertexCount=r.ReadInt32();
   if(vertexCount>0) {
    var vertices=new Vector3[vertexCount];var uv=new Vector2[vertexCount];var colors=new Color[vertexCount];
    for(int i=0;i<vertexCount;i++) {vertices[i]=MapVector400(r);uv[i]=new Vector2(r.ReadSingle(),r.ReadSingle());colors[i]=new Color(r.ReadSingle(),r.ReadSingle(),r.ReadSingle(),r.ReadSingle());}
    int[] triangles=new int[r.ReadInt32()];for(int i=0;i<triangles.Length;i++)triangles[i]=r.ReadInt32();
    var mesh=new Mesh();mesh.name="DG400 restored "+nodeName;mesh.vertices=vertices;mesh.uv=uv;mesh.colors=colors;mesh.triangles=triangles;mesh.RecalculateNormals();mesh.RecalculateBounds();
    node.AddComponent<MeshFilter>().sharedMesh=mesh;
    var renderer=node.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.enabled=r.ReadInt32()!=0;
   }
  }
  int clipCount=r.ReadInt32();
  if(clipCount>0) {
   if(clipCount!=4)throw new InvalidOperationException("Door animation slot count");
   var clips=new AnimationClip[clipCount];
   for(int c=0;c<clipCount;c++) {
    var clip=new AnimationClip();clips[c]=clip;clip.name=MapString400(r);clip.legacy=true;
    clip.wrapMode=(WrapMode)r.ReadInt32();clip.frameRate=r.ReadSingle();int curveCount=r.ReadInt32();
    for(int i=0;i<curveCount;i++) {
     string path=MapString400(r),property=MapString400(r);int pre=r.ReadInt32(),post=r.ReadInt32();
     var keys=new Keyframe[r.ReadInt32()];
     for(int k=0;k<keys.Length;k++)keys[k]=new Keyframe(r.ReadSingle(),r.ReadSingle(),r.ReadSingle(),r.ReadSingle());
     var curve=new AnimationCurve(keys);curve.preWrapMode=(WrapMode)pre;curve.postWrapMode=(WrapMode)post;
     clip.SetCurve(path,typeof(Transform),property,curve);
    }
    int eventCount=r.ReadInt32();
    for(int i=0;i<eventCount;i++) {var e=new UnityEngine.AnimationEvent();e.time=r.ReadSingle();e.functionName=MapString400(r);clip.AddEvent(e);}
   }
   // Template is under an inactive parent: Awake sees the assigned clips when
   // the engine instantiates the ready door. Existing game code owns playback.
   nodes[0].AddComponent<Animation>();
   var animations=nodes[0].AddComponent<AnimationArray>();animations.m_Clips=clips;
   nodes[0].AddComponent<global::AnimationEvent>();
  }
  if(stream.Position!=stream.Length)throw new InvalidOperationException("Map trailing data");
  stream.Close();Debug.Log("[DG400] Restored map "+name+": "+count+" nodes / "+clipCount+" clips");
  return nodes[0];
 }
}
