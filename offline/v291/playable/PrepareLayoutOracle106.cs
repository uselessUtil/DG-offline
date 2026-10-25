using System;using System.Linq;using System.Collections.Generic;using Mono.Cecil;using Mono.Cecil.Cil;
// Test-only: execute the layout instructions copied verbatim from the locked
// original 1.0.6 APK. No source reimplementation is used as the layout oracle.
class PrepareLayoutOracle106 {
 static void Main(string[] a) {
  var module=ModuleDefinition.ReadModule(a[0]);var t=module.Types.Single(x=>x.Name=="FieldManager");
  var old=t.Methods.Single(x=>x.Name=="CreateFieldArea");
  var slice=old.Body.Instructions.Where(i=>i.Offset>=0x459 && i.Offset<=0x9db).ToArray();
  if(slice.First().Offset!=0x459 || slice.Last().Offset!=0x9db || slice.Last().OpCode!=OpCodes.Stelem_I4)throw new Exception("Source APK IL contract changed");
  var method=new MethodDefinition("AuditOriginalLayout",MethodAttributes.Public,new ArrayType(new ArrayType(module.TypeSystem.Int32)));
  method.Parameters.Add(new ParameterDefinition(module.Types.Single(x=>x.Name=="MasterDataCategoryPattern")));
  method.Parameters.Add(new ParameterDefinition(module.Types.Single(x=>x.Name=="MasterDataExpectPattern")));
  method.Parameters.Add(new ParameterDefinition(module.TypeSystem.UInt32));
  t.Methods.Add(method);method.Body=new MethodBody(method){InitLocals=true};
  foreach(var v in old.Body.Variables)method.Body.Variables.Add(new VariableDefinition(v.VariableType));
  var il=method.Body.GetILProcessor();var vars=method.Body.Variables;
  if(vars[7].VariableType.FullName!="System.Int32" || vars[8].VariableType.FullName!="System.Int32[]" || vars[11].VariableType.Name!="MasterDataCategoryPattern" || vars[12].VariableType.Name!="MasterDataExpectPattern")throw new Exception("Source local contract changed");
  il.Emit(OpCodes.Ldc_I4,25);il.Emit(OpCodes.Stloc,vars[7]);
  foreach(int index in new[]{8,9}){il.Emit(OpCodes.Ldc_I4,100);il.Emit(OpCodes.Newarr,module.TypeSystem.Int32);il.Emit(OpCodes.Stloc,vars[index]);}
  il.Emit(OpCodes.Ldarg_1);il.Emit(OpCodes.Stloc,vars[11]);il.Emit(OpCodes.Ldarg_2);il.Emit(OpCodes.Stloc,vars[12]);
  il.Emit(OpCodes.Ldarg_0);il.Emit(OpCodes.Ldfld,t.Fields.Single(f=>f.Name=="m_FieldRand"));il.Emit(OpCodes.Ldarg_3);
  il.Emit(OpCodes.Callvirt,module.Types.Single(x=>x.Name=="Rand").Methods.Single(m=>m.Name=="SetRandSeed"));
  var copies=slice.ToDictionary(i=>i,i=>Instruction.Create(OpCodes.Nop));
  foreach(var i in slice) {
   var c=copies[i];c.OpCode=i.OpCode;
   if(i.Operand is Instruction)c.Operand=copies[(Instruction)i.Operand];
   else if(i.Operand is VariableDefinition)c.Operand=vars[((VariableDefinition)i.Operand).Index];
   else c.Operand=i.Operand;
   il.Append(c);
  }
  il.Emit(OpCodes.Ldc_I4_2);il.Emit(OpCodes.Newarr,new ArrayType(module.TypeSystem.Int32));
  il.Emit(OpCodes.Dup);il.Emit(OpCodes.Ldc_I4_0);il.Emit(OpCodes.Ldloc,vars[8]);il.Emit(OpCodes.Stelem_Ref);
  il.Emit(OpCodes.Dup);il.Emit(OpCodes.Ldc_I4_1);il.Emit(OpCodes.Ldloc,vars[9]);il.Emit(OpCodes.Stelem_Ref);il.Emit(OpCodes.Ret);
  module.Write(a[1]);Console.WriteLine("Copied "+slice.Length+" original 1.0.6 IL instructions; original Rand retained");
 }
}