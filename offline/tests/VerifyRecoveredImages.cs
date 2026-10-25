using System;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;

// Exercise the generated reader in the actual DLL extracted from the APK.
// No Unity rendering or native engine calls are used here.
public static class VerifyRecoveredImages
{
    public static void Main(string[] args)
    {
        if(args.Length>3) {
            Assembly support=Assembly.LoadFrom(Path.Combine(Path.GetDirectoryName(args[0]),"EngineSupport.dll"));
            support.GetType("EngineSupport",true).GetField("NativeApkPath").SetValue(null,args[3]);
        }
        Assembly assembly = Assembly.LoadFrom(args[0]);
        Type images = assembly.GetType("RecoveredImages", true);
        MethodInfo read = images.GetMethod("Read", BindingFlags.Public | BindingFlags.Static);
        MethodInfo sprite = images.GetMethod("Sprite", BindingFlags.Public | BindingFlags.Static);
        int count = 0;
        foreach (string line in File.ReadAllLines(args[1]))
        {
            string[] entry = line.Split('\t');
            byte[] data = (byte[])read.Invoke(null, new object[] { entry[0] });
            if (data == null || data.Length < 24 || data[0] != 137 || data[1] != 80 || data[2] != 78 || data[3] != 71)
                throw new Exception("Missing PNG: " + entry[0]);
            string hash;
            using (SHA256 sha = SHA256.Create())
                hash = BitConverter.ToString(sha.ComputeHash(data)).Replace("-", "").ToLowerInvariant();
            if (hash != entry[1]) throw new Exception("Embedded PNG differs: " + entry[0]);
            count++;
        }
        if (read.Invoke(null, new object[] { "missing/resource" }) != null)
            throw new Exception("Missing image must fall back safely");
        foreach (string line in File.ReadAllLines(args[2]))
        {
            string[] entry = line.Split('\t');
            int[] rect = (int[])sprite.Invoke(null, new object[] { entry[0], entry[1] });
            if (rect == null || string.Join(",", Array.ConvertAll(rect, delegate(int n) { return n.ToString(); })) != entry[2])
                throw new Exception("Wrong sprite coordinates: " + entry[0] + "/" + entry[1]);
        }
        if (sprite.Invoke(null, new object[] { "AtlasPanelIcon", "missing/sprite" }) != null)
            throw new Exception("Unknown sprite must fall back safely");
        Console.WriteLine("PASS actual APK image reader: " + count + " PNG hashes, sprite rectangles, missing-resource fallback");
    }
}
