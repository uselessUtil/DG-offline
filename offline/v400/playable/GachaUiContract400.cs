// Read-only contract check: preserve the native UI's load-completion and hits.
using System;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
class GachaUiContract400 {
 static MethodDefinition Method(ModuleDefinition m,string type,string name) {
  return m.Types.Single(t=>t.Name==type).Methods.Single(f=>f.Name==name);
 }
 static void Require(bool value,string message) { if(!value)throw new Exception(message); }
 static string Body(MethodDefinition method) {
  return String.Join("\n",method.Body.Instructions.Select(i=>i.ToString()).ToArray());
 }
 static void Hook(ModuleDefinition m,string type,string name,string helper) {
  var method=Method(m,type,name);
  var calls=method.Body.Instructions.Where(i=>i.OpCode==OpCodes.Call || i.OpCode==OpCodes.Callvirt).ToArray();
  Require(calls.Length==1,"Unexpected call count "+type+"."+name);
  var target=(MethodReference)calls[0].Operand;
  Require(target.DeclaringType.Name=="DivineGateOffline" && target.Name==helper,"Wrong hook "+name);
 }
 static void Main(string[] args) {
  var original=ModuleDefinition.ReadModule(args[0]);var patched=ModuleDefinition.ReadModule(args[1]);
  foreach(string name in new[]{"SetUpObjectLinker","SetUIEnable","CheckResourceLoad","IsFinishResouceLoad","CheckHitObject","ExecGachaPlay"})
   Require(Body(Method(original,"MainMenuPartsGachaItem",name))==Body(Method(patched,"MainMenuPartsGachaItem",name)),"Native card method changed: "+name);
  Require(Body(Method(original,"MainMenuSeqGachaList","PageSwitchSetting"))==Body(Method(patched,"MainMenuSeqGachaList","PageSwitchSetting")),"Native list creation changed");
  Hook(patched,"GachaResourceMaster","ResourceLoadRequest","ScratchRequest400");
  Hook(patched,"GachaResourceMaster","GetResourceTexture","ScratchTexture400");
  Hook(patched,"MainMenuPartsGachaItem","ExecDetail","ScratchDetail400");
  var update=Method(patched,"GachaResourceMaster","Update").Body.Instructions;
  Require(update.Count==1 && update[0].OpCode==OpCodes.Ret,"Remote loader is still active");
  var setup=Method(patched,"MainMenuPartsGachaItem","SetUpGachaData");
  foreach(var ret in setup.Body.Instructions.Where(i=>i.OpCode==OpCodes.Ret)) {
   var call=ret.Previous.Operand as MethodReference;
   Require(ret.Previous.OpCode==OpCodes.Call && call!=null && call.Name=="SetupScratchCard400","Card text hook missing from an exit");
  }
  var loading=Method(patched,"MainMenuPartsGachaItem","CheckResourceLoad");
  Require(loading.Body.Instructions.Any(i=>i.OpCode==OpCodes.Stfld && ((FieldReference)i.Operand).Name=="m_ResourceLoadFinish"),"Completion flag write missing");
  Console.WriteLine("PASS: native list creation, UI visibility, texture-completion flag, hit testing and draw-confirmation entry are unchanged; local resource and text hooks installed; remote loader update disabled.");
 }
}
