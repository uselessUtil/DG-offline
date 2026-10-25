using System;using System.Linq;using Mono.Cecil;using Mono.Cecil.Cil;
public static partial class PatchPlayable291 {
 static void LegacyQuestLayout291() {
  var m=Method("ServerForgery","CreateQuestBuild");
  var body=m.Body;var il=body.GetILProcessor();
  Func<string,VariableDefinition> local=name=>body.Variables.Single(v=>v.VariableType.FullName==name);
  var panels=local("ServerDataDefine.PacketStructQuestBuildPanelDetail[]");
  var category=local("MasterDataCategoryPattern");
  var expect=local("MasterDataExpectPattern");
  var floor=local("MasterDataQuestFloor");
  var constructors=body.Instructions.Where(i=>i.OpCode==OpCodes.Newobj && ((MethodReference)i.Operand).DeclaringType.FullName=="ServerDataDefine.PacketStructQuestBuildBattle").ToArray();
  if(constructors.Length!=2)throw new Exception("Unexpected tutorial/normal quest builder shape");
  // The last null-battle sentinel immediately follows the generic layout loop.
  // Retarget the instruction itself so incoming branches also execute our hook.
  var point=constructors[1];var ctor=(MethodReference)point.Operand;
  var apply=Helper("ApplyLegacyFloor291");
  var state=new VariableDefinition(apply.ReturnType);body.Variables.Add(state);
  point.OpCode=OpCodes.Ldloc;point.Operand=panels;
  var seq=new[]{Instruction.Create(OpCodes.Ldloc,category),Instruction.Create(OpCodes.Ldloc,expect),
   Instruction.Create(OpCodes.Call,apply),Instruction.Create(OpCodes.Stloc,state),Instruction.Create(OpCodes.Newobj,ctor)};
  var tail=point;foreach(var i in seq){il.InsertAfter(tail,i);tail=i;}
  var store=body.Instructions.Last(i=>i.OpCode==OpCodes.Stfld && ((FieldReference)i.Operand).FullName=="ServerDataDefine.PacketStructQuestBuildPanelDetail[] ServerDataDefine.PacketStructQuestBuildPanel::panel_detail");
  tail=store;foreach(var i in new[]{Instruction.Create(OpCodes.Ldloc,state),Instruction.Create(OpCodes.Ldloc,panels),Instruction.Create(OpCodes.Ldloc,floor),Instruction.Create(OpCodes.Call,Helper("FinishLegacyFloor291"))}){il.InsertAfter(tail,i);tail=i;}
 }
}
