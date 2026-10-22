// Test-only adapters. Never change the actual menu routing method or APK.
using System;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
public static class PrepareNoticeProbe
{
    public static void Main(string[] args)
    {
        var game=ModuleDefinition.ReadModule(args[0]);
        var control=new TypeDefinition("","NoticeRoutingControl",TypeAttributes.Public|TypeAttributes.Abstract|TypeAttributes.Sealed,game.TypeSystem.Object);
        var next=new FieldDefinition("Next",FieldAttributes.Public|FieldAttributes.Static,game.TypeSystem.Int32);
        var count=new FieldDefinition("Calls",FieldAttributes.Public|FieldAttributes.Static,game.TypeSystem.Int32);
        control.Fields.Add(next);control.Fields.Add(count);game.Types.Add(control);
        var request=game.Types.Single(t=>t.Name=="MainMenuManager").Methods.Single(m=>m.Name=="AddSwitchRequest");
        request.Body=new MethodBody(request);var il=request.Body.GetILProcessor();
        il.Emit(OpCodes.Ldarg_1);il.Emit(OpCodes.Stsfld,next);
        il.Emit(OpCodes.Ldsfld,count);il.Emit(OpCodes.Ldc_I4_1);il.Emit(OpCodes.Add);il.Emit(OpCodes.Stsfld,count);
        il.Emit(OpCodes.Ldc_I4_1);il.Emit(OpCodes.Ret);
        var fade=game.Types.Single(t=>t.Name=="MainMenuSeq").Methods.Single(m=>m.Name=="PageSwitchUpdate");
        fade.Body=new MethodBody(fade);il=fade.Body.GetILProcessor();il.Emit(OpCodes.Ldc_I4_1);il.Emit(OpCodes.Ret);
        // Avoid text/font setup; only provide a nonempty earned-reward message.
        var rewardText=game.Types.Single(t=>t.Name=="MainMenuSeqLoginBonus").Methods.Single(m=>m.Name=="CreateLoginBonusMsg");
        rewardText.Body=new MethodBody(rewardText);il=rewardText.Body.GetILProcessor();il.Emit(OpCodes.Ldstr,"probe reward message");il.Emit(OpCodes.Ret);
        var send=game.Types.Single(t=>t.Name=="ServerDataUtilSend");
        var finish=send.Methods.Single(m=>m.Name=="CheckPacketFinish");
        finish.Body=new MethodBody(finish);il=finish.Body.GetILProcessor();il.Emit(OpCodes.Ldc_I4_1);il.Emit(OpCodes.Ret);
        var result=game.Types.Single(t=>t.Name=="PacketDataResult");
        var get=send.Methods.Single(m=>m.Name=="GetPacketResult");
        get.Body=new MethodBody(get);il=get.Body.GetILProcessor();
        il.Emit(OpCodes.Newobj,result.Methods.Single(m=>m.IsConstructor && m.Parameters.Count==0));
        il.Emit(OpCodes.Dup);
        int success=Convert.ToInt32(game.Types.Single(t=>t.Name=="API_CODE").Fields.Single(f=>f.Name=="API_CODE_SUCCESS").Constant);
        il.Emit(OpCodes.Ldc_I4,success);il.Emit(OpCodes.Stfld,result.Fields.Single(f=>f.Name=="m_PacketCode"));il.Emit(OpCodes.Ret);
        var random=game.Types.Single(t=>t.Name=="RandManager").Methods.Single(m=>m.Name=="GetRand" && m.Parameters.Count==2);
        var head=random.Body.Instructions[0];il=random.Body.GetILProcessor();
        il.InsertBefore(head,Instruction.Create(OpCodes.Ldarg_0));il.InsertBefore(head,Instruction.Create(OpCodes.Brtrue,head));
        il.InsertBefore(head,Instruction.Create(OpCodes.Ldarg_1));il.InsertBefore(head,Instruction.Create(OpCodes.Ldc_I4,10));
        il.InsertBefore(head,Instruction.Create(OpCodes.Bne_Un,head));il.InsertBefore(head,Instruction.Create(OpCodes.Ldc_I4_2));
        il.InsertBefore(head,Instruction.Create(OpCodes.Ret));
        game.Write(args[1]);
        Console.WriteLine("PROBE ONLY: native fade/request/transport and reward-text stubs, deterministic 2-helper random input; dispatcher, login-pack receipt handler and FP award calculations unchanged.");
    }
}
