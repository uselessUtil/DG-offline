using System;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

public static partial class PatchPlayable290
{
    static void EvolutionPageLifecycle290()
    {
        const string typeName = "MainMenuSeqPartyEvolveQuestFix";
        var type = module.Types.Single(t => t.Name == typeName);
        var dialogs = type.Fields.Where(f => f.FieldType.Name == "Dialog").ToArray();
        if (dialogs.Length != 9) throw new Exception("Unexpected evolution dialog fields");
        var recovering = type.Fields.Single(f => f.Name == "m_IsDialogStamina");
        var setting = Method(typeName, "PageSwitchSetting");
        var il = setting.Body.GetILProcessor();
        var first = setting.Body.Instructions.First();
        il.InsertBefore(first, Instruction.Create(OpCodes.Ldarg_1));
        il.InsertBefore(first, Instruction.Create(OpCodes.Brfalse, first));
        foreach (var field in dialogs) {
            il.InsertBefore(first, Instruction.Create(OpCodes.Ldarg_0));
            il.InsertBefore(first, Instruction.Create(OpCodes.Ldnull));
            il.InsertBefore(first, Instruction.Create(OpCodes.Stfld, field));
        }
        il.InsertBefore(first, Instruction.Create(OpCodes.Ldarg_0));
        il.InsertBefore(first, Instruction.Create(OpCodes.Ldc_I4_0));
        il.InsertBefore(first, Instruction.Create(OpCodes.Stfld, recovering));

        // The original ready gate still runs first. Keep any in-flight quest
        // request and committed departure untouched; this only releases idle
        // waits on dialogs whose entire manager is fully closed.
        var update = Method(typeName, "Update");
        il = update.Body.GetILProcessor();
        var gate = update.Body.Instructions.Single(i => i.Operand is MethodReference &&
            ((MethodReference)i.Operand).Name == "PageSwitchUpdate");
        var branch = gate.Next;
        if (branch.OpCode != OpCodes.Brtrue && branch.OpCode != OpCodes.Brtrue_S)
            throw new Exception("Unexpected evolution page ready gate");
        var original = (Instruction)branch.Operand;
        var start = Instruction.Create(OpCodes.Ldarg_0);
        il.InsertBefore(original, start);
        il.InsertBefore(original, Instruction.Create(OpCodes.Ldfld, type.Fields.Single(f => f.Name == "m_PacketUniqueID")));
        il.InsertBefore(original, Instruction.Create(OpCodes.Brtrue, original));
        il.InsertBefore(original, Instruction.Create(OpCodes.Ldarg_0));
        il.InsertBefore(original, Instruction.Create(OpCodes.Ldfld, type.Fields.Single(f => f.Name == "m_BlendExec")));
        il.InsertBefore(original, Instruction.Create(OpCodes.Brtrue, original));
        var released = new VariableDefinition(module.TypeSystem.Boolean);
        update.Body.Variables.Add(released); update.Body.InitLocals = true;
        il.InsertBefore(original, Instruction.Create(OpCodes.Ldc_I4_0));
        il.InsertBefore(original, Instruction.Create(OpCodes.Stloc, released));
        foreach (var field in dialogs) {
            var next = Instruction.Create(OpCodes.Nop);
            il.InsertBefore(original, Instruction.Create(OpCodes.Ldarg_0));
            il.InsertBefore(original, Instruction.Create(OpCodes.Ldfld, field));
            il.InsertBefore(original, Instruction.Create(OpCodes.Call, Helper("IsOrphanEvolutionDialog290")));
            il.InsertBefore(original, Instruction.Create(OpCodes.Brfalse, next));
            il.InsertBefore(original, Instruction.Create(OpCodes.Ldarg_0));
            il.InsertBefore(original, Instruction.Create(OpCodes.Ldnull));
            il.InsertBefore(original, Instruction.Create(OpCodes.Stfld, field));
            il.InsertBefore(original, Instruction.Create(OpCodes.Ldc_I4_1));
            il.InsertBefore(original, Instruction.Create(OpCodes.Stloc, released));
            il.InsertBefore(original, next);
        }
        il.InsertBefore(original, Instruction.Create(OpCodes.Ldloc, released));
        il.InsertBefore(original, Instruction.Create(OpCodes.Brfalse, original));
        il.InsertBefore(original, Instruction.Create(OpCodes.Ldarg_0));
        il.InsertBefore(original, Instruction.Create(OpCodes.Ldc_I4_0));
        il.InsertBefore(original, Instruction.Create(OpCodes.Stfld, recovering));
        // Consume the recovery frame. A stale result never authorizes departure.
        il.InsertBefore(original, Instruction.Create(OpCodes.Ret));
        branch.Operand = start;
    }
}
