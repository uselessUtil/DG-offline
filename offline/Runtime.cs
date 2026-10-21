// New offline adapter for the archived 1.0.6 client. No original source is included.
using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

public static partial class DivineGateOffline
{
    public const string Build = "1.0.6 offline prototype 8";
    private static GameObject templates;
    private static Texture2D atlasTexture;
    private static Material atlasMaterial;
    private static readonly Dictionary<string, Object> assets = new Dictionary<string, Object>();
    private static readonly Dictionary<string, UIAtlas.Sprite> sprites = new Dictionary<string, UIAtlas.Sprite>();
    private static readonly Dictionary<string, Texture2D> recoveredTextures = new Dictionary<string, Texture2D>();
    private static readonly Dictionary<string, Material> recoveredAtlasMaterials = new Dictionary<string, Material>();
    private static readonly Dictionary<Material, Material> enemyHudMaterials = new Dictionary<Material, Material>();
    private static readonly Dictionary<Material, Material> enemyHudOriginals = new Dictionary<Material, Material>();
    private static string error = "";
    private static bool showNotice = true;

    public static void Bootstrap(ResidentManager resident)
    {
        // Awake registers the singleton immediately. Its Start runs once in Unity's lifecycle.
        if (Singleton<ServerForgery>.Instance == null)
            resident.gameObject.AddComponent<ServerForgery>();
        Debug.Log("[DG offline] " + Build);
    }

    public static void Ready(ServerForgery server)
    {
        MasterDataManager master = Singleton<MasterDataManager>.Instance;
        master.m_MasterDataUserRank = server.m_MasterDataUserRank;
        master.m_MasterDataLoginChain = server.m_MasterDataLoginChain;
        master.m_MasterDataLoginTotal = server.m_MasterDataLoginTotal;
        master.m_MasterDataLoginEvent = server.m_MasterDataLoginEvent;
        master.m_MasterDataDefaultParty = server.m_MasterDataDefaultParty;
        master.m_MasterDataArea = server.m_MasterDataArea;
        master.m_MasterDataQuest = server.m_MasterDataQuest;
        master.m_MasterDataQuestFloor = server.m_MasterDataQuestFloor;
        master.m_MasterDataQuestFloorCategory = server.m_MasterDataQuestFloorCategory;
        master.m_MasterDataQuestFloorExpect = server.m_MasterDataQuestFloorExpect;
        master.m_MasterDataParamChara = server.m_MasterDataParamChara;
        master.m_MasterDataParamCharaEvol = server.m_MasterDataParamCharaEvol;
        master.m_MasterDataParamPanel = server.m_MasterDataParamPanel;
        master.m_MasterDataParamPanelGroup = server.m_MasterDataParamPanelGroup;
        master.m_MasterDataParamEnemy = server.m_MasterDataParamEnemy;
        master.m_MasterDataParamEnemyGroup = server.m_MasterDataParamEnemyGroup;
        master.m_MasterDataSkillActive = server.m_MasterDataSkillActive;
        master.m_MasterDataSkillLeader = server.m_MasterDataSkillLeader;
        master.m_MasterDataSkillPassive = server.m_MasterDataSkillPassive;
        master.m_MasterDataSkillLimitBreak = server.m_MasterDataSkillLimitBreak;
        master.m_MasterDataEvent = server.m_MasterDataEvent;
        master.m_MasterDataAreaAmend = server.m_MasterDataAreaAmend;
        master.m_MasterDataGuerrillaBoss = server.m_MasterDataGuerrillaBoss;
        master.m_MasterDataGacha = server.m_MasterDataGacha;
        master.m_MasterDataStoreProduct = server.m_MasterDataStoreProduct;
        master.m_MasterDataAssetBundlePath = server.m_MasterDataAssetBundlePath;
        master.m_MasterDataInformation = server.m_MasterDataInformation;
        MasterDataUtil.ConvertMasterSkillLimitBreak(ref master.m_MasterDataSkillLimitBreak);
        // The embedded notices describe an obsolete live service. Use a local notice instead.
        master.m_MasterDataInformation = new MasterDataInformation[0];
        server.m_MasterDataInformation = master.m_MasterDataInformation;
        master.m_MasterDataStoreProduct = new MasterDataStoreProduct[0];
        server.m_MasterDataStoreProduct = master.m_MasterDataStoreProduct;
        server.UserParamLoad();
        Debug.Log("[DG offline] Local master data ready");
    }

