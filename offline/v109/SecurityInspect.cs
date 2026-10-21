using System;
using System.Linq;
using System.Collections.Generic;
using Mono.Cecil;
using Mono.Cecil.Cil;

// Read metadata and IL only; do not load application code into the CLR.
public static class SecurityInspect109
{
    static IEnumerable<TypeDefinition> Types(TypeDefinition t)
    {
        yield return t;
        foreach (var child in t.NestedTypes)
            foreach (var item in Types(child)) yield return item;
    }
    public static void Main(string[] args)
    {
        foreach (string path in args)
        {
            var module = ModuleDefinition.ReadModule(path);
            foreach (var method in module.Types.SelectMany(Types).SelectMany(t => t.Methods))
            {
                if (method.IsPInvokeImpl)
                    Console.WriteLine("pinvoke\t"+method.FullName+"\t"+method.PInvokeInfo.Module.Name+"\t"+method.PInvokeInfo.EntryPoint);
                if (!method.HasBody) continue;
                foreach (var i in method.Body.Instructions)
                {
                    var called = i.Operand as MethodReference;
                    if (called != null && (called.DeclaringType.FullName == "System.Diagnostics.Process" ||
                        called.DeclaringType.FullName == "System.Reflection.Assembly" ||
                        called.DeclaringType.FullName == "UnityEngine.AndroidJavaObject" ||
                        called.DeclaringType.FullName == "UnityEngine.AndroidJavaClass" ||
                        called.DeclaringType.FullName == "UnityEngine.WWW" ||
                        called.FullName.Contains("DownloadFile") || called.FullName.Contains("OpenURL")))
                        Console.WriteLine("sensitive\t"+method.FullName+"\t"+i);
                    if (i.OpCode == OpCodes.Ldstr && (((string)i.Operand).StartsWith("http") ||
                        ((string)i.Operand).Contains("/system/") || ((string)i.Operand).Contains(".sh")))
                        Console.WriteLine("literal\t"+method.FullName+"\t"+((string)i.Operand).Replace("\n","\\n"));
                }
            }
        }
    }
}
