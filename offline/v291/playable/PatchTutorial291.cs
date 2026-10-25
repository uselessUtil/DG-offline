using System.Linq;
using Mono.Cecil.Cil;

public static partial class PatchPlayable291
{
    static void TutorialSkip291()
    {
        BindBridge("TutorialHeader291", "ServerForgery", (il, type) => {
            il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Call, Method(type.Name, "CreateForgeryStructHeader")); il.Emit(OpCodes.Ret);
        });
        BindBridge("TutorialPlayer291", "ServerForgery", (il, type) => {
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Call, Method(type.Name, "CreateForgeryStructPlayer")); il.Emit(OpCodes.Ret);
        });
        BindBridge("SetTutorialPlayer291", "ServerForgery", (il, type) => {
            il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Stfld, type.Fields.Single(f => f.Name == "m_ForgeryPlayerSave")); il.Emit(OpCodes.Ret);
        });
        GuardWarehouseRequest("CreateRecvForgery", "HandleTutorialSkip291");
    }
}