    public static void SetupPatch(PatchManager patch)
    {
        patch.m_PatchUpdateRequestText = false;
        patch.m_PatchUpdateRequestBanner = false;
        patch.m_PatchStructTextAcquired = true;
        patch.m_PatchStructBannerAcquired = true;
        patch.m_PatchStructText = new PatchStructText[0];
        patch.m_PatchStructBanner = new PatchStructBanner[0];
        patch.m_PatchUpdateTime = DateTime.Now;
    }

    public static string PatchText(string key)
    {
        if (key == "APP_VERSION_PATCH_ANDROID") return "1.0.6";
        if (key == "APP_VERSION_PATCH_MULTI_ANDROID") return "[1.0.6]";
        if (key == "DATA_UPDATE_COUNTER") return "offline106-1";
        if (key == "SAVE_TRANSFER_DISABLE" || key == "IOS_SHOP_OVERWRITE_DISABLE") return "1";
        return "0";
    }

    public static void SkipAgreement(TitleMenuSeqInformationFox page, bool ready)
    {
        if (!ready) return;
        Singleton<LocalSaveManager>.Instance.SaveFuncInformationOK(LocalSaveManager.AGREEMENT.AGREE_OK);
        Singleton<TitleMenuManager>.Instance.AddSwitchRequest(TITLEMENU_SEQ.SEQ_SWITCH_PAGE, false);
    }

    public static void LoadLocalAssets(TitleMenuSeqLoadAssetBundle page, bool ready)
    {
        if (!ready || Singleton<ServerForgery>.Instance == null ||
            Singleton<ServerForgery>.Instance.m_MasterDataQuest == null) return;
        Singleton<ResidentResourceManager>.Instance.LoadResidentResourceRequest();
        Singleton<LocalSaveManager>.Instance.SaveFuncAssetBundleInit(LocalSaveManager.ASSETBUNDLE_INIT.ASSETBUNDLE_INIT_CACHE);
        Singleton<TitleMenuManager>.Instance.AddSwitchRequest(TITLEMENU_SEQ.SEQ_SWITCH_PAGE, false);
    }

    public static void DisableStore(StoreManager store)
    {
        store.m_StoreInitOK = false;
        store.m_StoreRestoreOK = true;
        store.m_UserStoreLock = true;
    }

    private static GameObject Template(string name)
    {
        if (templates == null)
        {
            templates = new GameObject("DG offline asset templates");
            Object.DontDestroyOnLoad(templates);
            templates.SetActive(false);
        }
        GameObject result = new GameObject(name);
        result.transform.parent = templates.transform;
        return result;
    }

    private static Shader FlatShader()
    {
        Shader shader = Shader.Find("Unlit/Transparent Colored");
        if (shader == null) shader = Shader.Find("Unlit/Transparent");
        if (shader == null) throw new InvalidOperationException("No bundled alpha-blended shader");
        return shader;
    }

    private static Color ColorFor(int id)
    {
        Color[] palette = { new Color(.6f,.6f,.65f), new Color(.9f,.28f,.2f),
            new Color(.2f,.55f,.95f), new Color(.95f,.8f,.2f),
            new Color(.7f,.35f,.85f), new Color(.25f,.8f,.45f) };
        MasterDataParamChara chara = MasterDataUtil.GetCharaParamFromID((uint)Math.Max(id, 0));
        int element = chara == null ? Math.Abs(id) % 6 : chara.element - 1;
        return palette[Math.Max(0, Math.Min(5, element))];
    }

    private static readonly string[] digits = { "111101101101111", "010110010010111",
        "111001111100111", "111001111001111", "101101111001001", "111100111001111",
        "111100111101111", "111001001001001", "111101111101111", "111101111001111" };

