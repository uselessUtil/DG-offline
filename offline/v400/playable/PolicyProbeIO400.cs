// UI/scene recording boundaries for a disposable Mono test, never packaged.
using System.Collections.Generic;
using System.Runtime.Serialization;
public static class PolicyProbeIO400 {
 public static readonly List<string> Dialogs=new List<string>();
 public static readonly List<int> Scenes=new List<int>();
 public static readonly List<int> Pages=new List<int>();
 public static int PolicyReads;
 public static void Reset() {Dialogs.Clear();Scenes.Clear();Pages.Clear();PolicyReads=0;}
 public static void ReadPolicy() {PolicyReads++;}
 public static Dialog Open(string title,string text,string button,bool wall,bool sound) {
  Dialogs.Add(title+":"+text);
  return (Dialog)FormatterServices.GetUninitializedObject(typeof(Dialog));
 }
 public static void Scene(int scene) {Scenes.Add(scene);}
 public static bool Page(int page,bool fast) {Pages.Add(page);return true;}
}
