using System;
using System.IO;
using System.IO.Compression;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Deffatest.Editor
{
    /// <summary>
    /// Utility methods for building games and preparing them for Deffatest submission.
    /// Provides easy build options for Android APK, iOS, WebGL, and standalone builds.
    /// </summary>
    public static class DeffatestBuildUtility
    {
        private const string BUILDS_FOLDER = "Builds";

        #region Android Build

        /// <summary>
        /// Build Android APK and return the path
        /// </summary>
        public static void BuildAndroidAPK(Action<string> onSuccess, Action<string> onError)
        {
            try
            {
                // Check if Android build support is installed
                if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android))
                {
                    onError?.Invoke("Android Build Support is not installed.\n\nInstall it via Unity Hub:\n1. Open Unity Hub\n2. Go to Installs\n3. Click the gear icon on your Unity version\n4. Add Modules > Android Build Support");
                    return;
                }

                // Check if we need to switch platform
                if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
                {
                    bool switchPlatform = EditorUtility.DisplayDialog(
                        "Switch Platform",
                        "Current build target is not Android.\n\nSwitch to Android?\n\nThis may take several minutes for asset reimport.",
                        "Switch",
                        "Cancel"
                    );

                    if (!switchPlatform)
                    {
                        onError?.Invoke("Build cancelled. Android platform required.");
                        return;
                    }

                    EditorUtility.DisplayProgressBar("Deffatest", "Switching to Android platform...", 0.1f);
                    EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
                    EditorUtility.ClearProgressBar();
                }

                // Get scenes to build
                string[] scenes = GetEnabledScenes();
                if (scenes.Length == 0)
                {
                    onError?.Invoke("No scenes found in Build Settings.\n\nAdd scenes to your build:\n1. Open Build Settings (Ctrl+Shift+B)\n2. Click 'Add Open Scenes' or drag scenes to the list");
                    return;
                }

                // Get build path
                string buildPath = GetBuildPath("apk");
                string buildDir = Path.GetDirectoryName(buildPath);
                
                if (!Directory.Exists(buildDir))
                {
                    Directory.CreateDirectory(buildDir);
                }

                // Configure build settings
                EditorUserBuildSettings.buildAppBundle = false; // Build APK, not AAB
                PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64 | AndroidArchitecture.ARMv7;

                BuildPlayerOptions buildOptions = new BuildPlayerOptions
                {
                    scenes = scenes,
                    locationPathName = buildPath,
                    target = BuildTarget.Android,
                    options = BuildOptions.None
                };

                Debug.Log($"[Deffatest] Starting Android build: {buildPath}");
                Debug.Log($"[Deffatest] Scenes: {string.Join(", ", scenes)}");

                EditorUtility.DisplayProgressBar("Deffatest", "Building Android APK...", 0.3f);

                // Build
                BuildReport report = BuildPipeline.BuildPlayer(buildOptions);

                EditorUtility.ClearProgressBar();

                if (report.summary.result == BuildResult.Succeeded)
                {
                    float sizeMB = report.summary.totalSize / (1024f * 1024f);
                    Debug.Log($"[Deffatest] Build succeeded: {buildPath} ({sizeMB:F2} MB)");
                    onSuccess?.Invoke(buildPath);
                }
                else
                {
                    string errors = GetBuildErrors(report);
                    Debug.LogError($"[Deffatest] Build failed: {errors}");
                    onError?.Invoke($"Build failed:\n\n{errors}");
                }
            }
            catch (Exception e)
            {
                EditorUtility.ClearProgressBar();
                Debug.LogError($"[Deffatest] Build error: {e.Message}");
                onError?.Invoke($"Build error: {e.Message}");
            }
        }

        #endregion

        #region iOS Build

        /// <summary>
        /// Build iOS Xcode project
        /// </summary>
        public static void BuildIOSProject(Action<string> onSuccess, Action<string> onError)
        {
            try
            {
                if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.iOS, BuildTarget.iOS))
                {
                    onError?.Invoke("iOS Build Support is not installed.\n\nNote: iOS builds require a Mac.");
                    return;
                }

                string[] scenes = GetEnabledScenes();
                if (scenes.Length == 0)
                {
                    onError?.Invoke("No scenes found in Build Settings.");
                    return;
                }

                string buildPath = GetBuildPath("ios");

                BuildPlayerOptions buildOptions = new BuildPlayerOptions
                {
                    scenes = scenes,
                    locationPathName = buildPath,
                    target = BuildTarget.iOS,
                    options = BuildOptions.None
                };

                EditorUtility.DisplayProgressBar("Deffatest", "Building iOS Xcode project...", 0.3f);

                BuildReport report = BuildPipeline.BuildPlayer(buildOptions);

                EditorUtility.ClearProgressBar();

                if (report.summary.result == BuildResult.Succeeded)
                {
                    Debug.Log($"[Deffatest] iOS build succeeded: {buildPath}");
                    
                    EditorUtility.DisplayDialog(
                        "iOS Build Complete",
                        $"Xcode project exported to:\n{buildPath}\n\n" +
                        "Next steps:\n" +
                        "1. Open the Xcode project\n" +
                        "2. Configure signing\n" +
                        "3. Build and archive\n" +
                        "4. Export IPA for testing",
                        "OK"
                    );
                    
                    onSuccess?.Invoke(buildPath);
                }
                else
                {
                    string errors = GetBuildErrors(report);
                    onError?.Invoke($"Build failed:\n\n{errors}");
                }
            }
            catch (Exception e)
            {
                EditorUtility.ClearProgressBar();
                onError?.Invoke($"Build error: {e.Message}");
            }
        }

        #endregion

        #region WebGL Build

        /// <summary>
        /// Build WebGL
        /// </summary>
        public static void BuildWebGL(Action<string> onSuccess, Action<string> onError)
        {
            try
            {
                if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.WebGL, BuildTarget.WebGL))
                {
                    onError?.Invoke("WebGL Build Support is not installed.\n\nInstall it via Unity Hub.");
                    return;
                }

                string[] scenes = GetEnabledScenes();
                if (scenes.Length == 0)
                {
                    onError?.Invoke("No scenes found in Build Settings.");
                    return;
                }

                string buildPath = GetBuildPath("webgl");

                BuildPlayerOptions buildOptions = new BuildPlayerOptions
                {
                    scenes = scenes,
                    locationPathName = buildPath,
                    target = BuildTarget.WebGL,
                    options = BuildOptions.None
                };

                EditorUtility.DisplayProgressBar("Deffatest", "Building WebGL...", 0.3f);

                BuildReport report = BuildPipeline.BuildPlayer(buildOptions);

                EditorUtility.ClearProgressBar();

                if (report.summary.result == BuildResult.Succeeded)
                {
                    float sizeMB = report.summary.totalSize / (1024f * 1024f);
                    Debug.Log($"[Deffatest] WebGL build succeeded: {buildPath} ({sizeMB:F2} MB)");
                    onSuccess?.Invoke(buildPath);
                }
                else
                {
                    string errors = GetBuildErrors(report);
                    onError?.Invoke($"Build failed:\n\n{errors}");
                }
            }
            catch (Exception e)
            {
                EditorUtility.ClearProgressBar();
                onError?.Invoke($"Build error: {e.Message}");
            }
        }

        #endregion

        #region Standalone Builds

        /// <summary>
        /// Build for current platform (Windows/Mac/Linux)
        /// </summary>
        public static void BuildStandalone(Action<string> onSuccess, Action<string> onError)
        {
            try
            {
                BuildTarget target;
                string extension;

#if UNITY_EDITOR_WIN
                target = BuildTarget.StandaloneWindows64;
                extension = "exe";
#elif UNITY_EDITOR_OSX
                target = BuildTarget.StandaloneOSX;
                extension = "app";
#else
                target = BuildTarget.StandaloneLinux64;
                extension = "x86_64";
#endif

                string[] scenes = GetEnabledScenes();
                if (scenes.Length == 0)
                {
                    onError?.Invoke("No scenes found in Build Settings.");
                    return;
                }

                string buildPath = GetBuildPath(extension);

                BuildPlayerOptions buildOptions = new BuildPlayerOptions
                {
                    scenes = scenes,
                    locationPathName = buildPath,
                    target = target,
                    options = BuildOptions.None
                };

                EditorUtility.DisplayProgressBar("Deffatest", $"Building {target}...", 0.3f);

                BuildReport report = BuildPipeline.BuildPlayer(buildOptions);

                EditorUtility.ClearProgressBar();

                if (report.summary.result == BuildResult.Succeeded)
                {
                    Debug.Log($"[Deffatest] Build succeeded: {buildPath}");
                    onSuccess?.Invoke(buildPath);
                }
                else
                {
                    string errors = GetBuildErrors(report);
                    onError?.Invoke($"Build failed:\n\n{errors}");
                }
            }
            catch (Exception e)
            {
                EditorUtility.ClearProgressBar();
                onError?.Invoke($"Build error: {e.Message}");
            }
        }

        #endregion

        #region ZIP Creation

        /// <summary>
        /// Create a ZIP archive of a build folder
        /// </summary>
        public static string CreateBuildZip(string buildPath)
        {
            try
            {
                string zipPath;
                string sourceDir;

                if (File.Exists(buildPath))
                {
                    // Single file (APK, EXE, etc.)
                    sourceDir = Path.GetDirectoryName(buildPath);
                    string baseName = Path.GetFileNameWithoutExtension(buildPath);
                    zipPath = Path.Combine(sourceDir, $"{baseName}.zip");
                    
                    // Delete existing ZIP
                    if (File.Exists(zipPath))
                    {
                        File.Delete(zipPath);
                    }

                    // For single files, create ZIP containing just that file
                    using (var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
                    {
                        archive.CreateEntryFromFile(buildPath, Path.GetFileName(buildPath), CompressionLevel.Optimal);
                    }
                }
                else if (Directory.Exists(buildPath))
                {
                    // Directory (WebGL, standalone folder, etc.)
                    string parentDir = Path.GetDirectoryName(buildPath);
                    string dirName = Path.GetFileName(buildPath);
                    zipPath = Path.Combine(parentDir, $"{dirName}.zip");

                    // Delete existing ZIP
                    if (File.Exists(zipPath))
                    {
                        File.Delete(zipPath);
                    }

                    ZipFile.CreateFromDirectory(buildPath, zipPath, CompressionLevel.Optimal, true);
                }
                else
                {
                    Debug.LogError($"[Deffatest] Build path not found: {buildPath}");
                    return null;
                }

                float sizeMB = new FileInfo(zipPath).Length / (1024f * 1024f);
                Debug.Log($"[Deffatest] Created ZIP: {zipPath} ({sizeMB:F2} MB)");

                return zipPath;
            }
            catch (Exception e)
            {
                Debug.LogError($"[Deffatest] Failed to create ZIP: {e.Message}");
                return null;
            }
        }

        #endregion

        #region Helpers

        /// <summary>
        /// Get all enabled scenes from Build Settings
        /// </summary>
        public static string[] GetEnabledScenes()
        {
            var scenes = new List<string>();

            foreach (var scene in EditorBuildSettings.scenes)
            {
                if (scene.enabled && !string.IsNullOrEmpty(scene.path))
                {
                    scenes.Add(scene.path);
                }
            }

            return scenes.ToArray();
        }

        /// <summary>
        /// Get build path for specified platform/extension
        /// </summary>
        public static string GetBuildPath(string platformOrExtension)
        {
            string projectPath = Path.GetDirectoryName(Application.dataPath);
            string buildsFolder = Path.Combine(projectPath, BUILDS_FOLDER);

            if (!Directory.Exists(buildsFolder))
            {
                Directory.CreateDirectory(buildsFolder);
            }

            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string appName = SanitizeFileName(Application.productName);

            string fileName = platformOrExtension.ToLower() switch
            {
                "apk" => $"{appName}_{timestamp}.apk",
                "aab" => $"{appName}_{timestamp}.aab",
                "exe" => $"{appName}_{timestamp}.exe",
                "app" => $"{appName}_{timestamp}.app",
                "x86_64" => $"{appName}_{timestamp}.x86_64",
                "ios" => $"{appName}_iOS_{timestamp}",
                "webgl" => $"{appName}_WebGL_{timestamp}",
                _ => $"{appName}_{platformOrExtension}_{timestamp}"
            };

            return Path.Combine(buildsFolder, fileName);
        }

        /// <summary>
        /// Format file size in human-readable format
        /// </summary>
        public static string FormatFileSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            double len = bytes;
            int order = 0;

            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len /= 1024;
            }

            return $"{len:0.##} {sizes[order]}";
        }

        /// <summary>
        /// Get file size of a build
        /// </summary>
        public static long GetBuildSize(string buildPath)
        {
            if (File.Exists(buildPath))
            {
                return new FileInfo(buildPath).Length;
            }
            
            if (Directory.Exists(buildPath))
            {
                return GetDirectorySize(buildPath);
            }

            return 0;
        }

        private static long GetDirectorySize(string path)
        {
            long size = 0;

            foreach (var file in Directory.GetFiles(path, "*", SearchOption.AllDirectories))
            {
                size += new FileInfo(file).Length;
            }

            return size;
        }

        private static string SanitizeFileName(string name)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
            {
                name = name.Replace(c, '_');
            }
            return name.Replace(" ", "_");
        }

        private static string GetBuildErrors(BuildReport report)
        {
            var errors = new List<string>();

            foreach (var step in report.steps)
            {
                foreach (var message in step.messages)
                {
                    if (message.type == LogType.Error)
                    {
                        errors.Add(message.content);
                    }
                }
            }

            return errors.Count > 0 
                ? string.Join("\n", errors.GetRange(0, Math.Min(5, errors.Count)))
                : "Unknown build error. Check the console for details.";
        }

        #endregion

        #region Menu Items

        [MenuItem("Deffatest/Build/Android APK")]
        public static void MenuBuildAndroid()
        {
            BuildAndroidAPK(
                onSuccess: (path) =>
                {
                    EditorUtility.DisplayDialog("Build Complete", $"APK built successfully!\n\n{path}", "OK");
                    EditorUtility.RevealInFinder(path);
                },
                onError: (error) =>
                {
                    EditorUtility.DisplayDialog("Build Failed", error, "OK");
                }
            );
        }

        [MenuItem("Deffatest/Build/WebGL")]
        public static void MenuBuildWebGL()
        {
            BuildWebGL(
                onSuccess: (path) =>
                {
                    EditorUtility.DisplayDialog("Build Complete", $"WebGL built successfully!\n\n{path}", "OK");
                    EditorUtility.RevealInFinder(path);
                },
                onError: (error) =>
                {
                    EditorUtility.DisplayDialog("Build Failed", error, "OK");
                }
            );
        }

        [MenuItem("Deffatest/Build/Current Platform")]
        public static void MenuBuildStandalone()
        {
            BuildStandalone(
                onSuccess: (path) =>
                {
                    EditorUtility.DisplayDialog("Build Complete", $"Build succeeded!\n\n{path}", "OK");
                    EditorUtility.RevealInFinder(path);
                },
                onError: (error) =>
                {
                    EditorUtility.DisplayDialog("Build Failed", error, "OK");
                }
            );
        }

        [MenuItem("Deffatest/Open Builds Folder")]
        public static void OpenBuildsFolder()
        {
            string buildsFolder = Path.Combine(Path.GetDirectoryName(Application.dataPath), BUILDS_FOLDER);
            
            if (!Directory.Exists(buildsFolder))
            {
                Directory.CreateDirectory(buildsFolder);
            }

            EditorUtility.RevealInFinder(buildsFolder);
        }

        #endregion
    }
}