    private static Color[] Tile(string label, Color color, int size)
    {
        Color[] pixels = new Color[size * size];
        for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            pixels[y * size + x] = x < 2 || y < 2 || x >= size - 2 || y >= size - 2 ? Color.white : color;
        string number = "";
        foreach (char c in label) if (c >= '0' && c <= '9') number += c;
        if (number.Length == 0) number = "0";
        if (number.Length > 4) number = number.Substring(number.Length - 4);
        int scale = Math.Max(1, size / 24), start = (size - number.Length * 4 * scale) / 2;
        for (int i = 0; i < number.Length; i++) for (int y = 0; y < 5; y++) for (int x = 0; x < 3; x++)
            if (digits[number[i] - '0'][y * 3 + x] == '1')
                for (int dy = 0; dy < scale; dy++) for (int dx = 0; dx < scale; dx++)
                    pixels[(size / 2 + (2 - y) * scale + dy) * size + start + (i * 4 + x) * scale + dx] = Color.black;
        return pixels;
    }

    private static int Number(string name)
    {
        int result = 0;
        for (int i = name.Length - 1, place = 1; i >= 0; i--)
        {
            if (name[i] < '0' || name[i] > '9') { if (place > 1) break; continue; }
            result += (name[i] - '0') * place;
            place *= 10;
            if (place > 10000) break;
        }
        return result;
    }

    private static void AtlasTexture()
    {
        if (atlasTexture != null) return;
        atlasTexture = new Texture2D(2, 2);
        if (!atlasTexture.LoadImage(Convert.FromBase64String(OfflineImages.Atlas)))
            throw new InvalidOperationException("Cannot decode offline atlas");
        atlasTexture.name = "DG offline placeholder atlas";
        atlasTexture.wrapMode = TextureWrapMode.Clamp;
        Object.DontDestroyOnLoad(atlasTexture);
        atlasMaterial = new Material(FlatShader());
        atlasMaterial.mainTexture = atlasTexture;
        Object.DontDestroyOnLoad(atlasMaterial);
    }

    public static void EnsureSprite(UIAtlas atlas, string name)
    {
        if (!atlas.gameObject.name.StartsWith("DG offline atlas") || name == null) return;
        foreach (UIAtlas.Sprite existing in atlas.spriteList) if (existing.name == name) return;
        string atlasName = atlas.gameObject.name.Substring("DG offline atlas ".Length).Replace("Alpha", "");
        string spriteKey = atlasName + "/" + name;
        UIAtlas.Sprite sprite;
        if (!sprites.TryGetValue(spriteKey, out sprite))
        {
            sprite = new UIAtlas.Sprite();
            sprite.name = name;
            int[] rect = RecoveredImages.Sprite(atlasName, name);
            // InGameCard probes every character atlas with GetSprite. A fake
            // sprite in the first (wrong) atlas makes that search stop early.
            if (RecoveredImages.Enabled && rect == null &&
                atlasName.StartsWith("AtlasCharaIcon_") && name.StartsWith("chara_icon_")) return;
            if (rect == null && recoveredAtlasMaterials.ContainsKey(atlasName))
                rect = RecoveredImages.Sprite(atlasName, "__missing");
            if (rect != null)
                sprite.outer = new Rect(rect[0], rect[1], rect[2], rect[3]);
            else
            {
                int tile = Number(name) % 1024;
                // NGUI pixel coordinates start at the top, not Unity's UV origin.
                sprite.outer = new Rect(tile % 32 * 32, tile / 32 * 32, 32, 32);
            }
            sprite.inner = sprite.outer;
            sprites.Add(spriteKey, sprite);
        }
        atlas.spriteList.Add(sprite);
    }

    private static GameObject Atlas(string name)
    {
        GameObject obj = Template("DG offline atlas " + name);
        UIAtlas atlas = obj.AddComponent<UIAtlas>();
        string atlasName = name.Replace("Alpha", "");
        Material material;
        if (!recoveredAtlasMaterials.TryGetValue(atlasName, out material))
        {
            Texture2D texture = RecoveredTexture("atlas/" + atlasName);
            if (texture != null)
            {
                material = new Material(FlatShader());
                material.mainTexture = texture;
                Object.DontDestroyOnLoad(material);
                recoveredAtlasMaterials.Add(atlasName, material);
            }
            else
            {
                AtlasTexture();
                material = atlasMaterial;
            }
        }
        atlas.spriteMaterial = material;
        atlas.coordinates = UIAtlas.Coordinates.Pixels;
        return obj;
    }

