using System;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

public static class Inspect290
{
    public static void Main(string[] args)
    {
        var module = ModuleDefinition.ReadModule(args[0]);
        var server = module.Types.Single(t => t.Name == "ServerForgery");
        var start = server.Methods.Single(m => m.Name == "Start");
        foreach (var instruction in start.Body.Instructions)
            if (instruction.OpCode == OpCodes.Ldstr && ((string)instruction.Operand).StartsWith("CardDM/MasterData/"))
                Console.WriteLine("resource\t" + instruction.Operand);
        foreach (var field in server.Fields.Where(f => f.Name.StartsWith("m_MasterData")))
        {
            var array = field.FieldType as ArrayType;
            if (array == null) throw new Exception("Expected array: " + field);
            var type = module.Types.Single(t => t.FullName == array.ElementType.FullName);
            Console.WriteLine("table\t" + field.Name + "\t" + type.FullName);
            foreach (var column in type.Fields.Where(f => !f.IsStatic))
                Console.WriteLine("column\t" + type.FullName + "\t" + column.Name + "\t" + column.FieldType.FullName);
        }
        foreach (var method in server.Methods.Where(m => m.HasBody && m.Name.StartsWith("CreateRecv")))
            Console.WriteLine("handler\t" + method.FullName + "\t" + method.Body.Instructions.Count);
    }
}
