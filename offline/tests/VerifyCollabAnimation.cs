using System;
using System.Reflection;
using System.Runtime.Serialization;
using UnityEngine;

// Execute the APK adapter AND original UIButtonScale/TweenScale methods.
// Only engine component lookup, messages, transforms and enabled flags are mocked.
public static class VerifyCollabAnimation
{
    static T Empty<T>() { return (T)FormatterServices.GetUninitializedObject(typeof(T)); }
    static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    static void Scale(Vector3 value, float expected, string message)
    {
        Check(Math.Abs(value.x-expected) < .0001f && Math.Abs(value.y-expected) < .0001f &&
            Math.Abs(value.z-1) < .0001f, message);
    }
    public static void Main()
    {
        var reset = typeof(DivineGateOffline).GetMethod("ResetCollabCardAnimation", BindingFlags.Static | BindingFlags.NonPublic);
        var hover = typeof(UIButtonScale).GetMethod("OnHover", BindingFlags.Instance | BindingFlags.NonPublic);
        var press = typeof(UIButtonScale).GetMethod("OnPress", BindingFlags.Instance | BindingFlags.NonPublic);
        var highlighted = typeof(UIButtonScale).GetField("mHighlighted", BindingFlags.Instance | BindingFlags.NonPublic);
        for (int cardIndex = 0; cardIndex < 4; cardIndex++)
        {
            var go = EngineSupport.CreateScaleTestObject();
            var button = Empty<UIButtonScale>();
            button.hover = new Vector3(1.1f,1.1f,1); button.pressed = new Vector3(1.05f,1.05f,1); button.duration = .2f;
            var tween = Empty<TweenScale>();
            EngineSupport.RegisterComponent(go, button); EngineSupport.RegisterComponent(go, tween);
            button.enabled = true;
            for (int cycle = 0; cycle < 25; cycle++)
            {
                hover.Invoke(button, new object[] { true });
                Scale(tween.to,1.1f,"Original hover no longer works");
                go.transform.localScale = new Vector3(1.08f,1.08f,1); // A snapshot mid-hover would be wrong.
                reset.Invoke(null,new object[] { go });
                Scale(go.transform.localScale,1,"Snapshot captures transient hover scale");
                Check(!(bool)highlighted.GetValue(button) && !tween.enabled && button.enabled,"Highlight/tween not cleared or button disabled");
                Scale(tween.from,1,"Stale tween start"); Scale(tween.to,1,"Stale tween target");

                // Native press still works after reopening; return mid-press.
                press.Invoke(button,new object[] { true });
                Scale(tween.to,1.05f,"Original press no longer works");
                Check(tween.enabled,"Press animation disabled permanently");
                go.transform.localScale = new Vector3(1.04f,1.04f,1);
                reset.Invoke(null,new object[] { go });
                Scale(go.transform.localScale,1,"Return retains press scale");
                // Even recursive fade-in re-enabling this tween cannot restore
                // the previous enlarged target: both endpoints are neutral.
                tween.enabled = true;
                Scale(tween.from,1,"Fade activation revives old tween start");
                Scale(tween.to,1,"Fade activation revives old tween target");
                reset.Invoke(null,new object[] { go });
            }
        }
        reset.Invoke(null,new object[] { EngineSupport.CreateScaleTestObject() });
        Console.WriteLine("PASS collab button animations: 100 cycles across four buttons using original UIButtonScale hover/press and TweenScale.Begin; reset clears highlight, stops scale tween, normalizes snapshot/return size, freezes endpoints against fade reactivation, and preserves subsequent hover/press. Engine state mocked; Android rendering/touch pending.");
    }
}
