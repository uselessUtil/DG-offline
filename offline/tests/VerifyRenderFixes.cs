using System;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

// Inspect the actual packaged IL; visual behavior still requires Android.
public static class VerifyRenderFixes
{
    public static void Main(string[] args)
    {
        var game = ModuleDefinition.ReadModule(args[0]);
        var adapter = ModuleDefinition.ReadModule(args[1]);
        var view = game.Types.Single(t => t.Name == "AssetAutoSetCharaMesh");
        var update = view.Methods.Single(m => m.Name == "Update");
        var exits = update.Body.Instructions.Where(i => i.OpCode == OpCodes.Ret).ToArray();
        foreach (var exit in exits)
        {
            var call = exit.Previous;
            var method = call.Operand as MethodReference;
            if (call.OpCode != OpCodes.Call || method == null || method.Name != "FixCharacterShadow")
                throw new Exception("A shadow update exit bypasses alpha repair");
        }
        var runtime = adapter.Types.Single(t => t.Name == "DivineGateOffline");
        var flat = runtime.Methods.Single(m => m.Name == "FlatShader");
        var shaders = flat.Body.Instructions.Where(i => i.OpCode == OpCodes.Ldstr).Select(i => (string)i.Operand).ToArray();
        if (!shaders.Contains("Unlit/Transparent Colored") || shaders.Any(s => s.Contains("AlphaNone") || s == "Unlit/Texture"))
            throw new Exception("Opaque shader fallback remains");
        var map = runtime.Methods.Single(m => m.Name == "Map");
        int primitives = map.Body.Instructions.Count(i => i.Operand is MethodReference && ((MethodReference)i.Operand).Name == "CreatePrimitive");
        if (primitives != 1) throw new Exception("Unexpected map primitives; only the textured floor may remain");
        if (!map.Body.Instructions.Any(i => i.OpCode == OpCodes.Ldstr && (string)i.Operand == "DG upper map backdrop") ||
            !map.Body.Instructions.Any(i => i.OpCode == OpCodes.Ldstr && (string)i.Operand == "upper"))
            throw new Exception("Upper map scenery is not bound to its display mesh");
        if (map.Body.Instructions.Any(i => i.Operand is MethodReference &&
            ((MethodReference)i.Operand).Name == "AddComponent" &&
            ((MethodReference)i.Operand).FullName.Contains("Collider")))
            throw new Exception("Display backdrop must not intercept entrance taps");
        var battle = game.Types.Single(t => t.Name == "BattleManager");
        var hud = battle.Methods.Single(m => m.Name == "LateUpdate");
        if (!hud.Body.Instructions.Any(i => i.Operand is MethodReference && ((MethodReference)i.Operand).Name == "RaiseEnemyHud"))
            throw new Exception("Enemy HUD foreground hook missing");
        var node = runtime.Methods.Single(m => m.Name == "RaiseEnemyHudNode");
        if (!node.Body.Instructions.Any(i => i.OpCode == OpCodes.Ldstr && (string)i.Operand == "Unlit/Transparent Colored Overlay") ||
            !node.Body.Instructions.Any(i => i.Operand is MethodReference && ((MethodReference)i.Operand).Name == "set_material"))
            throw new Exception("Enemy HUD does not use dedicated overlay materials");
        var raise = runtime.Methods.Single(m => m.Name == "RaiseEnemyHud");
        if (!raise.Body.Instructions.Any(i => i.Operand is MethodReference && ((MethodReference)i.Operand).Name == "EnemyHudBehindPopup") ||
            !node.Body.Instructions.Any(i => i.Operand is FieldReference && ((FieldReference)i.Operand).Name == "enemyHudOriginals"))
            throw new Exception("HUD overlay must restore its original material under popup UI");
        var fix = runtime.Methods.Single(m => m.Name == "FixCharacterShadow");
        if (!fix.Body.Instructions.Any(i => i.OpCode == OpCodes.Ldstr && (string)i.Operand == "shadow/"))
            throw new Exception("Shadow alpha-mask binding missing");
        Console.WriteLine("PASS packaged rendering contracts: " + exits.Length + " shadow update exits repaired, transparent shaders, upper scenery mesh without new colliders, enemy HUD overlay hook");
    }
}
