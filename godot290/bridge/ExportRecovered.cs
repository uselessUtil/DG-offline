using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
class ExportRecovered {
 static void Main(string[] args) {
  var type=Assembly.LoadFrom(args[0]).GetType("RecoveredImages",true);
  var files=(Dictionary<string,int>)type.GetField("files",BindingFlags.NonPublic|BindingFlags.Static).GetValue(null);
  var read=type.GetMethod("Read",BindingFlags.Public|BindingFlags.Static);
  foreach(var item in files) {
   string target=Path.GetFullPath(Path.Combine(args[1],item.Key+".png"));
   if(!target.StartsWith(Path.GetFullPath(args[1])+Path.DirectorySeparatorChar))throw new Exception("Invalid asset path");
   Directory.CreateDirectory(Path.GetDirectoryName(target));
   File.WriteAllBytes(target,(byte[])read.Invoke(null,new object[]{item.Key}));
  }
  Console.WriteLine("Extracted "+files.Count+" original embedded images");
 }
}
