using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
public static partial class PatchPlayable290
{
    static void SaveTransfer290()
    {
        var rename=adapter.Types.Single(t=>t.Name=="DivineGateOffline").Methods.Single(m=>m.Name=="RenameSaveFile");
        var libc=new ModuleReference("libc");adapter.ModuleReferences.Add(libc);
        rename.IsPInvokeImpl=true;rename.ImplAttributes=MethodImplAttributes.PreserveSig;
        rename.PInvokeInfo=new PInvokeInfo(PInvokeAttributes.CallConvCdecl|PInvokeAttributes.CharSetAnsi|
            PInvokeAttributes.SupportsLastError,"rename",libc);rename.Body=null;
        Prefix("ServerForgery","UserParamLoad","BeforeSaveAccountLoad");
        ReadyPageInput("MainMenuSeqOthers","UpdateSaveTransferUI");
        var page=Method("MainMenuSeqOthers","WindowUpdateTransfer");var il=Reset(page);
        il.Emit(OpCodes.Ldarg_0);il.Emit(OpCodes.Call,Helper("SaveTransferInput"));il.Emit(OpCodes.Ret);
    }
}
