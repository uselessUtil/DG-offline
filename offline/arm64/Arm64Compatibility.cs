using System;
using UnityEngine;

// This project targets Android only; removed WebPlayer platforms are unreachable.
public static class Arm64Compatibility
{
    public const RuntimePlatform WindowsWebPlayer = (RuntimePlatform)(-1001);
    public const RuntimePlatform OSXWebPlayer = (RuntimePlatform)(-1002);

    public static Component AddComponentByName(this GameObject target, string name)
    {
        Type type = typeof(ResidentManager).Assembly.GetType(name, false)
            ?? typeof(Component).Assembly.GetType("UnityEngine." + name, false);
        if (type == null || !typeof(Component).IsAssignableFrom(type))
            throw new ArgumentException("Unknown component: " + name);
        return target.AddComponent(type);
    }
}
