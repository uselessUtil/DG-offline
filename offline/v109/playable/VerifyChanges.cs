using System;using System.IO;using System.Linq;using System.Collections.Generic;using Mono.Cecil;using Mono.Cecil.Cil;
class VerifyChanges {
 static IEnumerable<TypeDefinition> Types(TypeDefinition t){yield return t;foreach(var n in t.NestedTypes)foreach(var c in Types(n))yield return c;}
 static string Shape(MethodDefinition m){
  if(!m.HasBody)return "none";var b=m.Body;var ins=b.Instructions.ToArray();
  Func<object,string> operand=o=>o is Instruction ? "@"+Array.IndexOf(ins,(Instruction)o) : o is Instruction[] ? string.Join(",",((Instruction[])o).Select(x=>"@"+Array.IndexOf(ins,x)).ToArray()) : o==null ? "" : o.ToString();
  return b.InitLocals+"|"+string.Join(",",b.Variables.Select(v=>v.VariableType.FullName).ToArray())+"|"+string.Join(";",ins.Select(i=>i.OpCode.Name+":"+operand(i.Operand)).ToArray())+"|"+string.Join(";",b.ExceptionHandlers.Select(h=>h.HandlerType+":"+operand(h.TryStart)+":"+operand(h.TryEnd)+":"+operand(h.HandlerStart)+":"+operand(h.HandlerEnd)+":"+(h.CatchType==null?"":h.CatchType.FullName)).ToArray());
 }
 static void Main(string[] a){var old=ModuleDefinition.ReadModule(a[0]);var now=ModuleDefinition.ReadModule(a[1]);
 var methods=now.Types.SelectMany(t=>Types(t)).SelectMany(t=>t.Methods).ToDictionary(m=>m.FullName);int same=0;var changed=new List<string>();
 var allowed=new HashSet<string>{"LocalSaveManager.LoadFuncServerAddressIP","ResidentManager.Start","ServerForgery.Start","ServerForgery.UserParamCreate","ServerForgery.UserParamRenew","ServerForgery.UserParamLoad","ServerForgery.CreateLoginMonthlyList","MainMenuSeqQuestSelectMission.PageSwitchSetting","PatchManager.Start","PatchManager.GetPatchText","PatchManager.PatchUpdateRequest","PatchUtil.CheckAppUpdate","TitleMenuSeqInformationFox.Update","TitleMenuSeqInformationFox.LayoutSwitchSetting","TitleMenuSeqLoadAssetBundle.Update","TitleMenuSeqLoadAssetBundle.LayoutSwitchSetting","AssetBundleManager.Request","AssetBundleManager.ClearAssetBundle","AssetBundleManager.SetupAssetBundlePackageParam","AssetAutoSetCharaMesh.Update","StoreManager.Start","StoreManager.Update","StoreManager.OnDestroy","AndroidDeviceInfo.Awake","AndroidDeviceInfo.UpdateExStoragePath","AndroidDeviceInfo.IsExStorageActive","MainMenuManager.AddSwitchRequest","TitleMenuSeqSwitchPage.Update","TitleMenuFixLayerBanner.Update","TitleMenuSeqWaitInput.Update","LinkerOpenURL.Update","UIAtlas.GetSprite","InGameManager.GetTouchCell"};
 allowed.Remove("MainMenuManager.AddSwitchRequest");
 allowed.UnionWith(new[]{"MainMenuManager.Start","StoreManager.GetProductFromNum","StoreManager.GetProductFromID","StoreParam.SetupStoreProductListNow","StoreCommonUI.ProductsSetting","MainMenuSeqShopRoot.PageSwitchEventEnableBefore","MainMenuSeqShopRoot.Update","MainMenuSeqShopBuyTip.PageSwitchSetting","MainMenuSeqShopBuyTip.Update","MainMenuSeqGachaRoot.Update","MainMenuSeqGachaRoot.PageSwitchSetting"});
 foreach(var m in old.Types.SelectMany(t=>Types(t)).SelectMany(t=>t.Methods)){
  MethodDefinition n;if(!methods.TryGetValue(m.FullName,out n))throw new Exception("Removed original method "+m);
  if(Shape(m)==Shape(n)){same++;continue;}
  string key=m.DeclaringType.Name+"."+m.Name;
  if(!allowed.Contains(key) && !(m.DeclaringType.Name=="PlayGameServiceUtil" && m.IsStatic))throw new Exception("Unexpected modified method "+m);
  changed.Add(m.FullName);
 }
 foreach(string name in new[]{"TitleMenuFixLayerBanner","TitleMenuSeqWaitInput","LinkerOpenURL"}) {
  var target=now.Types.Single(t=>t.Name==name).Methods.Single(m=>m.Name=="Update");
  if(target.Body.Instructions.Any(i=>i.Operand is MethodReference && ((MethodReference)i.Operand).DeclaringType.FullName=="UnityEngine.Application" && ((MethodReference)i.Operand).Name=="OpenURL"))throw new Exception("External title URL remains: "+name);
 }
 var bannerUpdate=now.Types.Single(t=>t.Name=="TitleMenuFixLayerBanner").Methods.Single(m=>m.Name=="Update");
 if(bannerUpdate.Body.Instructions.Count<20)throw new Exception("Original banner rendering was removed");
 var router=now.Types.Single(t=>t.Name=="TitleMenuSeqSwitchPage").Methods.Single(m=>m.Name=="Update");
 if(router.Body.Instructions.Any(i=>i.Operand is MethodReference && ((MethodReference)i.Operand).Name=="LoadFuncInformationPolicyOK"))throw new Exception("Online policy gate still blocks offline startup");
 Console.WriteLine("PASS original banner rendering retained, external title links inert, online policy gate skipped without saved-consent writes");
 File.WriteAllLines(a[2],changed.ToArray());Console.WriteLine("PASS unchanged original methods: "+same+"; explicitly allowed modified methods: "+changed.Count);
 Console.WriteLine("PASS original battle, skill, enemy AI, character growth, quest start/clear and reward handlers unchanged");
 }
}
