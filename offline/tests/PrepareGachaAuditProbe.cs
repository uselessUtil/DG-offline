// Prepare a disposable test copy only. The packaged APK is never rewritten.
using System;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

public static class PrepareGachaAuditProbe
{
    static string Body(MethodDefinition method)
    { return string.Join("|",method.Body.Instructions.Select(i=>i.ToString()).ToArray()); }
    static MethodDefinition Find(ModuleDefinition m,string type,string method,int parameters)
    { return m.Types.Single(t=>t.Name==type).Methods.Single(x=>x.Name==method && x.Parameters.Count==parameters); }
    public static void Main(string[] args)
    {
        var source=ModuleDefinition.ReadModule(args[0]);
        var game=ModuleDefinition.ReadModule(args[1]);
        foreach(var item in new[]{"ServerForgery/ForgeryGachaPlay/2","Rand/GetRand/2","RandManager/GetRand/2"})
        {
            var p=item.Split('/');int count=int.Parse(p[2]);
            if(Body(Find(source,p[0],p[1],count))!=Body(Find(game,p[0],p[1],count)))
                throw new Exception("Packaged method differs from original: "+item);
            Console.WriteLine("PASS original packaged draw/range IL preserved: "+item);
        }
        var control=new TypeDefinition("","GachaAuditControl",TypeAttributes.Public|TypeAttributes.Abstract|TypeAttributes.Sealed,
                                       game.TypeSystem.Object);
        var ticket=new FieldDefinition("Ticket",FieldAttributes.Public|FieldAttributes.Static,game.TypeSystem.UInt32);
        control.Fields.Add(ticket);game.Types.Add(control);
        var random=Find(game,"RandManager","GetRand",2);
        random.Body=new MethodBody(random);
        var il=random.Body.GetILProcessor();il.Append(Instruction.Create(OpCodes.Ldsfld,ticket));il.Append(Instruction.Create(OpCodes.Ret));
        var add=Find(game,"ServerForgery","AddUnit",4);
        add.Body=new MethodBody(add);il=add.Body.GetILProcessor();
        il.Append(Instruction.Create(OpCodes.Ldarg_1));il.Append(Instruction.Create(OpCodes.Conv_I8));il.Append(Instruction.Create(OpCodes.Ret));
        var save=Find(game,"ServerForgery","UserParamSave",0);
        save.Body=new MethodBody(save);save.Body.GetILProcessor().Append(Instruction.Create(OpCodes.Ret));
        // ForgeryGachaPlay itself is left intact. Supply every possible ticket,
        // return the chosen character ID from AddUnit, and suppress test I/O.
        game.Write(args[2]);
        Console.WriteLine("PROBE ONLY: deterministic ticket input, AddUnit returns selected ID, account-save I/O disabled; draw method unchanged.");
    }
}
