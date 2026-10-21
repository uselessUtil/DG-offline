using System;
using System.IO;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

// Fail-closed, version-specific IL edits. Never rebuild the decompiled project.
public static class Patch106
{
    static ModuleDefinition module;
    static ModuleDefinition adapter;
    static MethodDefinition Method(string type, string name, int count = -1)
    {
        return module.Types.Single(t => t.Name == type).Methods.Single(m => m.Name == name &&
            (count < 0 || m.Parameters.Count == count));
    }
    static MethodReference Helper(string name)
    {
        return module.ImportReference(adapter.Types.Single(t => t.Name == "DivineGateOffline").Methods.Single(m => m.Name == name));
    }
    static ILProcessor Reset(MethodDefinition method)
    {
        method.Body = new MethodBody(method);
        return method.Body.GetILProcessor();
    }
    static void Empty(string type, string name)
    {
        Reset(Method(type, name)).Emit(OpCodes.Ret);
    }
    static void Constant(string type, string name, string value)
    {
        var il = Reset(Method(type, name));
        il.Emit(OpCodes.Ldstr, value); il.Emit(OpCodes.Ret);
    }
    static void Boolean(string type, string name, bool value)
    {
        var il = Reset(Method(type, name));
        il.Emit(value ? OpCodes.Ldc_I4_1 : OpCodes.Ldc_I4_0); il.Emit(OpCodes.Ret);
    }
    static void CallHelper(string type, string name, string helper, params int[] args)
    {
        var method = Method(type, name);
        var il = Reset(method);
        foreach (int arg in args)
            if (arg == 0) il.Emit(OpCodes.Ldarg_0);
            else il.Emit(OpCodes.Ldarg, method.Parameters[arg - 1]);
        il.Emit(OpCodes.Call, Helper(helper)); il.Emit(OpCodes.Ret);
    }
    static void StartHelper(string type, string helper)
    {
        var method = Method(type, "Start");
        var baseStart = (MethodReference)method.Body.Instructions.First(i => i.OpCode == OpCodes.Call &&
            ((MethodReference)i.Operand).Name == "Start").Operand;
        var il = Reset(method);
        il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Call, baseStart);
        il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Call, Helper(helper)); il.Emit(OpCodes.Ret);
    }
    static void Append(string type, string name, string helper)
    {
        var m = Method(type, name);
        var ends = m.Body.Instructions.Where(i => i.OpCode == OpCodes.Ret).ToArray();
        if (ends.Length != 1) throw new Exception(type + "." + name + " has unexpected exits");
        // Change the return itself so branch targets cannot skip the adapter call.
        var end = ends.Single();
        end.OpCode = OpCodes.Ldarg_0;
        var il = m.Body.GetILProcessor();
        il.InsertAfter(end, Instruction.Create(OpCodes.Call, Helper(helper)));
        il.Append(Instruction.Create(OpCodes.Ret));
    }
    static void BeforeEveryReturn(string type, string name, string helper)
    {
        var method = Method(type, name);
        var exits = method.Body.Instructions.Where(i => i.OpCode == OpCodes.Ret).ToArray();
        if (exits.Length == 0) throw new Exception("No exits in " + type + "." + name);
        var il = method.Body.GetILProcessor();
        foreach (var end in exits)
        {
            end.OpCode = OpCodes.Ldarg_0;
            var call = Instruction.Create(OpCodes.Call, Helper(helper));
            il.InsertAfter(end, call);
            il.InsertAfter(call, Instruction.Create(OpCodes.Ret));
        }
    }
    static void PageUpdate(string type, string helper)
    {
        var il = Reset(Method(type, "Update"));
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Call, Method("LayoutSwitchSeq", "LayoutSwitchUpdate"));
        il.Emit(OpCodes.Call, Helper(helper));
        il.Emit(OpCodes.Ret);
    }
    static void BaseLayoutOnly(string type)
    {
        var il = Reset(Method(type, "LayoutSwitchSetting"));
        il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Ldarg_1);
        il.Emit(OpCodes.Call, Method("LayoutSwitchSeq", "LayoutSwitchSetting"));
        il.Emit(OpCodes.Ret);
    }
    static void EventMissionAccess()
    {
        var method = Method("MainMenuSeqQuestSelectMission", "PageSwitchSetting");
        var writes = method.Body.Instructions.Where(i => i.OpCode == OpCodes.Stfld &&
            ((FieldReference)i.Operand).Name == "m_QuestPlayable").ToArray();
        // The second write enables the first uncleared mission. Keep it intact.
        if (writes.Length != 2) throw new Exception("Unexpected quest-playable writes");
        var write = writes.Single(i => i.Previous.OpCode == OpCodes.Ldloc_S);
        if (writes.Count(i => i.Previous.OpCode == OpCodes.Ldc_I4_1) != 1)
            throw new Exception("Unexpected first-uncleared quest access");
        if (method.Body.Instructions.Any(i => i.Operand == write ||
            (i.Operand is Instruction[] && ((Instruction[])i.Operand).Contains(write))))
            throw new Exception("Unexpected branch into quest-playable assignment");
        var owner = ((FieldReference)write.Operand).DeclaringType.Resolve();
        var questField = owner.Fields.Single(f => f.Name == "m_QuestMasterData");
        var original = new VariableDefinition(module.TypeSystem.Boolean);
        method.Body.Variables.Add(original);
        method.Body.InitLocals = true;
        var il = method.Body.GetILProcessor();
        // Stack before: [QuestParam, original bool]. Keep that object and pass
        // its assigned master row to the scoped event-only access rule.
        il.InsertBefore(write, Instruction.Create(OpCodes.Stloc, original));
        il.InsertBefore(write, Instruction.Create(OpCodes.Dup));
        il.InsertBefore(write, Instruction.Create(OpCodes.Ldfld, questField));
        il.InsertBefore(write, Instruction.Create(OpCodes.Ldloc, original));
        il.InsertBefore(write, Instruction.Create(OpCodes.Call, Helper("EventQuestPlayable")));
    }
    static void FreeChipShop()
    {
        // The current PageShopBuyTip is only a title/container for dialogs.
        // Our inline Yes/No flow needs the complete, still-bundled old prefab.
        var pageResource = module.Types.Single(t => t.Name == "MainMenuManager").Methods
            .Where(m => m.HasBody).SelectMany(m => m.Body.Instructions)
            .Single(i => i.OpCode == OpCodes.Ldstr && (string)i.Operand == "Prefab/MainMenu/MainMenuPage/PageShopBuyTip");
        pageResource.Operand = "Prefab/MainMenu/MainMenuPage/_old_PageShopBuyTip";
        var server = module.Types.Single(t => t.Name == "ServerForgery");
        var save = server.Fields.Single(f => f.Name == "m_ForgeryPlayerSave" && f.FieldType.Name == "ServerForgerySaveUser");
        if (server.Methods.Any(m => m.Name == "DGOfflineShopPlayer")) throw new Exception("Shop save accessor already exists");
        var accessor = new MethodDefinition("DGOfflineShopPlayer", MethodAttributes.Public | MethodAttributes.HideBySig, save.FieldType);
        server.Methods.Add(accessor);
        var il = accessor.Body.GetILProcessor();
        il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Ldfld, save); il.Emit(OpCodes.Ret);
        var stub = adapter.Types.Single(t => t.Name == "DivineGateOffline").Methods.Single(m => m.Name == "LocalShopPlayer");
        var stubIL = Reset(stub);
        stubIL.Emit(OpCodes.Ldarg_0); stubIL.Emit(OpCodes.Callvirt, adapter.ImportReference(accessor)); stubIL.Emit(OpCodes.Ret);

        CallHelper("StoreManager", "GetProductFromNum", "FreeChipProductAt", 1);
        CallHelper("StoreManager", "GetProductFromID", "FreeChipProductByID", 1);
        CallHelper("MainMenuSeqShopRoot", "ProductSetup", "SetupFreeChipProduct", 1, 2, 3, 4);
        var rootUpdate = Method("MainMenuSeqShopRoot", "Update");
        var storeGate = rootUpdate.Body.Instructions.Single(i => i.OpCode == OpCodes.Call &&
            i.Operand is MethodReference && ((MethodReference)i.Operand).DeclaringType.Name == "SystemUtil" &&
            ((MethodReference)i.Operand).Name == "GetStoreOpen");
        storeGate.Operand = Helper("LocalChipShopOpen");

        var page = Method("MainMenuSeqShopBuyTip", "PageSwitchSetting");
        var baseSetting = (MethodReference)page.Body.Instructions.First(i => i.OpCode == OpCodes.Call &&
            i.Operand is MethodReference && ((MethodReference)i.Operand).DeclaringType.Name == "MainMenuSeq" &&
            ((MethodReference)i.Operand).Name == "PageSwitchSetting").Operand;
        il = Reset(page);
        il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Ldarg_1); il.Emit(OpCodes.Ldarg_2); il.Emit(OpCodes.Call, baseSetting);
        il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Ldarg_1); il.Emit(OpCodes.Call, Helper("SetupFreeChipPage")); il.Emit(OpCodes.Ret);
        var update = Method("MainMenuSeqShopBuyTip", "Update");
        il = Reset(update);
        il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Call, Method("MainMenuSeq", "PageSwitchUpdate"));
        il.Emit(OpCodes.Call, Helper("UpdateFreeChipPage")); il.Emit(OpCodes.Ret);
    }
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
            throw new Exception("Gacha route must preserve the original request result and add the page argument");
        request.OpCode = OpCodes.Call;
        request.Operand = route;
        var first = update.Body.Instructions.First();
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
        foreach (var end in setup.Body.Instructions.Where(i => i.OpCode == OpCodes.Ret).ToArray())
        {
            end.OpCode = OpCodes.Ldarg_0;
            var active = Instruction.Create(OpCodes.Ldarg_1);
            var call = Instruction.Create(OpCodes.Call, Helper("RefreshCollabRoot"));
            il.InsertAfter(end, active); il.InsertAfter(active, call);
            il.InsertAfter(call, Instruction.Create(OpCodes.Ret));
        }
    }
    public static void Main(string[] args)
    {
        var resolver = new DefaultAssemblyResolver();
        resolver.AddSearchDirectory(Path.GetDirectoryName(args[0]));
        resolver.AddSearchDirectory(Path.GetDirectoryName(args[1]));
        module = ModuleDefinition.ReadModule(args[0], new ReaderParameters { AssemblyResolver = resolver });
        adapter = ModuleDefinition.ReadModule(args[1], new ReaderParameters { AssemblyResolver = resolver });
        if (module.Types.Any(t => t.Name == "DGOfflinePatched")) throw new Exception("Already patched");
        var fields = module.Types.Single(t => t.Name == "MasterDataManager").Fields.Where(f => f.Name.StartsWith("m_MasterData"));
        var server = module.Types.Single(t => t.Name == "ServerForgery");
        foreach (var field in fields)
            if (!server.Fields.Any(f => f.Name == field.Name && f.FieldType.FullName == field.FieldType.FullName))
                throw new Exception("Missing local table " + field.Name);
        Constant("LocalSaveManager", "LoadFuncServerAddressIP", "");
        Append("ResidentManager", "Start", "Bootstrap");
        Append("ServerForgery", "Start", "Ready");
        EventMissionAccess();
        FreeChipShop();
        CollabGachaSelector();
        StartHelper("PatchManager", "SetupPatch");
        CallHelper("PatchManager", "GetPatchText", "PatchText", 1);
        Empty("PatchManager", "PatchUpdateRequest");
        Boolean("PatchUtil", "CheckAppUpdate", false);
        PageUpdate("TitleMenuSeqInformationFox", "SkipAgreement");
        BaseLayoutOnly("TitleMenuSeqInformationFox");
        PageUpdate("TitleMenuSeqLoadAssetBundle", "LoadLocalAssets");
        BaseLayoutOnly("TitleMenuSeqLoadAssetBundle");
        CallHelper("AssetBundleManager", "Request", "Request", 1, 2, 3, 4);
        var clearAssets = Method("AssetBundleManager", "ClearAssetBundle");
        var firstClear = clearAssets.Body.Instructions.First();
        var clearIL = clearAssets.Body.GetILProcessor();
        clearIL.InsertBefore(firstClear, Instruction.Create(OpCodes.Ldarg_0));
        clearIL.InsertBefore(firstClear, Instruction.Create(OpCodes.Call, Helper("ClearTransientAssets")));
        Empty("AssetBundleManager", "SetupAssetBundlePackageParam");
        BeforeEveryReturn("AssetAutoSetCharaMesh", "Update", "FixCharacterShadow");
        var battleType = module.Types.Single(t => t.Name == "BattleManager");
        if (battleType.Methods.Any(m => m.Name == "LateUpdate")) throw new Exception("Unexpected battle LateUpdate");
        var hudUpdate = new MethodDefinition("LateUpdate", MethodAttributes.Private | MethodAttributes.HideBySig, module.TypeSystem.Void);
        battleType.Methods.Add(hudUpdate);
        var hudIL = hudUpdate.Body.GetILProcessor();
        hudIL.Emit(OpCodes.Ldarg_0); hudIL.Emit(OpCodes.Call, Helper("RaiseEnemyHud")); hudIL.Emit(OpCodes.Ret);
        StartHelper("StoreManager", "DisableStore");
        Empty("StoreManager", "Update");
        foreach (var method in module.Types.Single(t => t.Name == "PlayGameServiceUtil").Methods.Where(m => m.IsStatic && m.ReturnType.FullName == "System.Void"))
            Reset(method).Emit(OpCodes.Ret);
        // Prevent the title from showing an unusable sign-in button.
        Boolean("PlayGameServiceUtil", "isSignedIn", true);
        var touchCell = Method("InGameManager", "GetTouchCell");
        var firstTouch = touchCell.Body.Instructions.First();
        var touchIL = touchCell.Body.GetILProcessor();
        touchIL.InsertBefore(firstTouch, Instruction.Create(OpCodes.Ldarg_0));
        touchIL.InsertBefore(firstTouch, Instruction.Create(OpCodes.Call, Helper("TraceDoorInput")));
        var getSprite = Method("UIAtlas", "GetSprite");
        var first = getSprite.Body.Instructions.First();
        var spriteIL = getSprite.Body.GetILProcessor();
        spriteIL.InsertBefore(first, Instruction.Create(OpCodes.Ldarg_0));
        spriteIL.InsertBefore(first, Instruction.Create(OpCodes.Ldarg_1));
        spriteIL.InsertBefore(first, Instruction.Create(OpCodes.Call, Helper("EnsureSprite")));
        var gui = new MethodDefinition("OnGUI", MethodAttributes.Private | MethodAttributes.HideBySig, module.TypeSystem.Void);
        module.Types.Single(t => t.Name == "ResidentManager").Methods.Add(gui);
        gui.Body.GetILProcessor().Emit(OpCodes.Call, Helper("DrawNotice"));
        gui.Body.GetILProcessor().Emit(OpCodes.Ret);
        module.Types.Add(new TypeDefinition("", "DGOfflinePatched", TypeAttributes.NotPublic, module.TypeSystem.Object));
        module.Write(args[2]);
        // The adapter's save accessor now references the patched game method.
        // Write through a new file so the input assembly is never truncated.
        adapter.Write(args[1] + ".patched");
        adapter.Dispose();
        File.Copy(args[1] + ".patched", args[1], true);
        File.Delete(args[1] + ".patched");
        Console.WriteLine("Patched 1.0.6: local API, local master data, assets, patch checks, billing, sign-in, notice");
    }
}
