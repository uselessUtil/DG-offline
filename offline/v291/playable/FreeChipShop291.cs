using System;
using UnityEngine;

public static partial class DivineGateOffline
{
    // Android's first six original MasterDataStoreProduct entries, in the
    // order actually used by StoreManager.GetProductFromNum (not stale names).
    private static readonly uint[] freeChipAmounts = { 1, 6, 12, 25, 52, 85 };
    private static MainMenuSeqShopBuyTip freeChipPage;
    private static string freeChipProduct;
    private static bool freeChipCommitted;
    private static Dialog freeChipResult;

    private static ServerForgerySaveUser LocalShopPlayer(ServerForgery server)
    {
        // Patch.cs connects this to a version-specific read accessor in the
        // original server assembly. Never use reflection or a second save.
        throw new InvalidOperationException("Missing local shop save accessor");
    }

    public static StoreProduct FreeChipProductAt(int index)
    {
        if (index < 0 || index >= freeChipAmounts.Length) return null;
        uint amount = freeChipAmounts[index];
        MasterDataStoreProduct row = new MasterDataStoreProduct();
        row.fix_id = (uint)(index + 6);
        row.platform = 1;
        row.id = "jp.gungho.dg.chip" + amount;
        row.name = "FREE " + amount + " chips";
        row.price = 0;
        row.point = amount;
        StoreProduct product = new StoreProduct();
        product.product = row;
        product.product_price_format = "FREE";
        return product;
    }

    public static StoreProduct FreeChipProductByID(string id)
    {
        for (int i = 0; i < freeChipAmounts.Length; i++)
        {
            StoreProduct product = FreeChipProductAt(i);
            if (product.product_id == id) return product;
        }
        return null;
    }

    public static bool LocalChipShopOpen()
    {
        ServerForgery server = Singleton<ServerForgery>.Instance;
        return server != null && LocalShopPlayer(server) != null;
    }

    public static void SetupFreeChipProduct(GameObject root, StoreProduct product,
        StoreProduct unusedBase, string unusedDiscount)
    {
        UnityUtil.SetUILabelValue(UnityUtil.GetChildNode(root, "StonePrice"), "FREE");
        UnityUtil.SetUILabelValue(UnityUtil.GetChildNode(root, "StoneNum"),
            product == null ? "-" : product.product_num.ToString());
        UnityUtil.SetObjectEnabled(UnityUtil.GetChildNode(root, "StoneDiscountMsg"), false);
        // Original ProductSetup divides by the base price for discount labels;
        // a free shop must not execute that calculation with a zero price.
    }

    public static uint ClaimFreeChips(string productID)
    {
        StoreProduct product = FreeChipProductByID(productID);
        if (product == null) throw new InvalidOperationException("Unknown chip pack.");
        ServerForgery server = Singleton<ServerForgery>.Instance;
        if (server == null) throw new InvalidOperationException("Local account is unavailable.");
        ServerForgerySaveUser player = LocalShopPlayer(server);
        if (player == null) throw new InvalidOperationException("Local account is unavailable.");
        uint amount = product.product_num;
        // Preserve existing paid chips and all original purchase counters.
        // Refuse the whole pack if it would cross the original 999-chip gate.
        if (player.have_stone_pay > 999 || player.have_stone_free > 999 - player.have_stone_pay ||
            amount > 999 - player.have_stone_pay - player.have_stone_free)
            throw new InvalidOperationException("Chip limit: 999. Use chips or choose a smaller pack.");
        uint oldFree = player.have_stone_free, oldTotal = player.have_stone;
        player.have_stone_free += amount;
        player.have_stone = player.have_stone_free + player.have_stone_pay;
        try { server.UserParamSave(); }
        catch { player.have_stone_free = oldFree; player.have_stone = oldTotal; throw; }
        if (Singleton<UserDataAdmin>.Instance != null && Singleton<UserDataAdmin>.Instance.m_StructPlayer != null)
        {
            var display = Singleton<UserDataAdmin>.Instance.m_StructPlayer;
            display.have_stone_free = player.have_stone_free;
            display.have_stone_pay = player.have_stone_pay;
            display.have_stone = player.have_stone;
        }
        return player.have_stone;
    }

