using System;using System.Linq;using System.Collections.Generic;using System.Web.Script.Serialization;using Mono.Cecil;
class SerializedTypes {
 static void Main(string[] a){
 var result=new Dictionary<string,object>();var resolver=new DefaultAssemblyResolver();resolver.AddSearchDirectory(a[0]);
 foreach(string asm in new[]{"Assembly-CSharp.dll","Assembly-CSharp-firstpass.dll"}){
  var m=ModuleDefinition.ReadModule(System.IO.Path.Combine(a[0],asm),new ReaderParameters{AssemblyResolver=resolver});
  foreach(var t in m.Types.SelectMany(Flatten)){
   if(t.IsEnum){result[t.FullName]=new{kind="enum",fields=new object[0]};continue;}
   var fs=t.Fields.Where(f=>!f.IsStatic&&!f.IsLiteral&&!f.IsInitOnly&&!f.IsNotSerialized &&
     (f.IsPublic || f.CustomAttributes.Any(c=>c.AttributeType.Name=="SerializeField"))).Select(f=>new{name=f.Name,type=f.FieldType.FullName}).ToArray();
   result[t.FullName]=new{kind=t.IsValueType?"struct":"class",serializable=t.IsSerializable,baseType=t.BaseType==null?null:t.BaseType.FullName,fields=fs};
  }
 }
 Console.WriteLine(new JavaScriptSerializer{MaxJsonLength=int.MaxValue}.Serialize(result));
 }
 static IEnumerable<TypeDefinition> Flatten(TypeDefinition t){yield return t;foreach(var n in t.NestedTypes)foreach(var x in Flatten(n))yield return x;}
}
