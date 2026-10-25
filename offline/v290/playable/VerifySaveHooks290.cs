using System;using System.Linq;using Mono.Cecil;using Mono.Cecil.Cil;
class VerifySaveHooks290 {
 static void Check(bool b,string s){if(!b)throw new Exception(s);}
 static void Main(string[] a){
  var game=ModuleDefinition.ReadModule(a[0]);var adapter=ModuleDefinition.ReadModule(a[1]);
  var others=game.Types.Single(t=>t.Name=="MainMenuSeqOthers");
  var input=others.Methods.Single(m=>m.Name=="WindowUpdateTransfer");
  var calls=input.Body.Instructions.Where(i=>i.Operand is MethodReference).Select(i=>(MethodReference)i.Operand).ToArray();
  Check(calls.Length==1 && calls[0].Name=="SaveTransferInput","Original online transfer path retained");
  var update=others.Methods.Single(m=>m.Name=="Update").Body.Instructions;
  int gate=update.ToList().FindIndex(i=>i.Operand is MethodReference && ((MethodReference)i.Operand).Name=="PageSwitchUpdate");
  int backup=update.ToList().FindIndex(i=>i.Operand is MethodReference && ((MethodReference)i.Operand).Name=="UpdateSaveTransferUI");
  Check(gate>=0 && backup>gate,"Save UI bypasses original page-ready gate");
  var load=game.Types.Single(t=>t.Name=="ServerForgery").Methods.Single(m=>m.Name=="UserParamLoad").Body.Instructions;
  Check(load[1].Operand is MethodReference && ((MethodReference)load[1].Operand).Name=="RecoverRestoreRetirement290" &&
        load[3].Operand is MethodReference && ((MethodReference)load[3].Operand).Name=="BeforeSaveAccountLoad","Retirement/import journal recovery must precede account load");
  var type=adapter.Types.Single(t=>t.Name=="DivineGateOffline");
  var rename=type.Methods.Single(m=>m.Name=="RenameSaveFile");
  Check(rename.IsPInvokeImpl && rename.PInvokeInfo.EntryPoint=="rename" && rename.PInvokeInfo.Module.Name=="libc","Atomic file commit binding");
  string[] strings=type.Methods.Single(m=>m.Name=="SaveTransferInput").Body.Instructions.Where(i=>i.OpCode==OpCodes.Ldstr).Select(i=>(string)i.Operand).ToArray();
  Check(strings.Contains("ButtonTransferBackup") && strings.Contains("ButtonTransferInfomation"),"Wrong 2.9 input nodes");
  Check(!strings.Contains("ButtonTransferOrder") && !strings.Contains("ButtonTransferExec"),"Legacy input nodes retained");
  Console.WriteLine("PASS actual APK hooks: Others ready-gate retained, online transfer input fully replaced, 2.9 button names, journal recovery before account load, libc atomic rename.");
 }
}
