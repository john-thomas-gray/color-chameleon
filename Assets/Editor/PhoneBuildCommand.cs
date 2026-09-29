using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;

namespace CandyCruisers.Editor
{
    public static class PhoneBuildCommand
    {
        public static void BuildIos()
        {
            string output = Environment.GetEnvironmentVariable("CANDY_CRUISERS_IOS_BUILD_PATH");
            if (string.IsNullOrWhiteSpace(output))
                output = Path.Combine(Path.GetTempPath(), "candy-cruisers-ios-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss"));

            var scenes = EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .ToArray();
            if (scenes.Length == 0)
                throw new InvalidOperationException("No enabled scenes are configured for the iOS build.");

            Directory.CreateDirectory(output);
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.iOS, BuildTarget.iOS);
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = output,
                target = BuildTarget.iOS,
                options = BuildOptions.Development | BuildOptions.AllowDebugging
            });

            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException(
                    $"iOS build failed with {report.summary.result}: {report.summary.totalErrors} errors.");
        }
    }
}
