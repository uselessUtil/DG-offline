using System;
using System.Reflection;
using System.Runtime.Serialization;
using UnityEngine;

// Exercise the exact APK adapter with managed active/enabled state stand-ins.
// This reproduces the fade-in reactivation precondition, not native animations.
public static class VerifyCollabReturn
{
    static readonly Type runtime = typeof(DivineGateOffline);
    static void Set(string field, object value)
    {
        runtime.GetField(field, BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, value);
    }
    static T Empty<T>() { return (T)FormatterServices.GetUninitializedObject(typeof(T)); }
    static void Check(bool value, string message) { if (!value) throw new Exception(message); }

    public static void Main()
    {
        var page = Empty<MainMenuSeqGachaRoot>();
        var other = Empty<MainMenuSeqGachaRoot>();
        var cards = new[] { Empty<GameObject>(), Empty<GameObject>(), Empty<GameObject>(), Empty<GameObject>() };
        var back = Empty<BoxCollider>();
        var originalColliders = new[] { Empty<BoxCollider>(), Empty<BoxCollider>(), Empty<BoxCollider>() };
        Set("collabOwner", page); Set("collabCards", cards); Set("collabBackCollider", back);
        for (int i = 0; i < 3; i++) { cards[i].SetActive(true); originalColliders[i].enabled = true; }
        var close = runtime.GetMethod("SetCollabMode", BindingFlags.Static | BindingFlags.NonPublic);
        for (int cycle = 0; cycle < 100; cycle++)
        {
            // Selecting must retain the fourth card and selector Back input.
            Set("collabSelecting", true); cards[3].SetActive(true); back.enabled = true;
            DivineGateOffline.MaintainCollabRoot(page);
            Check(cards[3].activeSelf && back.enabled && DivineGateOffline.CollabSelectorOpen(page), "Open selector hidden");

            // Pool selection/cancel has already closed the selector. Returning
            // after a draw or leaving another page then recursively enables it.
            Set("collabSelecting", false); cards[3].SetActive(false); back.enabled = false;
            cards[3].SetActive(true); back.enabled = true;
            DivineGateOffline.MaintainCollabRoot(other);
            Check(cards[3].activeSelf && back.enabled, "Unrelated page changed selector controls");
            DivineGateOffline.MaintainCollabRoot(page);
            Check(!cards[3].activeSelf && !back.enabled && !DivineGateOffline.CollabSelectorOpen(page), "Ghost card/collider after return");

            // Repeated SetCollabMode(false) must still repair reactivation,
            // without restoring old snapshots over refreshed normal balances.
            cards[3].SetActive(true); back.enabled = true;
            close.Invoke(null, new object[] { false });
            DivineGateOffline.MaintainCollabRoot(page);
            Check(!cards[3].activeSelf && !back.enabled, "Idempotent close skipped cleanup");
            for (int i = 0; i < 3; i++) Check(cards[i].activeSelf && originalColliders[i].enabled, "Normal card disabled");
        }
        Set("collabCards", null); Set("collabBackCollider", null);
        DivineGateOffline.MaintainCollabRoot(page);
        Console.WriteLine("PASS collab return state: 100 simulated selector-open/closed/fade-reactivation cycles; fourth card and selector Back disabled on normal page, repeated close repairs activation, original three cards/colliders untouched, unrelated page isolated. Exact APK adapter; native animation/touch still requires Android testing.");
    }
}
