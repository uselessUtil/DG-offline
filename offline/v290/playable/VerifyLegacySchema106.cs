using System;using System.Linq;using System.Collections.Generic;using Mono.Cecil;
class VerifyLegacySchema106 {
 static Dictionary<string,string> Fields(TypeDefinition type) {
  return type.Fields.Where(f=>f.IsPublic && !f.IsStatic).ToDictionary(f=>f.Name,
   f=>f.FieldType.FullName.Replace("DGSave106Present","ServerDataDefine.PacketStructPresent"));
 }
 static void Same(TypeDefinition a,TypeDefinition b,bool subset) {
  var x=Fields(a);var y=Fields(b);
  if(!subset && x.Count!=y.Count)throw new Exception(a.Name+" field count mismatch");
  foreach(var f in x)if(!y.ContainsKey(f.Key) || y[f.Key]!=f.Value)throw new Exception(a.Name+" lost/changed "+f.Key);
 }
 static void Main(string[] args) {
  var source=ModuleDefinition.ReadModule(args[0]);var target=ModuleDefinition.ReadModule(args[1]);var adapter=ModuleDefinition.ReadModule(args[2]);
  Same(source.GetType("ServerForgerySaveUser"),adapter.GetType("DGSave106Account"),false);
  Same(source.GetType("ServerDataDefine.PacketStructPresent"),adapter.GetType("DGSave106Present"),false);
  Same(source.GetType("LocalSaveOption"),adapter.GetType("DGSave106Option"),false);
  foreach(string type in new[]{"ServerForgerySaveUser","ServerDataDefine.PacketStructUnit","ServerDataDefine.PacketStructPartyAssign",
   "ServerDataDefine.PacketStructPresent","LocalSaveOption","LocalSaveFriendUse"})Same(source.GetType(type),target.GetType(type),true);
  Console.WriteLine("PASS fixed donor APK schema: legacy DTO fields/types exact; every original account/unit/party/gift/option/friend-use field exists with identical type in target; new fields handled separately.");
 }
}
