using System;
using System.Reflection;
using ServerDataDefine;

public static class ProbeMissingData110
{
    public static void Main()
    {
        // Exercise the actual delivered, unmodified backend with engine I/O stubbed.
        var server = new ServerForgery();
        var request = new SendCreateUser();
        request.uuid = "offline110-missing-data-test";
        request.terminal = new PacketStructTerminal();
        request.terminal.platform = 1;
        request.terminal.name = "isolated managed probe";
        if (server.m_MasterDataUserRank != null) throw new Exception("Unexpected injected rank data");
        try
        {
            server.UserParamCreate(request);
            throw new Exception("Account creation unexpectedly succeeded without rank data");
        }
        catch (NullReferenceException ex)
        {
            if (!ex.StackTrace.Contains("ServerForgery.UserParamCreate")) throw;
            Console.WriteLine("PASS reproduced real UserParamCreate failure with absent MasterDataUserRank");
            Console.WriteLine(ex.ToString());
        }
        server.m_MasterDataUserRank = new MasterDataUserRank[0];
        try
        {
            server.UserParamCreate(request);
            throw new Exception("Empty rank data unexpectedly succeeded");
        }
        catch (IndexOutOfRangeException)
        {
            Console.WriteLine("PASS empty master array is insufficient; real rank rows required");
        }
        Console.WriteLine("NOT TESTED: Android engine, launch, rendering, account persistence or playable quests");
    }
}
