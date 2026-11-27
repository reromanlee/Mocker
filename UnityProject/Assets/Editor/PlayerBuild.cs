using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace reromanlee.Mocker.ContinuousIntegration
{
    public static class PlayerBuild
    {
        private static readonly string[] ScenePaths =
        {
            "Assets/Samples/GettingStarted/GettingStarted.unity"
        };

        public static void Build()
        {
            foreach (string scenePath in ScenePaths)
            {
                if (!File.Exists(scenePath))
                {
                    throw new BuildFailedException($"{scenePath} is missing; copy UnityPackage/Samples~ into Assets/Samples first.");
                }
            }

            BuildTarget target = EditorUserBuildSettings.activeBuildTarget;
            BuildTargetGroup targetGroup = BuildPipeline.GetBuildTargetGroup(target);
            NamedBuildTarget namedTarget = NamedBuildTarget.FromBuildTargetGroup(targetGroup);

            if (target == BuildTarget.Android)
            {
                PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
                PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
                PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.Android, ManagedStrippingLevel.High);
            }
            else if (target == BuildTarget.WebGL)
            {
                PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.WebGL, ManagedStrippingLevel.High);
                PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
            }
            else
            {
                PlayerSettings.SetManagedStrippingLevel(namedTarget, ManagedStrippingLevel.High);
            }

            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = ScenePaths,
                target = target,
                targetGroup = targetGroup,
                locationPathName = ReadCommandLineValue("-customBuildPath")
            });

            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new BuildFailedException($"The {target} build ended as {report.summary.result} with {report.summary.totalErrors} errors.");
            }
        }

        private static string ReadCommandLineValue(string flag)
        {
            string[] arguments = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(arguments, flag);

            if (index < 0 || index >= arguments.Length - 1)
            {
                throw new ArgumentException($"Missing command-line flag: {flag}");
            }

            return arguments[index + 1];
        }
    }
}