    private static Texture2D CharacterTexture(string name)
    {
        Texture2D recovered = RecoveredTexture("portrait/" + Number(name));
        if (recovered != null) return recovered;
        Texture2D texture = new Texture2D(2, 2);
        texture.name = "DG offline " + name;
        if (!texture.LoadImage(Convert.FromBase64String(OfflineImages.Portraits[Number(name) % 1024])))
            throw new InvalidOperationException("Cannot decode offline portrait");
        Object.DontDestroyOnLoad(texture);
        return texture;
    }

    private static Texture2D RecoveredTexture(string key)
    {
        Texture2D texture;
        if (recoveredTextures.TryGetValue(key, out texture)) return texture;
        byte[] data = RecoveredImages.Read(key);
        if (data == null) return null;
        texture = new Texture2D(2, 2);
        if (!texture.LoadImage(data)) throw new InvalidOperationException("Cannot decode recovered " + key);
        texture.name = "DG recovered " + key;
        texture.wrapMode = TextureWrapMode.Clamp;
        if (key.StartsWith("atlas/")) Object.DontDestroyOnLoad(texture);
        recoveredTextures.Add(key, texture);
        return texture;
    }

    public static void ClearTransientAssets(AssetBundleManager manager)
    {
        // Match AssetBundle.Unload(false): discard our template/cache ownership,
        // while existing scene instances retain their own materials and textures.
        // Full portraits are much larger than the old numbered substitutes.
        bool removed = false;
        List<string> keys = new List<string>();
        foreach (string key in assets.Keys) keys.Add(key);
        foreach (string key in keys)
        {
            GameObject obj = assets[key] as GameObject;
            if (obj != null && obj.name.StartsWith("DG offline atlas ")) continue;
            assets.Remove(key);
            removed = true;
        }
        keys.Clear();
        foreach (string key in recoveredTextures.Keys) keys.Add(key);
        foreach (string key in keys)
            if (!key.StartsWith("atlas/")) recoveredTextures.Remove(key);
        if (templates != null)
            for (int i = templates.transform.childCount - 1; i >= 0; i--)
            {
                GameObject obj = templates.transform.GetChild(i).gameObject;
                if (!obj.name.StartsWith("DG offline atlas ")) Object.Destroy(obj);
            }
        if (removed) UnityUtil.ResourceRefreshAndGC();
    }

    private static Texture2D MapTexture(string bundle, string name)
    {
        return MapPartTexture(bundle, name, "lower");
    }

    private static Texture2D MapPartTexture(string bundle, string name, string part)
    {
        // A map bundle identifies the shared mesh, not the area's texture.
        // Respect an explicit res_map request; mesh/material requests use the
        // selected area's master entry so the simplified floor also matches.
        string resource = name;
        if (!(name.StartsWith("map_") && name.IndexOf("_tex") >= 0))
        {
            SceneGoesParam scene = Singleton<SceneGoesParam>.Instance;
            if (scene != null && scene.m_SceneGoesParamToGameMain != null)
            {
                MasterDataArea area = MasterDataUtil.GetAreaParamFromID(
                    scene.m_SceneGoesParamToGameMain.m_QuestAreaID);
                if (area != null) resource = area.res_map;
            }
        }
        string map = resource.StartsWith("map_") && resource.IndexOf("_tex") >= 0 ?
            resource.Replace("_tex", "") : bundle.Replace(".unity3d", "");
        Texture2D texture = RecoveredTexture("map/" + map + "/" + part);
        if (part == "upper") return texture;
        return texture != null ? texture : CharacterTexture(name);
    }

    private static GameObject Card(string name)
    {
        GameObject obj = Template(name);
        Mesh mesh = new Mesh();
        mesh.vertices = new Vector3[] { new Vector3(-.5f,-.5f,0), new Vector3(.5f,-.5f,0),
            new Vector3(.5f,.5f,0), new Vector3(-.5f,.5f,0) };
        mesh.uv = new Vector2[] { new Vector2(0,0), new Vector2(1,0), new Vector2(1,1), new Vector2(0,1) };
        mesh.colors = new Color[] { Color.white, Color.white, Color.white, Color.white };
        mesh.triangles = new int[] { 0,2,1,0,3,2 };
        mesh.RecalculateNormals();
        obj.AddComponent<MeshFilter>().mesh = mesh;
        Material material = new Material(FlatShader());
        material.mainTexture = CharacterTexture(name);
        obj.AddComponent<MeshRenderer>().sharedMaterial = material;
        return obj;
    }

