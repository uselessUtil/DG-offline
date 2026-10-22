using System;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

// Native WWW/GPU work is supplied by EngineSupport. Replace only the legacy
// fallback factory in a disposable adapter copy to isolate loader transitions.
public static class PrepareNativeProbe
{
    public static void Main(string[] args)
    {
        var resolver = new DefaultAssemblyResolver();
        resolver.AddSearchDirectory(System.IO.Path.GetDirectoryName(args[0]));
        var module = ModuleDefinition.ReadModule(args[0],new ReaderParameters { AssemblyResolver=resolver });
        var runtime = module.Types.Single(t => t.Name=="DivineGateOffline");
        var method = runtime.Methods.Single(m => m.Name=="FallbackRequest");
        var resource = method.ReturnType.Resolve();
        var ctor = module.ImportReference(resource.Methods.Single(m => m.IsConstructor && !m.HasParameters));
        var name = module.ImportReference(resource.Fields.Single(f=>f.Name=="m_RequestName"));
        var step = module.ImportReference(resource.Fields.Single(f=>f.Name=="m_ResultStep"));
        method.Body = new MethodBody(method);
        var il=method.Body.GetILProcessor();
        il.Emit(OpCodes.Newobj,ctor);
        il.Emit(OpCodes.Dup); il.Emit(OpCodes.Ldarg_1); il.Emit(OpCodes.Stfld,name);
        il.Emit(OpCodes.Dup); il.Emit(OpCodes.Ldc_I4_2); il.Emit(OpCodes.Stfld,step);
        il.Emit(OpCodes.Ret);
        module.Write(args[1]);
        Console.WriteLine("Prepared native loader probe; fallback factory only is a stand-in");
    }
}
