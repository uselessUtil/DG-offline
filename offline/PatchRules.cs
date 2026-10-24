using System;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

public static partial class Patch106
{
    static void Prefix(string type, string method, string helper)
    {
        var m = Method(type, method); var il = m.Body.GetILProcessor(); var first = m.Body.Instructions.First();
        il.InsertBefore(first, Instruction.Create(OpCodes.Ldarg_0));
        il.InsertBefore(first, Instruction.Create(OpCodes.Call, Helper(helper)));
    }
    static void BindBridge(string helperName, string typeName, Action<ILProcessor,TypeDefinition> body)
    {
        var stub = adapter.Types.Single(t => t.Name == "DivineGateOffline").Methods.Single(m => m.Name == helperName);
        var type = module.Types.Single(t => t.Name == typeName);
        var bridge = new MethodDefinition("DGOffline" + helperName, MethodAttributes.Public | MethodAttributes.HideBySig,
            module.ImportReference(stub.ReturnType));
        foreach (var p in stub.Parameters.Skip(1))
            bridge.Parameters.Add(new ParameterDefinition(p.Name, p.Attributes, module.ImportReference(p.ParameterType)));
        type.Methods.Add(bridge); body(bridge.Body.GetILProcessor(),type);
        var il = Reset(stub);
        foreach (var p in stub.Parameters) il.Emit(OpCodes.Ldarg,p);
        il.Emit(OpCodes.Callvirt,adapter.ImportReference(bridge)); il.Emit(OpCodes.Ret);
    }
    static void TransformReturn(string type, string method, string helper)
    {
        var m = Method(type, method); var il = m.Body.GetILProcessor();
        foreach (var end in m.Body.Instructions.Where(i => i.OpCode == OpCodes.Ret).ToArray())
        {
            end.OpCode = OpCodes.Ldarg_0;
            var call = Instruction.Create(OpCodes.Call,Helper(helper));
            il.InsertAfter(end,call); il.InsertAfter(call,Instruction.Create(OpCodes.Ret));
        }
    }
    static void ReadyPageInput(string type, string helper)
    {
        var m = Method(type,"Update"); var il = m.Body.GetILProcessor();
        var check = m.Body.Instructions.First(i => i.Operand is MethodReference &&
            ((MethodReference)i.Operand).Name == "PageSwitchUpdate");
        var branch = check.Next;
        if (branch.OpCode != OpCodes.Brtrue && branch.OpCode != OpCodes.Brtrue_S)
            throw new Exception("Unexpected page input gate " + type);
        var start = (Instruction)branch.Operand; var load = Instruction.Create(OpCodes.Ldarg_0);
        il.InsertBefore(start,load); il.InsertBefore(start,Instruction.Create(OpCodes.Call,Helper(helper)));
        il.InsertBefore(start,Instruction.Create(OpCodes.Brfalse,start)); il.InsertBefore(start,Instruction.Create(OpCodes.Ret));
        branch.Operand = load;
    }
    static void GuardWarehouseRequest(string method, string helper)
    {
        var m = Method("ServerForgery",method); var il = m.Body.GetILProcessor(); var first = m.Body.Instructions.First();
        var result = new VariableDefinition(module.TypeSystem.String); m.Body.Variables.Add(result); m.Body.InitLocals = true;
        il.InsertBefore(first,Instruction.Create(OpCodes.Ldarg_0)); il.InsertBefore(first,Instruction.Create(OpCodes.Ldarg_1));
        il.InsertBefore(first,Instruction.Create(OpCodes.Ldarg_2)); il.InsertBefore(first,Instruction.Create(OpCodes.Call,Helper(helper)));
        il.InsertBefore(first,Instruction.Create(OpCodes.Stloc,result)); il.InsertBefore(first,Instruction.Create(OpCodes.Ldloc,result));
        il.InsertBefore(first,Instruction.Create(OpCodes.Brfalse,first)); il.InsertBefore(first,Instruction.Create(OpCodes.Ldloc,result));
        il.InsertBefore(first,Instruction.Create(OpCodes.Ret));
    }
    static void OfflineQualityOfLife()
    {
        var rename = adapter.Types.Single(t => t.Name == "DivineGateOffline").Methods.Single(m => m.Name == "RenameSaveFile");
        var libc = new ModuleReference("libc"); adapter.ModuleReferences.Add(libc);
        rename.IsPInvokeImpl = true; rename.ImplAttributes = MethodImplAttributes.PreserveSig;
        rename.PInvokeInfo = new PInvokeInfo(PInvokeAttributes.CallConvCdecl | PInvokeAttributes.CharSetAnsi |
            PInvokeAttributes.SupportsLastError,"rename",libc); rename.Body = null;
        var add = Method("ServerForgery","AddUnit");
        var allocations = add.Body.Instructions.Where(i => i.OpCode == OpCodes.Newobj &&
            ((MethodReference)i.Operand).DeclaringType.FullName == "ServerDataDefine.PacketStructUnit").ToArray();
        if (allocations.Length != 1) throw new Exception("Unexpected new-unit allocation");
        var addIL = add.Body.GetILProcessor();
        var duplicate = Instruction.Create(OpCodes.Dup);
        var server = Instruction.Create(OpCodes.Ldarg_0);
        addIL.InsertAfter(allocations[0],duplicate); addIL.InsertAfter(duplicate,server);
        addIL.InsertAfter(server,Instruction.Create(OpCodes.Call,Helper("InitializeAcquisitionTime")));
        foreach (var spec in new[]{new[]{"SortCreateID_GetTime","AcquisitionSortKey"},
            new[]{"SortCreateID_VerForm_GetTime","AcquisitionSortKeyForParty"}})
        {
            var sort = Method("MainMenuListSort",spec[0]); var sortIL = Reset(sort);
            foreach (var parameter in sort.Parameters) sortIL.Emit(OpCodes.Ldarg,parameter);
            sortIL.Emit(OpCodes.Call,Helper(spec[1])); sortIL.Emit(OpCodes.Ret);
        }
        // General news and scratch news are separate automatic pages.
        // Both flags reset with the resident/menu lifecycle; bypass each gate.
        foreach (string field in new[]{"m_StartingFirstInformation","m_StartingFirstScratch"})
        {
            var notice = Method("MainMenuSeqSwitchPage","Update").Body.Instructions.Single(i =>
                i.OpCode == OpCodes.Ldsfld && ((FieldReference)i.Operand).Name == field);
            notice.OpCode = OpCodes.Ldc_I4_1; notice.Operand = null;
        }
        Prefix("MainMenuSeqSwitchPage","Update","QuietHelperRewardNotice");
        var presents = Method("MainMenuSeqSwitchPage","Update").Body.Instructions.Where(i =>
            i.OpCode == OpCodes.Ldfld && ((FieldReference)i.Operand).Name == "m_StructPresentList").ToArray();
        if (presents.Length != 7) throw new Exception("Unexpected automatic-present reads: " + presents.Length);
        foreach (var present in presents)
        { present.OpCode = OpCodes.Call; present.Operand = Helper("NotificationPresents"); }
        foreach (var pair in new[] {new[]{"ServerForgery","StaminaAutoRecovery"},
            new[]{"UserDataAdmin","Update"},new[]{"MainMenuFixPlayerParam","Update"}})
        {
            var literals = Method(pair[0],pair[1]).Body.Instructions.Where(i =>
                i.OpCode == OpCodes.Ldc_R8 && (double)i.Operand == 600.0).ToArray();
            if (literals.Length != 1) throw new Exception("Unexpected stamina interval " + pair[0]); literals[0].Operand = 180.0;
            if (pair[0] != "MainMenuFixPlayerParam")
            {
                var due = Method(pair[0],pair[1]).Body.Instructions.Single(i => i.Operand is MethodReference &&
                    ((MethodReference)i.Operand).DeclaringType.FullName == "System.DateTime" &&
                    ((MethodReference)i.Operand).Name == "op_GreaterThan");
                var dateType = ((MethodReference)due.Operand).DeclaringType.Resolve();
                due.Operand = module.ImportReference(dateType.Methods.Single(m => m.Name == "op_GreaterThanOrEqual"));
            }
        }
        module.Types.Single(t => t.Name == "GlobalDefine").Fields.Single(f => f.Name == "STAMINA_RECOVERY_SEC").Constant = 180f;
        var caps = Method("ServerForgery","ForgeryStoneUsedUnit").Body.Instructions.Where(i =>
            i.OpCode == OpCodes.Ldc_I4 && (int)i.Operand == 400).ToArray();
        if (caps.Length != 1) throw new Exception("Unexpected warehouse cap"); caps[0].Operand = 2000;
        var shopCaps = module.Types.Single(t => t.Name == "MainMenuSeqShopUseTipUnit").Methods.Where(m => m.HasBody)
            .SelectMany(m => m.Body.Instructions).Where(i => i.OpCode == OpCodes.Ldc_I4 && (int)i.Operand == 400).ToArray();
        if (shopCaps.Length != 2) throw new Exception("Unexpected warehouse shop UI caps");
        foreach (var cap in shopCaps) cap.Operand = 2000;
        Prefix("ServerForgery","UserParamSave","BeforeAccountSave");
        Prefix("ServerForgery","UserParamLoad","BeforeSaveAccountLoad");
        BeforeEveryReturn("ServerForgery","UserParamLoad","AfterAccountLoad");
        Prefix("ServerForgery","CreateForgeryStructPlayer","BeforeAccountSave");
        var gacha = Method("ServerForgery","ForgeryGachaPlay");
        var request = gacha.Body.Variables.Single(v => v.VariableType.Name == "SendGachaPlay");
        var rolls = gacha.Body.Instructions.Where(i => i.Operand is MethodReference &&
            ((MethodReference)i.Operand).DeclaringType.Name == "RandManager" && ((MethodReference)i.Operand).Name == "GetRand").ToArray();
        if (rolls.Length != 2) throw new Exception("Unexpected scratch sampler");
        foreach (var roll in rolls)
        {
            var il = gacha.Body.GetILProcessor(); il.InsertBefore(roll,Instruction.Create(OpCodes.Ldarg_0));
            il.InsertBefore(roll,Instruction.Create(OpCodes.Ldloc,request)); roll.OpCode = OpCodes.Call; roll.Operand = Helper("ScratchTicket");
        }
    }
    static void WarehouseSupports()
    {
        ReadyPageInput("MainMenuSeqOthers","UpdateSaveTransferUI");
        CallHelper("MainMenuSeqOthers","WindowUpdateTransfer","SaveTransferInput",0);
        BindBridge("RebuildQuestSupports","MainMenuSeqQuestSelectFriend",(il,type) => {
            il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Ldc_I4_1); il.Emit(OpCodes.Ldc_I4_0);
            il.Emit(OpCodes.Call,Method(type.Name,"PageSwitchSetting")); il.Emit(OpCodes.Ret);
        });
        TransformReturn("MainMenuSeqQuestSelectFriend","FriendListThinning","QuestSupportList");
        ReadyPageInput("MainMenuSeqQuestSelectFriend","UpdateQuestSupportSource");
        GuardWarehouseRequest("ForgeryQuestStart","ValidateWarehouseQuest");
        foreach (string name in new[]{"GetLocalSaveUseFriend","SaveFuncUseFriend"})
        {
            var m = Method("LocalSaveManager",name); var il = m.Body.GetILProcessor(); var first=m.Body.Instructions.First();
            il.InsertBefore(first,Instruction.Create(OpCodes.Ldarg_1)); il.InsertBefore(first,Instruction.Create(OpCodes.Call,Helper("IsWarehouseSupport")));
            il.InsertBefore(first,Instruction.Create(OpCodes.Brfalse,first));
            if (name == "GetLocalSaveUseFriend")
            { il.InsertBefore(first,Instruction.Create(OpCodes.Ldarg_1)); il.InsertBefore(first,Instruction.Create(OpCodes.Call,Helper("WarehouseUseRecord"))); }
            il.InsertBefore(first,Instruction.Create(OpCodes.Ret));
        }
    }
}
