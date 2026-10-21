using System;
using System.Reflection;
using System.Runtime.Serialization;
using UnityEngine;

public static class VerifyHudPopup
{
    static MethodInfo policy;
    static int checks;
    static void Check(bool expected, string label)
    {
        bool actual = (bool)policy.Invoke(null, null);
        if (actual != expected) throw new Exception("Wrong enemy HUD popup mode: " + label);
        checks++;
    }
    public static void Main(string[] args)
    {
        policy = Assembly.LoadFrom(args[0]).GetType("DivineGateOffline",true)
            .GetMethod("EnemyHudBehindPopup",BindingFlags.NonPublic|BindingFlags.Static);
        var scene = new SceneObjReferGameMain();
        Singleton<SceneObjReferGameMain>.Instance = scene;
        var menu = new InGameMenuManager();
        Singleton<InGameMenuManager>.Instance = menu;
        var dialog = new DialogManager();
        Singleton<DialogManager>.Instance = dialog;
        Check(false,"normal battle");
        scene.m_UIInstanceMenu = (GameObject)FormatterServices.GetUninitializedObject(typeof(GameObject));
        EngineSupport.SetActiveState(scene.m_UIInstanceMenu,true);
        Check(true,"opening frame before menu state advances");
        EngineSupport.SetActiveState(scene.m_UIInstanceMenu,false);
        var current = typeof(InGameMenuManager).GetField("m_CurrentMenu",BindingFlags.NonPublic|BindingFlags.Instance);
        foreach (int state in new int[] {2,3,4,1})
        {
            current.SetValue(menu,state);
            Check(true,"quest info/options/retire/close transition " + state);
        }
        current.SetValue(menu,0);
        Check(false,"return from menu to battle");
        dialog.m_DialogLayout = new LayoutSwitchManager();
        var layout = typeof(LayoutSwitchManager).GetField("m_WorkSwitchID",BindingFlags.NonPublic|BindingFlags.Instance);
        for (int id=1;id<16;id++)
        {
            layout.SetValue(dialog.m_DialogLayout,id);
            Check(true,"dialog " + id);
        }
        layout.SetValue(dialog.m_DialogLayout,0);
        Check(false,"return from dialog to battle");
        Console.WriteLine("PASS actual adapter popup policy: " + checks +
            " battle/menu/opening-frame/transition/dialog states; original NGUI HUD mode requested while popup is active");
    }
}
