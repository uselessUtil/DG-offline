using System;
using System.Linq;
using Mono.Cecil;

public static class VerifyReferences
{
    public static void Main(string[] args)
    {
        var resolver = new DefaultAssemblyResolver();
        resolver.AddSearchDirectory(args[1]);
        resolver.AddSearchDirectory(args[0]);
        var module = ModuleDefinition.ReadModule(args[1]+"/DivineGateOffline.dll", new ReaderParameters { AssemblyResolver=resolver });
        int count = 0;
        foreach (var member in module.GetMemberReferences())
        {
            if (member is MethodReference && ((MethodReference)member).Resolve() == null) throw new Exception("Missing method " + member);
            if (member is FieldReference && ((FieldReference)member).Resolve() == null) throw new Exception("Missing field " + member);
            count++;
        }
        foreach (var reference in module.AssemblyReferences)
            if (reference.Name == "mscorlib" && reference.Version.Major != 2) throw new Exception("Requires a newer BCL than the Unity player");
        Console.WriteLine("PASS adapter references resolve against archived Unity/BCL: " + count);
    }
}
