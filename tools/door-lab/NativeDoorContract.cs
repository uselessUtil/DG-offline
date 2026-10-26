using System;using System.Linq;using Mono.Cecil;
class NativeDoorContract {
 static void Main(string[] args){
  var m=ModuleDefinition.ReadModule(args[0]);
  foreach(var t in m.Types){
   if(t.IsEnum && t.Name.ToLowerInvariant().Contains("door"))foreach(var f in t.Fields.Where(f=>f.HasConstant))Console.WriteLine(t.Name+" "+f.Name+"="+f.Constant);
   foreach(var f in t.Methods.Where(f=>f.HasBody&&(f.Name=="DoorOpen"||t.Name=="AnimationArray"||f.Name=="StepFuncInitFloorCreate"))){Console.WriteLine("METHOD "+f.FullName);foreach(var i in f.Body.Instructions)Console.WriteLine(i);}
  }
 }
}
