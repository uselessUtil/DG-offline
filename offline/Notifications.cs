using ServerDataDefine;

public static partial class DivineGateOffline
{
    public static void QuietHelperRewardNotice(MainMenuSeqSwitchPage page)
    {
        // These are pending display counters, not the account's FP balance.
        // SetupServerSend already applied/saved friend_point_now before this.
        MainMenuParam.m_LoginFriendPointGet = 0;
        MainMenuParam.m_LoginFriendHelpCt = 0;
    }

    public static PacketStructPresent[] NotificationPresents(UserDataAdmin user)
    {
        // Multiple placeholder rows also triggered the original Present Info.
        // Filter only the automatic view, keeping the stored list untouched.
        PacketStructPresent[] rows = user == null ? null : user.m_StructPresentList;
        if (rows != null)
            foreach (PacketStructPresent row in rows)
                if (row != null && row.present_type != 0) return rows;
        return null;
    }
}
