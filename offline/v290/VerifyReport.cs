using System;

public static class VerifyReport290
{
    public static void Main()
    {
        string report = DG290Diagnostic.MissingReport();
        if (!report.Contains("GAMEPLAY BLOCKED") || !report.Contains("not a playable release") ||
            DG290Requirements.Names.Length != 42) throw new Exception("Misleading runtime diagnostic");
        foreach (string name in DG290Requirements.Names)
            if (!report.Contains(name)) throw new Exception("Missing diagnostic table: "+name);
        Console.WriteLine("PASS delivered runtime reports all 42 absent resources and blocks gameplay");
    }
}
