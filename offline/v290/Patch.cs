using System;
using System.IO;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

public static class Patch290
{
    static ModuleDefinition module, adapter;
    static StreamWriter changes;
    static MethodDefinition Method(string type, string name, int count)
    {
        return module.Types.Single(t => t.Name == type).Methods.Single(m => m.Name == name && m.Parameters.Count == count);
    }
    static ILProcessor Reset(MethodDefinition method)
    {
        changes.WriteLine(method.FullName);
        method.Body = new MethodBody(method);
        return method.Body.GetILProcessor();
    }
    static void Void(MethodDefinition method)
    {
        if (method.ReturnType.FullName != "System.Void") throw new Exception("Not void: " + method);
        Reset(method).Emit(OpCodes.Ret);
    }
    static void BaseOnly(string type, string name, bool boot)
    {
        var method = Method(type, name, 0);
        var call = method.Body.Instructions.Single(i => i.OpCode == OpCodes.Call &&
            ((MethodReference)i.Operand).Name == name);
        var baseMethod = (MethodReference)call.Operand;
        var il = Reset(method);
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Call, baseMethod);
        if (boot) il.Emit(OpCodes.Call, Boot());
        il.Emit(OpCodes.Ret);
    }
    static MethodReference Boot()
    {
        return module.ImportReference(adapter.Types.Single(t => t.Name == "DG290Diagnostic").Methods.Single(m => m.Name == "Boot"));
    }
    public static void Main(string[] args)
    {
        module = ModuleDefinition.ReadModule(args[0]);
        adapter = ModuleDefinition.ReadModule(args[1]);
        if (module.Types.Any(t => t.Name == "DG290DiagnosticPatched")) throw new Exception("Already patched");
        using (changes = new StreamWriter(args[3]))
        {
            var route = Method("LocalSaveManager", "LoadFuncServerAddressIP", 0);
            if (route.ReturnType.FullName != "System.String") throw new Exception("Unexpected route");
            var il = Reset(route);
            il.Emit(OpCodes.Ldstr, ""); il.Emit(OpCodes.Ret);
            // Keep original boot setup and attach the data gate at its only exit.
            var resident = Method("ResidentManager", "Start", 0);
            var end = resident.Body.Instructions.Single(i => i.OpCode == OpCodes.Ret);
            changes.WriteLine(resident.FullName);
            end.OpCode = OpCodes.Call; end.Operand = Boot();
            resident.Body.GetILProcessor().Append(Instruction.Create(OpCodes.Ret));
            BaseOnly("TitleMenuManager", "Start", true);
            Void(Method("TitleMenuManager", "Update", 0));
            BaseOnly("StoreManager", "Start", false);
            BaseOnly("StoreManager", "OnDestroy", false);
            Void(Method("StoreManager", "Update", 0));
            Void(Method("PatchManager", "Update", 0));
            Void(Method("PatchManager", "PatchUpdateRequest", 2));
            foreach (string type in new [] { "PlayGameServiceUtil", "LocalNotificationUtil" })
                foreach (var method in module.Types.Single(t => t.Name == type).Methods.Where(m =>
                    m.IsStatic && !m.IsConstructor && m.HasBody && m.ReturnType.FullName == "System.Void"))
                    Void(method);
            module.Types.Add(new TypeDefinition("", "DG290DiagnosticPatched", TypeAttributes.NotPublic, module.TypeSystem.Object));
            module.Write(args[2]);
        }
    }
}
