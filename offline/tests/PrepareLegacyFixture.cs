// Disposable fixtures; never packaged or presented as historical APKs.
using System;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
public static class PrepareLegacyFixture
{
    public static void Main(string[] args)
    {
        var module=ModuleDefinition.ReadModule(args[1]);
        if(args[0]=="notice")
        {
            var method=module.Types.Single(t=>t.Name=="MainMenuSeqSwitchPage").Methods.Single(m=>m.Name=="Update");int quiet=0,presents=0;
            foreach(var i in method.Body.Instructions.ToArray())
            {
                var call=i.Operand as MethodReference;if(call==null)continue;
                if(call.Name=="QuietHelperRewardNotice")
                {if(i.Previous.OpCode!=OpCodes.Ldarg_0)throw new Exception("Unexpected hook");i.Previous.OpCode=OpCodes.Nop;i.OpCode=OpCodes.Nop;i.Operand=null;quiet++;}
                if(call.Name=="NotificationPresents")
                {i.OpCode=OpCodes.Ldfld;i.Operand=module.Types.Single(t=>t.Name=="UserDataAdmin").Fields.Single(f=>f.Name=="m_StructPresentList");presents++;}
            }
            if(quiet!=1 || presents!=7)throw new Exception("Unexpected fixture scope");
            Console.WriteLine("FIXTURE: historical22r2 APK unavailable; disposable dispatcher restores pending-helper/raw-present behavior. Not an archived22r2 APK test.");
        }
        else
        {
            var type=module.Types.Single(t=>t.Name=="DivineGateOffline");
            foreach(string name in new[]{"RestoreBackupStages","InitializeAcquisitionTime","ApplyAccountRules"})
            {
                var method=type.Methods.Single(m=>m.Name==name);method.Body=new MethodBody(method);var il=method.Body.GetILProcessor();
                if(name=="ApplyAccountRules")il.Emit(OpCodes.Ldc_I4_0);il.Emit(OpCodes.Ret);
            }
            Console.WriteLine("FIXTURE: historical prototype20 APK unavailable; seed uses original embedded stage tables, no account floors and no acquisition timestamps. Not an archived prototype20 APK test.");
        }
        module.Write(args[2]);
    }
}
