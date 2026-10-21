using System;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

// Read metadata only. Does not load or execute the Android application.
public static class InspectComparison
{
    public static void Main(string[] args)
    {
        var module = ModuleDefinition.ReadModule(args[0]);
        foreach (var type in module.Types)
        {
            if (type.Name.StartsWith("MasterData") || type.Name == "ServerForgerySaveUser" ||
                type.Namespace == "ServerDataDefine")
            {
                Console.WriteLine("type\t" + type.FullName);
                foreach (var field in type.Fields)
                {
                    if (!field.IsStatic)
                        Console.WriteLine("field\t" + type.FullName + "\t" + field.Name + "\t" + field.FieldType.FullName);
                    if (field.HasConstant)
                        Console.WriteLine("constant\t" + type.FullName + "\t" + field.Name + "\t" + field.Constant);
                }
            }
        }
        var server = module.Types.Single(t => t.Name == "ServerForgery");
        foreach (var field in server.Fields.Where(f => f.Name.StartsWith("m_MasterData")))
            Console.WriteLine("table\t" + field.Name + "\t" + ((ArrayType)field.FieldType).ElementType.FullName);
        foreach (var method in server.Methods.Where(m => m.HasBody))
        {
            foreach (var ins in method.Body.Instructions)
            {
                if (method.Name == "Start" && ins.OpCode == OpCodes.Ldstr &&
                    ((string)ins.Operand).StartsWith("CardDM/MasterData/"))
                    Console.WriteLine("resource\t" + ins.Operand);
                var called = ins.Operand as MethodReference;
                if (method.Name == "CreateRecvForgery" && called != null && called.DeclaringType.Name == "ServerForgery")
                    Console.WriteLine("dispatch\t" + called.Name);
            }
        }
    }
}
