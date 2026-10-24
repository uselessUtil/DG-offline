// Container-only regression for the actual 4.0.0 String.Format call contract.
// Does not load Unity or exercise Android UI.
using System;
using System.Linq;
using System.Text;
using Mono.Cecil;
using Mono.Cecil.Cil;

class PartyFormatProbe
{
    static void Main(string[] args)
    {
        var module = ModuleDefinition.ReadModule(args[0]);
        int calls = 0;
        foreach (var typeName in new[] { "MainMenuSeqPartyForm2", "MainMenuSeqQuestSelectFriend" })
        {
            var type = module.Types.Single(t => t.Name == typeName);
            int typeCalls = 0;
            foreach (var method in type.Methods.Where(m => m.HasBody))
            {
                var instructions = method.Body.Instructions;
                for (int i = 0; i < instructions.Count; i++)
                {
                    if (instructions[i].OpCode != OpCodes.Ldstr ||
                        (string)instructions[i].Operand != "PARTY_NUM_RATE") continue;
                    var getText = instructions[i + 1].Operand as MethodReference;
                    if (getText == null || getText.Name != "GetText")
                        throw new Exception("Party title text lookup changed");
                    var format = instructions.Skip(i + 2).Take(24)
                        .Select(ins => ins.Operand as MethodReference)
                        .FirstOrDefault(m => m != null && m.DeclaringType.FullName == "System.String" && m.Name == "Format");
                    if (format == null || format.Parameters.Count != 2 ||
                        format.Parameters[1].ParameterType.FullName != "System.Object")
                        throw new Exception("Party title no longer has the one-argument format contract");
                    typeCalls++;
                }
            }
            if (typeCalls != 4) throw new Exception("Expected four ordinal branches in " + typeName);
            calls += typeCalls;
        }
        string oldText = Encoding.UTF8.GetString(Convert.FromBase64String(args[1]));
        string newText = Encoding.UTF8.GetString(Convert.FromBase64String(args[2]));
        bool reproduced = false;
        try { string.Format(oldText, "1st"); }
        catch (FormatException) { reproduced = true; }
        if (!reproduced) throw new Exception("Expected the reported resident format failure");
        foreach (var ordinal in new[] { "1st", "2nd", "3rd", "4th", "5th", "10th" })
        {
            string rendered = string.Format(newText, ordinal);
            if (!rendered.Contains(ordinal) || rendered.Contains("{") || rendered.Contains("?"))
                throw new Exception("Invalid party title: " + rendered);
        }
        Console.WriteLine("PASS: resident FormatException reproduced; corrected ordinal titles formatted; " + calls + " actual DLL call sites checked");
    }
}