    public static void SetupFreeChipPage(MainMenuSeqShopBuyTip page, bool active)
    {
        if (freeChipResult != null) freeChipResult.Close();
        freeChipResult = null;
        freeChipPage = active ? page : null;
        freeChipCommitted = !active;
        freeChipProduct = active ? (MainMenuParam.m_ShopBuyProduct == null ? null : MainMenuParam.m_ShopBuyProduct.product_id) : null;
        if (!active) return;
        LocalShopLabels291(page);
        // Validate the actual instantiated prefab, rather than assuming that
        // a title-only page has the controls found in the legacy layout.
        GameObject messageNode = UnityUtil.GetChildNode(page.gameObject, "MessageMain");
        if (messageNode == null || UnityUtil.GetChildNode(page.gameObject, "ButtonYes") == null ||
            UnityUtil.GetChildNode(page.gameObject, "ButtonNo") == null)
        {
            Debug.LogError("[DG offline] Free chip confirmation controls missing; returning to shop");
            LeaveFreeChipPage();
            return;
        }
        StoreProduct product = FreeChipProductByID(freeChipProduct);
        uint current = Singleton<UserDataAdmin>.Instance.m_StructPlayer.have_stone;
        string message = product == null ? "Unknown chip pack." :
            "Receive " + product.product_num + " chips for FREE?\nCurrent: " + current +
            "   After: " + (current + product.product_num) + "\nRepeatable. Chip limit: 999.";
        UnityUtil.SetUILabelValue(messageNode, message);
        MainMenuFixLayerLinker.UnderMsgRequest("Offline shop: receive free chips. No payment.");
        Debug.Log("[DG offline] Free chip confirmation ready: " + freeChipProduct);
    }

    private static void LocalShopLabels291(MainMenuSeqShopBuyTip page)
    {
        foreach(string name in new[]{"DialogTitle","ButtonYes","ButtonNo"}) {
            GameObject node=UnityUtil.GetChildNode(page.gameObject,name);
            if(node==null)continue;
            foreach(var setter in node.GetComponentsInChildren<LabelSetter>(true)){setter.enabled=false;setter.m_LabelTextKey="";}
            foreach(var label in node.GetComponentsInChildren<UILabel>(true))
                label.text=name=="DialogTitle"?"無料チップ":name=="ButtonYes"?"受け取る":"戻る";
        }
    }

    private static void LeaveFreeChipPage()
    {
        freeChipCommitted = true;
        Singleton<MainMenuManager>.Instance.AddSwitchRequest(MAINMENU_SEQ.SEQ_SHOP_ROOT, false, true);
    }

    // Only this confirmation can claim once. A new visit starts a new claim.
    // Set the guard before saving, so repeated release events cannot double it.
    private static bool CommitFreeChipPage(MainMenuSeqShopBuyTip page, out string message)
    {
        message = "";
        if (page != freeChipPage || freeChipCommitted) return false;
        freeChipCommitted = true;
        try
        {
            uint total = ClaimFreeChips(freeChipProduct);
            message = "Received " + FreeChipProductByID(freeChipProduct).product_num +
                " free chips.\nTotal: " + total;
        }
        catch (InvalidOperationException ex) { message = ex.Message; }
        catch (Exception ex)
        {
            Debug.LogError("[DG offline] Free chip save failed: " + ex.Message);
            message = "Could not save chips. Please return to the shop and try again.";
        }
        return true;
    }

    public static void UpdateFreeChipPage(MainMenuSeqShopBuyTip page, bool ready)
    {
        if (!ready || page != freeChipPage) return;
        LocalShopLabels291(page);
        Input2DManager input = Input2DManager.Instance;
        if (freeChipResult != null)
        {
            if (freeChipResult.m_DialogPushButton != EDIALOG_BTN.eDIALOG_BTN_NONE || input.m_BackButonInputTrigger)
            {
                freeChipResult.Close(); freeChipResult = null; LeaveFreeChipPage();
            }
            return;
        }
        if (freeChipCommitted || (Singleton<DialogManager>.Instance != null &&
            Singleton<DialogManager>.Instance.CheckDialogActive())) return;
        bool released = input.m_TouchObjectRelease != null && input.m_TouchObjectReleaseEqualTriger;
        GameObject touched = released ? input.m_TouchObjectRelease : null;
        if (input.m_BackButonInputTrigger || (released &&
            (touched == UnityUtil.GetChildNode(page.gameObject, "PageTitle") ||
             touched == UnityUtil.GetChildNode(page.gameObject, "ButtonNo"))))
        {
            SoundUtil.PlaySE(SEID.SE_MENU_RET); LeaveFreeChipPage(); return;
        }
        if (!released || touched != UnityUtil.GetChildNode(page.gameObject, "ButtonYes")) return;
        string message;
        if (CommitFreeChipPage(page, out message))
        {
            SoundUtil.PlaySE(SEID.SE_MENU_OK);
            freeChipResult = DialogManager.Open1B_Direct("FREE CHIPS", message, "BTN_OK", true);
            if (freeChipResult == null) LeaveFreeChipPage();
        }
    }
}
