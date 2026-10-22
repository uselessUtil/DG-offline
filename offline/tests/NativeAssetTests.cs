using System;
using System.Reflection;
using System.Runtime.Serialization;
using UnityEngine;

public static class NativeAssetTests
{
    static void Assert(bool test,string message) { if (!test) throw new Exception(message); }
    static object Call(string name,params object[] args)
    { return typeof(DivineGateOffline).GetMethod(name,BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,args); }
    static string Package(string bundle,string name,Type type)
    { return (string)Call("NativePackage",bundle,name,type); }
    static UnityEngine.Object Token(Type type)
    { return (UnityEngine.Object)FormatterServices.GetUninitializedObject(type); }
    public static void Main()
    {
        string map=Package("map_0001.unity3d","map_0001_mesh",typeof(GameObject));
        string chars=Package("Unit_010_019.unity3d","chara_prefab_018",typeof(UnityEngine.Object));
        string other=Package("map_5005.unity3d","map_5005_mesh",typeof(GameObject));
        Assert(map!=null && chars!=null && other!=null,"Native required routes missing");
        Assert(Package("map_0001.unity3d","mapMaterial",typeof(Material))==map,"Shared model material route lost");
        Assert(Package("map_0001.unity3d","map_0006_tex",typeof(Texture))==map,"Texture variant must use native full sheet");
        Assert(Package("map_6001.unity3d","map_6001_mesh",typeof(GameObject))==null,"Missing Halloween map must use fallback");
        Assert(Package("map_0008.unity3d","ef_stage_0012",typeof(GameObject))==null,"Missing stage effect must use fallback");
        var floor=DivineGateOffline.Request("map_0001.unity3d","map_0001_mesh",ASSETBUNDLE_TAG.INGAME,typeof(GameObject));
        var door=DivineGateOffline.Request("map_0001.unity3d","map_0001_door",ASSETBUNDLE_TAG.INGAME,typeof(GameObject));
        var mat=DivineGateOffline.Request("map_0001.unity3d","mapMaterial",ASSETBUNDLE_TAG.INGAME,typeof(Material));
        var chara=DivineGateOffline.Request("Unit_010_019.unity3d","chara_prefab_018",ASSETBUNDLE_TAG.NONE,typeof(UnityEngine.Object));
        var third=DivineGateOffline.Request("map_5005.unity3d","map_5005_mesh",ASSETBUNDLE_TAG.INGAME,typeof(GameObject));
        Assert(floor.m_ResultStep==ASSETBUNDLE_STEP.LOAD_LOADING,"Must not report early success");
        DivineGateOffline.PumpNativeAssets();
        Assert(EngineSupport.NativeUrls().Length==2,"Concurrent loads must be bounded at two");
        foreach (string url in EngineSupport.NativeUrls()) Assert(url.StartsWith("jar:file://") && url.IndexOf("!/assets/dg-native/")>0,"Non-local bundle URL");
        var b=EngineSupport.NativeComplete(map,null);
        var fg=Token(typeof(GameObject)); var dg=Token(typeof(GameObject));var mg=Token(typeof(Material));
        EngineSupport.NativeSetAsset(b,"map_0001_mesh",fg);
        EngineSupport.NativeSetAsset(b,"map_0001_door",dg);
        EngineSupport.NativeSetAsset(b,"mapmaterial",mg);
        DivineGateOffline.PumpNativeAssets();
        Assert(floor.m_ResultStep==ASSETBUNDLE_STEP.LOAD_SUCCESS && object.ReferenceEquals(floor.m_ResultObject,fg),"Real loaded floor lost");
        Assert(object.ReferenceEquals(door.m_ResultObject,dg) && object.ReferenceEquals(mat.m_ResultObject,mg),"Native object/material identity lost");
        var again=DivineGateOffline.Request("map_0001.unity3d","mapMaterial",ASSETBUNDLE_TAG.INGAME,typeof(Material));
        Assert(object.ReferenceEquals(again.m_ResultObject,mg),"MapMaterial must stay shared with renderers");
        var cb=EngineSupport.NativeComplete(chars,null); var cg=Token(typeof(GameObject));
        EngineSupport.NativeSetAsset(cb,"chara_prefab_018",cg);
        DivineGateOffline.PumpNativeAssets();
        Assert(object.ReferenceEquals(chara.m_ResultObject,cg),"Native character replaced by a quad");
        Assert(EngineSupport.GetComponentType(cg,typeof(DGNativeCharacterMarker))!=null,"Native shadow protection marker missing");
        EngineSupport.NativeComplete(other,"simulated corrupt local bundle");
        DivineGateOffline.PumpNativeAssets();
        Assert(third.m_ResultStep==ASSETBUNDLE_STEP.LOAD_SUCCESS && third.m_ResultObject==null,"Failure must resolve through fallback factory");
        Assert(DivineGateOffline.Request("map_5005.unity3d","map_5005_door",ASSETBUNDLE_TAG.INGAME,typeof(GameObject)).m_ResultStep==ASSETBUNDLE_STEP.LOAD_SUCCESS,"Failed packages must not keep reloading");
        string slow=Package("map_4001.unity3d","map_4001_mesh",typeof(GameObject));
        var timeout=DivineGateOffline.Request("map_4001.unity3d","map_4001_mesh",ASSETBUNDLE_TAG.INGAME,typeof(GameObject));
        DivineGateOffline.PumpNativeAssets(); EngineSupport.NativeTime=31f; DivineGateOffline.PumpNativeAssets();
        Assert(timeout.m_ResultStep==ASSETBUNDLE_STEP.LOAD_SUCCESS,"Timeout stranded a scene request");
        // Cleanup releases finished ownership without destroying cloned scenes.
        Call("ClearNativeAssets"); Assert(EngineSupport.NativeUnloads>=2,"Native bundle ownership leaked");
        var pending=DivineGateOffline.Request("map_0001.unity3d","map_0001_mesh",ASSETBUNDLE_TAG.INGAME,typeof(GameObject));
        Call("ClearNativeAssets"); DivineGateOffline.PumpNativeAssets();
        Assert(pending.m_ResultStep==ASSETBUNDLE_STEP.LOAD_LOADING && EngineSupport.NativeUrls().Length==1,"Cleanup stranded a queued request");
        EngineSupport.NativeComplete(map,"cleanup test"); DivineGateOffline.PumpNativeAssets(); Call("ClearNativeAssets");
        // A missing object must fail the whole batch before publishing a mix.
        var missing=DivineGateOffline.Request("map_0001.unity3d","map_0001_mesh",ASSETBUNDLE_TAG.INGAME,typeof(GameObject));
        var missingDoor=DivineGateOffline.Request("map_0001.unity3d","map_0001_door",ASSETBUNDLE_TAG.INGAME,typeof(GameObject));
        DivineGateOffline.PumpNativeAssets(); var mb=EngineSupport.NativeComplete(map,null);
        EngineSupport.NativeSetAsset(mb,"map_0001_mesh",fg); DivineGateOffline.PumpNativeAssets();
        Assert(missing.m_ResultObject==null && missingDoor.m_ResultObject==null && missing.m_ResultStep==ASSETBUNDLE_STEP.LOAD_SUCCESS,"Partial native/fallback batch published");
        Console.WriteLine("PASS native loader state machine: local APK URLs, two-load limit, pending/success, shared native material, character marker, cached objects, missing-resource routing, bundle error/timeout/partial-batch fallback, cleanup/retry and pending-request preservation. WWW/GPU and fallback factory are stand-ins.");
    }
}
