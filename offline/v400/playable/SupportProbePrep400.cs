// Only the isolated probe copy receives a managed equivalent of native Mathf.Pow.
using System;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
class SupportProbePrep400 {
 static void Main(string[] args) {
  var m=ModuleDefinition.ReadModule(args[0]);
  var pow=m.Types.Single(t=>t.FullName=="UnityEngine.Mathf").Methods.Single(f=>f.Name=="Pow");
  pow.Body=new MethodBody(pow);var il=pow.Body.GetILProcessor();
  il.Emit(OpCodes.Ldarg_0);il.Emit(OpCodes.Conv_R8);il.Emit(OpCodes.Ldarg_1);il.Emit(OpCodes.Conv_R8);
  il.Emit(OpCodes.Call,m.ImportReference(typeof(Math).GetMethod("Pow",new[]{typeof(double),typeof(double)})));
  il.Emit(OpCodes.Conv_R4);il.Emit(OpCodes.Ret);m.Write(args[1]);
 }
}
