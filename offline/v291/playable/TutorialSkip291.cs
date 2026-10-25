using System;
using LitJson;
using ServerDataDefine;
using UnityEngine;

public static partial class DivineGateOffline
{
    private static PacketStructHeaderRecv TutorialHeader291(ServerForgery server, uint packetID)
    { throw new InvalidOperationException("Missing tutorial header bridge"); }
    private static PacketStructPlayer TutorialPlayer291(ServerForgery server)
    { throw new InvalidOperationException("Missing tutorial player bridge"); }
    private static void SetTutorialPlayer291(ServerForgery server, ServerForgerySaveUser player)
    { throw new InvalidOperationException("Missing tutorial save bridge"); }

    private static string TutorialReply291(ServerForgery server, uint packetID, int code)
    {
        var reply = new RecvSkipTutorial();
        reply.header = TutorialHeader291(server, packetID);
        reply.header.code = code;
        if (code == 4096)
            reply.result = new RecvSkipTutorialValue { player = TutorialPlayer291(server) };
        return JsonMapper.ToJson(reply);
    }

    // The original 2.9 client sends this API, but its local ServerForgery
    // dispatcher has no case for it and returns an empty response forever.
    public static string HandleTutorialSkip291(ServerForgery server, ref PacketData packet, string json)
    {
        if (packet.m_PacketAPI != SERVER_API.SERVER_API_TUTORIAL_SKIP) return null;
        ServerForgerySaveUser previous = Local291Player(server);
        if (previous == null) return TutorialReply291(server, packet.m_PacketUniqueNum, 8195);
        SendSkipTutorial request;
        try { request = JsonMapper.ToObject<SendSkipTutorial>(json); }
        catch { return TutorialReply291(server, packet.m_PacketUniqueNum, 8194); }
        if (request == null || request.select_party < 1 || request.select_party > 3)
            return TutorialReply291(server, packet.m_PacketUniqueNum, 8194);

        // Retry after a committed selection (including a process restart) is
        // read-only: never replace an existing party or award anything twice.
        if (previous.first_select_num >= 1)
            return TutorialReply291(server, packet.m_PacketUniqueNum, 4096);
        if ((previous.unit_list != null && previous.unit_list.Length != 0) || previous.quest_id != 0)
            return TutorialReply291(server, packet.m_PacketUniqueNum, 8194);

        MasterDataDefaultParty party = null;
        foreach (var row in server.m_MasterDataDefaultParty)
            if (row != null && row.fix_id == request.select_party) party = row;
        MasterDataQuest quest = null;
        foreach (var row in server.m_MasterDataQuest)
            if (row != null && row.fix_id == 1) quest = row;
        if (party == null || quest == null || quest.clear_stone != 5)
            return TutorialReply291(server, packet.m_PacketUniqueNum, 8195);
        uint[] units = { party.party_chara0_id, party.party_chara1_id, party.party_chara2_id,
            party.party_chara3_id, request.select_party == 1 ? 205u : request.select_party == 2 ? 208u : 211u,
            request.select_party == 1 ? 43u : request.select_party == 2 ? 45u : 47u };
        foreach (uint id in units)
        {
            if (id == 0) continue;
            bool exists = false;
            foreach (var row in server.m_MasterDataParamChara)
                if (row != null && row.fix_id == id) { exists = true; break; }
            if (!exists) return TutorialReply291(server, packet.m_PacketUniqueNum, 8195);
        }

        try
        {
            // Work on a copy until the original selection API commits it.
            // This also rolls back AddUnit flags/timestamps if saving fails.
            var player = JsonMapper.ToObject<ServerForgerySaveUser>(JsonMapper.ToJson(previous));
            // Skip omits quest 1, whose original first-clear award funds the
            // still-required tutorial scratch. Set its existing clear bit and
            // settle only that award; no fabricated battle money/EXP/drops.
            if (ServerDataUtil.AddBitFlag(ref player.flag_quest_clear, 1u))
            {
                checked { player.have_stone_free += (uint)quest.clear_stone;
                    player.have_stone = player.have_stone_free + player.have_stone_pay; }
                if (player.have_stone > 999)
                    return TutorialReply291(server, packet.m_PacketUniqueNum, 8194);
            }
            SetTutorialPlayer291(server, player);
            var select = new PacketData();
            select.m_PacketAPI = SERVER_API.SERVER_API_USER_SELECT_DEF_PARTY;
            select.m_PacketUniqueNum = packet.m_PacketUniqueNum;
            // Both native responses contain exactly header + result.player.
            // Reuse native unit allocation, starter levels, five teams, and
            // the single UserParamSave that commits selection + award together.
            return server.CreateRecvForgery(ref select, json);
        }
        catch (Exception ex)
        {
            SetTutorialPlayer291(server, previous);
            Debug.LogError("[DG offline] Tutorial skip failed: " + ex.Message);
            return TutorialReply291(server, packet.m_PacketUniqueNum, 8195);
        }
    }
}
