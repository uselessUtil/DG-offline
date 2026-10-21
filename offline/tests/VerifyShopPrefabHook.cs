using System;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

public static class VerifyShopPrefabHook
{
    public static void Main(string[] args)
    {
        var game = ModuleDefinition.ReadModule(args[0]);
        var strings = game.Types.Single(t => t.Name == "MainMenuManager").Methods.Where(m => m.HasBody)
            .SelectMany(m => m.Body.Instructions).Where(i => i.OpCode == OpCodes.Ldstr).Select(i => (string)i.Operand).ToArray();
        if (strings.Count(s => s == "Prefab/MainMenu/MainMenuPage/_old_PageShopBuyTip") != 1 ||
            strings.Any(s => s == "Prefab/MainMenu/MainMenuPage/PageShopBuyTip"))
            throw new Exception("Shop confirmation still loads the title-only container");
        Console.WriteLine("PASS packaged menu loader selects the complete _old_PageShopBuyTip, not the title-only PageShopBuyTip.");
    }
}
