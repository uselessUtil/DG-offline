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
    private static Vector3 collabOriginalGroupPosition;
    private static CollabCardView[] collabOriginalCards;

    private sealed class CollabCardView
    {
        public GameObject root;
        public Vector3 position, scale, labelScale, hintPosition, hintScale;
        public string label, hint, sprite;
        public UIAtlas atlas;
        public bool hintActive, countActive;

        public void Capture(GameObject card)
        {
            ResetCollabCardAnimation(card);
            root = card; position = card.transform.localPosition; scale = card.transform.localScale;
            GameObject labelNode = UnityUtil.GetChildNode(card, "ElementLabel");
            GameObject hintNode = UnityUtil.GetChildNode(card, "ElementHint");
            labelScale = labelNode.transform.localScale; label = labelNode.GetComponent<UILabel>().text;
            hintPosition = hintNode.transform.localPosition; hintScale = hintNode.transform.localScale;
            hint = hintNode.GetComponent<UILabel>().text; hintActive = hintNode.activeSelf;
            countActive = UnityUtil.GetChildNode(card, "CountMark").activeSelf;
            UISprite image = UnityUtil.GetChildNode(card, "ElementObj").GetComponent<UISprite>();
            atlas = image.atlas; sprite = image.spriteName;
        }

        public void Restore()
        {
            root.transform.localPosition = position; root.transform.localScale = scale;
            GameObject labelNode = UnityUtil.GetChildNode(root, "ElementLabel");
            GameObject hintNode = UnityUtil.GetChildNode(root, "ElementHint");
            labelNode.transform.localScale = labelScale; UnityUtil.SetUILabelValue(labelNode, label);
            hintNode.transform.localPosition = hintPosition; hintNode.transform.localScale = hintScale;
            UnityUtil.SetUILabelValue(hintNode, hint); hintNode.SetActive(hintActive);
            UnityUtil.GetChildNode(root, "CountMark").SetActive(countActive);
            UnityUtil.SetUIAtlasSprite(UnityUtil.GetChildNode(root, "ElementObj"), atlas, sprite);
            ResetCollabCardAnimation(root);
        }
    }

    private static void ResetCollabCardAnimation(GameObject card)
    {
        UIButtonScale button = card.GetComponent<UIButtonScale>();
        if (button == null || !button.enabled) return;
        // OnHover(false) clears the button's private highlight state and uses
        // its cached neutral scale, rather than the current pressed/hover frame.
        card.SendMessage("OnHover", false, SendMessageOptions.DontRequireReceiver);
        if (button.tweenTarget == null) return;
        TweenScale tween = button.tweenTarget.GetComponent<TweenScale>();
        if (tween == null) return;
        tween.enabled = false;
        button.tweenTarget.localScale = tween.to;
        // Page fade-in enables disabled behaviours again. A revived old tween
        // must have neutral endpoints, not the pre-switch hover/press targets.
        tween.from = tween.to;
    }

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

    public static void MaintainCollabRoot(MainMenuSeqGachaRoot page)
    {
        if (collabOwner != page || collabSelecting) return;
        // The original fade-in recursively enables every child, including
        // inactive clones and colliders. Reapply normal-page visibility even
        // when the selector was already closed before leaving for a draw.
        if (collabCards != null && collabCards.Length > 3 && collabCards[3] != null &&
            collabCards[3].activeSelf) collabCards[3].SetActive(false);
        if (collabBackCollider != null && collabBackCollider.enabled) collabBackCollider.enabled = false;
    }

    private static void SetCollabMode(bool selecting)
    {
        if (selecting == collabSelecting)
        {
            MaintainCollabRoot(collabOwner);
            return;
        }
        if (selecting)
        {
            collabOriginalGroupPosition = collabChoices.transform.localPosition;
            collabChoices.transform.localPosition = new Vector3(0, 0, collabOriginalGroupPosition.z);
            for (int i = 0; i < 3; i++) collabOriginalCards[i].Capture(collabCards[i]);
            for (int i = 0; i < collabCards.Length; i++) SetupCollabCard(collabCards[i], i);
        }
        else
        {
            for (int i = 0; i < 3; i++) collabOriginalCards[i].Restore();
            collabCards[3].SetActive(false);
            collabChoices.transform.localPosition = collabOriginalGroupPosition;
        }
        collabSelecting = selecting;
        // Keep the three original visible cards in their original hierarchy.
        // Hiding GachaType3 would also hide this selector.
        UnityUtil.SetObjectEnabled(UnityUtil.GetChildNode(collabOwner.gameObject, "ListObject"), !selecting);
        if (collabBackCollider != null) collabBackCollider.enabled = selecting;
        MaintainCollabRoot(collabOwner);
        UnityUtil.SetUILabelValue(UnityUtil.GetChildNode(collabOwner.gameObject, "PageTitleLabel"),
            selecting ? "Collab Scratch" : collabOriginalTitle);
        Debug.Log("[DG offline] Collab selector " + (selecting ? "open: original 3 cards + fourth, native hierarchy" : "closed: original scratch restored"));
    }

    private static void BindCollabWidgets(GameObject root, int layer)
    {
        root.layer = layer;
        UIWidget widget = root.GetComponent<UIWidget>();
        if (widget != null)
        {
            widget.enabled = true;
            widget.alpha = 1;
            widget.ParentHasChanged();
            widget.CreatePanel();
        }
        for (int i = 0; i < root.transform.GetChildCount(); i++)
            BindCollabWidgets(root.transform.GetChild(i).gameObject, layer);
    }

    private static void SetupCollabCard(GameObject card, int index)
    {
        card.transform.localPosition = CollabChoicePosition(index);
        card.transform.localScale = new Vector3(1, 1, 1);
        UIPanel panel = UIPanel.Find(collabChoices.transform, false);
        int layer = panel != null ? panel.gameObject.layer : collabChoices.layer;
        UnityUtil.SetObjectLayer(card, layer);
        UnityUtil.SetObjectEnabledFix(card, true);
        ResetCollabCardAnimation(card);
        GameObject label = UnityUtil.GetChildNode(card, "ElementLabel");
        label.transform.localScale = new Vector3(20, 20, 1);
        UnityUtil.SetUILabelValue(label, collabCardNames[index]);
        GameObject hint = UnityUtil.GetChildNode(card, "ElementHint");
        hint.transform.localPosition = new Vector3(-70, 90, -100);
        hint.transform.localScale = new Vector3(19, 19, 1);
        UnityUtil.SetUILabelValue(hint, "5 chips / draw");
        UnityUtil.SetUIAtlasSprite(UnityUtil.GetChildNode(card, "ElementObj"),
            Singleton<SceneObjReferMainMenu>.Instance.m_MainMenuAtlasGacha, "gacha_event");
        BindCollabWidgets(card, layer);
        UnityUtil.GetChildNode(card, "CountMark").SetActive(false);
    }

    private static bool CreateCollabSelector(MainMenuSeqGachaRoot page)
    {
        if (collabOwner == page && collabChoices != null) return true;
        GameObject source = UnityUtil.GetChildNode(page.gameObject, "Element02");
        GameObject parent = UnityUtil.GetChildNode(page.gameObject, "GachaType3");
        GameObject title = UnityUtil.GetChildNode(page.gameObject, "PageTitle");
        GameObject titleLabel = UnityUtil.GetChildNode(page.gameObject, "PageTitleLabel");
        if (source == null || parent == null || title == null || titleLabel == null ||
            source.GetComponent<BoxCollider>() == null) return false;
        collabOwner = page;
        collabSelecting = false;
        collabOriginalTitle = titleLabel.GetComponent<UILabel>().text;
        collabChoices = parent;
        collabCards = new GameObject[collabPoolIDs.Length];
        collabOriginalCards = new CollabCardView[3];
        for (int i = 0; i < 3; i++)
        {
            collabCards[i] = UnityUtil.GetChildNode(parent, "Element0" + i);
            if (collabCards[i] == null) return false;
            collabOriginalCards[i] = new CollabCardView();
        }
        // Instantiate while inactive; assign the parent and layer before any
        // cloned widget can register itself with an unrelated temporary panel.
        bool sourceActive = source.activeSelf;
        source.SetActive(false);
        GameObject fourth;
        try { fourth = Object.Instantiate(source) as GameObject; }
        finally { source.SetActive(sourceActive); }
        fourth.name = "DG collab pool 300400";
        fourth.transform.parent = parent.transform;
        fourth.transform.localRotation = source.transform.localRotation;
        UnityUtil.SetObjectLayer(fourth, source.layer);
        fourth.SetActive(false);
        collabCards[3] = fourth;
        collabBackCollider = title.GetComponent<BoxCollider>();
        if (collabBackCollider == null) collabBackCollider = title.AddComponent<BoxCollider>();
        collabBackCollider.size = new Vector3(1, 1, 1);
        collabBackCollider.center = new Vector3(.5f, 0, 0);
        collabBackCollider.enabled = false;
        return true;
    }

    public static void BeforeCollabRootRefresh(MainMenuSeqGachaRoot page, bool active)
    {
        if (active && collabOwner == page && collabSelecting) SetCollabMode(false);
    }

    public static void RefreshCollabRoot(MainMenuSeqGachaRoot page, bool active)
    {
        if (!active) return;
        MaintainCollabRoot(page);
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
