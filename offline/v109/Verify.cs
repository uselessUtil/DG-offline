using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Mono.Cecil;
using Mono.Cecil.Cil;

public static class Verify109
{
    static IEnumerable<TypeDefinition> Types(TypeDefinition type)
    {
        yield return type;
        foreach (var child in type.NestedTypes)
            foreach (var item in Types(child)) yield return item;
    }
    static string Body(MethodDefinition method)
    {
        if (!method.HasBody) return "NO BODY";
        return method.Body.InitLocals + "|" + string.Join("|", method.Body.Variables.Select(v => v.VariableType.FullName)) +
            "|" + string.Join("|",method.Body.Instructions.Select(i => i.ToString())) +
            "|" + string.Join("|",method.Body.ExceptionHandlers.Select(e => e.HandlerType+":"+e.TryStart+":"+e.TryEnd+
                ":"+e.HandlerStart+":"+e.HandlerEnd+":"+e.FilterStart+":"+e.CatchType));
    }
    static void Check(bool value,string reason)
    {
        if (!value) throw new Exception(reason);
    }
    public static void Main(string[] args)
    {
        var source = ModuleDefinition.ReadModule(args[0]);
        var modified = ModuleDefinition.ReadModule(args[1]);
        var allowed = new HashSet<string>(File.ReadAllLines(args[3]));
        var changed = new HashSet<string>();
        var methods = modified.Types.SelectMany(Types).SelectMany(t => t.Methods).ToDictionary(m => m.FullName);
        int unchanged = 0;
        foreach (var method in source.Types.SelectMany(Types).SelectMany(t => t.Methods))
        {
            Check(methods.ContainsKey(method.FullName),"Original method removed: "+method);
            if (Body(method) != Body(methods[method.FullName])) changed.Add(method.FullName);
            else unchanged++;
        }
        Check(changed.SetEquals(allowed),"Actual changed methods differ from declared patch list; undeclared=" +
            string.Join(", ",changed.Except(allowed)) + "; declared but unchanged=" + string.Join(", ",allowed.Except(changed)));
        Check(changed.All(n => !n.Contains("ServerForgery::")),"Backend unexpectedly changed");
        var title = modified.Types.Single(t => t.Name == "TitleMenuManager");
        var start = title.Methods.Single(m => m.Name == "Start");
        Check(start.Body.Instructions.Count == 4,"Title could enter original online flow");
        Check(start.Body.Instructions[2].OpCode == OpCodes.Call &&
            ((MethodReference)start.Body.Instructions[2].Operand).FullName == "System.Void DG109Diagnostic::Boot()",
            "Missing diagnostic gate");
        Check(title.Methods.Single(m => m.Name == "Update").Body.Instructions.Single().OpCode == OpCodes.Ret,
            "Title update not blocked");
        var route = modified.Types.Single(t => t.Name == "LocalSaveManager").Methods.Single(m => m.Name == "LoadFuncServerAddressIP");
        Check(route.Body.Instructions.Count == 2 && route.Body.Instructions[0].OpCode == OpCodes.Ldstr &&
            (string)route.Body.Instructions[0].Operand == "","Non-local server route");
        var patchStart = modified.Types.Single(t => t.Name == "PatchManager").Methods.Single(m => m.Name == "Start");
        Check(patchStart.Body.Instructions.Count == 3 && patchStart.Body.Instructions[1].OpCode == OpCodes.Call &&
            ((MethodReference)patchStart.Body.Instructions[1].Operand).DeclaringType.FullName.StartsWith("Singleton`1<"),
            "PatchManager may still start online update coroutine");
        var resident = modified.Types.Single(t => t.Name == "ResidentManager").Methods.Single(m => m.Name == "Start");
        Check(resident.Body.Instructions.Any(i => i.OpCode == OpCodes.Call && i.Operand is MethodReference &&
            ((MethodReference)i.Operand).FullName == "System.Void DG109Diagnostic::Boot()"), "Missing resident boot hook");
        var device = modified.Types.Single(t => t.Name == "AndroidDeviceInfo");
        var awake = device.Methods.Single(m => m.Name == "Awake");
        Check(awake.Body.Instructions.Count == 3 &&
            ((MethodReference)awake.Body.Instructions[1].Operand).DeclaringType.FullName.StartsWith("Singleton`1<"),
            "External storage initialization still active");
        var storage = device.Methods.Single(m => m.Name == "IsExStorageActive");
        Check(storage.Body.Instructions.Count == 2 && storage.Body.Instructions[0].OpCode == OpCodes.Ldc_I4_0,
            "External keychain path still active");
        var resolver = new DefaultAssemblyResolver();
        resolver.AddSearchDirectory(Path.GetDirectoryName(args[1]));
        var adapter = ModuleDefinition.ReadModule(args[2],new ReaderParameters {AssemblyResolver=resolver});
        int references = 0;
        foreach (var member in adapter.GetMemberReferences())
        {
            var m = member as MethodReference;
            var f = member as FieldReference;
            if (m != null) Check(m.Resolve() != null,"Unresolved method: "+m);
            if (f != null) Check(f.Resolve() != null,"Unresolved field: "+f);
            references++;
        }
        Check(adapter.AssemblyReferences.All(r => r.Name == "mscorlib" || r.Name == "UnityEngine"),"Unexpected adapter dependency");
        Check(adapter.AssemblyReferences.Single(r => r.Name == "mscorlib").Version.Major == 2,"Newer BCL required");
        Console.WriteLine("PASS original method bodies unchanged outside patch list: "+unchanged);
        Console.WriteLine("PASS actual changed methods match patch list: "+changed.Count);
        Console.WriteLine("PASS original ServerForgery retained; title blocked; patch coroutine stopped; local route selected");
        Console.WriteLine("PASS adapter references resolve against original Unity/BCL: "+references);
    }
}
