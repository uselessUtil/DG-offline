using System;
using System.Collections.Generic;
using LitJson;
using ServerDataDefine;
using UnityEngine;
using Object = UnityEngine.Object;

// One state object per original page; no global friend-list replacement.
public sealed class WarehouseSupportView : MonoBehaviour
{
    public bool warehouse;
    public GameObject button;
    public int party = -1;
}

public static partial class DivineGateOffline
{
    // Patch.cs binds these typed bridges to private fields/methods in 1.0.6.
    private static void RebuildQuestSupports(MainMenuSeqQuestSelectFriend page) { throw new Exception("Bridge"); }
    private static readonly Dictionary<long,uint> warehouseIDs = new Dictionary<long,uint>();
    private static uint nextWarehouseID = 3000000000u;
    public static bool IsWarehouseSupport(uint id) { return id >= 3000000000u; }
    public static PacketStructFriend WarehouseFriend(PacketStructUnit unit)
    {
        uint id;
        if (!warehouseIDs.TryGetValue(unit.unique_id, out id))
        { id = nextWarehouseID++; warehouseIDs.Add(unit.unique_id,id); }
        PacketStructFriend friend = new PacketStructFriend();
        friend.user_id = id; friend.user_name = "自分の倉庫"; friend.friend_state = 0;
        friend.user_rank = Singleton<UserDataAdmin>.Instance.m_StructPlayer.rank;
        friend.unit = new PacketStructUnit(); friend.unit.Copy(unit);
        return friend;
    }

    public static LocalSaveFriendUse WarehouseUseRecord(uint id)
    {
        LocalSaveFriendUse use = new LocalSaveFriendUse(); use.m_FriendID = id;
        return use; // disables friendship-point awards, never written to disk
    }

    private static WarehouseSupportView SupportView(Component page)
    {
        WarehouseSupportView view = page.gameObject.GetComponent<WarehouseSupportView>();
        return view != null ? view : page.gameObject.AddComponent<WarehouseSupportView>();
    }

    private static bool SupportTutorialActive()
    {
        return Singleton<TutorialManager>.Instance != null &&
            !Singleton<TutorialManager>.Instance.TutorialChkAllFinish();
    }

    private static void SetupSourceButton(Component page, string sourceName, bool visible)
    {
        WarehouseSupportView view = SupportView(page);
        GameObject source = UnityUtil.GetChildNode(page.gameObject,sourceName);
        if (source == null) return;
        if (view.button == null)
        {
            GameObject prefab = Resources.Load("Prefab/MainMenu/MainMenuPage/PagePartyBlendBuildUp") as GameObject;
            GameObject template = prefab == null ? null : UnityUtil.GetChildNode(prefab,"SwitchButtonClear");
            if (template == null) return;
            bool active = template.activeSelf; template.SetActive(false);
            try { view.button = Object.Instantiate(template) as GameObject; }
            finally { template.SetActive(active); }
            view.button.name = "DGWarehouseSource";
            view.button.transform.parent = source.transform.parent;
            view.button.transform.localRotation = source.transform.localRotation;
            Vector3 p = source.transform.localPosition;
            view.button.transform.localScale = source.transform.localScale;
            BoxCollider hit = view.button.GetComponent<BoxCollider>();
            // Mirror the original left inset, and align the new right edge
            // using the native centered clear-button collider (184 x 38).
            float right = -p.x;
            float edge = (hit.center.x + hit.size.x * 0.5f) * view.button.transform.localScale.x;
            view.button.transform.localPosition = new Vector3(right - edge, p.y, p.z);
            foreach (LabelSetter setter in view.button.GetComponentsInChildren<LabelSetter>(true))
            { setter.m_LabelTextKey = ""; setter.enabled = false; }
            BindCollabWidgets(view.button,source.layer);
            foreach (UILabel label in view.button.GetComponentsInChildren<UILabel>(true)) label.text = "";
            GameObject text = UnityUtil.GetChildNode(view.button,"ButtonMsg");
            if (text != null)
            {
                Vector3 pText = text.transform.localPosition;
                text.transform.localPosition = new Vector3(0,pText.y,pText.z);
                text.transform.localScale = new Vector3(22,22,1);
            }
        }
        UnityUtil.SetObjectEnabledFix(view.button,visible && !SupportTutorialActive());
        if (visible)
        {
            UnityUtil.SetUILabelValue(UnityUtil.GetChildNode(view.button,"ButtonMsg"),
                view.warehouse ? "フレンドに戻る" : "倉庫から選択");
            ResetCollabCardAnimation(view.button);
        }
    }

