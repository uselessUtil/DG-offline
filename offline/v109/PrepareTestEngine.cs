using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

// TEST ONLY: replaces native entry points in an isolated copy of UnityEngine.dll.
// Default Resources.Load returns null, modeling the absence verified by UnityPy.
public static class PrepareTestEngine109
{
    public static void Main(string[] args)
    {
        var module = ModuleDefinition.ReadModule(args[0]);
        foreach (var type in module.Types) foreach (var method in type.Methods)
        {
            if (!method.IsInternalCall && !method.IsPInvokeImpl) continue;
            method.ImplAttributes = MethodImplAttributes.IL | MethodImplAttributes.Managed;
            method.Attributes &= ~MethodAttributes.PInvokeImpl;
            method.Body = new MethodBody(method) { InitLocals = true };
            var il = method.Body.GetILProcessor();
            if (method.ReturnType.FullName != "System.Void")
            {
                var local = new VariableDefinition(method.ReturnType);
                method.Body.Variables.Add(local);
                il.Emit(OpCodes.Ldloca, local); il.Emit(OpCodes.Initobj, method.ReturnType); il.Emit(OpCodes.Ldloc, local);
            }
            il.Emit(OpCodes.Ret);
        }
        var obj = module.Types.Single(t => t.FullName == "UnityEngine.Object");
        foreach (var method in obj.Methods.Where(m => m.Name == "op_Equality" || m.Name == "op_Inequality" || m.Name == "op_Implicit"))
        {
            method.Body = new MethodBody(method);
            var il = method.Body.GetILProcessor();
            il.Emit(OpCodes.Ldarg_0);
            if (method.Parameters.Count == 1) il.Emit(OpCodes.Ldnull); else il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Ceq);
            if (method.Name != "op_Equality") { il.Emit(OpCodes.Ldc_I4_0); il.Emit(OpCodes.Ceq); }
            il.Emit(OpCodes.Ret);
        }
        module.Write(args[1]);
    }
}
