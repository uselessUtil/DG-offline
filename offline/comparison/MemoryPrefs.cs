using System.Collections.Generic;

// TEST ONLY: in-process preferences; never packaged into an APK.
public static class ComparisonMemoryPrefs
{
    static readonly Dictionary<string, string> values = new Dictionary<string, string>();
    public static void SetString(string key, string value) { values[key] = value; }
    public static string GetString(string key, string fallback) { return values.ContainsKey(key) ? values[key] : fallback; }
    public static bool HasKey(string key) { return values.ContainsKey(key); }
    public static void DeleteKey(string key) { values.Remove(key); }
    public static void Save() { }
}
