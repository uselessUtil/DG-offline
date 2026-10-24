using UnityEngine;

public static partial class DivineGateOffline
{
    // Only a fully idle dialog manager proves that this is an abandoned
    // reference. Never release an opening, visible, or closing confirmation.
    public static bool IsOrphanEvolutionDialog290(Dialog dialog)
    {
        if (dialog == null || dialog.m_DialogPushButton != EDIALOG_BTN.eDIALOG_BTN_NONE)
            return false;
        var manager = Singleton<DialogManager>.Instance;
        if (manager == null || manager.m_DialogLayout == null) return false;
        var layout = manager.m_DialogLayout;
        return layout.GetLayoutCurrent() == 0 && layout.GetLayoutPauseOK() &&
            !layout.ChkLayoutRequestNG() && !dialog.m_LayoutExecOK &&
            !UnityUtil.ChkObjectEnabled(dialog.gameObject);
    }
}
