using System;using System.IO;using System.Linq;using System.Collections.Generic;using Mono.Cecil;
class VerifyImagePackaging {
 static IEnumerable<TypeDefinition> All(TypeDefinition t){yield return t;foreach(var n in t.NestedTypes)foreach(var c in All(n))yield return c;}
 static long Data(TypeDefinition t){return t.Fields.Where(f=>f.IsStatic&&!f.IsLiteral).Sum(f=>(long)f.InitialValue.Length);}
 static void Main(string[] a){
  var old=ModuleDefinition.ReadModule(a[0]);var now=ModuleDefinition.ReadModule(a[1]);
  long before=Data(old.Types.Single(t=>t.Name=="RecoveredImages"));
  var image=now.Types.Single(t=>t.Name=="RecoveredImages");
  if(before!=797044173 || Data(image)!=0 || image.NestedTypes.Any(t=>t.Name.StartsWith("ImageBytes")))throw new Exception("PNG RVA blob retained");
  var rows=now.Types.SelectMany(t=>All(t)).Select(t=>new{t.FullName,Size=Data(t)}).OrderByDescending(t=>t.Size).ToArray();
  if(rows[0].Size>32*1024*1024 || new FileInfo(a[1]).Length>32*1024*1024)throw new Exception("Large static class/assembly regressed");
  Console.WriteLine("PASS signed image packaging: PNG RVA static bytes "+before+" ->0; adapter "+new FileInfo(a[1]).Length+" bytes; largest remaining RVA class "+rows[0].FullName+"="+rows[0].Size+" bytes; no nested PNG blob types.");
 }
}
