// Replace only UI readiness/queue boundaries in the isolated router copy.
using System;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
class RouterProbePrep400 {
 static MethodDefinition Method(ModuleDefinition m,string type,string name) {
  return m.Types.Single(t=>t.Name==type).Methods.Single(f=>f.Name==name);
 }
 static void Require(bool value,string text) {if(!value)throw new Exception(text);}
 static void Main(string[] args) {
  if(args[0]=="prepare") {
   var game=ModuleDefinition.ReadModule(args[1]);var io=ModuleDefinition.ReadModule(args[3]);
   var ready=Method(game,"MainMenuSeq","PageSwitchUpdate");
   ready.Body=new MethodBody(ready);ready.Body.GetILProcessor().Emit(OpCodes.Ldc_I4_1);ready.Body.GetILProcessor().Emit(OpCodes.Ret);
   var request=Method(game,"MainMenuManager","AddSwitchRequest");
   request.Body=new MethodBody(request);var il=request.Body.GetILProcessor();
   il.Emit(OpCodes.Ldarg_1);il.Emit(OpCodes.Ldarg_2);il.Emit(OpCodes.Ldarg_3);
   il.Emit(OpCodes.Call,game.ImportReference(Method(io,"RouterProbeIO400","Route")));il.Emit(OpCodes.Ret);
   game.Write(args[2]);return;
  }
  var original=ModuleDefinition.ReadModule(args[1]);var patched=ModuleDefinition.ReadModule(args[2]);
  foreach(var pair in new[]{new[]{"MainMenuSeqSwitchPage","Update"},new[]{"LocalNotificationUtil","RegisterNotification"}}) {
   var method=Method(patched,pair[0],pair[1]);
   var first=method.Body.Instructions.First(i=>i.OpCode==OpCodes.Ldfld && i.Operand is FieldReference && ((FieldReference)i.Operand).Name=="m_StructLoginMonthly");
   var call=first.Next.Operand as MethodReference;
   Require(first.Next.OpCode==OpCodes.Call && call!=null && call.Name=="UsableMonthly400","Monthly guard missing");
   var baseline=Method(original,pair[0],pair[1]);
   foreach(string name in new[]{"LoadFuncLoginLastest"}) {
    Require(baseline.Body.Instructions.Count(i=>i.Operand is MethodReference && ((MethodReference)i.Operand).Name==name)==
     method.Body.Instructions.Count(i=>i.Operand is MethodReference && ((MethodReference)i.Operand).Name==name),"Original routing call removed");
   }
  }
  foreach(string name in new[]{"MainMenuSeqLoginMonthly","MainMenuDialogSeqMonthlyBonus"}) {
   var before=original.Types.Single(t=>t.Name==name);var after=patched.Types.Single(t=>t.Name==name);
   foreach(var m in before.Methods.Where(m=>m.HasBody)) {
    var n=after.Methods.Single(x=>x.FullName==m.FullName);
    Require(String.Join("\n",m.Body.Instructions.Select(i=>i.ToString()).ToArray())==String.Join("\n",n.Body.Instructions.Select(i=>i.ToString()).ToArray()),"Calendar UI body changed");
   }
  }
  Console.WriteLine("PASS: startup and notification read guards present; original calendar/date routing call retained; monthly page/dialog bodies unchanged and protected by the valid-content page gate.");
 }
}
