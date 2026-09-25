using System;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Wildtide.EditorTools
{
    /// <summary>
    /// Entry point for GitHub Actions (game-ci/unity-builder's buildMethod). A fresh checkout has no player
    /// settings yet, so this runs the same setup as the editor does on first open, then builds.
    /// </summary>
    public static class CiBuild
    {
        public static void Build()
        {
            string targetName = Arg("-customBuildTarget") ?? Arg("-buildTarget");
            string path = Arg("-customBuildPath");
            if (targetName == null || path == null) Fail("Missing -customBuildTarget/-buildTarget or -customBuildPath");
            var target = (BuildTarget)Enum.Parse(typeof(BuildTarget), targetName, true);

            ProjectSetup.Run();

            string bundleId = Arg("-bundleId");
            if (!string.IsNullOrEmpty(bundleId))
            {
                PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, bundleId);
                PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, bundleId);
            }
            string version = Arg("-buildVersion");
            if (!string.IsNullOrEmpty(version) && version != "none") PlayerSettings.bundleVersion = version;
            string code = Arg("-androidVersionCode");
            if (int.TryParse(code, out int buildNumber) && buildNumber > 0)
            {
                PlayerSettings.Android.bundleVersionCode = buildNumber;
                PlayerSettings.iOS.buildNumber = buildNumber.ToString();
            }
            EditorUserBuildSettings.buildAppBundle = false; // an .apk you can install directly

            Debug.Log($"Wildtide CI: building {target} to {path} as {PlayerSettings.applicationIdentifier} {PlayerSettings.bundleVersion}");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ProjectSetup.ScenePath },
                locationPathName = path,
                target = target,
                targetGroup = BuildPipeline.GetBuildTargetGroup(target),
                options = BuildOptions.None,
            });
            Debug.Log($"Wildtide CI: {report.summary.result}, {report.summary.totalErrors} errors, {report.summary.totalSize / (1024 * 1024)} MB");
            EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 1);
        }

        static string Arg(string name)
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase)) return args[i + 1];
            return null;
        }

        static void Fail(string message)
        {
            Debug.LogError("Wildtide CI: " + message);
            EditorApplication.Exit(1);
        }
    }
}
