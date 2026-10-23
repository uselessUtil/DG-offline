using System;using System.Linq;using Mono.Cecil;
class CompileApi210 { static void Main(string[] a){var m=ModuleDefinition.ReadModule(a[0]);var t=m.Types.Single(x=>x.Name=="StatusAilmentUtil");if(!t.IsNotPublic)throw new Exception("Unexpected 2.1.0 status utility visibility");t.IsPublic=true;m.Write(a[1]);}}
