using System;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

// TEST ONLY: connect the disposable engine stub to in-memory preferences.
public static class PrepareComparisonPrefs
{
    public static void Main(string[] args)
    {
        var engine = ModuleDefinition.ReadModule(args[0]);
        var support = ModuleDefinition.ReadModule(args[1]);
        var prefs = engine.Types.Single(t => t.FullName == "UnityEngine.PlayerPrefs");
        var memory = support.Types.Single(t => t.Name == "ComparisonMemoryPrefs");
        foreach (var source in memory.Methods.Where(m => m.IsPublic && m.IsStatic))
        {
            var target = prefs.Methods.Single(m => m.Name == source.Name && m.Parameters.Count == source.Parameters.Count);
            if (target.ReturnType.FullName != source.ReturnType.FullName) throw new Exception("Preference signature mismatch");
            target.Body = new MethodBody(target);
            var il = target.Body.GetILProcessor();
            foreach (var parameter in target.Parameters) il.Emit(OpCodes.Ldarg, parameter);
            il.Emit(OpCodes.Call, engine.ImportReference(source)); il.Emit(OpCodes.Ret);
        }
        engine.Write(args[2]);
    }
}
