// Disposable managed probe: replace engine/native I/O only, never packaged.
using System;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
class PrepareQuestProbe400 {
 static void Main(string[] a) {
  var m=ModuleDefinition.ReadModule(a[0]);
  foreach(var r in m.AssemblyReferences) if(new[]{"mscorlib","System","System.Core"}.Contains(r.Name)) {
   r.Version=new Version(4,0,0,0);r.PublicKeyToken=new byte[]{0xb7,0x7a,0x5c,0x56,0x19,0x34,0xe0,0x89};
  }
  if(m.Assembly.Name.Name!="UnityEngine"){m.Write(a[1]);return;}
  foreach(var t in m.Types) foreach(var f in t.Methods) {
   if(!f.IsInternalCall && !f.IsPInvokeImpl && t.FullName!="UnityEngine.Debug" &&
      !(t.FullName=="UnityEngine.Object" && f.Name.StartsWith("op_")))continue;
   f.ImplAttributes=MethodImplAttributes.IL|MethodImplAttributes.Managed;
   f.Attributes &= ~MethodAttributes.PInvokeImpl;
   f.Body=new MethodBody(f){InitLocals=true};var il=f.Body.GetILProcessor();
   if(t.FullName=="UnityEngine.Object" && (f.Name=="op_Equality" || f.Name=="op_Inequality")) {
    il.Emit(OpCodes.Ldarg_0);il.Emit(OpCodes.Ldarg_1);il.Emit(OpCodes.Ceq);
    if(f.Name=="op_Inequality"){il.Emit(OpCodes.Ldc_I4_0);il.Emit(OpCodes.Ceq);}
   } else if(t.FullName=="UnityEngine.Object" && f.Name=="op_Implicit") {
    il.Emit(OpCodes.Ldarg_0);il.Emit(OpCodes.Ldnull);il.Emit(OpCodes.Cgt_Un);
   } else if(f.ReturnType.FullName!="System.Void") {
    var v=new VariableDefinition(f.ReturnType);f.Body.Variables.Add(v);
    il.Emit(OpCodes.Ldloca,v);il.Emit(OpCodes.Initobj,f.ReturnType);il.Emit(OpCodes.Ldloc,v);
   }
   il.Emit(OpCodes.Ret);
  }
  m.Write(a[1]);
 }
}
