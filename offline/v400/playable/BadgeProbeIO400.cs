// In-memory PlayerPrefs equivalent for the disposable footer probe only.
using System.Collections.Generic;
public static class BadgeProbeIO400 {
 public static readonly Dictionary<string,object> Values=new Dictionary<string,object>();
 public static readonly List<string> Writes=new List<string>();
 public static void Reset() {Values.Clear();Writes.Clear();}
 public static string GetValueString(string key,string fallback) {object v;return Values.TryGetValue(key,out v)?(string)v:fallback;}
 public static int GetValueInt(string key,int fallback) {object v;return Values.TryGetValue(key,out v)?(int)v:fallback;}
 public static void SetValueString(string key,string value) {Values[key]=value;Writes.Add(key);}
 public static void SetValueInt(string key,int value) {Values[key]=value;Writes.Add(key);}
}
