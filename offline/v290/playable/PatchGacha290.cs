using System;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
public static partial class PatchPlayable290 {
 static void GachaTabs290() {
  GuardWarehouseRequest("ForgeryGachaPlay","ValidateScratchCapacity290");
  BindBridge("GachaLinks290","MainMenuSeqGachaRootNew",(il,t)=>{il.Emit(OpCodes.Ldarg_0);il.Emit(OpCodes.Ldfld,t.Fields.Single(f=>f.Name=="m_LinkerObject"));il.Emit(OpCodes.Ret);});
  BindBridge("GachaWindow290","MainMenuSeqGachaRootNew",(il,t)=>{il.Emit(OpCodes.Ldarg_0);il.Emit(OpCodes.Ldfld,t.Fields.Single(f=>f.Name=="m_WindowNow"));il.Emit(OpCodes.Ret);});
  BindBridge("RefreshGacha290","MainMenuSeqGachaRootNew",(il,t)=>{il.Emit(OpCodes.Ldarg_0);il.Emit(OpCodes.Ldc_I4_1);il.Emit(OpCodes.Ldc_I4_0);il.Emit(OpCodes.Call,Method(t.Name,"PageSwitchSetting"));il.Emit(OpCodes.Ret);});
  var setup=Method("MainMenuSeqGachaRootNew","PageSwitchSetting");
  var tabs=setup.Body.Instructions.Single(i=>i.Operand is MethodReference && ((MethodReference)i.Operand).Name=="GetActiveGachaMaster");
  tabs.Operand=Helper("LocalGachaTabs290");
  ReadyPageInput("MainMenuSeqGachaRootNew","UpdateGachaMode290");
  var renderIL=Reset(Method("MainMenuSeqGachaRootNew","RenderGachaTexture"));
  renderIL.Emit(OpCodes.Ldarg_0);renderIL.Emit(OpCodes.Call,Helper("RenderLocalGacha290"));renderIL.Emit(OpCodes.Ret);
  // Native local UI supplies visuals synchronously. Do not queue dead CDN work.
  Reset(Method("GachaResourceMaster","ResourceLoadRequest")).Emit(OpCodes.Ret);
 }
}