    public static void FixCharacterShadow(AssetAutoSetCharaMesh view)
    {
        GameObject shadow = view.m_AssetBundleObjectShadow;
        if (shadow == null || shadow.renderer == null) return;
        Material previous = shadow.renderer.sharedMaterial;
        if (previous == null || previous.name.StartsWith("DG alpha shadow ")) return;
        Texture2D mask = RecoveredTexture("shadow/" + Number(shadow.name));
        if (mask == null) return;
        Shader shader = Shader.Find("Unlit/Colored Mult Diffuse Transparent");
        if (shader == null)
        {
            // Do not fall back to an opaque tinted rectangle.
            shadow.SetActive(false);
            Debug.LogError("[DG offline] Missing transparent shadow shader");
            return;
        }
        Material material = new Material(shader);
        material.name = "DG alpha shadow " + shadow.name;
        material.SetColor("_Color", previous.GetColor("_Color"));
        material.mainTexture = mask;
        shadow.renderer.sharedMaterial = material;
        Object.Destroy(previous);
    }

    private static GameObject Map(string bundle, string name)
    {
        GameObject root = Template(name);
        if (name.ToLower().IndexOf("door") >= 0)
        {
            // Top-down camera: place the split upper art past the 5x5 grid's
            // far edge. This display mesh has no collider; GoalCollision stays
            // at (0,-.3,3), owned by the original scene.
            Texture2D upper = MapPartTexture(bundle, name, "upper");
            if (upper != null)
            {
                float top = 2.5f + 5f * upper.height / upper.width;
                GameObject backdrop = new GameObject("DG upper map backdrop");
                backdrop.transform.parent = root.transform;
                backdrop.transform.localPosition = Vector3.zero;
                Mesh mesh = new Mesh();
                mesh.vertices = new Vector3[] { new Vector3(-2.5f,0,2.5f), new Vector3(2.5f,0,2.5f),
                    new Vector3(2.5f,0,top), new Vector3(-2.5f,0,top) };
                mesh.uv = new Vector2[] { new Vector2(0,0), new Vector2(1,0), new Vector2(1,1), new Vector2(0,1) };
                mesh.colors = new Color[] { Color.white, Color.white, Color.white, Color.white };
                mesh.triangles = new int[] { 0,2,1,0,3,2 };
                mesh.RecalculateNormals();
                backdrop.AddComponent<MeshFilter>().mesh = mesh;
                Material material = new Material(FlatShader());
                material.mainTexture = upper;
                backdrop.AddComponent<MeshRenderer>().sharedMaterial = material;
            }
            return root;
        }
        if (name.ToLower().IndexOf("mesh") >= 0)
        {
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            // A primitive cube includes a collider. Its top sits above the original
            // GoalCollision and intercepts the ray intended for the boss entrance.
            Object.Destroy(floor.GetComponent<Collider>());
            floor.transform.parent = root.transform;
            Material material = new Material(FlatShader());
            Texture2D lower = MapTexture(bundle, name);
            floor.transform.localPosition = new Vector3(0, -.1f, 0);
            floor.transform.localScale = new Vector3(12f, .1f, 12f);
            material.mainTexture = lower;
            floor.renderer.sharedMaterial = material;
        }
        return root;
    }

    public static void RaiseEnemyHud(BattleManager battle)
    {
        // Keep original portrait size, HP values and collision objects. NGUI
        // batches can otherwise sort behind the large replacement portrait.
        // Clone only enemy HUD materials. The original HUD atlases use straight
        // alpha; the included Overlay shader has the same blend plus queue +1.
        if (battle.m_EnemyObjectHP == null) return;
        bool foreground = true;
        foreach (GameObject hp in battle.m_EnemyObjectHP)
            if (hp != null) RaiseEnemyHudNode(hp.transform, foreground);
    }

