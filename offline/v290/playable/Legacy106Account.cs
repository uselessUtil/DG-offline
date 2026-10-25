// Fixed 22r7 export schema, audited against the delivered 1.0.6 APK.
using ServerDataDefine;

public sealed class DGSave106Account
{
	public uint hard_platform;

	public string hard_name;

	public string uuid;

	public string shop_product_id;

	public uint user_id;

	public string user_name;

	public uint user_group;

	public uint login_total;

	public uint login_chain;

	public ulong login_last;

	public int first_select_num;

	public uint player_rank;

	public uint player_exp;

	public uint pay_total;

	public uint pay_month;

	public uint have_money;

	public uint have_stone;

	public uint have_stone_free;

	public uint have_stone_pay;

	public uint have_friend_pt;

	public uint stamina_now;

	public uint stamina_max;

	public ulong stamina_recover;

	public uint unit_paid;

	public uint unit_base;

	public uint unit_total;

	public uint friend_paid;

	public uint friend_base;

	public uint friend_total;

	public uint party_paid;

	public uint party_base;

	public uint party_total;

	public byte[] flag_quest_check;

	public byte[] flag_quest_clear;

	public byte[] flag_unit_check;

	public byte[] flag_unit_get;

	public byte[] flag_tutorial;

	public uint quest_id;

	public uint quest_status;

	public uint quest_continue_ct;

	public uint quest_reset_ct;

	public bool evolve_quest_active;

	public uint evolve_quest_id;

	public uint evolve_quest_status;

	public long evolve_unit_base;

	public long[] evolve_unit_parts;

	public uint evolve_pattern;

	public uint evolve_helper_user_id;

	public PacketStructUnit evolve_helper_unit;

	public uint evolve_helper_premium;

	public PacketStructPartyAssign[] unit_party_assign;

	public int unit_party_current;

	public PacketStructUnit[] unit_list;

	public DGSave106Present[] present_list;

	public int review;
}

public sealed class DGSave106Present {
    public long serial_id;
    public ulong send_timing;
    public string message;
    public uint present_type;
    public uint present_value0;
    public uint present_value1;
    public long delete_timing;
    public int message_fix;
}
