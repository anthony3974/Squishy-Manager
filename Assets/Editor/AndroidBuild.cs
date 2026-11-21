using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Tools > Android > Build APK, or from a shell with Unity closed: Tools/android-build.sh
/// </summary>
public static class AndroidBuild
{
    public const string PackageId = "com.Tony3974.DeepSeaStation";
    public const string ApkPath = "Builds/Android/DeepSeaStation.apk";
    const string ScenePath = "Assets/Scenes/SampleScene.unity";

    [MenuItem("Tools/Android/Apply Android Settings")]
    public static void ApplySettings()
    {
        var android = NamedBuildTarget.Android;
        PlayerSettings.productName = "Deep-Sea Station";
        PlayerSettings.companyName = "Tony3974";
        PlayerSettings.SetApplicationIdentifier(android, PackageId);
        PlayerSettings.SetScriptingBackend(android, ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
        PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
        PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
        PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.Vulkan, GraphicsDeviceType.OpenGLES3 });

        // The game is portrait only.
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
        PlayerSettings.allowedAutorotateToPortrait = true;
        PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
        PlayerSettings.allowedAutorotateToLandscapeLeft = false;
        PlayerSettings.allowedAutorotateToLandscapeRight = false;
        PlayerSettings.Android.startInFullscreen = true;

        if (string.IsNullOrEmpty(PlayerSettings.bundleVersion) || PlayerSettings.bundleVersion == "0.1" || PlayerSettings.bundleVersion == "1.0")
            PlayerSettings.bundleVersion = "0.1.0";
        if (PlayerSettings.Android.bundleVersionCode < 1) PlayerSettings.Android.bundleVersionCode = 1;
        EditorUserBuildSettings.buildAppBundle = false;
        AssetDatabase.SaveAssets();
    }

    [MenuItem("Tools/Android/Build APK")]
    static void MenuBuild() => Build(ApkPath);

    public static bool Build(string path, bool development = false)
    {
        ApplySettings();
        PlayerSettings.SetIl2CppStacktraceInformation(NamedBuildTarget.Android,
            development ? Il2CppStacktraceInformation.MethodFileLineNumber : Il2CppStacktraceInformation.MethodOnly);
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");

        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { ScenePath },
            locationPathName = path,
            target = BuildTarget.Android,
            targetGroup = BuildTargetGroup.Android,
            options = development ? BuildOptions.Development : BuildOptions.None,
        });

        var s = report.summary;
        if (s.result == BuildResult.Succeeded)
        {
            long bytes = File.Exists(path) ? new FileInfo(path).Length : 0;
            Debug.Log($"Deep-Sea Station: built {path} ({bytes / (1024f * 1024f):0.0} MB, {s.totalTime.TotalSeconds:0} s, " +
                      $"version {PlayerSettings.bundleVersion} code {PlayerSettings.Android.bundleVersionCode}).");
            return true;
        }
        Debug.LogError($"Deep-Sea Station: Android build {s.result} with {s.totalErrors} errors.");
        return false;
    }

    /// <summary>Batch mode entry. Switches the build target back afterwards so the editor still opens on PC.</summary>
    public static void CommandLine()
    {
        var args = Environment.GetCommandLineArgs();
        int outIndex = Array.IndexOf(args, "-stationOut");
        string path = outIndex >= 0 && outIndex + 1 < args.Length ? args[outIndex + 1] : ApkPath;

        var previous = EditorUserBuildSettings.activeBuildTarget;
        var previousGroup = BuildPipeline.GetBuildTargetGroup(previous);
        bool ok;
        try { ok = Build(path, args.Contains("-stationDev")); }
        catch (Exception e) { Debug.LogException(e); ok = false; }

        if (previous != BuildTarget.Android && EditorUserBuildSettings.activeBuildTarget != previous)
            EditorUserBuildSettings.SwitchActiveBuildTarget(previousGroup, previous);
        EditorApplication.Exit(ok ? 0 : 1);
    }
}
