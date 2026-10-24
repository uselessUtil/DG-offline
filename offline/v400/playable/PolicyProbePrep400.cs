using System;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
class PolicyProbePrep400 {
 static MethodDefinition Method(ModuleDefinition m,string type,string name) {
  return m.Types.Single(t=>t.Name==type).Methods.Single(f=>f.Name==name);
 }
 static void Require(bool value,string text) {if(!value)throw new Exception(text);}
 static string Body(MethodDefinition m) {return String.Join("\n",m.Body.Instructions.Select(i=>i.ToString()).ToArray());}
 static void Main(string[] args) {
  if(args[0]=="prepare") {
   var game=ModuleDefinition.ReadModule(args[1]);var io=ModuleDefinition.ReadModule(args[3]);
   foreach(var pair in new[]{new[]{"MainMenuManager","CheckMenuControlNG","false"},new[]{"LayoutSwitchSeq","LayoutSwitchUpdate","true"},
     new[]{"RemoteNotificationManager","UpdateProcess","false"},new[]{"Dialog","Close","void"},new[]{"SoundUtil","StopBGM","void"},
     new[]{"LocalSaveUtil","ExecDataSave","void"}}) {
    var method=Method(game,pair[0],pair[1]);method.Body=new MethodBody(method);var il=method.Body.GetILProcessor();
    if(pair[2]!="void")il.Emit(pair[2]=="true"?OpCodes.Ldc_I4_1:OpCodes.Ldc_I4_0);
    il.Emit(OpCodes.Ret);
   }
   foreach(var pair in new[]{new[]{"SceneManager","RequestLoad","Scene"},new[]{"TitleMenuManager","AddSwitchRequest","Page"},new[]{"DialogManager","Open1B","Open"}}) {
    var methods=game.Types.Single(t=>t.Name==pair[0]).Methods.Where(m=>m.Name==pair[1]);
    var method=pair[2]=="Open"?methods.Single(m=>m.Parameters.Count==5 && m.Parameters[0].ParameterType.FullName=="System.String"):methods.Single();
    method.Body=new MethodBody(method);var il=method.Body.GetILProcessor();
    foreach(var p in method.Parameters)il.Emit(OpCodes.Ldarg,p);
    il.Emit(OpCodes.Call,game.ImportReference(Method(io,"PolicyProbeIO400",pair[2])));il.Emit(OpCodes.Ret);
   }
   var getter=Method(game,"LocalSaveManager","LoadFuncInformationPolicyOK");
   getter.Body.GetILProcessor().InsertBefore(getter.Body.Instructions.First(),Instruction.Create(OpCodes.Call,game.ImportReference(Method(io,"PolicyProbeIO400","ReadPolicy"))));
   game.Write(args[2]);return;
  }
  var original=ModuleDefinition.ReadModule(args[1]);var patched=ModuleDefinition.ReadModule(args[2]);
  // Normalize only the specified policy call replacement; every other instruction,
  // branch target and dialog/update-counter path must match the original method.
  foreach(var pair in new[]{new[]{"TitleMenuSeqSwitchPage","Update"},new[]{"MainMenuSeq","PageSwitchUpdate"}}) {
   var before=Method(original,pair[0],pair[1]);var after=Method(patched,pair[0],pair[1]);
   var source=before.Body.Instructions.Single(i=>i.Operand is MethodReference && ((MethodReference)i.Operand).Name=="LoadFuncInformationPolicyOK");
   var index=before.Body.Instructions.IndexOf(source);
   Require(after.Body.Instructions.Count==before.Body.Instructions.Count+1,"Unexpected gate edits");
   var pop=after.Body.Instructions[index];var constant=pop.Next;
   Require(pop.OpCode==OpCodes.Pop && constant.OpCode==OpCodes.Ldc_I4_2,"Policy boundary missing");
   pop.OpCode=source.OpCode;pop.Operand=patched.ImportReference((MethodReference)source.Operand);
   after.Body.GetILProcessor().Remove(constant);
   // Recompute offsets after normalization via an in-memory round trip.
  }
  var stream=new System.IO.MemoryStream();patched.Write(stream);stream.Position=0;var normalized=ModuleDefinition.ReadModule(stream);
  foreach(var pair in new[]{new[]{"TitleMenuSeqSwitchPage","Update"},new[]{"MainMenuSeq","PageSwitchUpdate"},
    new[]{"LocalSaveManager","LoadFuncInformationPolicyOK"},new[]{"LocalSaveManager","SaveFuncInformationPolicyOK"},
    new[]{"LocalSaveManager","LoadFuncInformationOK"},new[]{"MainMenuSeq","RequestPatchUpdate"},new[]{"MainMenuSeq","OnApplicationPause"},
    new[]{"TitleMenuSeqInformationPolicy","Update"}})
   Require(Body(Method(original,pair[0],pair[1]))==Body(Method(normalized,pair[0],pair[1])),"Unrelated policy/menu method changed: "+String.Join(".",pair));
  Console.WriteLine("PASS: exactly one policy read replaced in each of title and in-menu gates; all other gate instructions, update checks, dialog/title handling, resume events and stored policy accessors match original.");
 }
}