    private static bool SourceButtonPressed(WarehouseSupportView view)
    {
        if (SupportTutorialActive() || view.button == null || !view.button.activeInHierarchy) return false;
        if (Singleton<DialogManager>.Instance != null && Singleton<DialogManager>.Instance.CheckDialogActive()) return false;
        Input2DManager input = Input2DManager.Instance;
        return input.m_TouchObjectReleaseEqualTriger && input.m_TouchObjectRelease == view.button;
    }

    private static bool PartyContains(PacketStructPartyAssign party, long id)
    {
        return party != null && (party.unit0_unique_id == id || party.unit1_unique_id == id ||
            party.unit2_unique_id == id || party.unit3_unique_id == id);
    }

    public static bool WarehouseQuestEligible(PacketStructUnit unit)
    {
        UserDataAdmin admin = Singleton<UserDataAdmin>.Instance;
        MasterDataParamChara master = unit == null ? null : MasterDataUtil.GetCharaParamFromID(unit.id);
        if (master == null || master.rare < 4) return false;
        PacketStructPlayer player = admin.m_StructPlayer;
        return !PartyContains(player.unit_party_assign[player.unit_party_current],unit.unique_id);
    }

    public static TemplateList<MainMenuSeqQuestSelectFriend.SortParamFriend> QuestSupportList(
        TemplateList<MainMenuSeqQuestSelectFriend.SortParamFriend> original, MainMenuSeqQuestSelectFriend page)
    {
        WarehouseSupportView view = SupportView(page);
        if (!view.warehouse || SupportTutorialActive()) return original;
        TemplateList<MainMenuSeqQuestSelectFriend.SortParamFriend> list = new TemplateList<MainMenuSeqQuestSelectFriend.SortParamFriend>();
        foreach (PacketStructUnit unit in Singleton<UserDataAdmin>.Instance.m_StructPlayer.unit_list)
        {
            if (!WarehouseQuestEligible(unit)) continue;
            MainMenuSeqQuestSelectFriend.SortParamFriend row = new MainMenuSeqQuestSelectFriend.SortParamFriend();
            row.m_FriendStruct = WarehouseFriend(unit);
            row.m_FriendCharaMaster = MasterDataUtil.GetCharaParamFromID(unit.id);
            list.Add(row);
        }
        return list;
    }

    public static bool UpdateQuestSupportSource(MainMenuSeqQuestSelectFriend page)
    {
        WarehouseSupportView view = SupportView(page);
        SetupSourceButton(page,"ListSort",true);
        int party = Singleton<UserDataAdmin>.Instance.m_StructPlayer.unit_party_current;
        bool switched = SourceButtonPressed(view);
        if (switched) { view.warehouse = !view.warehouse; SoundUtil.PlaySE(SEID.SE_MENU_OK); }
        if (switched || (view.warehouse && view.party != party))
        {
            view.party = party; RebuildQuestSupports(page);
            MainMenuFixLayerLinker.UnderMsgRequest(view.warehouse ?
                "倉庫の★5以上を助っ人に選択。使用中の4体は除外します。" : GameTextUtil.GetText("MM_UNDERMSG_QUEST_HELPER"));
            return true;
        }
        view.party = party; return false;
    }

    private static bool SameOwnedSupport(ServerForgerySaveUser player, PacketStructUnit unit)
    {
        if (unit == null || player == null || player.unit_list == null) return false;
        foreach (PacketStructUnit owned in player.unit_list)
            if (owned != null && owned.unique_id == unit.unique_id)
                return owned.id == unit.id && owned.level == unit.level && owned.exp == unit.exp &&
                    owned.limitbreak_lv == unit.limitbreak_lv && owned.add_pow == unit.add_pow && owned.add_hp == unit.add_hp;
        return false;
    }

    public static string ValidateWarehouseQuest(ServerForgery server, ref PacketData packet, string json)
    {
        SendQuestStart request = JsonMapper.ToObject<SendQuestStart>(json);
        if (request == null) return null;
        if (!IsWarehouseSupport(request.helper_user_id)) return null;
        ServerForgerySaveUser p = LocalShopPlayer(server);
        if (SameOwnedSupport(p,request.helper_unit) && request.helper_point_ok == 0 &&
            request.party_current >= 0 && request.party_current < p.unit_party_assign.Length &&
            !PartyContains(p.unit_party_assign[request.party_current],request.helper_unit.unique_id)) return null;
        RecvQuestStart response = new RecvQuestStart(); response.header = new PacketStructHeaderRecv();
        response.header.packet_unique_id = packet.m_PacketUniqueNum; response.header.code = 8194;
        return JsonMapper.ToJson(response);
    }

}
