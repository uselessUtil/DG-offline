using System;using System.Linq;using Mono.Cecil;using Mono.Cecil.Cil;
class PrepareImportFailure290 {
 static void Main(string[] a){
  var m=ModuleDefinition.ReadModule(a[0]);
  var t=new TypeDefinition("","BackupFault290",TypeAttributes.Public|TypeAttributes.Abstract|TypeAttributes.Sealed,m.TypeSystem.Object);
  var count=new FieldDefinition("FailLoads",FieldAttributes.Public|FieldAttributes.Static,m.TypeSystem.Int32);t.Fields.Add(count);m.Types.Add(t);
  var load=m.Types.Single(x=>x.Name=="ServerForgery").Methods.Single(x=>x.Name=="UserParamLoad");
  var il=load.Body.GetILProcessor();var first=load.Body.Instructions[0];
  il.InsertBefore(first,Instruction.Create(OpCodes.Ldsfld,count));il.InsertBefore(first,Instruction.Create(OpCodes.Brfalse,first));
  il.InsertBefore(first,Instruction.Create(OpCodes.Ldsfld,count));il.InsertBefore(first,Instruction.Create(OpCodes.Ldc_I4_1));
  il.InsertBefore(first,Instruction.Create(OpCodes.Sub));il.InsertBefore(first,Instruction.Create(OpCodes.Stsfld,count));
  il.InsertBefore(first,Instruction.Create(OpCodes.Ldc_I4_0));il.InsertBefore(first,Instruction.Create(OpCodes.Ret));m.Write(a[1]);
 }
}
