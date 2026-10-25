using ServerDataDefine;

public static partial class DivineGateOffline
{
    // Called only on the newly allocated unit in ServerForgery.AddUnit.
    // Never backfill or rewrite existing units on load/save/evolution.
    public static void InitializeAcquisitionTime(PacketStructUnit unit, ServerForgery server)
    {
        unit.get_time = server.GetServerTime();
    }

    public static ulong AcquisitionSortKey(PacketStructUnit unit, MasterDataParamChara master)
    {
        const uint tieMask = 0x7fffff;
        ulong time = unit.get_time;
        uint recorded = (uint)(time > uint.MaxValue ? uint.MaxValue : time);
        uint tie = master.draw_id & tieMask;
        // Keep the legacy zero-time cards' existing ID ordering. For recorded
        // times, inventory insertion order breaks same-second/multi-draw ties.
        UserDataAdmin admin = Singleton<UserDataAdmin>.Instance;
        PacketStructPlayer player = admin == null ? null : admin.m_StructPlayer;
        if (time != 0 && player != null && player.unit_list != null)
            for (int i = 0; i < player.unit_list.Length; i++)
                if (player.unit_list[i] != null && player.unit_list[i].unique_id == unit.unique_id)
                { tie = tieMask - (uint)i; break; }
        // Leave bits 55+ available to the original party-priority prefix.
        // Actual 2.9.1 list comparators sort larger keys first.
        // A complemented timestamp incorrectly put old/zero-time units first.
        return ((ulong)recorded << 23) + tie;
    }

    public static ulong AcquisitionSortKeyForParty(PacketStructUnit unit, MasterDataParamChara master, bool assigned)
    {
        return ((ulong)MainMenuUtil.GetSortNumPartyAssign(assigned) << 55) + AcquisitionSortKey(unit, master);
    }
}
