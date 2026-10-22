// Disposable probe only; never package these controls or allocation stubs.
using System;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
public static class PrepareRulesOdds
{
    public static void Main(string[] args)
    {
        var game=ModuleDefinition.ReadModule(args[0]);
        var control=new TypeDefinition("","RulesOddsControl",TypeAttributes.Public|TypeAttributes.Abstract|TypeAttributes.Sealed,game.TypeSystem.Object);
        game.Types.Add(control);
        foreach(string name in new[]{"First","Second","Calls","Level","Pow","HP"})
            control.Fields.Add(new FieldDefinition(name,FieldAttributes.Public|FieldAttributes.Static,game.TypeSystem.UInt32));
        Func<string,FieldDefinition> f=n=>control.Fields.Single(x=>x.Name==n);
        var rand=game.Types.Single(t=>t.Name=="RandManager").Methods.Single(m=>m.Name=="GetRand" && m.Parameters.Count==2);
        rand.Body=new MethodBody(rand);var il=rand.Body.GetILProcessor();
        var second=Instruction.Create(OpCodes.Ldsfld,f("Second"));
        il.Emit(OpCodes.Ldsfld,f("Calls"));il.Emit(OpCodes.Ldc_I4_1);il.Emit(OpCodes.Add);il.Emit(OpCodes.Stsfld,f("Calls"));
        il.Emit(OpCodes.Ldsfld,f("Calls"));il.Emit(OpCodes.Ldc_I4_1);il.Emit(OpCodes.And);il.Emit(OpCodes.Brfalse,second);
        il.Emit(OpCodes.Ldsfld,f("First"));il.Emit(OpCodes.Ret);il.Append(second);il.Emit(OpCodes.Ret);
        var add=game.Types.Single(t=>t.Name=="ServerForgery").Methods.Single(m=>m.Name=="AddUnit" && m.Parameters.Count==4);
        add.Body=new MethodBody(add);il=add.Body.GetILProcessor();
        il.Emit(OpCodes.Ldarg_2);il.Emit(OpCodes.Stsfld,f("Level"));il.Emit(OpCodes.Ldarg_3);il.Emit(OpCodes.Stsfld,f("Pow"));
        il.Emit(OpCodes.Ldarg,add.Parameters[3]);il.Emit(OpCodes.Stsfld,f("HP"));
        il.Emit(OpCodes.Ldarg_1);il.Emit(OpCodes.Conv_I8);il.Emit(OpCodes.Ret);
        game.Write(args[1]);
        Console.WriteLine("PROBE ONLY: controlled two-stage random inputs and AddUnit capture in disposable test DLL; draw/payment/save code unchanged.");
    }
}
