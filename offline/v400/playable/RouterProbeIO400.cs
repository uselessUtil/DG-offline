// In-memory routing boundary for a disposable managed router test only.
using System.Collections.Generic;
public static class RouterProbeIO400 {
 public static readonly List<int> Routes=new List<int>();
 public static bool Route(int page,bool fast,bool back) {Routes.Add(page);return true;}
}
