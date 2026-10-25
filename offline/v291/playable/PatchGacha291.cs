using System;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
public static partial class PatchPlayable291 {
 static void GachaTabs291() {
  GuardWarehouseRequest("ForgeryGachaPlay","ValidateScratchCapacity291");
  BindBridge("GachaLinks291","MainMenuSeqGachaRootNew",(il,t)=>{il.Emit(OpCodes.Ldarg_0);il.Emit(OpCodes.Ldfld,t.Fields.Single(f=>f.Name=="m_LinkerObject"));il.Emit(OpCodes.Ret);});
  BindBridge("GachaWindow291","MainMenuSeqGachaRootNew",(il,t)=>{il.Emit(OpCodes.Ldarg_0);il.Emit(OpCodes.Ldfld,t.Fields.Single(f=>f.Name=="m_WindowNow"));il.Emit(OpCodes.Ret);});
  BindBridge("RefreshGacha291","MainMenuSeqGachaRootNew",(il,t)=>{il.Emit(OpCodes.Ldarg_0);il.Emit(OpCodes.Ldc_I4_1);il.Emit(OpCodes.Ldc_I4_0);il.Emit(OpCodes.Call,Method(t.Name,"PageSwitchSetting"));il.Emit(OpCodes.Ret);});
  var setup=Method("MainMenuSeqGachaRootNew","PageSwitchSetting");
  var tabs=setup.Body.Instructions.Single(i=>i.Operand is MethodReference && ((MethodReference)i.Operand).Name=="GetActiveGachaMaster");
  tabs.Operand=Helper("LocalGachaTabs291");
  ReadyPageInput("MainMenuSeqGachaRootNew","UpdateGachaMode291");
  var renderIL=Reset(Method("MainMenuSeqGachaRootNew","RenderGachaTexture"));
  renderIL.Emit(OpCodes.Ldarg_0);renderIL.Emit(OpCodes.Call,Helper("RenderLocalGacha291"));renderIL.Emit(OpCodes.Ret);
  // Native local UI supplies visuals synchronously. Do not queue dead CDN work.
  Reset(Method("GachaResourceMaster","ResourceLoadRequest")).Emit(OpCodes.Ret);
 }
}
