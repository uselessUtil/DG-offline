using System;

public static class VerifyReport109
{
    public static void Main()
    {
        string report = DG109Diagnostic.MissingReport();
        if (!report.Contains("GAMEPLAY BLOCKED") || !report.Contains("not a playable release") ||
            DG109Requirements.Names.Length != 34) throw new Exception("Misleading runtime diagnostic");
        foreach (string name in DG109Requirements.Names)
            if (!report.Contains(name)) throw new Exception("Missing table: " + name);
        Console.WriteLine("PASS delivered diagnostic reports all 34 absent master resources; gameplay unavailable");
    }
}
