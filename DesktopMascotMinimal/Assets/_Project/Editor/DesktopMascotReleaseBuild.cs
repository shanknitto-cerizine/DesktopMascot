#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;

public static class DesktopMascotReleaseBuild
{
    internal const string ProductionScenePath =
        "Assets/_Project/Scenes/MascotMain.unity";

    private const string ReleaseOutputEnvironmentVariable =
        "DESKTOP_MASCOT_RELEASE_OUTPUT";
    private const string ExpectedProductName =
        "\u3042\u306a\u305f\u3068\u3044\u3064\u3082";
    private const string ExpectedCompanyName = "Ceritizine_poc";
    private const string ExpectedVersion = "0.1.0";

    [MenuItem("Tools/Desktop Mascot/Build Windows x64 Release")]
    public static void BuildWindowsX64Release()
    {
        var outputPath = Environment.GetEnvironmentVariable(
            ReleaseOutputEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            throw new InvalidOperationException(
                ReleaseOutputEnvironmentVariable + " is required.");
        }

        outputPath = Path.GetFullPath(outputPath);
        if (!Path.IsPathRooted(outputPath)
            || !string.Equals(Path.GetExtension(outputPath), ".exe",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Release output must be an absolute .exe path.");
        }
        if (File.Exists(outputPath))
        {
            throw new InvalidOperationException(
                "Release output already exists; refusing to overwrite it.");
        }

        ValidateProjectSettings();
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath));

        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { ProductionScenePath },
            locationPathName = outputPath,
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None
        });
        if (report.summary.result != BuildResult.Succeeded)
        {
            throw new InvalidOperationException(
                "Windows x64 Release Player build failed: " +
                report.summary.result);
        }

        var receipt = new ReleaseBuildReceipt
        {
            unityVersion = Application.unityVersion,
            buildTarget = BuildTarget.StandaloneWindows64.ToString(),
            buildOptions = BuildOptions.None.ToString(),
            productName = PlayerSettings.productName,
            companyName = PlayerSettings.companyName,
            bundleVersion = PlayerSettings.bundleVersion,
            productionScene = ProductionScenePath
        };
        File.WriteAllText(
            Path.Combine(Path.GetDirectoryName(outputPath),
                "release-build-receipt.json"),
            JsonUtility.ToJson(receipt, true));
    }

    private static void ValidateProjectSettings()
    {
        if (PlayerSettings.productName != ExpectedProductName
            || PlayerSettings.companyName != ExpectedCompanyName
            || PlayerSettings.bundleVersion != ExpectedVersion)
        {
            throw new InvalidOperationException(
                "ProjectSettings product metadata does not match the " +
                "M-058 release identity.");
        }

        var enabledScenes = Array.FindAll(EditorBuildSettings.scenes,
            scene => scene.enabled);
        if (enabledScenes.Length != 1
            || enabledScenes[0].path != ProductionScenePath)
        {
            throw new InvalidOperationException(
                "Exactly MascotMain.unity must be the enabled production " +
                "Scene.");
        }

        var graphicsApis = PlayerSettings.GetGraphicsAPIs(
            BuildTarget.StandaloneWindows64);
        if (graphicsApis.Length != 1
            || graphicsApis[0] != GraphicsDeviceType.Direct3D12)
        {
            throw new InvalidOperationException(
                "Windows x64 Release must be Direct3D 12 only.");
        }
    }

    [Serializable]
    private sealed class ReleaseBuildReceipt
    {
        public string unityVersion;
        public string buildTarget;
        public string buildOptions;
        public string productName;
        public string companyName;
        public string bundleVersion;
        public string productionScene;
    }
}
#endif
