using System;
using System.IO;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

// Fail-closed, version-specific IL edits. Never rebuild the decompiled project.
public static partial class PatchPlayable210
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
    static void LegacySkillHooks210() {
        BeforeEveryReturn("InGameUtil","ActivityLimitBreakSkill","LegacyActivity210");
        var damage=Method("InGameUtil","GetDamageValue");
        if(damage.Parameters.Count!=9 || damage.ReturnType.FullName!="System.Int32" || damage.Parameters[8].ParameterType.Name!="BattleSkillActivity")throw new Exception("2.1.0 damage signature changed");
        var result=new VariableDefinition(module.TypeSystem.Int32);damage.Body.Variables.Add(result);damage.Body.InitLocals=true;
        var il=damage.Body.GetILProcessor();var first=damage.Body.Instructions.First();
        foreach(var a in damage.Parameters)il.InsertBefore(first,Instruction.Create(OpCodes.Ldarg,a));
        il.InsertBefore(first,Instruction.Create(OpCodes.Ldloca,result));il.InsertBefore(first,Instruction.Create(OpCodes.Call,Helper("LegacyDamage210")));
        il.InsertBefore(first,Instruction.Create(OpCodes.Brfalse,first));il.InsertBefore(first,Instruction.Create(OpCodes.Ldloc,result));il.InsertBefore(first,Instruction.Create(OpCodes.Ret));
        var status=Method("BattleUtil","SkillUpdate_StatusAilment");
        if(status.Parameters.Count!=3)throw new Exception("2.1.0 ailment signature changed");
        first=status.Body.Instructions.First();il=status.Body.GetILProcessor();
        foreach(var a in status.Parameters)il.InsertBefore(first,Instruction.Create(OpCodes.Ldarg,a));
        il.InsertBefore(first,Instruction.Create(OpCodes.Call,Helper("LegacyAilment210")));il.InsertBefore(first,Instruction.Create(OpCodes.Brfalse,first));il.InsertBefore(first,Instruction.Create(OpCodes.Ret));
    }
    static void RemoveDiagnosticAchievements210() {
        var method=Method("ServerForgery","ForgeryGetMasterDataAll");
        var loads=method.Body.Instructions.Where(i=>i.OpCode==OpCodes.Ldfld && ((FieldReference)i.Operand).Name=="m_MasterDataAchievement").ToArray();
        if(loads.Length!=5)throw new Exception("Unexpected achievement diagnostic references");
        var last=loads.Last();while(last.OpCode!=OpCodes.Stelem_I4)last=last.Next;
        var store=method.Body.Instructions.Single(i=>i.OpCode==OpCodes.Stfld && ((FieldReference)i.Operand).Name=="achievement_clear");
        var array=store.Previous;if(array.OpCode!=OpCodes.Newarr || array.Previous.OpCode!=OpCodes.Ldc_I4_6)throw new Exception("Unexpected achievement header shape");
        array.Previous.OpCode=OpCodes.Ldc_I4_0;
        method.Body.GetILProcessor().InsertAfter(store,Instruction.Create(OpCodes.Br,last.Next));
    }
    public static void Main(string[] args)
    {
        var resolver = new DefaultAssemblyResolver();
        resolver.AddSearchDirectory(Path.GetDirectoryName(args[0]));
        resolver.AddSearchDirectory(Path.GetDirectoryName(args[1]));
        module = ModuleDefinition.ReadModule(args[0], new ReaderParameters { AssemblyResolver = resolver });
        adapter = ModuleDefinition.ReadModule(args[1], new ReaderParameters { AssemblyResolver = resolver });
        if (module.Types.Any(t => t.Name == "DG210PlayablePatched")) throw new Exception("Already patched");
        var statusApi=module.Types.Single(t=>t.Name=="StatusAilmentUtil");
        if(!statusApi.IsNotPublic)throw new Exception("Unexpected 2.1.0 status API visibility");
        statusApi.IsPublic=true;
        var server=module.Types.Single(t=>t.Name=="ServerForgery");
        Constant("LocalSaveManager", "LoadFuncServerAddressIP", "");
        Append("ResidentManager", "Start", "Bootstrap");
        StartHelper("ServerForgery", "Ready210");
        var startServer = Method("ServerForgery", "Start");
        var serverEnd = startServer.Body.Instructions.Last();
        foreach (string rebuild in new[] { "ReMakeFriendList", "ReMakeFriendHelper", "ReMakeMasterHash" }) {
            var rebuildIL = startServer.Body.GetILProcessor();
            rebuildIL.InsertBefore(serverEnd, Instruction.Create(OpCodes.Ldarg_0));
            rebuildIL.InsertBefore(serverEnd, Instruction.Create(OpCodes.Call, Method("ServerForgery",rebuild)));
        }
        var monthly = Reset(Method("ServerForgery","CreateLoginMonthlyList"));
        monthly.Emit(OpCodes.Ldnull); monthly.Emit(OpCodes.Ret);
        var save = server.Fields.Single(f=>f.Name=="m_ForgeryPlayerSave");
        var getter = new MethodDefinition("DG210Player",MethodAttributes.Public|MethodAttributes.HideBySig,save.FieldType);
        server.Methods.Add(getter);
        getter.Body.GetILProcessor().Emit(OpCodes.Ldarg_0);
        getter.Body.GetILProcessor().Emit(OpCodes.Ldfld,save);
        getter.Body.GetILProcessor().Emit(OpCodes.Ret);
        var bridge=adapter.Types.Single(t=>t.Name=="DivineGateOffline").Methods.Single(m=>m.Name=="Local210Player");
        var bridgeIL=Reset(bridge);bridgeIL.Emit(OpCodes.Ldarg_0);bridgeIL.Emit(OpCodes.Callvirt,adapter.ImportReference(getter));bridgeIL.Emit(OpCodes.Ret);
        BeforeEveryReturn("ServerForgery","UserParamCreate","Normalize210");
        BeforeEveryReturn("ServerForgery","UserParamRenew","Normalize210");
        BeforeEveryReturn("ServerForgery","UserParamLoad","Normalize210");
        EventMissionAccess();
        LegacySkillHooks210();
        RemoveDiagnosticAchievements210();
        FreeChipShop210();

        StartHelper("PatchManager", "SetupPatch");
        CallHelper("PatchManager", "GetPatchText", "PatchText210", 1);
        Empty("PatchManager", "PatchUpdateRequest");
        Boolean("PatchUtil", "CheckAppUpdate", false);
        // Preserve banner rendering, but never enter its link/app-scheme branch.
        var banner=Method("TitleMenuFixLayerBanner","Update");
        var bannerFirst=banner.Body.Instructions.First();var bannerIL=banner.Body.GetILProcessor();
        var touchFlag=module.Types.Single(t=>t.Name=="TitleMenuFixLayerBanner").Fields.Single(f=>f.Name=="m_BannerTouchActive");
        bannerIL.InsertBefore(bannerFirst,Instruction.Create(OpCodes.Ldarg_0));
        bannerIL.InsertBefore(bannerFirst,Instruction.Create(OpCodes.Ldc_I4_0));
        bannerIL.InsertBefore(bannerFirst,Instruction.Create(OpCodes.Stfld,touchFlag));
        foreach (string titleLinkType in new[] { "TitleMenuFixLayerBanner", "TitleMenuSeqWaitInput", "LinkerOpenURL" }) {
            var linkMethod=Method(titleLinkType,"Update");
            var links=linkMethod.Body.Instructions.Where(i=>i.OpCode==OpCodes.Call && i.Operand is MethodReference &&
                ((MethodReference)i.Operand).DeclaringType.FullName=="UnityEngine.Application" && ((MethodReference)i.Operand).Name=="OpenURL").ToArray();
            if(links.Length!=(titleLinkType=="TitleMenuFixLayerBanner"?4:1))throw new Exception("Unexpected title link contract: "+titleLinkType);
            foreach(var link in links) {
                var target=(MethodReference)link.Operand;
                if(target.Parameters.Count!=1 || target.ReturnType.FullName!="System.Void")throw new Exception("Unexpected OpenURL signature");
                link.OpCode=OpCodes.Pop;link.Operand=null;
            }
        }
        // Skip only the obsolete online policy gate in the offline title router.
        // Do not write agree_ok_policy or alter the user's saved consent state.
        var router=Method("TitleMenuSeqSwitchPage","Update");
        var policyRead=router.Body.Instructions.Single(i=>i.Operand is MethodReference && ((MethodReference)i.Operand).DeclaringType.Name=="LocalSaveManager" && ((MethodReference)i.Operand).Name=="LoadFuncInformationPolicyOK");
        var policyTarget=(MethodReference)policyRead.Operand;
        if(!policyTarget.HasThis || policyTarget.Parameters.Count!=0 || policyTarget.ReturnType.FullName!="System.Int32")throw new Exception("Policy gate contract changed");
        policyRead.OpCode=OpCodes.Pop;policyRead.Operand=null;
        router.Body.GetILProcessor().InsertAfter(policyRead,Instruction.Create(OpCodes.Ldc_I4_2));
        PageUpdate("TitleMenuSeqInformationFox", "SkipAgreement");
        BaseLayoutOnly("TitleMenuSeqInformationFox");
        PageUpdate("TitleMenuSeqLoadAssetBundle", "LoadLocalAssets");
        BaseLayoutOnly("TitleMenuSeqLoadAssetBundle");
        PageUpdate("TitleMenuSeqLoadMasterAndAssetBundle","LoadLocalAssets210");
        BaseLayoutOnly("TitleMenuSeqLoadMasterAndAssetBundle");
        var request=Method("AssetBundleManager","Request",5);
        var reqIL=Reset(request);
        for(int i=0;i<4;i++)reqIL.Emit(OpCodes.Ldarg,request.Parameters[i]);
        reqIL.Emit(OpCodes.Call,Helper("Request"));reqIL.Emit(OpCodes.Ret);
        var clearAssets = Method("AssetBundleManager", "ClearAssetBundle");
        var firstClear = clearAssets.Body.Instructions.First();
        var clearIL = clearAssets.Body.GetILProcessor();
        clearIL.InsertBefore(firstClear, Instruction.Create(OpCodes.Ldarg_0));
        clearIL.InsertBefore(firstClear, Instruction.Create(OpCodes.Call, Helper("ClearTransientAssets")));
        Empty("AssetBundleManager", "SetupAssetBundlePackageParam");
        BeforeEveryReturn("AssetAutoSetCharaMesh", "Update", "FixCharacterShadow");
        var residentType = module.Types.Single(t => t.Name == "ResidentManager");
        if (residentType.Methods.Any(m => m.Name == "Update")) throw new Exception("Unexpected resident Update");
        var nativePump = new MethodDefinition("Update", MethodAttributes.Private | MethodAttributes.HideBySig, module.TypeSystem.Void);
        residentType.Methods.Add(nativePump);
        nativePump.Body.GetILProcessor().Emit(OpCodes.Call, Helper("PumpNativeAssets"));
        nativePump.Body.GetILProcessor().Emit(OpCodes.Ret);
        var battleType = module.Types.Single(t => t.Name == "BattleManager");
        if (battleType.Methods.Any(m => m.Name == "LateUpdate")) throw new Exception("Unexpected battle LateUpdate");
        var hudUpdate = new MethodDefinition("LateUpdate", MethodAttributes.Private | MethodAttributes.HideBySig, module.TypeSystem.Void);
        battleType.Methods.Add(hudUpdate);
        var hudIL = hudUpdate.Body.GetILProcessor();
        hudIL.Emit(OpCodes.Ldarg_0); hudIL.Emit(OpCodes.Call, Helper("RaiseEnemyHud")); hudIL.Emit(OpCodes.Ret);
        Empty("StoreManager", "Update");
        var destroy=Method("StoreManager","OnDestroy");
        var destroyBase=(MethodReference)destroy.Body.Instructions.Single(i=>i.OpCode==OpCodes.Call && i.Operand is MethodReference && ((MethodReference)i.Operand).Name=="OnDestroy").Operand;
        var destroyIL=Reset(destroy);destroyIL.Emit(OpCodes.Ldarg_0);destroyIL.Emit(OpCodes.Call,destroyBase);destroyIL.Emit(OpCodes.Ret);
        var androidAwake=Method("AndroidDeviceInfo","Awake");
        var baseAwake=(MethodReference)androidAwake.Body.Instructions.Single(i=>i.OpCode==OpCodes.Call && i.Operand is MethodReference && ((MethodReference)i.Operand).Name=="Awake").Operand;
        var awakeIL=Reset(androidAwake);awakeIL.Emit(OpCodes.Ldarg_0);awakeIL.Emit(OpCodes.Call,baseAwake);awakeIL.Emit(OpCodes.Ret);
        Empty("AndroidDeviceInfo","UpdateExStoragePath");
        Boolean("AndroidDeviceInfo","IsExStorageActive",false);
        StartHelper("StoreManager","DisableStore");
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
        module.Types.Add(new TypeDefinition("", "DG210PlayablePatched", TypeAttributes.NotPublic, module.TypeSystem.Object));
        module.Write(args[2]);
        File.WriteAllLines(args[3], module.Types.SelectMany(t=>t.Methods).Where(m=>m.HasBody)
          .Where(m=>m.Body.Instructions.Any(i=>i.Operand is MethodReference && ((MethodReference)i.Operand).DeclaringType.Name=="DivineGateOffline"))
          .Select(m=>m.FullName));
        // The adapter's save accessor now references the patched game method.
        // Write through a new file so the input assembly is never truncated.
        adapter.Write(args[1] + ".patched");
        adapter.Dispose();
        File.Copy(args[1] + ".patched", args[1], true);
        File.Delete(args[1] + ".patched");
        Console.WriteLine("Patched 2.1.0: local API, local master data, assets, patch checks, billing, sign-in, notice");
    }
}
