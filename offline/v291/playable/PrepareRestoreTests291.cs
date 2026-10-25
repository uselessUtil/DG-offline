// Temporary test DLL only: record UI navigation and inject one save failure.
using System;using System.Linq;using Mono.Cecil;using Mono.Cecil.Cil;
class PrepareRestoreTests291 {
 static void Main(string[] a) {
  var m=ModuleDefinition.ReadModule(a[0]);
  var t=new TypeDefinition("","RestoreProbe291",TypeAttributes.Public|TypeAttributes.Abstract|TypeAttributes.Sealed,m.TypeSystem.Object);
  m.Types.Add(t);
  foreach(string name in new[]{"Switches","Retires","FailSaves"})t.Fields.Add(new FieldDefinition(name,FieldAttributes.Public|FieldAttributes.Static,m.TypeSystem.Int32));
  Action<ILProcessor,Instruction,string> bump=(il,at,name)=>{
   var f=t.Fields.Single(x=>x.Name==name);il.InsertBefore(at,Instruction.Create(OpCodes.Ldsfld,f));
   il.InsertBefore(at,Instruction.Create(OpCodes.Ldc_I4_1));il.InsertBefore(at,Instruction.Create(OpCodes.Add));
   il.InsertBefore(at,Instruction.Create(OpCodes.Stsfld,f));
  };
  var navigation=m.GetType("TitleMenuManager").Methods.Single(x=>x.Name=="AddSwitchRequest");
  navigation.Body=new MethodBody(navigation);var n=navigation.Body.GetILProcessor();
  n.Emit(OpCodes.Ldc_I4_1);n.Emit(OpCodes.Ret);bump(n,navigation.Body.Instructions.First(),"Switches");
  var retire=m.GetType("ServerForgery").Methods.Single(x=>x.Name=="ForgeryQuestRetire");
  bump(retire.Body.GetILProcessor(),retire.Body.Instructions.First(),"Retires");
  var save=m.GetType("ServerForgery").Methods.Single(x=>x.Name=="UserParamSave");
  var ilc=save.Body.GetILProcessor();var first=save.Body.Instructions.First();var fail=t.Fields.Single(x=>x.Name=="FailSaves");
  ilc.InsertBefore(first,Instruction.Create(OpCodes.Ldsfld,fail));ilc.InsertBefore(first,Instruction.Create(OpCodes.Brfalse,first));
  ilc.InsertBefore(first,Instruction.Create(OpCodes.Ldc_I4_0));ilc.InsertBefore(first,Instruction.Create(OpCodes.Stsfld,fail));
  ilc.InsertBefore(first,Instruction.Create(OpCodes.Ldnull));ilc.InsertBefore(first,Instruction.Create(OpCodes.Throw));
  m.Write(a[1]);
 }
}
