using System;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

// Preserve the legacy managed type surface, replace only engine services.
// There is no native Unity runtime in the Godot application.
public static class RewriteEngine
{
    public static void Main(string[] args)
    {
        var m=ModuleDefinition.ReadModule(args[0]);
        var support=ModuleDefinition.ReadModule(args[1]);
        var helpers=support.Types.Single(t=>t.Name=="PortableEngine");
        var error=m.ImportReference(typeof(NotSupportedException).GetConstructor(new[]{typeof(string)}));
        foreach(var t in m.Types) foreach(var f in t.Methods) {
            if(!f.IsInternalCall && !f.IsPInvokeImpl) continue;
            f.ImplAttributes=MethodImplAttributes.IL|MethodImplAttributes.Managed;
            f.Attributes &= ~MethodAttributes.PInvokeImpl;
            f.Body=new MethodBody(f);
            var il=f.Body.GetILProcessor();
            il.Emit(OpCodes.Ldstr,"Unity API has not been ported: "+f.FullName);
            il.Emit(OpCodes.Newobj,error);il.Emit(OpCodes.Throw);
        }
        foreach(var t in m.Types) foreach(var f in t.Methods) {
            string name=null;
            if(t.FullName=="UnityEngine.Resources" && f.Name=="Load" && f.Parameters.Count>0)name="Load";
            if(t.FullName=="UnityEngine.TextAsset" && f.Name=="get_text")name="Text";
            if(t.FullName=="UnityEngine.Application" && f.Name=="get_dataPath")name="DataPath";
            if(t.FullName=="UnityEngine.Application" && f.Name=="get_persistentDataPath")name="PersistentDataPath";
            if(t.FullName=="UnityEngine.Time" && f.Name=="get_realtimeSinceStartup")name="Realtime";
            if(t.FullName=="UnityEngine.Debug" && (f.Name=="Log"||f.Name=="LogWarning"||f.Name=="LogError") && f.Parameters.Count==1)name="Log";
            if((t.FullName=="UnityEngine.PlayerPrefs" || t.FullName=="UnityEngine.Mathf") && helpers.Methods.Any(h=>h.Name==f.Name && h.Parameters.Count==f.Parameters.Count))name=f.Name;
            if(name!=null) {
                f.Body=new MethodBody(f);var il=f.Body.GetILProcessor();
                var target=helpers.Methods.Single(h=>h.Name==name);
                if(name=="Text")il.Emit(OpCodes.Ldarg_0);
                else for(int i=0;i<target.Parameters.Count;i++)il.Emit(OpCodes.Ldarg,f.Parameters[i]);
                il.Emit(OpCodes.Call,m.ImportReference(target));
                if(name=="Load")il.Emit(OpCodes.Castclass,f.ReturnType);
                il.Emit(OpCodes.Ret);
            }
            if(t.FullName=="UnityEngine.Object" && (f.Name=="op_Equality"||f.Name=="op_Inequality"||f.Name=="op_Implicit")) {
                f.Body=new MethodBody(f);var il=f.Body.GetILProcessor();
                il.Emit(OpCodes.Ldarg_0);if(f.Parameters.Count==1)il.Emit(OpCodes.Ldnull);else il.Emit(OpCodes.Ldarg_1);
                il.Emit(OpCodes.Ceq);if(f.Name!="op_Equality"){il.Emit(OpCodes.Ldc_I4_0);il.Emit(OpCodes.Ceq);}il.Emit(OpCodes.Ret);
            }
            // Managed-only services inherit MonoBehaviour but have no scene object.
            if(t.FullName=="UnityEngine.MonoBehaviour" && f.Name==".ctor") {
                f.Body=new MethodBody(f);f.Body.GetILProcessor().Emit(OpCodes.Ret);
            }
        }
        m.Write(args[2]);
    }
}
