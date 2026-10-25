using System;using System.Linq;using Mono.Cecil;using Mono.Cecil.Cil;
class PrepareDoorProbe {
 static void Main(string[] a){
  var engine=ModuleDefinition.ReadModule(a[0]);var probe=ModuleDefinition.ReadModule(a[1]);
  var t=probe.Types.Single(x=>x.Name=="DoorCurveProbe");
  foreach(var type in engine.Types)foreach(var m in type.Methods){
   string name=null;
   if(type.FullName=="UnityEngine.AnimationCurve" && m.Name=="Linear" && m.Parameters.Count==4)name="Curve";
   if(type.FullName=="UnityEngine.AnimationClip" && m.Name=="SetCurve" && m.Parameters.Count==4)name="Binding";
   if(name==null)continue;
   m.Body=new MethodBody(m);var il=m.Body.GetILProcessor();
   foreach(var p in m.Parameters)il.Emit(OpCodes.Ldarg,p);
   il.Emit(OpCodes.Call,engine.ImportReference(t.Methods.Single(x=>x.Name==name)));il.Emit(OpCodes.Ret);
  }
  engine.Write(a[2]);
 }
}
