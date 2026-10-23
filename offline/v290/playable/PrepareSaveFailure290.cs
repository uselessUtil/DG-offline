using System;using System.Linq;using Mono.Cecil;using Mono.Cecil.Cil;
class PrepareSaveFailure290 {
 static void Main(string[] a) {
  var m=ModuleDefinition.ReadModule(a[0]);
  var t=new TypeDefinition("","SaveFailureControl290",TypeAttributes.Public|TypeAttributes.Abstract|TypeAttributes.Sealed,m.TypeSystem.Object);
  var flag=new FieldDefinition("Fail",FieldAttributes.Public|FieldAttributes.Static,m.TypeSystem.Boolean);t.Fields.Add(flag);m.Types.Add(t);
  var save=m.Types.Single(x=>x.Name=="ServerForgery").Methods.Single(x=>x.Name=="UserParamSave");
  var il=save.Body.GetILProcessor();var first=save.Body.Instructions[0];
  il.InsertBefore(first,Instruction.Create(OpCodes.Ldsfld,flag));il.InsertBefore(first,Instruction.Create(OpCodes.Brfalse,first));
  il.InsertBefore(first,Instruction.Create(OpCodes.Ldnull));il.InsertBefore(first,Instruction.Create(OpCodes.Throw));
  m.Write(a[1]);
 }
}
