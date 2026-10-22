using System;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

public static class VerifyNativeHooks
{
    static string Body(MethodDefinition m)
    { return string.Join("\n",m.Body.Instructions.Select(i=>i.ToString()).ToArray()); }
    public static void Main(string[] args)
    {
        var original=ModuleDefinition.ReadModule(args[0]);
        var game=ModuleDefinition.ReadModule(args[1]);
        var adapter=ModuleDefinition.ReadModule(args[2]);
        var resident=game.Types.Single(t=>t.Name=="ResidentManager");
        var update=resident.Methods.Single(m=>m.Name=="Update");
        if (update.Body.Instructions.Count(i=>i.Operand is MethodReference && ((MethodReference)i.Operand).Name=="PumpNativeAssets")!=1)
            throw new Exception("Resident native loading pump missing/duplicated");
        var runtime=adapter.Types.Single(t=>t.Name=="DivineGateOffline");
        var request=runtime.Methods.Single(m=>m.Name=="Request");
        foreach (string name in new[]{"RequestNative","FallbackRequest"})
            if (!request.Body.Instructions.Any(i=>i.Operand is MethodReference && ((MethodReference)i.Operand).Name==name))
                throw new Exception("Missing native/fallback dispatch "+name);
        var shadow=runtime.Methods.Single(m=>m.Name=="FixCharacterShadow");
        var guard=shadow.Body.Instructions.FirstOrDefault(i=>i.Operand is GenericInstanceMethod &&
            ((GenericInstanceMethod)i.Operand).GenericArguments.Any(t=>t.Name=="DGNativeCharacterMarker"));
        var material=shadow.Body.Instructions.First(i=>i.Operand is MethodReference && ((MethodReference)i.Operand).Name=="get_sharedMaterial");
        if (guard==null || guard.Offset>=material.Offset) throw new Exception("Native shadow modified by quad-only repair");
        foreach (string name in new[]{"DoorOpen","StepFuncInitFloorCreate"})
        {
            var before=original.Types.Single(t=>t.Name=="InGameManager").Methods.Single(m=>m.Name==name);
            var after=game.Types.Single(t=>t.Name=="InGameManager").Methods.Single(m=>m.Name==name);
            if (Body(before)!=Body(after)) throw new Exception("Original door control changed: "+name);
        }
        var url=runtime.Methods.Single(m=>m.Name=="NativeUrl");
        if (!url.Body.Instructions.Any(i=>i.OpCode==OpCodes.Ldstr && (string)i.Operand=="jar:file://")) throw new Exception("Native bundles must load locally");
        Console.WriteLine("PASS packaged native hooks: resident pump, native/fallback dispatch, native-character shadow guard before material changes; original DoorOpen and floor animation state machine preserved; local APK URL.");
    }
}
