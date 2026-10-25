using System;using System.Linq;using Mono.Cecil;using Mono.Cecil.Cil;
public static partial class PatchPlayable291
{
    static void RestoreRetirement291()
    {
        BindBridge("SetRestorePlayer291","ServerForgery",(il,type)=>{
            il.Emit(OpCodes.Ldarg_0);il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Stfld,type.Fields.Single(f=>f.Name=="m_ForgeryPlayerSave"));il.Emit(OpCodes.Ret);
        });
        var init=Method("TitleMenuSeqRestoreChk","RestoreFuncInit");
        var idle=init.Body.Instructions.Single(i=>i.OpCode==OpCodes.Ldc_I4_6 &&
            i.Next.OpCode==OpCodes.Stfld && ((FieldReference)i.Next.Operand).Name=="m_RestoreSeq");
        idle.OpCode=OpCodes.Call;idle.Operand=Helper("RestoreIdlePhase291");
        var cancel=Method("TitleMenuSeqRestoreChk","RestoreFuncChkFinishCancel");
        var ilc=cancel.Body.GetILProcessor();var first=cancel.Body.Instructions.First();
        ilc.InsertBefore(first,Instruction.Create(OpCodes.Call,Helper("CompleteRestoreRetirement291")));
        ilc.InsertBefore(first,Instruction.Create(OpCodes.Brtrue,first));
        ilc.InsertBefore(first,Instruction.Create(OpCodes.Ret));
        // Existing full-account import journal recovery runs afterwards.
        Prefix("ServerForgery","UserParamLoad","RecoverRestoreRetirement291");
    }
}
