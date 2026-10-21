using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

// Runs outside the game's stripped BCL directory. The output is TEST ONLY.
public static class PrepareMapProbe
{
    public static void Main(string[] args)
    {
        var module = ModuleDefinition.ReadModule(args[0]);
        var runtime = module.Types.Single(t => t.Name == "DivineGateOffline");
        var probe = new FieldDefinition("MapTextureProbeKey",
            FieldAttributes.Public | FieldAttributes.Static, module.TypeSystem.String);
        runtime.Fields.Add(probe);
        var ctor = runtime.Methods.Where(m => m.HasBody).SelectMany(m => m.Body.Instructions)
            .Where(i => i.OpCode == OpCodes.Newobj).Select(i => i.Operand as MethodReference)
            .First(m => m.DeclaringType.FullName == "UnityEngine.Texture2D" && m.Parameters.Count == 2);
        var decode = runtime.Methods.Single(m => m.Name == "RecoveredTexture");
        decode.Body = new MethodBody(decode);
        var il = decode.Body.GetILProcessor();
        il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Stsfld, probe);
        il.Emit(OpCodes.Ldc_I4_2); il.Emit(OpCodes.Ldc_I4_2);
        il.Emit(OpCodes.Newobj, ctor); il.Emit(OpCodes.Ret);
        module.Write(args[1]);
    }
}
