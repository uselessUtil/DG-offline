using UnityEngine;

// A data gate, not a replacement for the missing gameplay data or downloaded assets.
public sealed class DG290Diagnostic : MonoBehaviour
{
    private static DG290Diagnostic instance;
    private Vector2 scroll;
    private GUIStyle heading, body;

    public static void Boot()
    {
        if (instance != null) return;
        GameObject host = new GameObject("Divine Gate 2.9.0 Offline Diagnostic");
        Object.DontDestroyOnLoad(host);
        instance = host.AddComponent<DG290Diagnostic>();
        Debug.Log("DG290: offline diagnostic only; gameplay blocked by missing master data.");
    }

    public static string MissingReport()
    {
        string text = "GAMEPLAY BLOCKED\n\n" +
            "This APK is an offline porting diagnostic, not a playable release.\n" +
            "The original 2.9.0 APK contains none of the 42 master tables required by its local backend.\n" +
            "Login, account creation, battles and saving are unavailable in this build.\n\n" +
            "Needed: version-matched master data, bundle catalog, character/icon/map/sound/scratch bundles, " +
            "and Android device validation. Existing artwork has not been mapped or embedded.\n\n" +
            "Required resources absent from the source APK:\n";
        for (int i = 0; i < DG290Requirements.Names.Length; i++)
            text += "- " + DG290Requirements.Names[i] + "\n";
        return text;
    }

    private void OnGUI()
    {
        GUI.depth = -32000;
        float scale = Mathf.Clamp(Screen.width / 480f, 0.5f, 4f);
        GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1f));
        float width = Screen.width / scale, height = Screen.height / scale;
        GUI.color = new Color(0.05f, 0.07f, 0.1f, 1f);
        GUI.DrawTexture(new Rect(0, 0, width, height), Texture2D.whiteTexture);
        GUI.color = Color.white;
        if (heading == null)
        {
            heading = new GUIStyle();
            heading.fontSize = 23;
            heading.wordWrap = true;
            heading.normal.textColor = Color.white;
            body = new GUIStyle();
            body.fontSize = 16;
            body.wordWrap = true;
            body.normal.textColor = Color.white;
        }
        GUI.Label(new Rect(18, 15, width - 36, 67), "Divine Gate 2.9.0\nOffline diagnostic prototype 1", heading);
        string text = MissingReport();
        float contentHeight = body.CalcHeight(new GUIContent(text), width - 62) + 20;
        scroll = GUI.BeginScrollView(new Rect(18, 90, width - 36, Mathf.Max(80, height - 108)),
            scroll, new Rect(0, 0, width - 60, contentHeight));
        GUI.Label(new Rect(0, 0, width - 62, contentHeight), text, body);
        GUI.EndScrollView();
    }
}
