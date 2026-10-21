using System;
using System.Reflection;

// Exercise the packaged routing code with real 1.0.6 master rows. Only texture
// decoding is replaced in the isolated TEST copy, to capture the selected key.
public static class VerifyMapTextureRouting
{
    public static void Main(string[] args)
    {
        Singleton<MasterDataManager>.Instance = new MasterDataManager();
        Singleton<LocalSaveManager>.Instance = new LocalSaveManager();
        var server = new ServerForgery();
        Singleton<ServerForgery>.Instance = server;
        typeof(ServerForgery).GetMethod("Start", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(server, null);
        var scene = new SceneGoesParam();
        Singleton<SceneGoesParam>.Instance = scene;
        var runtimeType = Assembly.LoadFrom(args[0]).GetType("DivineGateOffline", true);
        var select = runtimeType.GetMethod("MapTexture", BindingFlags.NonPublic | BindingFlags.Static);
        var partSelect = runtimeType.GetMethod("MapPartTexture", BindingFlags.NonPublic | BindingFlags.Static);
        var key = runtimeType.GetField("MapTextureProbeKey");
        int count = 0, shared = 0;
        foreach (var area in server.m_MasterDataArea)
        {
            if (area.res_map == null || !area.res_map.StartsWith("map_")) continue;
            scene.m_SceneGoesParamToGameMain = new SceneGoesParamToGameMain();
            scene.m_SceneGoesParamToGameMain.m_QuestAreaID = area.fix_id;
            string bundle = area.mesh_map.Replace("_mesh", ".unity3d");
            string expected = "map/" + area.res_map.Replace("_tex", "") + "/lower";
            foreach (string request in new string[] { area.res_map, area.mesh_map, "mapMaterial" })
            {
                select.Invoke(null, new object[] { bundle, request });
                if ((string)key.GetValue(null) != expected)
                    throw new Exception("Wrong texture for area " + area.fix_id + " request " + request);
                count++;
            }
            partSelect.Invoke(null, new object[] { bundle, area.mesh_door, "upper" });
            if ((string)key.GetValue(null) != expected.Replace("/lower", "/upper"))
                throw new Exception("Wrong upper scenery for area " + area.fix_id);
            count++;
            if (area.res_map.Replace("_tex", "") != area.mesh_map.Replace("_mesh", "")) shared++;
        }
        // Explicit texture names must take priority over the selected area and bundle.
        select.Invoke(null, new object[] { "map_0001.unity3d", "map_0042_tex" });
        if ((string)key.GetValue(null) != "map/map_0042/lower")
            throw new Exception("Explicit map texture was overridden");
        scene.m_SceneGoesParamToGameMain = null;
        select.Invoke(null, new object[] { "map_0001.unity3d", "mapMaterial" });
        if ((string)key.GetValue(null) != "map/map_0001/lower")
            throw new Exception("No-area bundle fallback broken");
        Console.WriteLine("PASS packaged map routing: " + count + " requests; " + shared +
            " areas use a texture different from their mesh; explicit-name priority and no-area fallback");
    }
}
