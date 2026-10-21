using UnityEngine;
using Object = UnityEngine.Object;

public static partial class DivineGateOffline
{
    private static readonly uint[] collabPoolIDs = { 300100, 300200, 300300, 300400 };
    private static readonly string[] collabPoolNames = {
        "FAIRY TAIL", "進撃の巨人", "ケリ姫スイーツ", "とある魔術の禁書目録" };
    private static readonly string[] collabCardNames = {
        "FAIRY TAIL", "進撃の巨人", "ケリ姫スイーツ", "とある魔術の\n禁書目録" };
    private static MainMenuSeqGachaRoot collabOwner;
    private static GameObject collabChoices;
    private static GameObject[] collabCards;
    private static BoxCollider collabBackCollider;
    private static bool collabSelecting;
    private static string collabOriginalTitle;

    public static uint CollabPoolID(int index)
    {
        return index >= 0 && index < collabPoolIDs.Length ? collabPoolIDs[index] : 0;
    }

    public static Vector3 CollabChoicePosition(int index)
    {
        return new Vector3(index % 2 == 0 ? -155 : 155, index < 2 ? 130 : -150, 0);
    }

    private static MasterDataGacha LocalCollabPool(uint id)
    {
        bool known = false;
        foreach (uint allowed in collabPoolIDs) if (id == allowed) known = true;
        if (!known || Singleton<MasterDataManager>.Instance == null ||
            Singleton<MasterDataManager>.Instance.m_MasterDataGacha == null) return null;
        foreach (MasterDataGacha pool in Singleton<MasterDataManager>.Instance.m_MasterDataGacha)
            if (pool != null && pool.fix_id == id && pool.type == 3 && pool.price > 0) return pool;
        return null;
    }

    private static void OpenLocalCollabPools(MasterDataManager master)
    {
        for (int i = 0; i < collabPoolIDs.Length; i++)
        {
            MasterDataGacha pool = LocalCollabPool(collabPoolIDs[i]);
            if (pool == null) continue;
            pool.event_id = 0;
            pool.name = collabPoolNames[i];
            // Do not present expired live-service campaign dates as current.
            pool.detail_2 = "[ffc000]Offline Collab Scratch[-]\n" +
                "FAIRY TAIL / 進撃の巨人\nケリ姫スイーツ / とある魔術の禁書目録\n" +
                "Choose a pool at the Collab entry. 5 chips per draw.";
        }
    }

    public static bool SelectLocalCollabPool(uint id)
    {
        if (LocalCollabPool(id) == null || Singleton<UserDataAdmin>.Instance == null ||
            Singleton<UserDataAdmin>.Instance.m_StructPlayer == null) return false;
        MainMenuParam.m_GachaID = id;
        MainMenuParam.m_GachaPrevUnitGetFlag = Singleton<UserDataAdmin>.Instance.m_StructPlayer.flag_unit_get;
        return true;
    }

    public static bool CollabSelectorOpen(MainMenuSeqGachaRoot page)
    {
        return collabSelecting && collabOwner == page;
    }

    private static void SetCollabMode(bool selecting)
    {
        collabSelecting = selecting;
        UnityUtil.SetObjectEnabled(UnityUtil.GetChildNode(collabOwner.gameObject, "GachaType3"), !selecting);
        UnityUtil.SetObjectEnabled(UnityUtil.GetChildNode(collabOwner.gameObject, "ListObject"), !selecting);
        UnityUtil.SetObjectEnabled(collabChoices, selecting);
        if (collabBackCollider != null)
        {
            collabBackCollider.enabled = selecting;
        }
        UnityUtil.SetUILabelValue(UnityUtil.GetChildNode(collabOwner.gameObject, "PageTitleLabel"), (!selecting) ? collabOriginalTitle : "Collab Scratch");
    }


