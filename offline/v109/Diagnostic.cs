using UnityEngine;

// This screen prevents entering gameplay with absent data; it does not invent data.
public sealed class DG109Diagnostic : MonoBehaviour
{
    private static DG109Diagnostic instance;
    private Vector2 scroll;
    private GUIStyle heading, body;

    public static void Boot()
    {
        if (instance != null) return;
        GameObject host = new GameObject("Divine Gate 1.0.9 Offline Diagnostic");
        Object.DontDestroyOnLoad(host);
        instance = host.AddComponent<DG109Diagnostic>();
        Debug.Log("DG109: gameplay blocked; 34 required master tables absent from original APK.");
    }

    public static string MissingReport()
    {
        string text = "GAMEPLAY BLOCKED\n\n" +
            "This is an offline porting diagnostic, not a playable release.\n" +
            "The original 1.0.9 APK contains none of the 34 master tables required by ServerForgery.\n" +
            "Account creation, login, battles, gacha and progress saving are unavailable.\n\n" +
            "Needed: version-matched master data and bundle catalog; unit portraits/prefabs, icons, " +
            "map meshes/materials/animations, downloaded sounds and scratch assets.\n" +
            "Existing DGDB, Wiki and encyclopedia artwork has not been mapped or embedded. " +
            "Android installation and launch have not been tested.\n\n" +
            "Missing original Resources paths (CardDM/MasterData/):\n";
        for (int i = 0; i < DG109Requirements.Names.Length; i++)
            text += "- " + DG109Requirements.Names[i] + "\n";
        return text;
    }

    private void OnGUI()
    {
        GUI.depth = -32000;
        float scale = Mathf.Clamp(Screen.width / 340f, 0.5f, 4f);
        GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1f));
        float width = Screen.width / scale, height = Screen.height / scale;
        GUI.color = new Color(0.05f, 0.07f, 0.1f, 1f);
        GUI.Box(new Rect(0, 0, width, height), "");
        GUI.color = Color.white;
        if (heading == null)
        {
            heading = new GUIStyle(); heading.wordWrap = true;
            heading.normal.textColor = Color.white;
            body = new GUIStyle(); body.wordWrap = true;
            body.normal.textColor = Color.white;
        }
        GUI.Label(new Rect(18, 15, width - 34, 67), "Divine Gate 1.0.9\nOffline diagnostic prototype 1", heading);
        string text = MissingReport();
        float contentHeight = body.CalcHeight(new GUIContent(text), width - 62) + 20;
        scroll = GUI.BeginScrollView(new Rect(18, 90, width - 34, Mathf.Max(80, height - 108)),
            scroll, new Rect(0, 0, width - 60, contentHeight));
        GUI.Label(new Rect(0, 0, width - 62, contentHeight), text, body);
        GUI.EndScrollView();
    }
}
