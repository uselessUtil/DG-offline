// Read-only contract checks and I/O changes confined to the disposable probe.
using System;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
class BadgeProbePrep400 {
 static MethodDefinition Method(ModuleDefinition m,string type,string name) {
  return m.Types.Single(t=>t.Name==type).Methods.Single(f=>f.Name==name);
 }
 static void Require(bool value,string text) {if(!value)throw new Exception(text);}
 static string Body(MethodDefinition method) {
  return String.Join("\n",method.Body.Instructions.Select(i=>i.ToString()).ToArray());
 }
 static void Main(string[] args) {
  if(args[0]=="io") {
   var game=ModuleDefinition.ReadModule(args[1]);var io=ModuleDefinition.ReadModule(args[3]);
   foreach(string name in new[]{"GetValueString","GetValueInt","SetValueString","SetValueInt"}) {
    var target=Method(game,"LocalSaveUtil",name);var bridge=Method(io,"BadgeProbeIO400",name);
    Require(target.Parameters.Count==2 && bridge.Parameters.Count==2,"I/O signature changed");
    target.Body=new MethodBody(target);var il=target.Body.GetILProcessor();
    il.Emit(OpCodes.Ldarg_0);il.Emit(OpCodes.Ldarg_1);il.Emit(OpCodes.Call,game.ImportReference(bridge));il.Emit(OpCodes.Ret);
   }
   game.Write(args[2]);return;
  }
  var original=ModuleDefinition.ReadModule(args[1]);var patched=ModuleDefinition.ReadModule(args[2]);
  var before=Method(original,"MainMenuPartsFooterChildMenuList","UpdateNewLabel").Body.Instructions;
  var after=Method(patched,"MainMenuPartsFooterChildMenuList","UpdateNewLabel").Body.Instructions;
  Require(before.Count==after.Count,"Badge algorithm instruction count changed");
  int changed=0;
  for(int i=0;i<before.Count;i++) {
   if(before[i].ToString()==after[i].ToString())continue;
   var field=before[i].Operand as FieldReference;var call=after[i].Operand as MethodReference;
   Require(before[i].OpCode==OpCodes.Ldfld && field!=null && field.DeclaringType.Name=="PacketStructLoginMonthly" && field.Name=="login_list","Unrelated badge edit");
   Require(after[i].OpCode==OpCodes.Call && call!=null && call.Name=="FooterBadgeDates400","Wrong safe getter");changed++;
  }
  Require(changed==1,"Expected one badge field-read replacement");
  foreach(var pair in new[]{new[]{"MainMenuManager","PageSwitchSeqNewFadeIn"},new[]{"MainMenuSeqResultServerSend","Update"},
    new[]{"MainMenuSeqResultServerSend","PageSwitchSetting"},new[]{"MainMenuSeqResultInfo","PageSwitchSetting"},
    new[]{"ServerForgery","ForgeryQuestClear"},new[]{"ServerForgery","UserParamSave"}})
   Require(Body(Method(original,pair[0],pair[1]))==Body(Method(patched,pair[0],pair[1])),"Result/save method changed: "+pair[0]+"."+pair[1]);
  Console.WriteLine("PASS: exactly one badge-array read replaced; original result API, reward/save, result-page setup and fade-in methods unchanged.");
 }
}
