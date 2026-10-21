// Managed stand-ins for engine I/O ONLY. Never packaged into the APK.
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using UnityEngine;

public static class EngineSupport
{
    sealed class IdentityComparer : IEqualityComparer<object>
    {
        public new bool Equals(object a, object b) { return ReferenceEquals(a, b); }
        public int GetHashCode(object value) { return System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(value); }
    }
    static Dictionary<object, string> texts = new Dictionary<object, string>(new IdentityComparer());
    static Dictionary<object, object> owners = new Dictionary<object, object>(new IdentityComparer());
    static Dictionary<object, string> names = new Dictionary<object, string>(new IdentityComparer());
    static Dictionary<object, bool> activeObjects = new Dictionary<object, bool>(new IdentityComparer());
    static Dictionary<object, bool> enabledColliders = new Dictionary<object, bool>(new IdentityComparer());
    static Dictionary<object, bool> enabledBehaviours = new Dictionary<object, bool>(new IdentityComparer());
    static Dictionary<object, List<object>> components = new Dictionary<object, List<object>>(new IdentityComparer());
    static Dictionary<object, object> transforms = new Dictionary<object, object>(new IdentityComparer());
    static Dictionary<object, Vector3> scales = new Dictionary<object, Vector3>(new IdentityComparer());
    public static void SetActiveState(object obj, bool active) { activeObjects[obj] = active; }
    public static bool GameObjectActive(object obj) { bool active; return activeObjects.TryGetValue(obj, out active) && active; }
    public static void SetColliderEnabled(object obj, bool enabled) { enabledColliders[obj] = enabled; }
    public static bool ColliderEnabled(object obj) { bool enabled; return enabledColliders.TryGetValue(obj, out enabled) && enabled; }
    public static void SetBehaviourEnabled(object obj, bool enabled) { enabledBehaviours[obj] = enabled; }
    public static bool BehaviourEnabled(object obj) { bool enabled; return enabledBehaviours.TryGetValue(obj, out enabled) && enabled; }
    public static GameObject CreateScaleTestObject()
    {
        var go = (GameObject)FormatterServices.GetUninitializedObject(typeof(GameObject));
        var tr = (Transform)FormatterServices.GetUninitializedObject(typeof(Transform));
        transforms[go] = tr; scales[tr] = new Vector3(1, 1, 1);
        RegisterComponent(go, tr);
        return go;
    }
    public static void RegisterComponent(object go, object component)
    {
        owners[component] = go;
        if (!components.ContainsKey(go)) components[go] = new List<object>();
        components[go].Add(component);
    }
    public static object GetComponentType(object obj, Type type)
    {
        object go;
        if (!owners.TryGetValue(obj, out go)) go = obj;
        List<object> values;
        if (components.TryGetValue(go, out values)) foreach (object value in values)
            if (type.IsInstanceOfType(value)) return value;
        return null;
    }
    public static object ObjectTransform(object obj)
    {
        object go, tr;
        if (!owners.TryGetValue(obj, out go)) go = obj;
        return transforms.TryGetValue(go, out tr) ? tr : null;
    }
    public static Vector3 TransformScale(object obj)
    {
        Vector3 scale; return scales.TryGetValue(obj, out scale) ? scale : new Vector3();
    }
    public static void SetTransformScale(object obj, Vector3 scale) { scales[obj] = scale; }
    public static void SendTestMessage(object go, string name, object arg, SendMessageOptions options)
    {
        List<object> values;
        if (!components.TryGetValue(go, out values)) return;
        foreach (object component in values)
        {
            var method = component.GetType().GetMethod(name,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
            if (method != null) method.Invoke(component, new object[] { arg });
        }
    }
    public static void BindComponentOwner(object component, string name)
    {
        object owner = FormatterServices.GetUninitializedObject(typeof(GameObject));
        owners[component] = owner;
        names[owner] = name;
    }
    public static object ComponentGameObject(object component) { return owners[component]; }
    public static string ObjectName(object obj) { string name; return names.TryGetValue(obj, out name) ? name : ""; }
    static Dictionary<string, string> prefs = new Dictionary<string, string>();
    static string file = Environment.GetEnvironmentVariable("DG_TEST_PREFS");
    static EngineSupport()
    {
        if (File.Exists(file)) foreach (string line in File.ReadAllLines(file))
        {
            string[] parts = line.Split(':');
            prefs[System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(parts[0]))] =
                System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(parts[1]));
        }
    }
    public static object Load(string path)
    {
        string filename = Path.GetFileName(path) + ".json";
        string table = Path.Combine(Environment.GetEnvironmentVariable("DG_TEST_TABLES"), filename);
        if (!File.Exists(table)) throw new FileNotFoundException(path);
        var result = FormatterServices.GetUninitializedObject(typeof(TextAsset));
        texts[result] = File.ReadAllText(table);
        return result;
    }
    public static string Text(object obj) { return texts[obj]; }
    public static void SetString(string k, string v) { prefs[k] = v; }
    public static string GetString(string k) { return prefs.ContainsKey(k) ? prefs[k] : ""; }
    public static void SetInt(string k, int v) { SetString(k, v.ToString()); }
    public static int GetInt(string k) { int v; return int.TryParse(GetString(k), out v) ? v : 0; }
    public static bool HasKey(string k) { return prefs.ContainsKey(k); }
    public static void DeleteKey(string k) { prefs.Remove(k); }
    public static void DeleteAll() { prefs.Clear(); }
    public static void Save()
    {
        var lines = new List<string>();
        foreach (var pair in prefs) lines.Add(Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(pair.Key)) + ":" +
            Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(pair.Value)));
        File.WriteAllLines(file, lines.ToArray());
    }
    public static float Pow(float a, float b) { return (float)Math.Pow(a, b); }
    public static float Floor(float a) { return (float)Math.Floor(a); }
    public static float Ceil(float a) { return (float)Math.Ceiling(a); }
    public static int FloorToInt(float a) { return (int)Math.Floor(a); }
    public static int CeilToInt(float a) { return (int)Math.Ceiling(a); }
    public static int RoundToInt(float a) { return (int)Math.Round(a); }
    public static float Sqrt(float a) { return (float)Math.Sqrt(a); }
}
