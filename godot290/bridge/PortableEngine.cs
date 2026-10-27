// Engine-independent services used by the preserved r17 rules assembly.
// Rendering and input belong to Godot. Unsupported Unity calls fail explicitly.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.Serialization;
using System.Text;
using UnityEngine;

public static class PortableEngine
{
    // Android Mono rejects the old >1 MiB explicit-layout image structs.
    // Preserve their bytes as ordinary manifest resources instead.
    public static void InitializeImageArray(Array target, string resource)
    {
        using(var stream=System.Reflection.Assembly.Load("DivineGateOffline").GetManifestResourceStream(resource)) {
            if(stream==null)throw new InvalidDataException("Missing embedded data: "+resource);
            var bytes=new byte[stream.Length];int offset=0,read;
            while(offset<bytes.Length && (read=stream.Read(bytes,offset,bytes.Length-offset))>0)offset+=read;
            if(offset!=bytes.Length || Buffer.ByteLength(target)!=bytes.Length)throw new InvalidDataException("Embedded data length mismatch");
            Buffer.BlockCopy(bytes,0,target,0,bytes.Length);
        }
    }
    static readonly Dictionary<string,string> prefs = new Dictionary<string,string>();
    static readonly Dictionary<object,string> texts = new Dictionary<object,string>();
    static readonly Stopwatch clock = Stopwatch.StartNew();
    static string directory, tableDirectory;
    public static void Configure(string saveDirectory, string tables)
    {
        directory = saveDirectory; tableDirectory = tables;
        Directory.CreateDirectory(directory); prefs.Clear();
        string file = Path.Combine(directory,"preferences.dat");
        if (File.Exists(file)) foreach(string line in File.ReadAllLines(file)) {
            string[] pair=line.Split(':');
            if(pair.Length!=2) throw new InvalidDataException("Corrupt Godot preferences");
            prefs[Encoding.UTF8.GetString(Convert.FromBase64String(pair[0]))]=Encoding.UTF8.GetString(Convert.FromBase64String(pair[1]));
        }
    }
    public static void Save()
    {
        if(directory==null) throw new InvalidOperationException("Configure storage before boot");
        var lines=new List<string>();
        foreach(var p in prefs) lines.Add(Convert.ToBase64String(Encoding.UTF8.GetBytes(p.Key))+":"+Convert.ToBase64String(Encoding.UTF8.GetBytes(p.Value)));
        string file=Path.Combine(directory,"preferences.dat"), temp=file+".tmp";
        using(var stream=new FileStream(temp,FileMode.Create,FileAccess.Write,FileShare.None)) {
            byte[] data=Encoding.UTF8.GetBytes(string.Join("\n",lines.ToArray()));
            stream.Write(data,0,data.Length); stream.Flush(true);
        }
        if(File.Exists(file)) File.Replace(temp,file,file+".bak"); else File.Move(temp,file);
    }
    public static void SetString(string k,string v) { prefs[k]=v; }
    public static string GetString(string k) { string v;return prefs.TryGetValue(k,out v)?v:""; }
    public static void SetInt(string k,int v) { SetString(k,v.ToString(CultureInfo.InvariantCulture)); }
    public static int GetInt(string k) { int n;return int.TryParse(GetString(k),out n)?n:0; }
    public static bool HasKey(string k) { return prefs.ContainsKey(k); }
    public static void DeleteKey(string k) { prefs.Remove(k); }
    public static void DeleteAll() { prefs.Clear(); }
    public static object Load(string path)
    {
        string table=Path.Combine(tableDirectory,Path.GetFileName(path)+".json");
        if(!File.Exists(table)) throw new FileNotFoundException("Unported resource: "+path,table);
        object text=FormatterServices.GetUninitializedObject(typeof(TextAsset));
        texts[text]=File.ReadAllText(table);return text;
    }
    public static string Text(object text) { return texts[text]; }
    public static string DataPath() { return tableDirectory; }
    public static string PersistentDataPath() { return directory; }
    public static float Realtime() { return (float)clock.Elapsed.TotalSeconds; }
    public static void Log(object message) { Console.WriteLine(message); }
    public static float Pow(float a,float b) { return (float)Math.Pow(a,b); }
    public static float Floor(float a) { return (float)Math.Floor(a); }
    public static float Ceil(float a) { return (float)Math.Ceiling(a); }
    public static int FloorToInt(float a) { return (int)Math.Floor(a); }
    public static int CeilToInt(float a) { return (int)Math.Ceiling(a); }
    public static int RoundToInt(float a) { return (int)Math.Round(a,MidpointRounding.ToEven); }
    public static float Sqrt(float a) { return (float)Math.Sqrt(a); }
}
