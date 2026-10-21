// Editor-only build entry point for the independent, recovered ARM64 project.
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;

public static class BuildArm64
{
    public static void Build()
    {
        string output = Environment.GetEnvironmentVariable("DG_ARM64_APK");
        if (string.IsNullOrEmpty(output) || !Path.IsPathRooted(output) || File.Exists(output))
            throw new InvalidOperationException("Specify a new absolute DG_ARM64_APK output path.");
        string[] scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
        if (scenes.Length != 6 || scenes.Any(s => !File.Exists(s)))
            throw new InvalidOperationException("Recovered six-scene build list is incomplete.");
        PlayerSettings.companyName = "DivineGateOffline";
        PlayerSettings.productName = "Divine Gate ARM64 Port (Experimental)";
        PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, "jp.gungho.dg.offline106.arm64");
        PlayerSettings.bundleVersion = "1.0.6";
        PlayerSettings.Android.bundleVersionCode = 10001;
        PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel21;
        PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevel29;
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
        PlayerSettings.SetApiCompatibilityLevel(BuildTargetGroup.Android, ApiCompatibilityLevel.NET_4_6);
        string key = Environment.GetEnvironmentVariable("DG_ARM64_KEYSTORE");
        if (string.IsNullOrEmpty(key) || !File.Exists(key))
            throw new InvalidOperationException("Signing keystore is unavailable.");
        PlayerSettings.Android.useCustomKeystore = true;
        PlayerSettings.Android.keystoreName = key;
        PlayerSettings.Android.keystorePass = Environment.GetEnvironmentVariable("DG_ARM64_KEYSTORE_PASS");
        PlayerSettings.Android.keyaliasName = Environment.GetEnvironmentVariable("DG_ARM64_KEY_ALIAS");
        PlayerSettings.Android.keyaliasPass = Environment.GetEnvironmentVariable("DG_ARM64_KEYSTORE_PASS");
        Directory.CreateDirectory(Path.GetDirectoryName(output));
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
            scenes = scenes, target = BuildTarget.Android,
            locationPathName = output, options = BuildOptions.Development
        });
        if (report.summary.result != BuildResult.Succeeded)
            throw new InvalidOperationException("ARM64 player build failed: " + report.summary.result);
    }
}
