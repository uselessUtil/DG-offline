using System;
using System.IO;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

// Embed PNGs as RVA data rather than resources: Unity's stripped mscorlib does
// not retain Assembly.GetManifestResourceStream. Each PNG is allocated lazily.
public static class EmbedImages
{
    public static void Main(string[] args)
    {
        var resolver = new DefaultAssemblyResolver();
        resolver.AddSearchDirectory(Path.GetDirectoryName(args[1]));
        var module = ModuleDefinition.ReadModule(args[0], new ReaderParameters { AssemblyResolver = resolver });
        var bcl = ModuleDefinition.ReadModule(args[1]);
        var valueType = module.ImportReference(bcl.Types.Single(t => t.FullName == "System.ValueType"));
        var init = module.ImportReference(bcl.Types.Single(t => t.FullName == "System.Runtime.CompilerServices.RuntimeHelpers")
            .Methods.Single(m => m.Name == "InitializeArray" && m.Parameters.Count == 2 &&
                m.Parameters[1].ParameterType.FullName == "System.RuntimeFieldHandle"));
        // Optional owner lets old Unity builds use the same RVA transport for
        // master tables too, without unavailable manifest-resource APIs.
        var owner = module.Types.Single(t => t.Name == (args.Length > 5 ? args[5] : "RecoveredImages"));
        var method = owner.Methods.Single(m => m.Name == "Payload");
        method.Body = new MethodBody(method);
        var il = method.Body.GetILProcessor();
        var rows = File.ReadAllLines(args[3]).Select(line => line.Split('\t').Select(int.Parse).ToArray()).ToArray();
        var targets = rows.Select(row => Instruction.Create(OpCodes.Ldc_I4, row[2])).ToArray();
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Switch, targets);
        il.Emit(OpCodes.Ldnull);
        il.Emit(OpCodes.Ret);
        using (var input = File.OpenRead(args[2]))
        {
            for (int i = 0; i < rows.Length; i++)
            {
                var row = rows[i];
                if (row[0] != i) throw new Exception("Unexpected image index");
                var raw = new byte[row[2]];
                input.Position = row[1];
                int offset = 0;
                while (offset < raw.Length)
                {
                    int count = input.Read(raw, offset, raw.Length - offset);
                    if (count == 0) throw new EndOfStreamException();
                    offset += count;
                }
                var sizeType = new TypeDefinition("", "ImageBytes" + i,
                    TypeAttributes.NestedPrivate | TypeAttributes.Sealed | TypeAttributes.ExplicitLayout,
                    valueType) { ClassSize = raw.Length, PackingSize = 1 };
                owner.NestedTypes.Add(sizeType);
                var field = new FieldDefinition("image" + i,
                    FieldAttributes.Private | FieldAttributes.Static | FieldAttributes.InitOnly | FieldAttributes.HasFieldRVA,
                    sizeType) { InitialValue = raw };
                owner.Fields.Add(field);
                il.Append(targets[i]);
                il.Emit(OpCodes.Newarr, module.TypeSystem.Byte);
                il.Emit(OpCodes.Dup);
                il.Emit(OpCodes.Ldtoken, field);
                il.Emit(OpCodes.Call, init);
                il.Emit(OpCodes.Ret);
            }
        }
        module.Write(args[4]);
        Console.WriteLine("Embedded lazy PNG arrays: " + rows.Length);
    }
}
