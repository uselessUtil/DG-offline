using System;using System.Linq;using Mono.Cecil;using Mono.Cecil.Cil;
public static partial class PatchPlayable291 {
 static void QuestRewards291() {
  CallHelper("MasterDataUtil","GetGuerrillaBossParamFromQuestID","PermanentInvasion291",1);
  var pickup=Method("MasterDataUtil","GetQuestPickupUnit");
  foreach(var ret in pickup.Body.Instructions.Where(i=>i.OpCode==OpCodes.Ret).ToArray()) {
   ret.OpCode=OpCodes.Ldarg_0;var pi=pickup.Body.GetILProcessor();
   var expand=Instruction.Create(OpCodes.Call,Helper("PermanentInvasionPickup291"));
   pi.InsertAfter(ret,expand);pi.InsertAfter(expand,Instruction.Create(OpCodes.Ret));
  }
  var m=Method("ServerForgery","CreateQuestBuildBattle");var il=m.Body.GetILProcessor();
  var rate=m.Body.Instructions.Single(i=>i.OpCode==OpCodes.Ldfld && ((FieldReference)i.Operand).Name=="drop_unit_rate");
  var signed=rate.Next;var roll=signed.Next;var unsigned=roll.Next;var branch=unsigned.Next;
  if(signed.OpCode!=OpCodes.Conv_I8 || unsigned.OpCode!=OpCodes.Conv_U8 || branch.OpCode!=OpCodes.Bge ||
    (rate.Previous.OpCode!=OpCodes.Ldloc_S && rate.Previous.OpCode!=OpCodes.Ldloc) ||
    (roll.OpCode!=OpCodes.Ldloc_S && roll.OpCode!=OpCodes.Ldloc))throw new Exception("Drop probability contract changed");
  var enemy=(VariableDefinition)rate.Previous.Operand;var failure=(Instruction)branch.Operand;
  rate.OpCode=OpCodes.Nop;rate.Operand=null;signed.OpCode=OpCodes.Nop;unsigned.OpCode=OpCodes.Nop;
  branch.OpCode=OpCodes.Call;branch.Operand=Helper("QuestDrop291");
  il.InsertAfter(branch,Instruction.Create(OpCodes.Brfalse,failure));

  var plus=m.Body.Instructions.Single(i=>i.OpCode==OpCodes.Stfld && ((FieldReference)i.Operand).FullName=="System.Int32 ServerDataDefine.PacketStructQuestBuildDrop::plus_pow");
  if(plus.Previous.OpCode!=OpCodes.Ldc_I4_0 || (plus.Previous.Previous.OpCode!=OpCodes.Ldloc_S && plus.Previous.Previous.OpCode!=OpCodes.Ldloc))
   throw new Exception("Drop plus contract changed");
  var drop=(VariableDefinition)plus.Previous.Previous.Operand;var tail=plus;
  foreach(var i in new[]{Instruction.Create(OpCodes.Ldloc,drop),Instruction.Create(OpCodes.Ldloc,enemy),Instruction.Create(OpCodes.Call,Helper("QuestDropPlus291"))}) {
   il.InsertAfter(tail,i);tail=i;
  }
  // DeadEnemy previously stored literal0 in the native acquisition record.
  // Replace that one level initializer; keep drop identity/pluses, coin/EXP,
  // visuals, anti-duplicate state and original settlement APIs intact.
  var death=Method("BattleManager","DeadEnemy");var di=death.Body.GetILProcessor();
  var acquire=death.Body.Instructions.Single(i=>i.OpCode==OpCodes.Callvirt &&
   i.Operand is MethodReference && ((MethodReference)i.Operand).Name=="AddAcquireUnitParam");
  var levelLoad=acquire.Previous.Previous.Previous.Previous;
  if((levelLoad.OpCode!=OpCodes.Ldloc_S && levelLoad.OpCode!=OpCodes.Ldloc) ||
   ((VariableDefinition)levelLoad.Operand).VariableType.FullName!="System.Int32")
   throw new Exception("Drop acquisition level argument contract changed");
  var level=(VariableDefinition)levelLoad.Operand;
  var assign=death.Body.Instructions.Single(i=>(i.OpCode==OpCodes.Stloc_S || i.OpCode==OpCodes.Stloc) && i.Operand==level);
  var zero=assign.Previous;
  if(zero.OpCode!=OpCodes.Ldc_I4_0)throw new Exception("Drop level is no longer literal0");
  var defeated=death.Body.Variables.Single(v=>v.VariableType.Name=="BattleEnemy");
  var deathDrop=death.Body.Variables.Single(v=>v.VariableType.Name=="PacketStructQuestBuildDrop");
  zero.OpCode=OpCodes.Ldloc;zero.Operand=defeated;
  var dropArg=Instruction.Create(OpCodes.Ldloc,deathDrop);di.InsertAfter(zero,dropArg);
  di.InsertAfter(dropArg,Instruction.Create(OpCodes.Call,Helper("QuestDropLevel291")));
  // Patch after LegacyQuestLayout291 so the saved original star selects
  // invasions before question-mark masking. Bosses and chained battles keep
  // their native groups; only the generic enemy-cell call is intercepted.
  var build=Method("ServerForgery","CreateQuestBuild");var bi=build.Body.GetILProcessor();
  var invasion=build.Body.Variables.Single(v=>v.VariableType.FullName=="MasterDataGuerrillaBoss");
  var layout=build.Body.Variables.Single(v=>v.VariableType.FullName=="LegacyFloor291");
  var call=build.Body.Instructions.Single(i=>i.OpCode==OpCodes.Call && i.Operand is MethodReference &&
   ((MethodReference)i.Operand).Name=="CreateQuestBuildBattle" && i.Previous.OpCode==OpCodes.Ldelem_Ref);
  if(call.Previous.Previous.OpCode!=OpCodes.Ldloc_S && call.Previous.Previous.OpCode!=OpCodes.Ldloc)throw new Exception("Enemy star lookup changed");
  var star=(VariableDefinition)call.Previous.Previous.Operand;
  foreach(var i in new[]{Instruction.Create(OpCodes.Ldloc,invasion),Instruction.Create(OpCodes.Ldloc,star),
   Instruction.Create(OpCodes.Ldloc,layout),Instruction.Create(OpCodes.Call,Helper("QuestInvasion291"))})bi.InsertBefore(call,i);
 }
}
