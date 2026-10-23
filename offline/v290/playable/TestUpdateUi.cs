// Instrument presentation endpoints only; retain original update check and PlayerPrefs logic.
using System;using System.Linq;using Mono.Cecil;using Mono.Cecil.Cil;
class TestUpdateUi {
 static void Main(string[] a) {
  var game=ModuleDefinition.ReadModule(a[0]);var support=ModuleDefinition.ReadModule(a[1]);
  string[,] hooks={{"DialogManager","Open1B","Open","4"},{"MainMenuManager","AddSwitchRequest","MainRoute","3"},{"TitleMenuManager","AddSwitchRequest","TitleRoute","2"},{"ResidentResourceManager","LoadResidentResourceRequest","Resident","0"}};
  foreach(var i in Enumerable.Range(0,hooks.GetLength(0))) {
   var m=game.Types.Single(t=>t.Name==hooks[i,0]).Methods.Single(x=>x.Name==hooks[i,1] && x.Parameters.Count==int.Parse(hooks[i,3]));
   var target=support.Types.Single(t=>t.Name=="UpdateUiSupport").Methods.Single(x=>x.Name==hooks[i,2]);
   if(m.ReturnType.FullName!=target.ReturnType.FullName)throw new Exception("UI return contract");
   m.Body=new MethodBody(m);var il=m.Body.GetILProcessor();
   foreach(var p in m.Parameters)il.Emit(OpCodes.Ldarg,p);
   il.Emit(OpCodes.Call,game.ImportReference(target));il.Emit(OpCodes.Ret);
  }
  game.Write(a[2]);
 }
}