    private static bool CreateCollabSelector(MainMenuSeqGachaRoot page)
    {
        if (collabOwner == page && collabChoices != null)
        {
            return true;
        }
        GameObject childNode = UnityUtil.GetChildNode(page.gameObject, "Element02");
        GameObject childNode2 = UnityUtil.GetChildNode(page.gameObject, "PageRoot");
        GameObject childNode3 = UnityUtil.GetChildNode(page.gameObject, "PageTitle");
        GameObject childNode4 = UnityUtil.GetChildNode(page.gameObject, "PageTitleLabel");
        if (childNode == null || childNode2 == null || childNode3 == null || childNode4 == null || childNode.GetComponent<BoxCollider>() == null)
        {
            return false;
        }
        collabOwner = page;
        collabOriginalTitle = childNode4.GetComponent<UILabel>().text;
        collabChoices = new GameObject("DG offline collab pool choices");
        collabChoices.transform.parent = childNode2.transform;
        collabChoices.transform.localPosition = new Vector3(0f, 0f, -200f);
        collabChoices.transform.localScale = new Vector3(1f, 1f, 1f);
        collabCards = new GameObject[collabPoolIDs.Length];
        for (int i = 0; i < collabCards.Length; i++)
        {
            GameObject gameObject = Object.Instantiate(childNode) as GameObject;
            gameObject.name = "DG collab pool " + collabPoolIDs[i];
            gameObject.transform.parent = collabChoices.transform;
            gameObject.transform.localPosition = CollabChoicePosition(i);
            gameObject.transform.localScale = new Vector3(1f, 1f, 1f);
            collabCards[i] = gameObject;
            UnityUtil.SetObjectEnabled(gameObject, true);
            UnityUtil.SetObjectEnabled(UnityUtil.GetChildNode(gameObject, "CountMark"), false);
            GameObject childNode5 = UnityUtil.GetChildNode(gameObject, "ElementLabel");
            childNode5.transform.localScale = new Vector3(20f, 20f, 1f);
            UnityUtil.SetUILabelValue(childNode5, collabCardNames[i]);
            GameObject childNode6 = UnityUtil.GetChildNode(gameObject, "ElementHint");
            UnityUtil.SetObjectEnabled(childNode6, true);
            childNode6.transform.localPosition = new Vector3(-70f, 90f, -100f);
            childNode6.transform.localScale = new Vector3(19f, 19f, 1f);
            UnityUtil.SetUILabelValue(childNode6, "5 chips / draw");
            UnityUtil.SetUIAtlasSprite(UnityUtil.GetChildNode(gameObject, "ElementObj"), Singleton<SceneObjReferMainMenu>.Instance.m_MainMenuAtlasGacha, "gacha_event");
        }
        collabBackCollider = childNode3.GetComponent<BoxCollider>();
        if (collabBackCollider == null)
        {
            collabBackCollider = childNode3.AddComponent<BoxCollider>();
        }
        collabBackCollider.size = new Vector3(1f, 1f, 1f);
        collabBackCollider.center = new Vector3(0.5f, 0f, 0f);
        collabBackCollider.enabled = false;
        collabChoices.SetActive(false);
        return true;
    }


    public static void RefreshCollabRoot(MainMenuSeqGachaRoot page, bool active)
    {
        if (!active) return;
        if (collabOwner == page && collabChoices != null) SetCollabMode(false);
        GameObject entry = UnityUtil.GetChildNode(page.gameObject, "Element02");
        UnityUtil.SetUILabelValue(UnityUtil.GetChildNode(entry, "ElementLabel"), "コラボ選択");
        GameObject hint = UnityUtil.GetChildNode(entry, "ElementHint");
        UnityUtil.SetObjectEnabled(hint, true);
        UnityUtil.SetUILabelValue(hint, "4 pools\n5 chips / draw");
    }

    // Only the root page's confirmation request is redirected. Friendship and
    // regular scratch keep their original code and tutorial restrictions.
    public static bool RouteGachaRootRequest(MainMenuManager menu, MAINMENU_SEQ next, bool fast, bool back,
        MainMenuSeqGachaRoot page)
    {
        if (LocalCollabPool(MainMenuParam.m_GachaID) == null)
        {
            return menu.AddSwitchRequest(next, fast, back);
        }
        if (page == null || !CreateCollabSelector(page))
        {
            Debug.LogError("[DG offline] Collab selector controls unavailable");
            DialogManager.Open1B_Direct("Collab Scratch", "Cannot open the pool selection. Please return to Scratch.", "BTN_OK", true);
            return false;
        }
        SetCollabMode(true);
        MainMenuFixLayerLinker.UnderMsgRequest("Choose a collab pool. Back cancels. Each draw costs 5 chips.");
        return true;
    }

    public static void UpdateCollabSelector(MainMenuSeqGachaRoot page, bool ready)
    {
        if (!ready || !CollabSelectorOpen(page)) return;
        if (Singleton<DialogManager>.Instance != null && Singleton<DialogManager>.Instance.CheckDialogActive()) return;
        Input2DManager input = Input2DManager.Instance;
        bool released = input.m_TouchObjectRelease != null && input.m_TouchObjectReleaseEqualTriger;
        if (input.m_BackButonInputTrigger || (released && input.m_TouchObjectRelease ==
            UnityUtil.GetChildNode(page.gameObject, "PageTitle")))
        {
            SetCollabMode(false);
            SoundUtil.PlaySE(SEID.SE_MENU_RET);
            return;
        }
        if (!released) return;
        for (int i = 0; i < collabCards.Length; i++)
        {
            if (input.m_TouchObjectRelease != collabCards[i]) continue;
            if (!SelectLocalCollabPool(collabPoolIDs[i])) return;
            SetCollabMode(false);
            SoundUtil.PlaySE(SEID.SE_MENU_OK);
            Singleton<MainMenuManager>.Instance.AddSwitchRequest(MAINMENU_SEQ.SEQ_GACHA_CONFIRM, false, false);
            return;
        }
    }
}
