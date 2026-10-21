using System;

public static class VerifyReport210
{
    public static void Main()
    {
        string report = DG210Diagnostic.MissingReport();
        if (!report.Contains("GAMEPLAY BLOCKED") || !report.Contains("not a playable release") ||
            DG210Requirements.Names.Length != 37) throw new Exception("Misleading runtime diagnostic");
        foreach (string name in DG210Requirements.Names)
            if (!report.Contains(name)) throw new Exception("Missing table: " + name);
        Console.WriteLine("PASS delivered diagnostic reports all 37 absent master resources; gameplay unavailable");
    }
}
