using System;using System.IO;using LitJson;
class LayoutOracle106 {
 static void Main(string[] a){
  var cs=JsonMapper.ToObject<MasterDataCategoryPattern[]>(File.ReadAllText(a[0]));
  var es=JsonMapper.ToObject<MasterDataExpectPattern[]>(File.ReadAllText(a[1]));
  var field=new FieldManager();int count=0;
  using(var w=new BinaryWriter(File.Create(a[2])))foreach(var c in cs)foreach(var e in es)for(uint s=0;s<32;s++){
   uint seed=unchecked(s*2654435761u);
   var result=field.AuditOriginalLayout(c,e,seed);
   w.Write((uint)c.fix_id);w.Write((uint)e.fix_id);w.Write(seed);
   for(int i=0;i<25;i++)w.Write(result[0][i]);for(int i=0;i<25;i++)w.Write(result[1][i]);count++;
  }
  Console.WriteLine("Original APK IL oracle cases="+count);
 }
}