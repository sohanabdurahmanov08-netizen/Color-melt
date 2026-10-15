using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace ColorMelt.Editor
{
    /// <summary>
    /// Builds the Android test APK into Builds/Android. From the editor:
    /// Tools > Color Melt > Build Android APK. From the command line:
    /// Unity -batchmode -quit -projectPath . -executeMethod ColorMelt.Editor.BuildScript.BuildAndroidApk
    /// The APK is signed with the debug key; Google Play needs a release
    /// keystore set in Player Settings > Publishing Settings and an .aab.
    /// </summary>
    public static class BuildScript
    {
        private const string OutputFolder = "Builds/Android";

        [MenuItem("Tools/Color Melt/Build Android APK", priority = 20)]
        public static void BuildAndroidApk()
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android &&
                !EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android))
            {
                Finish(false, "could not switch to Android (is Android Build Support installed?)");
                return;
            }

            ResolveAndroidDependencies();
            WorkAroundJavaTempPath();

            EditorUserBuildSettings.buildAppBundle = false;
            Directory.CreateDirectory(OutputFolder);
            var options = new BuildPlayerOptions
            {
                scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray(),
                locationPathName = $"{OutputFolder}/ColorMelt-{PlayerSettings.bundleVersion}.apk",
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.None
            };

            var summary = BuildPipeline.BuildPlayer(options).summary;
            Finish(summary.result == BuildResult.Succeeded,
                $"{summary.result}, {summary.totalSize / (1024f * 1024f):F1} MB, {summary.totalErrors} error(s), " +
                $"{summary.totalTime.TotalMinutes:F1} min -> {options.locationPathName}");
        }

        /// <summary>
        /// Writes the LevelPlay (ads) Android libraries into mainTemplate.gradle
        /// through the Mobile Dependency Resolver, found by reflection so the
        /// build still works without it.
        /// </summary>
        private static void ResolveAndroidDependencies()
        {
            var resolver = System.AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType("GooglePlayServices.PlayServicesResolver"))
                .FirstOrDefault(type => type != null);
            var resolve = resolver?.GetMethod("ResolveSync", new[] { typeof(bool) });
            if (resolve == null)
            {
                Debug.LogWarning("Color Melt build: Mobile Dependency Resolver not found, Android libraries not resolved.");
                return;
            }
            var resolved = resolve.Invoke(null, new object[] { true });
            Debug.Log($"Color Melt build: Android dependencies resolved = {resolved}");
        }

        /// <summary>
        /// Java 17 opens an AF_UNIX socket in the temp folder for every NIO
        /// selector, and that fails when the user folder has a space in it
        /// ("C:\Users\WINDOWS 11"): Gradle stops with "Unable to establish
        /// loopback connection". Point the JVMs Unity starts at a folder without
        /// spaces; child processes inherit this editor's environment.
        /// </summary>
        private static void WorkAroundJavaTempPath()
        {
            if (Application.platform != RuntimePlatform.WindowsEditor) return;

            var folder = Path.Combine(
                System.Environment.GetFolderPath(System.Environment.SpecialFolder.CommonApplicationData), "ColorMeltBuild");
            if (folder.Contains(" ")) return;
            Directory.CreateDirectory(folder);

            const string variable = "JAVA_TOOL_OPTIONS";
            var option = "-Djdk.net.unixdomain.tmpdir=" + folder;
            var current = System.Environment.GetEnvironmentVariable(variable) ?? "";
            if (!current.Contains("jdk.net.unixdomain.tmpdir"))
                System.Environment.SetEnvironmentVariable(variable, (current + " " + option).Trim());
        }

        private static void Finish(bool succeeded, string message)
        {
            if (succeeded) Debug.Log("Color Melt build: " + message);
            else Debug.LogError("Color Melt build failed: " + message);

            if (Application.isBatchMode)
                EditorApplication.Exit(succeeded ? 0 : 1);
        }
    }
}
