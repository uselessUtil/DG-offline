using System;
using System.Collections.Generic;
using ServerDataDefine;

public static partial class DivineGateOffline
{
    public const uint StartingRank = 184;
    public const uint WarehouseCapacity = 2000;
    public const double StaminaSeconds = 180;
    private static uint MaxUInt(uint a, uint b) { return a > b ? a : b; }

    // Called on create/save/load and before serializing a player response.
    // Keep the original save format and all progress; only raise lower floors.
    public static bool ApplyAccountRules(ServerForgery server)
    {
        ServerForgerySaveUser p = LocalShopPlayer(server);
        if (p == null || server.m_MasterDataUserRank == null) return false;
        bool changed = false;
        if (p.player_rank < StartingRank)
        {
            MasterDataUserRank rank = server.m_MasterDataUserRank[StartingRank - 1];
            p.player_rank = StartingRank;
            p.player_exp = MaxUInt(p.player_exp, rank.exp_next_total);
            p.party_base = rank.party_cost; p.party_total = p.party_base + p.party_paid;
            p.friend_base = rank.friend_max; p.friend_total = p.friend_base + p.friend_paid;
            p.unit_base = rank.unit_max;
            p.stamina_max = rank.stamina;
            p.stamina_now = MaxUInt(p.stamina_now, rank.stamina);
            changed = true;
        }
        // A higher saved rank also needs its corresponding EXP floor, otherwise
        // the original quest-clear recalculation could demote it.
        if (p.player_rank > 0 && p.player_rank <= server.m_MasterDataUserRank.Length)
        {
            uint floor = server.m_MasterDataUserRank[p.player_rank - 1].exp_next_total;
            if (p.player_exp < floor) { p.player_exp = floor; changed = true; }
        }
        uint total = MaxUInt(p.unit_total, MaxUInt(WarehouseCapacity, p.unit_base + p.unit_paid));
        if (p.unit_total != total || p.unit_paid != total - p.unit_base)
        { p.unit_paid = total - p.unit_base; p.unit_total = total; changed = true; }
        return changed;
    }

    public static void BeforeAccountSave(ServerForgery server) { ApplyAccountRules(server); }
    public static void AfterAccountLoad(ServerForgery server)
    { if (ApplyAccountRules(server)) server.UserParamSave(); }

    private sealed class ScratchOdds
    {
        public uint total, five, fiveWeight, otherWeight;
        public MasterDataGachaAssign[] rows;
        public bool[] isFive;
    }
    private static readonly Dictionary<uint, ScratchOdds> scratchOdds = new Dictionary<uint, ScratchOdds>();
    private static readonly uint[] addedStandardFiveStars = {
        335,337,339,341,343,345,418,420,422,424,426,428,610,612,614,616,618,620 };

    private static bool BoostedScratch(uint id)
    { return id == 200100 || id == 300100 || id == 300200 || id == 300300 || id == 300400; }

    private static bool FiveStar(ServerForgery server, uint id)
    {
        foreach (MasterDataParamChara c in server.m_MasterDataParamChara)
            if (c != null && c.fix_id == id) return c.rare == 4;
        return false;
    }

