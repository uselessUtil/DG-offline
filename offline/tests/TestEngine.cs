using System;
using System.IO;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

// Stub only the engine library in an isolated TEST directory. The APK retains
// the byte-for-byte original UnityEngine.dll and native libraries.
public static class TestEngine
{
    public static void Main(string[] args)
    {
        var module = ModuleDefinition.ReadModule(args[0]);
        var support = ModuleDefinition.ReadModule(args[1]);
        var helper = support.Types.Single(t => t.Name == "EngineSupport");
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
        foreach (var type in module.Types) foreach (var method in type.Methods)
        {
            string helperName = null;
            if (type.FullName == "UnityEngine.Resources" && method.Name == "Load" && method.Parameters.Count > 0) helperName = "Load";
            if (type.FullName == "UnityEngine.TextAsset" && method.Name == "get_text") helperName = "Text";
            if (type.FullName == "UnityEngine.Component" && method.Name == "get_gameObject") helperName = "ComponentGameObject";
            if (type.FullName == "UnityEngine.Object" && method.Name == "get_name") helperName = "ObjectName";
            if (type.FullName == "UnityEngine.GameObject" && (method.Name == "get_activeInHierarchy" || method.Name == "get_activeSelf")) helperName = "GameObjectActive";
            if (type.FullName == "UnityEngine.GameObject" && method.Name == "SetActive") helperName = "SetActiveState";
            if (type.FullName == "UnityEngine.Collider" && method.Name == "get_enabled") helperName = "ColliderEnabled";
            if (type.FullName == "UnityEngine.Collider" && method.Name == "set_enabled") helperName = "SetColliderEnabled";
            if (type.FullName == "UnityEngine.Behaviour" && method.Name == "get_enabled") helperName = "BehaviourEnabled";
            if (type.FullName == "UnityEngine.Behaviour" && method.Name == "set_enabled") helperName = "SetBehaviourEnabled";
            if ((type.FullName == "UnityEngine.GameObject" || type.FullName == "UnityEngine.Component") && method.Name == "get_transform") helperName = "ObjectTransform";
            if ((type.FullName == "UnityEngine.GameObject" || type.FullName == "UnityEngine.Component") && method.Name == "GetComponent" &&
                method.Parameters.Count == 1 && method.Parameters[0].ParameterType.FullName == "System.Type") helperName = "GetComponentType";
            if (type.FullName == "UnityEngine.Transform" && method.Name == "get_localScale") helperName = "TransformScale";
            if (type.FullName == "UnityEngine.Transform" && method.Name == "set_localScale") helperName = "SetTransformScale";
            if (type.FullName == "UnityEngine.GameObject" && method.Name == "SendMessage" && method.Parameters.Count == 3 &&
                method.Parameters[1].ParameterType.FullName == "System.Object") helperName = "SendTestMessage";
            if (type.FullName == "UnityEngine.PlayerPrefs" && helper.Methods.Any(m => m.Name == method.Name && m.Parameters.Count == method.Parameters.Count)) helperName = method.Name;
            if (type.FullName == "UnityEngine.Mathf" && helper.Methods.Any(m => m.Name == method.Name && m.Parameters.Count == method.Parameters.Count)) helperName = method.Name;
            if (helperName != null)
            {
                var target = helper.Methods.Single(m => m.Name == helperName);
                method.Body = new MethodBody(method);
                var il = method.Body.GetILProcessor();
                if (new[] { "Text", "ComponentGameObject", "ObjectName", "GameObjectActive", "ColliderEnabled", "BehaviourEnabled",
                    "ObjectTransform", "GetComponentType", "TransformScale", "SetTransformScale", "SetActiveState",
                    "SetColliderEnabled", "SetBehaviourEnabled", "SendTestMessage" }.Contains(helperName))
                {
                    il.Emit(OpCodes.Ldarg_0);
                    foreach (var parameter in method.Parameters) il.Emit(OpCodes.Ldarg, parameter);
                }
                else for (int i = 0; i < target.Parameters.Count; i++) il.Emit(OpCodes.Ldarg, method.Parameters[i]);
                il.Emit(OpCodes.Call, module.ImportReference(target));
                if (helperName == "Load" || helperName == "ComponentGameObject" || helperName == "ObjectTransform" || helperName == "GetComponentType") il.Emit(OpCodes.Castclass, method.ReturnType);
                il.Emit(OpCodes.Ret);
            }
            if (type.FullName == "UnityEngine.Object" && (method.Name == "op_Equality" || method.Name == "op_Inequality" || method.Name == "op_Implicit"))
            {
                method.Body = new MethodBody(method);
                var il = method.Body.GetILProcessor();
                il.Emit(OpCodes.Ldarg_0);
                if (method.Parameters.Count == 1) il.Emit(OpCodes.Ldnull); else il.Emit(OpCodes.Ldarg_1);
                il.Emit(OpCodes.Ceq);
                if (method.Name != "op_Equality") { il.Emit(OpCodes.Ldc_I4_0); il.Emit(OpCodes.Ceq); }
                il.Emit(OpCodes.Ret);
            }
        }
        module.Write(args[2]);
    }
}
