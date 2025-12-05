using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Deffatest.Editor
{
    /// <summary>
    /// Automatically hooks into Unity's build pipeline.
    /// Prompts to submit builds for testing after successful compilation.
    /// </summary>
    public class DeffatestBuildPostProcessor : IPostprocessBuildWithReport
    {
        public int callbackOrder => 999; // Run after other post-processors

        public void OnPostprocessBuild(BuildReport report)
        {
            // Only proceed if auto-submit is enabled
            if (!DeffatestSettings.AutoSubmitOnBuild)
            {
                return;
            }

            // Only proceed if authenticated
            if (!DeffatestSettings.IsAuthenticated)
            {
                Debug.LogWarning("[Deffatest] Auto-submit enabled but not authenticated. Skipping.");
                return;
            }

            // Only handle successful builds
            if (report.summary.result != BuildResult.Succeeded)
            {
                Debug.Log($"[Deffatest] Build did not succeed ({report.summary.result}). Skipping auto-submit.");
                return;
            }

            string buildPath = report.summary.outputPath;
            BuildTarget target = report.summary.platform;
            long buildSizeBytes = report.summary.totalSize;
            float buildSizeMB = buildSizeBytes / (1024f * 1024f);

            Debug.Log($"[Deffatest] Build completed successfully!");
            Debug.Log($"  Platform: {target}");
            Debug.Log($"  Path: {buildPath}");
            Debug.Log($"  Size: {buildSizeMB:F2} MB");

            // Handle different platforms
            switch (target)
            {
                case BuildTarget.Android:
                    HandleAndroidBuild(buildPath, buildSizeMB);
                    break;

                case BuildTarget.iOS:
                    HandleIOSBuild(buildPath);
                    break;

                case BuildTarget.StandaloneWindows:
                case BuildTarget.StandaloneWindows64:
                case BuildTarget.StandaloneOSX:
                case BuildTarget.StandaloneLinux64:
                    HandleStandaloneBuild(buildPath, target);
                    break;

                case BuildTarget.WebGL:
                    HandleWebGLBuild(buildPath);
                    break;

                default:
                    Debug.Log($"[Deffatest] Platform {target} auto-submit not yet supported.");
                    break;
            }
        }

        private void HandleAndroidBuild(string buildPath, float buildSizeMB)
        {
            // Check if it's an APK file
            string extension = Path.GetExtension(buildPath).ToLower();
            
            if (extension != ".apk" && extension != ".aab")
            {
                Debug.LogWarning("[Deffatest] Android build is not an APK/AAB. Skipping auto-submit.");
                return;
            }

            // Check file size limits
            float maxSizeMB = DeffatestSettings.Plan.ToLower() == "free" ? 100 : 500;
            if (buildSizeMB > maxSizeMB)
            {
                Debug.LogWarning($"[Deffatest] Build size ({buildSizeMB:F1}MB) exceeds limit ({maxSizeMB}MB) for {DeffatestSettings.Plan} plan.");
                
                EditorUtility.DisplayDialog(
                    "Deffatest - File Too Large",
                    $"Build size ({buildSizeMB:F1}MB) exceeds the {maxSizeMB}MB limit for your plan.\n\n" +
                    "Consider:\n" +
                    "• Upgrading your plan\n" +
                    "• Reducing build size\n" +
                    "• Using URL-based testing instead",
                    "OK"
                );
                return;
            }

            bool shouldSubmit = EditorUtility.DisplayDialog(
                "Deffatest - Submit Build?",
                $"Android build completed successfully!\n\n" +
                $"File: {Path.GetFileName(buildPath)}\n" +
                $"Size: {buildSizeMB:F2} MB\n\n" +
                $"Submit to Deffatest for AI testing?",
                "Submit Now",
                "Skip"
            );

            if (shouldSubmit)
            {
                SubmitBuild(buildPath, "mobile");
            }
        }

        private void HandleIOSBuild(string buildPath)
        {
            // iOS builds are Xcode projects
            EditorUtility.DisplayDialog(
                "Deffatest - iOS Build",
                "iOS build completed!\n\n" +
                "To test your iOS build:\n" +
                "1. Open the Xcode project\n" +
                "2. Build and archive the IPA\n" +
                "3. Submit the IPA from Deffatest window\n\n" +
                "Or deploy to TestFlight and test via URL.",
                "OK"
            );
        }

        private void HandleStandaloneBuild(string buildPath, BuildTarget target)
        {
            string platformName = target switch
            {
                BuildTarget.StandaloneWindows => "Windows (32-bit)",
                BuildTarget.StandaloneWindows64 => "Windows (64-bit)",
                BuildTarget.StandaloneOSX => "macOS",
                BuildTarget.StandaloneLinux64 => "Linux",
                _ => "Standalone"
            };

            bool shouldSubmit = EditorUtility.DisplayDialog(
                "Deffatest - Submit Build?",
                $"{platformName} build completed!\n\n" +
                $"Path: {buildPath}\n\n" +
                "Note: Standalone builds will be zipped before upload.\n\n" +
                "Submit to Deffatest for AI testing?",
                "Submit Now",
                "Skip"
            );

            if (shouldSubmit)
            {
                // Create ZIP of build
                string zipPath = DeffatestBuildUtility.CreateBuildZip(buildPath);
                
                if (!string.IsNullOrEmpty(zipPath))
                {
                    SubmitBuild(zipPath, "game");
                }
                else
                {
                    EditorUtility.DisplayDialog(
                        "Deffatest - Error",
                        "Failed to create ZIP archive of build.\n\nPlease check the console for details.",
                        "OK"
                    );
                }
            }
        }

        private void HandleWebGLBuild(string buildPath)
        {
            EditorUtility.DisplayDialog(
                "Deffatest - WebGL Build",
                "WebGL build completed!\n\n" +
                $"Output: {buildPath}\n\n" +
                "To test your WebGL build:\n" +
                "1. Deploy to a web server (e.g., itch.io, GitHub Pages)\n" +
                "2. Open Deffatest window (Ctrl+Shift+D)\n" +
                "3. Enter the deployed URL\n" +
                "4. Start test\n\n" +
                "For local testing, use a local web server.",
                "OK"
            );
        }

        private void SubmitBuild(string buildPath, string testType)
        {
            var apiClient = new DeffatestAPIClient(
                DeffatestSettings.BaseURL,
                DeffatestSettings.ApiKey
            );

            string duration = DeffatestSettings.DefaultDuration;

            EditorUtility.DisplayProgressBar("Deffatest", "Preparing upload...", 0f);

            EditorCoroutineUtility.StartCoroutine(
                apiClient.SubmitBundleTest(
                    buildPath,
                    testType,
                    duration,
                    onProgress: (progress) =>
                    {
                        EditorUtility.DisplayProgressBar(
                            "Deffatest",
                            $"Uploading... {(progress * 100):F0}%",
                            progress
                        );
                    },
                    onSuccess: (testData) =>
                    {
                        EditorUtility.ClearProgressBar();

                        bool openWindow = EditorUtility.DisplayDialog(
                            "Deffatest - Test Submitted!",
                            $"Build submitted successfully!\n\n" +
                            $"Test ID: {testData.id}\n" +
                            $"Status: {testData.status}\n\n" +
                            "Track progress in the Deffatest window.",
                            "Open Deffatest",
                            "Close"
                        );

                        if (openWindow)
                        {
                            DeffatestEditorWindow.ShowWindow();
                        }

                        // Track analytics
                        EditorCoroutineUtility.StartCoroutine(
                            apiClient.TrackEvent("build_auto_submitted", new System.Collections.Generic.Dictionary<string, object>
                            {
                                { "test_type", testType },
                                { "duration", duration },
                                { "build_size_mb", new FileInfo(buildPath).Length / (1024f * 1024f) }
                            }),
                            null
                        );
                    },
                    onError: (error) =>
                    {
                        EditorUtility.ClearProgressBar();

                        EditorUtility.DisplayDialog(
                            "Deffatest - Submission Failed",
                            $"Failed to submit build:\n\n{error}\n\n" +
                            "You can try again from the Deffatest window.",
                            "OK"
                        );

                        Debug.LogError($"[Deffatest] Build submission failed: {error}");
                    }
                ),
                null
            );
        }
    }

    /// <summary>
    /// Pre-process build to log Deffatest status
    /// </summary>
    public class DeffatestBuildPreProcessor : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            if (DeffatestSettings.AutoSubmitOnBuild && DeffatestSettings.IsAuthenticated)
            {
                Debug.Log("[Deffatest] Build started. Will prompt for test submission after successful build.");
            }
        }
    }
}