    private static ServerForgery scratchServer;
    public static void ConfigureScratch(ServerForgery server)
    {
        if (scratchServer == server) return;
        scratchServer = server; scratchOdds.Clear();
        // Capture the actual old <=0 sampler probability BEFORE adding units.
        // Its first bucket gets one extra ticket and its last loses one.
        foreach (uint id in new uint[] {200100,300100,300200,300300,300400})
        {
            List<MasterDataGachaAssign> rows = new List<MasterDataGachaAssign>();
            foreach (MasterDataGachaAssign row in server.m_MasterDataGachaAssign)
                if (row.gacha_id == id && row.active == 2 && row.assign_rate > 0) rows.Add(row);
            ScratchOdds odds = new ScratchOdds();
            for (int i = 0; i < rows.Count; i++)
            {
                uint weight = rows[i].assign_rate;
                odds.total += weight;
                if (FiveStar(server, rows[i].assign_chara_id))
                    odds.five += (uint)((long)weight + (i == 0 ? 1 : 0) - (i == rows.Count - 1 ? 1 : 0));
            }
            if (odds.total == 0 || odds.five * 2 > odds.total)
                throw new InvalidOperationException("Unexpected scratch odds " + id);
            scratchOdds.Add(id, odds);
        }
        List<MasterDataGachaAssign> added = new List<MasterDataGachaAssign>();
        uint next = 0;
        foreach (MasterDataGachaAssign row in server.m_MasterDataGachaAssign) next = MaxUInt(next, row.fix_id);
        foreach (uint id in addedStandardFiveStars)
        {
            if (!FiveStar(server, id)) throw new InvalidOperationException("Missing standard five star " + id);
            foreach (MasterDataGachaAssign donor in server.m_MasterDataGachaAssign)
            {
                if (donor.gacha_id != 200100 || donor.assign_chara_id != 127) continue;
                MasterDataGachaAssign row = new MasterDataGachaAssign();
                row.fix_id = ++next; row.active = donor.active; row.gacha_id = 200100;
                row.assign_chara_id = id; row.assign_rate = donor.assign_rate;
                row.assign_level = donor.assign_level; row.plus_pow = donor.plus_pow;
                row.plus_hp = donor.plus_hp; row.plus_none = donor.plus_none;
                added.Add(row);
            }
        }
        // Insert within this pool's contiguous block, never span other pools.
        List<MasterDataGachaAssign> all = new List<MasterDataGachaAssign>();
        int last = -1;
        for (int i = 0; i < server.m_MasterDataGachaAssign.Length; i++)
            if (server.m_MasterDataGachaAssign[i].gacha_id == 200100) last = i;
        for (int i = 0; i < server.m_MasterDataGachaAssign.Length; i++)
        { all.Add(server.m_MasterDataGachaAssign[i]); if (i == last) all.AddRange(added); }
        server.m_MasterDataGachaAssign = all.ToArray();
        foreach (KeyValuePair<uint,ScratchOdds> pair in scratchOdds)
        {
            List<MasterDataGachaAssign> rows = new List<MasterDataGachaAssign>();
            foreach (MasterDataGachaAssign row in server.m_MasterDataGachaAssign)
                if (row.gacha_id == pair.Key && row.active == 2) rows.Add(row);
            ScratchOdds odds = pair.Value; odds.rows = rows.ToArray(); odds.isFive = new bool[rows.Count];
            for (int i = 0; i < rows.Count; i++)
            {
                odds.isFive[i] = FiveStar(server,rows[i].assign_chara_id);
                if (odds.isFive[i]) odds.fiveWeight += rows[i].assign_rate;
                else odds.otherWeight += rows[i].assign_rate;
            }
        }
        foreach (MasterDataGacha pool in server.m_MasterDataGacha)
        {
            if (!BoostedScratch(pool.fix_id)) continue;
            ScratchOdds odds = scratchOdds[pool.fix_id];
            pool.detail_2 = "[ffc000]Offline Scratch[-]\n" +
                (pool.fix_id == 200100 ? "42 five-star units (18 restored).\n" : pool.name + "\n") +
                "5-star chance: " + ((float)(200.0 * odds.five / odds.total)).ToString() +
                "% (2x previous offline pool).\n5 chips per draw.";
        }
    }

    // The original game subtracts weights then tests <=0: return a 1-based
    // ticket, not zero-based. Two-stage sampling doubles total 5-star odds
    // exactly while preserving conditional unit/plus weights in each category.
    public static uint ScratchTicket(uint min, uint total, ServerForgery server, SendGachaPlay request)
    {
        ScratchOdds odds;
        if (!scratchOdds.TryGetValue(request.gacha_id, out odds))
            return RandManager.GetRand(0u, total); // friendship/tutorial unchanged
        bool five = RandManager.GetRand(0u, odds.total) < 2 * odds.five;
        uint category = five ? odds.fiveWeight : odds.otherWeight;
        if (category == 0) throw new InvalidOperationException("Empty scratch category");
        uint chosen = RandManager.GetRand(0u, category), offset = 0;
        for (int i = 0; i < odds.rows.Length; i++)
        {
            MasterDataGachaAssign row = odds.rows[i];
            if (odds.isFive[i] == five)
            {
                if (chosen < row.assign_rate) return offset + chosen + 1;
                chosen -= row.assign_rate;
            }
            offset += row.assign_rate;
        }
        throw new InvalidOperationException("Scratch ticket outside pool");
    }
}
