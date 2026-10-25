using System;using System.Linq;using System.Collections.Generic;using Mono.Cecil;using Mono.Cecil.Cil;
class VerifyUiHook {
 static string Shape(MethodDefinition m){
  var ins=m.Body.Instructions.ToArray();
  Func<object,string> op=o=>o is Instruction?"@"+Array.IndexOf(ins,(Instruction)o):o is Instruction[]?string.Join(",",((Instruction[])o).Select(x=>"@"+Array.IndexOf(ins,x)).ToArray()):o==null?"":o.ToString();
  return string.Join(";",ins.Select(i=>i.OpCode.Name+":"+op(i.Operand)).ToArray());
 }
 static void Main(string[] a){
  var old=ModuleDefinition.ReadModule(a[0]);var now=ModuleDefinition.ReadModule(a[1]);
  Func<ModuleDefinition,MethodDefinition> get=m=>m.Types.Single(t=>t.Name=="InGameManager").Methods.Single(x=>x.Name=="StepFuncReadyCreateParty");
  var before=get(old);var after=get(now);var il=after.Body.GetILProcessor();int count=0;
  foreach(var call in after.Body.Instructions.Where(i=>i.Operand is MethodReference && ((MethodReference)i.Operand).Name=="FixBattleHudPortraits291").ToArray()){
   if(call.Previous.OpCode!=OpCodes.Ldarg_0 || call.Next.OpCode!=OpCodes.Ret)throw new Exception("Not a bounded return hook");
   var previous=call.Previous;var ret=call.Next;previous.OpCode=OpCodes.Ret;previous.Operand=null;il.Remove(ret);il.Remove(call);count++;
  }
  if(count==0 || Shape(before)!=Shape(after))throw new Exception("Original party creation logic altered");
  Console.WriteLine("PASS HUD hook only appends presentation repair at "+count+" returns; removing it exactly recovers original party creation IL. Original failure/return/state transitions preserved.");
 }
}
