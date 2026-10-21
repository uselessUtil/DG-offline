using System;

public static class VerifyReport110
{
    public static void Main()
    {
        string report = DG110Diagnostic.MissingReport();
        if (!report.Contains("GAMEPLAY BLOCKED") || !report.Contains("not a playable release") ||
            DG110Requirements.Names.Length != 36) throw new Exception("Misleading runtime diagnostic");
        foreach (string name in DG110Requirements.Names)
            if (!report.Contains(name)) throw new Exception("Missing table: " + name);
        Console.WriteLine("PASS delivered diagnostic reports all 36 absent master resources; gameplay unavailable");
    }
}
