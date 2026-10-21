using System;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

public static class VerifyCollabHooks
{
    public static void Main(string[] args)
    {
        var original = ModuleDefinition.ReadModule(args[0]);
        var game = ModuleDefinition.ReadModule(args[1]);
        var adapter = ModuleDefinition.ReadModule(args[2]);
        var root = game.Types.Single(t => t.Name == "MainMenuSeqGachaRoot");
        var update = root.Methods.Single(m => m.Name == "Update");
        var calls = update.Body.Instructions.Where(i => i.Operand is MethodReference).Select(i => (MethodReference)i.Operand).ToArray();
        foreach (var name in new[] { "CollabSelectorOpen", "UpdateCollabSelector", "RouteGachaRootRequest" })
            if (calls.Count(c => c.Name == name) != 1) throw new Exception("Missing/duplicate selector hook " + name);
        var route = adapter.Types.Single(t => t.Name == "DivineGateOffline").Methods.Single(m => m.Name == "RouteGachaRootRequest");
        var oldRequest = original.Types.Single(t => t.Name == "MainMenuManager").Methods.Single(m => m.Name == "AddSwitchRequest");
        if (route.ReturnType.FullName != oldRequest.ReturnType.FullName || route.ReturnType.FullName != "System.Boolean" ||
            calls.Single(c => c.Name == "RouteGachaRootRequest").ReturnType.FullName != "System.Boolean")
            throw new Exception("Selector request hook does not preserve the original bool result/stack");
        var requestCall = update.Body.Instructions.Single(i => i.Operand is MethodReference && ((MethodReference)i.Operand).Name == "RouteGachaRootRequest");
        if (requestCall.Previous.OpCode != OpCodes.Ldarg_0 || requestCall.Next.OpCode != OpCodes.Pop)
            throw new Exception("Wrong route arguments/original return disposal");
        if (calls.Count(c => c.Name == "PageSwitchUpdate") != 2 || !calls.Any(c => c.Name == "TutorialChkAllFinish"))
            throw new Exception("Page animation or tutorial flow lost");
        foreach (var end in root.Methods.Single(m => m.Name == "PageSwitchSetting").Body.Instructions.Where(i => i.OpCode == OpCodes.Ret))
            if (!(end.Previous.Operand is MethodReference) || ((MethodReference)end.Previous.Operand).Name != "RefreshCollabRoot")
                throw new Exception("Return to Scratch does not reset selector mode");
        // Confirmation and scratching are still the original instructions.
        foreach (var type in new[] { "MainMenuSeqGachaConfirm", "MainMenuSeqGachaMain" })
        {
            var before = original.Types.Single(t => t.Name == type);
            var after = game.Types.Single(t => t.Name == type);
            foreach (var method in before.Methods.Where(m => m.HasBody))
            {
                var matching = after.Methods.Single(m => m.FullName == method.FullName);
                if (string.Join("\n", method.Body.Instructions.Select(i => i.ToString())) !=
                    string.Join("\n", matching.Body.Instructions.Select(i => i.ToString())))
                    throw new Exception("Original confirmation/scratch modified: " + method.FullName);
            }
        }
        Console.WriteLine("PASS collab hooks: bool request stack, animation/tutorial, confirmation and scratch instructions preserved.");
    }
}
