using System;
using System.IO;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

// Applies only after the Python builder verifies the exact original APK hash.
public static class Patch109
{
    static ModuleDefinition module, adapter;
    static StreamWriter changes;
    static MethodDefinition Method(string type, string name, int count)
    {
        return module.Types.Single(t => t.Name == type).Methods.Single(m => m.Name == name && m.Parameters.Count == count);
    }
    static ILProcessor Reset(MethodDefinition method)
    {
        if (!method.HasBody) throw new Exception("Expected managed body: " + method);
        changes.WriteLine(method.FullName);
        method.Body = new MethodBody(method);
        return method.Body.GetILProcessor();
    }
    static void Void(MethodDefinition method)
    {
        if (method.ReturnType.FullName != "System.Void") throw new Exception("Not void: " + method);
        if (method.Body.Instructions.Count == 1 && method.Body.Instructions[0].OpCode == OpCodes.Ret &&
            !method.Body.InitLocals && method.Body.Variables.Count == 0 && method.Body.ExceptionHandlers.Count == 0) return;
        Reset(method).Emit(OpCodes.Ret);
    }
    static MethodReference Boot()
    {
        return module.ImportReference(adapter.Types.Single(t => t.Name == "DG109Diagnostic")
            .Methods.Single(m => m.Name == "Boot" && m.IsStatic && m.Parameters.Count == 0));
    }
    static void BaseOnly(string type, string name, bool boot)
    {
        var method = Method(type, name, 0);
        if (method.ReturnType.FullName != "System.Void") throw new Exception("Unexpected return: " + method);
        var call = method.Body.Instructions.Single(i => i.OpCode == OpCodes.Call &&
            i.Operand is MethodReference && ((MethodReference)i.Operand).Name == name);
        var target = (MethodReference)call.Operand;
        if (!target.DeclaringType.FullName.StartsWith("Singleton`1<")) throw new Exception("Unexpected base: " + target);
        var il = Reset(method);
        il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Call, target);
        if (boot) il.Emit(OpCodes.Call, Boot());
        il.Emit(OpCodes.Ret);
    }
    public static void Main(string[] args)
    {
        module = ModuleDefinition.ReadModule(args[0]);
        adapter = ModuleDefinition.ReadModule(args[1]);
        if (module.Types.Any(t => t.Name == "DG109DiagnosticPatched")) throw new Exception("Already patched");
        var resources = Method("ServerForgery", "Start", 0).Body.Instructions
            .Where(i => i.OpCode == OpCodes.Ldstr && ((string)i.Operand).StartsWith("CardDM/MasterData/"));
        if (resources.Count() != 34) throw new Exception("Unexpected 1.0.9 data contract");
        using (changes = new StreamWriter(args[3]))
        {
            var route = Method("LocalSaveManager", "LoadFuncServerAddressIP", 0);
            if (route.ReturnType.FullName != "System.String" || route.IsStatic) throw new Exception("Unexpected route");
            var il = Reset(route); il.Emit(OpCodes.Ldstr, ""); il.Emit(OpCodes.Ret);
            // Mutate the existing return so branches targeting it also run Boot.
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
            // This build starts a patch coroutine in Start.
            BaseOnly("PatchManager", "Start", false);
            Void(Method("PatchManager", "PatchUpdateRequest", 2));
            // Avoid the external-storage keychain bridge after removing its permissions.
            BaseOnly("AndroidDeviceInfo", "Awake", false);
            Void(Method("AndroidDeviceInfo", "UpdateExStoragePath", 0));
            var externalStorage = Method("AndroidDeviceInfo", "IsExStorageActive", 0);
            if (externalStorage.ReturnType.FullName != "System.Boolean") throw new Exception("Storage contract changed");
            var storageIL = Reset(externalStorage);
            storageIL.Emit(OpCodes.Ldc_I4_0); storageIL.Emit(OpCodes.Ret);
            if (module.Types.Any(t => t.Name == "LocalNotificationUtil"))
                throw new Exception("Unexpected notification type in exact 1.0.9 build");
            foreach (string name in new [] { "PlayGameServiceUtil" })
                foreach (var method in module.Types.Single(t => t.Name == name).Methods.Where(m =>
                    m.IsStatic && !m.IsConstructor && m.HasBody && m.ReturnType.FullName == "System.Void"))
                    Void(method);
            module.Types.Add(new TypeDefinition("", "DG109DiagnosticPatched", TypeAttributes.NotPublic, module.TypeSystem.Object));
            module.Write(args[2]);
        }
    }
}
