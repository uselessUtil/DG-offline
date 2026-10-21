using System;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

public static class VerifyEventPatch
{
    public static void Main(string[] args)
    {
        var original = ModuleDefinition.ReadModule(args[0]);
        var patched = ModuleDefinition.ReadModule(args[1]);
        Func<ModuleDefinition, MethodDefinition> find = m => m.Types.Single(t => t.Name == "MainMenuSeqQuestSelectMission").Methods.Single(x => x.Name == "PageSwitchSetting");
        var before = find(original).Body.Instructions;
        var after = find(patched).Body.Instructions;
        var call = after.Single(i => i.OpCode == OpCodes.Call && i.Operand is MethodReference && ((MethodReference)i.Operand).Name == "EventQuestPlayable");
        if (after.Count != before.Count + 5 || call.Next.OpCode != OpCodes.Stfld ||
            ((FieldReference)call.Next.Operand).Name != "m_QuestPlayable" || call.Previous.OpCode != OpCodes.Ldloc ||
            call.Previous.Previous.OpCode != OpCodes.Ldfld || ((FieldReference)call.Previous.Previous.Operand).Name != "m_QuestMasterData" ||
            call.Previous.Previous.Previous.OpCode != OpCodes.Dup || call.Previous.Previous.Previous.Previous.OpCode != OpCodes.Stloc)
            throw new Exception("Wrong event mission access injection");
        Console.WriteLine("PASS event mission hook: one scoped access call at the original quest-playable assignment; original menu loop retained.");
    }
}
