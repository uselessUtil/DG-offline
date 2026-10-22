// Local UnityRaw loading through APIs retained by the original Unity 4 player.
using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class DGNativeCharacterMarker : MonoBehaviour { }

public static partial class DivineGateOffline
{
    private sealed class NativeTicket
    {
        public string Bundle, Name, Kind;
        public ASSETBUNDLE_TAG Tag;
        public Type Type;
        public AssetBundleResource Result;
    }
    private sealed class NativeLoad
    {
        public string Id;
        public WWW Reader;
        public AssetBundle Bundle;
        public bool Failed;
        public float Started;
        public readonly List<NativeTicket> Pending = new List<NativeTicket>();
        public readonly Dictionary<string,Object> Objects = new Dictionary<string,Object>();
    }
    private static readonly Dictionary<string,NativeLoad> nativeLoads = new Dictionary<string,NativeLoad>();

    private static string NativeKind(string name, Type type)
    {
        if (type == typeof(Material)) return "Material";
        if (type == typeof(Texture) || type == typeof(Texture2D)) return "Texture";
        if (type == typeof(GameObject) || name.StartsWith("chara_prefab_")) return "GameObject";
        return null;
    }

    private static string NativePackage(string bundle, string name, Type type)
    {
        if (!NativeAssetIndex.Enabled || name.Length == 0) return null;
        string kind = NativeKind(name,type);
        if (kind == null) return null;
        if (kind == "Material")
        {
            if (name.ToLowerInvariant() != "mapmaterial") return null;
            string map = bundle.Replace(".unity3d", "");
            return NativeAssetIndex.Find("Material",map);
        }
        return NativeAssetIndex.Find(kind,name);
    }

    private static AssetBundleResource RequestNative(string bundle, string name, ASSETBUNDLE_TAG tag, Type type)
    {
        string id = NativePackage(bundle,name,type);
        if (id == null) return null;
        NativeLoad load;
        if (!nativeLoads.TryGetValue(id,out load))
        {
            load = new NativeLoad();
            load.Id = id;
            nativeLoads.Add(id,load);
        }
        if (load.Failed) return null; // Existing, tested simplified path.
        NativeTicket ticket = new NativeTicket();
        ticket.Bundle = bundle; ticket.Name = name; ticket.Tag = tag; ticket.Type = type;
        ticket.Kind = NativeKind(name,type);
        ticket.Result = new AssetBundleResource();
        ticket.Result.m_RequestAssetBundleName = "[offline106-native]" + bundle;
        ticket.Result.m_RequestName = name;
        ticket.Result.m_RequestTag = tag;
        ticket.Result.m_RequestType = type;
        ticket.Result.m_ResultStep = ASSETBUNDLE_STEP.LOAD_LOADING;
        load.Pending.Add(ticket);
        // Complete a cached request immediately, retaining shared mapMaterial.
        if (load.Bundle != null) CompleteNative(load);
        return ticket.Result;
    }

    private static string NativeUrl(string id)
    {
        // IDs come only from the generated, hash-verified CAB index. No server
        // address, external storage or runtime extraction is involved.
        string path = Application.dataPath;
        if (!path.EndsWith(".apk") || path.IndexOf("://") >= 0 ||
            !id.StartsWith("CAB-") || id.IndexOf('/') >= 0 || id.IndexOf('\\') >= 0)
            throw new InvalidOperationException("Invalid local native bundle path");
        return "jar:file://" + path + "!/assets/dg-native/" + id + ".unity3d";
    }

    private static void NativeFailure(NativeLoad load, string reason)
    {
        load.Failed = true;
        Debug.LogError("[DG offline] Native fallback " + load.Id + ": " + reason);
        if (load.Reader != null) { load.Reader.Dispose(); load.Reader = null; }
        if (load.Bundle != null) { load.Bundle.Unload(false); load.Bundle = null; }
        load.Objects.Clear();
        foreach (NativeTicket ticket in load.Pending)
        {
            AssetBundleResource fallback = FallbackRequest(ticket.Bundle,ticket.Name,ticket.Tag,ticket.Type);
            ticket.Result.m_ResultObject = fallback.m_ResultObject;
            ticket.Result.m_ResultStep = fallback.m_ResultStep;
        }
        load.Pending.Clear();
    }

    private static void CompleteNative(NativeLoad load)
    {
        try
        {
            // Resolve the whole pending batch before exposing any result. A
            // bundle error must not combine a native map with a split material.
            foreach (NativeTicket ticket in load.Pending)
            {
                string key = ticket.Kind + "/" + ticket.Name.ToLowerInvariant();
                if (load.Objects.ContainsKey(key)) continue;
                Type type = ticket.Kind == "GameObject" ? typeof(GameObject) :
                    ticket.Kind == "Material" ? typeof(Material) : typeof(Texture);
                Object value = load.Bundle.Load(ticket.Name.ToLowerInvariant(),type);
                if (value == null) throw new InvalidOperationException("Missing native object " + key);
                GameObject go = value as GameObject;
                if (go != null && ticket.Name.StartsWith("chara_prefab_") &&
                    go.GetComponent<DGNativeCharacterMarker>() == null)
                    go.AddComponent<DGNativeCharacterMarker>();
                load.Objects.Add(key,value);
            }
            foreach (NativeTicket ticket in load.Pending)
            {
                ticket.Result.m_ResultObject = load.Objects[ticket.Kind + "/" + ticket.Name.ToLowerInvariant()];
                ticket.Result.m_ResultStep = ASSETBUNDLE_STEP.LOAD_SUCCESS;
            }
            load.Pending.Clear();
        }
        catch (Exception ex) { NativeFailure(load,ex.Message); }
    }

    public static void PumpNativeAssets()
    {
        if (!NativeAssetIndex.Enabled) return;
        int active = 0;
        foreach (NativeLoad load in nativeLoads.Values) if (load.Reader != null) active++;
        foreach (NativeLoad load in nativeLoads.Values)
        {
            if (load.Failed || load.Pending.Count == 0 || load.Bundle != null) continue;
            try
            {
                if (load.Reader == null)
                {
                    if (active >= 2) continue;
                    load.Reader = new WWW(NativeUrl(load.Id));
                    load.Started = Time.realtimeSinceStartup;
                    active++;
                }
                if (!load.Reader.isDone)
                {
                    if (Time.realtimeSinceStartup - load.Started > 30f)
                    { NativeFailure(load,"local load timeout"); active--; }
                    continue;
                }
                if (load.Reader.error != null && load.Reader.error.Length != 0)
                    throw new InvalidOperationException(load.Reader.error);
                load.Bundle = load.Reader.assetBundle;
                if (load.Bundle == null) throw new InvalidOperationException("Unity rejected local UnityRaw bundle");
                load.Reader.Dispose(); load.Reader = null;
                active--;
                CompleteNative(load);
            }
            catch (Exception ex)
            {
                if (load.Reader != null) active--;
                NativeFailure(load,ex.Message);
            }
        }
    }

    private static bool ClearNativeAssets()
    {
        List<string> remove = new List<string>();
        foreach (NativeLoad load in nativeLoads.Values)
        {
            // Do not strand a request held by a live scene during cleanup.
            if (load.Pending.Count != 0 || load.Reader != null) continue;
            if (load.Bundle != null) load.Bundle.Unload(false);
            remove.Add(load.Id);
        }
        foreach (string id in remove) nativeLoads.Remove(id);
        return remove.Count != 0;
    }
}