    private static void RaiseEnemyHudNode(Transform node, bool foregroundAllowed)
    {
        UIWidget widget = node.gameObject.GetComponent<UIWidget>();
        if (widget != null)
        {
            Material current = widget.material;
            Material original;
            if (current != null && enemyHudOriginals.TryGetValue(current, out original))
            {
                // Modal UI uses the original NGUI depth order. Restore only our
                // cloned HUD materials; never modify a shared atlas material.
                if (!foregroundAllowed) widget.material = original;
            }
            else if (foregroundAllowed && current != null)
            {
                Material foreground;
                if (!enemyHudMaterials.TryGetValue(current, out foreground))
                {
                    foreground = Object.Instantiate(current) as Material;
                    foreground.name = "DG foreground enemy HUD " + current.name;
                    Shader overlay = Shader.Find("Unlit/Transparent Colored Overlay");
                    if (overlay == null) return;
                    foreground.shader = overlay;
                    Object.DontDestroyOnLoad(foreground);
                    enemyHudMaterials.Add(current, foreground);
                    enemyHudOriginals.Add(foreground, current);
                }
                widget.material = foreground;
            }
        }
        for (int i = 0; i < node.childCount; i++) RaiseEnemyHudNode(node.GetChild(i), foregroundAllowed);
    }

    public static void TraceDoorInput(InGameManager game)
    {
        Input3DManager input = Input3DManager.Instance;
        if (input == null || !input.m_TouchInputTriger || !game.m_AcquireKey) return;
        GameObject hit = input.m_TouchObject;
        SceneObjReferGameMain scene = Singleton<SceneObjReferGameMain>.Instance;
        Debug.Log("[DG offline] Door input: hit=" + (hit == null ? "none" : hit.name) +
            " goal=" + (scene != null && hit == scene.m_MapGoalCollision) +
            " cell=" + game.m_PlayerCellX + "," + game.m_PlayerCellZ +
            " keyDisplayed=" + game.m_AcquireKeyDisplayed + " moveChain=" + game.m_PlayerMoveChain);
    }

    public static AssetBundleResource Request(string bundle, string name, ASSETBUNDLE_TAG tag, Type type)
    {
        AssetBundleResource result = new AssetBundleResource();
        result.m_RequestAssetBundleName = "[offline106]" + bundle;
        result.m_RequestName = name;
        result.m_RequestTag = tag;
        result.m_RequestType = type;
        result.m_ResultStep = ASSETBUNDLE_STEP.LOAD_SUCCESS;
        if (name.Length == 0) return result;
        string key = bundle + "/" + name;
        Object asset;
        if (!assets.TryGetValue(key, out asset))
        {
            try
            {
                if (name.StartsWith("Atlas")) asset = Atlas(name);
                else if (name.StartsWith("chara_prefab_")) asset = Card(name);
                else if (type == typeof(Texture) || type == typeof(Texture2D))
                    asset = bundle.StartsWith("map_") ? MapTexture(bundle, name) : CharacterTexture(name);
                else if (type == typeof(Material))
                {
                    Material material = new Material(FlatShader());
                    material.mainTexture = bundle.StartsWith("map_") ? MapTexture(bundle, name) : CharacterTexture(name);
                    asset = material;
                }
                else asset = Map(bundle, name);
                assets.Add(key, asset);
            }
            catch (Exception exception)
            {
                error = name + ": " + exception.Message;
                Debug.LogError("[DG offline] " + error);
                result.m_ResultStep = ASSETBUNDLE_STEP.LOAD_ERROR;
                return result;
            }
        }
        result.m_ResultObject = asset;
        return result;
    }

    public static void DrawNotice()
    {
        GUI.depth = -1000;
        GUI.Label(new Rect(8, 2, Screen.width - 16, 26), RecoveredImages.Enabled ?
            "DG 1.0.6 OFFLINE TEST | recovered artwork" : "DG 1.0.6 OFFLINE TEST | placeholder assets");
        if (showNotice)
        {
            GUI.Box(new Rect(12, 35, Screen.width - 24, 160), "Offline prototype 8");
            GUI.Label(new Rect(24, 65, Screen.width - 48, 80),
                RecoveredImages.Enabled ?
                "Local account / save. No original server.\nRecovered character art, icons and panel images.\nMap geometry remains simplified. Testing pending." :
                "Local account / save. No original server.\nMissing downloaded art uses numbered placeholders.\nGameplay compatibility is still being tested.");
            if (GUI.Button(new Rect(24, 145, 160, 36), "Continue")) showNotice = false;
        }
        if (error.Length > 0) GUI.Label(new Rect(8, Screen.height - 65, Screen.width - 16, 60), error);
    }
}
