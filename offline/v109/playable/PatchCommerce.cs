using System;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

public static partial class PatchPlayable109
{
    static void FreeChipShop109()
    {
        var resource = module.Types.Single(t => t.Name == "MainMenuManager").Methods
            .Where(m => m.HasBody).SelectMany(m => m.Body.Instructions)
            .Single(i => i.OpCode == OpCodes.Ldstr && (string)i.Operand == "Prefab/MainMenu/MainMenuPage/PageShopBuyTip");
        resource.Operand = "Prefab/MainMenu/MainMenuPage/_old_PageShopBuyTip";
        var getter = Method("ServerForgery", "DG109Player");
        var stub = adapter.Types.Single(t => t.Name == "DivineGateOffline").Methods.Single(m => m.Name == "LocalShopPlayer");
        var il = Reset(stub);
        il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Callvirt, adapter.ImportReference(getter)); il.Emit(OpCodes.Ret);
        CallHelper("StoreManager", "GetProductFromNum", "FreeChipProductAt", 1);
        CallHelper("StoreManager", "GetProductFromID", "FreeChipProductByID", 1);
        CallHelper("StoreParam", "SetupStoreProductListNow", "SetupFreeChipList109");
        CallHelper("StoreCommonUI", "ProductsSetting", "SetupFreeChipProducts109", 0);
        // Never wait on the live-store product-event API before opening a page.
        CallHelper("MainMenuSeqShopRoot", "PageSwitchEventEnableBefore", "LocalShopBefore109");
        var update = Method("MainMenuSeqShopRoot", "Update");
        var storeGate = update.Body.Instructions.Single(i => i.OpCode == OpCodes.Call && i.Operand is MethodReference &&
            ((MethodReference)i.Operand).DeclaringType.Name == "SystemUtil" && ((MethodReference)i.Operand).Name == "GetStoreOpen");
        storeGate.Operand = Helper("LocalChipShopOpen");
        var page = Method("MainMenuSeqShopBuyTip", "PageSwitchSetting");
        var baseSetting = (MethodReference)page.Body.Instructions.First(i => i.OpCode == OpCodes.Call && i.Operand is MethodReference &&
            ((MethodReference)i.Operand).DeclaringType.Name == "MainMenuSeq" && ((MethodReference)i.Operand).Name == "PageSwitchSetting").Operand;
        il = Reset(page);
        il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Ldarg_1); il.Emit(OpCodes.Ldarg_2); il.Emit(OpCodes.Call, baseSetting);
        il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Ldarg_1); il.Emit(OpCodes.Call, Helper("SetupFreeChipPage")); il.Emit(OpCodes.Ret);
        il = Reset(Method("MainMenuSeqShopBuyTip", "Update"));
        il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Call, Method("MainMenuSeq", "PageSwitchUpdate"));
        il.Emit(OpCodes.Call, Helper("UpdateFreeChipPage")); il.Emit(OpCodes.Ret);
    }

    // Same three-card root as 1.0.6. The original tutorial and friendship paths
    // keep their requests and step transitions; only collab confirmation opens
    // the local four-pool selector.
    static void CollabGachaSelector()
    {
        var update = Method("MainMenuSeqGachaRoot", "Update");
        var request = update.Body.Instructions.Single(i => i.Operand is MethodReference &&
            ((MethodReference)i.Operand).DeclaringType.Name == "MainMenuManager" &&
            ((MethodReference)i.Operand).Name == "AddSwitchRequest");
        var il = update.Body.GetILProcessor();
        il.InsertBefore(request, Instruction.Create(OpCodes.Ldarg_0));
        var route = Helper("RouteGachaRootRequest");
        if (route.ReturnType.FullName != ((MethodReference)request.Operand).ReturnType.FullName || route.Parameters.Count != 5)
            throw new Exception("Unexpected gacha request contract");
        request.OpCode = OpCodes.Call; request.Operand = route;
        var first = update.Body.Instructions.First();
        il.InsertBefore(first, Instruction.Create(OpCodes.Ldarg_0));
        il.InsertBefore(first, Instruction.Create(OpCodes.Call, Helper("MaintainCollabRoot")));
        il.InsertBefore(first, Instruction.Create(OpCodes.Ldarg_0));
        il.InsertBefore(first, Instruction.Create(OpCodes.Call, Helper("CollabSelectorOpen")));
        il.InsertBefore(first, Instruction.Create(OpCodes.Brfalse, first));
        il.InsertBefore(first, Instruction.Create(OpCodes.Ldarg_0));
        il.InsertBefore(first, Instruction.Create(OpCodes.Ldarg_0));
        il.InsertBefore(first, Instruction.Create(OpCodes.Call, Method("MainMenuSeq", "PageSwitchUpdate")));
        il.InsertBefore(first, Instruction.Create(OpCodes.Call, Helper("UpdateCollabSelector")));
        il.InsertBefore(first, Instruction.Create(OpCodes.Ret));
        var setup = Method("MainMenuSeqGachaRoot", "PageSwitchSetting");
        il = setup.Body.GetILProcessor();
        var firstSetup = setup.Body.Instructions.First();
        il.InsertBefore(firstSetup, Instruction.Create(OpCodes.Ldarg_0));
        il.InsertBefore(firstSetup, Instruction.Create(OpCodes.Ldarg_1));
        il.InsertBefore(firstSetup, Instruction.Create(OpCodes.Call, Helper("BeforeCollabRootRefresh")));
        foreach (var end in setup.Body.Instructions.Where(i => i.OpCode == OpCodes.Ret).ToArray()) {
            end.OpCode = OpCodes.Ldarg_0;
            var active = Instruction.Create(OpCodes.Ldarg_1);
            var call = Instruction.Create(OpCodes.Call, Helper("RefreshCollabRoot"));
            il.InsertAfter(end, active); il.InsertAfter(active, call);
            il.InsertAfter(call, Instruction.Create(OpCodes.Ret));
        }
    }
}
