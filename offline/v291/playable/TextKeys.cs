using System;using System.Linq;using Mono.Cecil;using Mono.Cecil.Cil;
class Audit{
static void Main(string[] a){
 var m=ModuleDefinition.ReadModule(a[0]);
 foreach(var t in m.Types)foreach(var f in t.Methods.Where(x=>x.HasBody)){
 var ins=f.Body.Instructions;
 for(int i=0;i<ins.Count;i++)if(ins[i].Operand is MethodReference){
 var r=(MethodReference)ins[i].Operand;
 if(!((r.DeclaringType.Name=="GameTextUtil"||r.DeclaringType.Name=="GameTextManager")&&(r.Name=="GetText"||r.Name=="GetTextTry")) && r.DeclaringType.Name!="DialogManager")continue;
 for(int j=Math.Max(0,i-12);j<i;j++)if(ins[j].OpCode==OpCodes.Ldstr)Console.WriteLine(f.FullName+"\t"+r.Name+"\t"+ins[j].Operand);
 }
 }
}}
