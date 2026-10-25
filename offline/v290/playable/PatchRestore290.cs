using System;using System.Linq;using Mono.Cecil;using Mono.Cecil.Cil;
public static partial class PatchPlayable290
{
    static void RestoreRetirement290()
    {
        BindBridge("SetRestorePlayer290","ServerForgery",(il,type)=>{
            il.Emit(OpCodes.Ldarg_0);il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Stfld,type.Fields.Single(f=>f.Name=="m_ForgeryPlayerSave"));il.Emit(OpCodes.Ret);
        });
        var init=Method("TitleMenuSeqRestoreChk","RestoreFuncInit");
        var idle=init.Body.Instructions.Single(i=>i.OpCode==OpCodes.Ldc_I4_6 &&
            i.Next.OpCode==OpCodes.Stfld && ((FieldReference)i.Next.Operand).Name=="m_RestoreSeq");
        idle.OpCode=OpCodes.Call;idle.Operand=Helper("RestoreIdlePhase290");
        var cancel=Method("TitleMenuSeqRestoreChk","RestoreFuncChkFinishCancel");
        var ilc=cancel.Body.GetILProcessor();var first=cancel.Body.Instructions.First();
        ilc.InsertBefore(first,Instruction.Create(OpCodes.Call,Helper("CompleteRestoreRetirement290")));
        ilc.InsertBefore(first,Instruction.Create(OpCodes.Brtrue,first));
        ilc.InsertBefore(first,Instruction.Create(OpCodes.Ret));
        // Existing full-account import journal recovery runs afterwards.
        Prefix("ServerForgery","UserParamLoad","RecoverRestoreRetirement290");
    }
}
