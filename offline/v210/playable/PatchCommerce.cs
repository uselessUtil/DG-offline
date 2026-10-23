using System;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

public static partial class PatchPlayable210
{
    static void FreeChipShop210()
    {
        var resource = module.Types.Single(t => t.Name == "MainMenuManager").Methods
            .Where(m => m.HasBody).SelectMany(m => m.Body.Instructions)
            .Single(i => i.OpCode == OpCodes.Ldstr && (string)i.Operand == "Prefab/MainMenu/MainMenuPage/PageShopBuyTip");
        resource.Operand = "Prefab/MainMenu/MainMenuPage/_old_PageShopBuyTip";
        var getter = Method("ServerForgery", "DG210Player");
        var stub = adapter.Types.Single(t => t.Name == "DivineGateOffline").Methods.Single(m => m.Name == "LocalShopPlayer");
        var il = Reset(stub);
        il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Callvirt, adapter.ImportReference(getter)); il.Emit(OpCodes.Ret);
        CallHelper("StoreManager", "GetProductFromNum", "FreeChipProductAt", 1);
        CallHelper("StoreManager", "GetProductFromID", "FreeChipProductByID", 1);
        CallHelper("StoreParam", "SetupStoreProductListNow", "SetupFreeChipList210");
        CallHelper("StoreCommonUI", "ProductsSetting", "SetupFreeChipProducts210", 0);
        // Never wait on the live-store product-event API before opening a page.
        CallHelper("MainMenuSeqShopRoot", "PageSwitchEventEnableBefore", "LocalShopBefore210");
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

}
