using System;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

// Change only storage of RVA image bytes, leaving all game rules untouched.
public static class RewriteEmbeddedData
{
    public static void Main(string[] args)
    {
        var module=ModuleDefinition.ReadModule(args[0]);
        var helper=ModuleDefinition.ReadModule(args[1]).Types.Single(t=>t.Name=="PortableEngine").Methods.Single(m=>m.Name=="InitializeImageArray");
        int count=0;
        foreach(var type in module.Types)foreach(var field in type.Fields.Where(f=>f.InitialValue.Length>0).ToArray()) {
            string resource="portable.rva."+type.FullName+"."+field.Name;
            int uses=0;
            foreach(var owner in module.Types)foreach(var method in owner.Methods.Where(m=>m.HasBody))foreach(var instruction in method.Body.Instructions) {
                var reference=instruction.Operand as FieldReference;
                if(reference==null || reference.FullName!=field.FullName)continue;
                var call=instruction.Next;
                if(instruction.OpCode!=OpCodes.Ldtoken || call.OpCode!=OpCodes.Call || !((MethodReference)call.Operand).FullName.Contains("RuntimeHelpers::InitializeArray"))throw new Exception("Unexpected RVA use "+instruction);
                instruction.OpCode=OpCodes.Ldstr;instruction.Operand=resource;
                call.Operand=module.ImportReference(helper);uses++;
            }
            if(uses==0)throw new Exception("Unreferenced RVA field "+field.FullName);
            module.Resources.Add(new EmbeddedResource(resource,ManifestResourceAttributes.Private,field.InitialValue));
            type.Fields.Remove(field);count++;
        }
        module.Write(args[2]);Console.WriteLine("Portable embedded arrays: "+count);
    }
}
