using System;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
public static class VerifyRulesHooks
{
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    public static void Main(string[] args)
    {
        var game=ModuleDefinition.ReadModule(args[0]);
        Check(!game.Types.Any(t=>t.Name=="NoticeRoutingControl" || t.Name=="RulesOddsControl"),"Test controls leaked into APK");
        Func<string,string,MethodDefinition> method=(t,m)=>game.Types.Single(x=>x.Name==t).Methods.Single(x=>x.Name==m);
        Func<MethodDefinition,string,int> calls=(m,n)=>m.Body.Instructions.Count(i=>i.Operand is MethodReference && ((MethodReference)i.Operand).Name==n);
        foreach(string field in new[]{"m_StartingFirstInformation","m_StartingFirstScratch"})
            Check(!method("MainMenuSeqSwitchPage","Update").Body.Instructions.Any(i=>i.OpCode==OpCodes.Ldsfld &&
                ((FieldReference)i.Operand).Name==field),"Automatic notice gate remains: "+field);
        Check(calls(method("MainMenuSeqSwitchPage","Update"),"QuietHelperRewardNotice")==1,"Helper point notice not quieted");
        Check(calls(method("MainMenuSeqSwitchPage","Update"),"NotificationPresents")==7,"Empty-present reads not filtered");
        foreach(var pair in new[]{new[]{"ServerForgery","StaminaAutoRecovery"},new[]{"UserDataAdmin","Update"},new[]{"MainMenuFixPlayerParam","Update"}})
        {
            var m=method(pair[0],pair[1]);
            Check(m.Body.Instructions.Count(i=>i.OpCode==OpCodes.Ldc_R8 && (double)i.Operand==180)==1,"Wrong recovery interval "+pair[0]);
            Check(!m.Body.Instructions.Any(i=>i.OpCode==OpCodes.Ldc_R8 && (double)i.Operand==600),"Stale ten-minute interval");
            if(pair[0]!="MainMenuFixPlayerParam")Check(calls(m,"op_GreaterThanOrEqual")==1,"Boundary excludes exactly180s");
        }
        Check(calls(method("ServerForgery","UserParamSave"),"BeforeAccountSave")==1,"Save migration missing");
        Check(calls(method("ServerForgery","CreateForgeryStructPlayer"),"BeforeAccountSave")==1,"API response migration missing");
        Check(calls(method("ServerForgery","ForgeryGachaPlay"),"ScratchTicket")==2,"Award/blank sampler hook missing");
        var shop=game.Types.Single(t=>t.Name=="MainMenuSeqShopUseTipUnit").Methods.Where(m=>m.HasBody).SelectMany(m=>m.Body.Instructions);
        Check(shop.Count(i=>i.OpCode==OpCodes.Ldc_I4 && (int)i.Operand==2000)==2 &&
            !shop.Any(i=>i.OpCode==OpCodes.Ldc_I4 && (int)i.Operand==400),"Shop warehouse display cap stale");
        Check(calls(method("MainMenuSeqQuestSelectFriend","Update"),"UpdateQuestSupportSource")==1,"Quest input hook missing");
        Check(calls(method("ServerForgery","ForgeryQuestStart"),"ValidateWarehouseQuest")==1,"Quest ownership guard missing");
        Check(calls(method("ServerForgery","AddUnit"),"InitializeAcquisitionTime")==1,"New unit timestamp missing");
        Check(calls(method("ServerForgery","UserParamLoad"),"BeforeSaveAccountLoad")==1,"Save recovery is not before account loading");
        Check(calls(method("MainMenuSeqOthers","Update"),"UpdateSaveTransferUI")==1,"Save manager page hook missing");
        Check(calls(method("MainMenuSeqOthers","WindowUpdateTransfer"),"SaveTransferInput")==1,"Old online transfer handler remains");
        Check(calls(method("MainMenuListSort","SortCreateID_GetTime"),"AcquisitionSortKey")==1,"Second-precision acquisition sorting missing");
        Check(calls(method("MainMenuListSort","SortCreateID_VerForm_GetTime"),"AcquisitionSortKeyForParty")==1,"Party acquisition sorting missing");
        var original=ModuleDefinition.ReadModule(args[1]);
        var sortBefore=original.Types.Single(t=>t.Name=="MainMenuListSort");
        foreach(var before in sortBefore.Methods.Where(m=>m.HasBody && m.Name!="SortCreateID_GetTime" && m.Name!="SortCreateID_VerForm_GetTime"))
            Check(string.Join("\n",before.Body.Instructions.Select(i=>i.ToString()))==
                string.Join("\n",method("MainMenuListSort",before.Name).Body.Instructions.Select(i=>i.ToString())),
                "Unrelated sort changed: "+before.Name);
        var originalBuild=original.Types.Single(t=>t.Name=="MainMenuSeqPartyBuildUp");
        var currentBuild=game.Types.Single(t=>t.Name=="MainMenuSeqPartyBuildUp");
        Check(currentBuild.Methods.Count==originalBuild.Methods.Count,"Unexpected strengthening bridge remains");
        foreach(var before in originalBuild.Methods.Where(m=>m.HasBody))
        {
            var after=currentBuild.Methods.Single(m=>m.FullName==before.FullName);
            Check(string.Join("\n",before.Body.Instructions.Select(i=>i.ToString()))==
                string.Join("\n",after.Body.Instructions.Select(i=>i.ToString())),"Strengthening UI differs from original: "+before.FullName);
        }
        var originalApi=original.Types.Single(t=>t.Name=="ServerForgery").Methods.Single(m=>m.Name=="ForgeryUnitBlendBuildUp");
        Check(string.Join("\n",originalApi.Body.Instructions.Select(i=>i.ToString()))==
            string.Join("\n",method("ServerForgery","ForgeryUnitBlendBuildUp").Body.Instructions.Select(i=>i.ToString())),
            "Strengthening backend differs from original");
        foreach(string name in new[]{"CreateLoginParam","ForgeryLoginPack"})
        {
            var before=original.Types.Single(t=>t.Name=="ServerForgery").Methods.Single(m=>m.Name==name);
            Check(string.Join("\n",before.Body.Instructions.Select(i=>i.ToString()))==
                string.Join("\n",method("ServerForgery",name).Body.Instructions.Select(i=>i.ToString())),
                "Friend-point award API changed: "+name);
        }
        Console.WriteLine("PASS strengthening removal: every original strengthening-page method and strengthening API instruction restored exactly; no warehouse bridges/input hooks remain.");
        foreach(var type in game.Types)
            foreach(var m in type.Methods.Where(x=>x.HasBody))
                foreach(var i in m.Body.Instructions)
                    if(i.OpCode.OperandType==OperandType.ShortInlineBrTarget)
                    {
                        int delta=((Instruction)i.Operand).Offset-i.Offset-i.GetSize();
                        Check(delta>=-128 && delta<=127,"Short branch overflow "+m.FullName);
                    }
        Console.WriteLine("PASS packaged IL: automatic-notice gate bypass; API/client/countdown180s with inclusive boundary; save/load/response and both gacha samplers connected; quest source selector and ownership guard connected; all short branches in range.");
    }
}
