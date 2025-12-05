using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Deffatest.Editor
{
    /// <summary>
    /// Main Deffatest Editor Window.
    /// Accessible via Window > Deffatest or Ctrl+Shift+D shortcut.
    /// Provides authentication, test submission, and real-time status tracking.
    /// </summary>
    public class DeffatestEditorWindow : EditorWindow
    {
        #region State

        private enum Tab { Test, Status, History, Settings }
        private Tab currentTab = Tab.Test;

        // Authentication
        private string apiKeyInput = "";
        private bool isVerifying = false;
        private string verificationError = "";

        // Test submission
        private string testUrl = "http://localhost:3000";
        private int selectedDurationIndex = 2; // 2h default
        private int selectedTestTypeIndex = 2; // game default
        private bool isSubmitting = false;
        private float uploadProgress = 0f;
        private string submissionError = "";

        // Current test status
        private string currentTestId = "";
        private string testStatus = "";
        private int testProgress = 0;
        private string currentAction = "";
        private DeffatestAPIClient.BugCounts bugCounts;
        private bool isTestRunning = false;
        private string reportUrl = "";

        // History
        private DeffatestAPIClient.TestData[] recentTests;
        private bool isLoadingHistory = false;

        // API & WebSocket
        private DeffatestAPIClient apiClient;
        private DeffatestWebSocketClient wsClient;

        // UI
        private Vector2 scrollPosition;
        private GUIStyle headerStyle;
        private GUIStyle subHeaderStyle;
        private GUIStyle buttonStyle;
        private GUIStyle successButtonStyle;
        private GUIStyle dangerButtonStyle;
        private GUIStyle cardStyle;
        private GUIStyle bugCriticalStyle;
        private GUIStyle bugHighStyle;
        private GUIStyle bugMediumStyle;
        private GUIStyle bugLowStyle;
        private bool stylesInitialized = false;

        // Logo
        private Texture2D logoTexture;

        #endregion

        #region Menu Item

        [MenuItem("Window/Deffatest %#d")] // Ctrl+Shift+D
        public static void ShowWindow()
        {
            var window = GetWindow<DeffatestEditorWindow>("Deffatest");
            window.minSize = new Vector2(450, 600);
            window.Show();
        }

        [MenuItem("Window/Deffatest/Submit Test %#t")] // Ctrl+Shift+T
        public static void QuickSubmitTest()
        {
            var window = GetWindow<DeffatestEditorWindow>("Deffatest");
            window.currentTab = Tab.Test;
            window.Show();
            window.Focus();
        }

        #endregion

        #region Lifecycle

        private void OnEnable()
        {
            LoadSettings();
            InitializeApiClient();
            LoadLogo();

            // Start status polling if test is running
            if (!string.IsNullOrEmpty(currentTestId) && isTestRunning)
            {
                StartStatusPolling();
            }
        }

        private void OnDisable()
        {
            DisconnectWebSocket();
        }

        private void OnDestroy()
        {
            DisconnectWebSocket();
        }

        private void Update()
        {
            // Process WebSocket queue
            wsClient?.ProcessQueue();
        }

        #endregion

        #region OnGUI

        private void OnGUI()
        {
            InitializeStyles();

            // Header
            DrawHeader();

            // Navigation tabs
            DrawTabs();

            // Separator
            EditorGUILayout.Space(5);
            DrawSeparator();
            EditorGUILayout.Space(10);

            // Content
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            switch (currentTab)
            {
                case Tab.Test:
                    DrawTestTab();
                    break;
                case Tab.Status:
                    DrawStatusTab();
                    break;
                case Tab.History:
                    DrawHistoryTab();
                    break;
                case Tab.Settings:
                    DrawSettingsTab();
                    break;
            }

            EditorGUILayout.EndScrollView();

            // Footer
            DrawFooter();
        }

        #endregion

        #region Header & Navigation

        private void DrawHeader()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            // Logo
            GUILayout.Label("🔬 Deffatest", headerStyle, GUILayout.Height(24));

            GUILayout.FlexibleSpace();

            // User info
            if (DeffatestSettings.IsAuthenticated)
            {
                GUILayout.Label($"👤 {DeffatestSettings.Username}", EditorStyles.miniLabel);
                GUILayout.Label($"| {DeffatestSettings.PlanDisplayName}", EditorStyles.miniLabel);
                GUILayout.Label($"| 📊 {DeffatestSettings.TestsRemaining} left", EditorStyles.miniLabel);
            }
            else
            {
                GUI.color = new Color(1f, 0.6f, 0.6f);
                GUILayout.Label("⚠️ Not authenticated", EditorStyles.miniLabel);
                GUI.color = Color.white;
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawTabs()
        {
            EditorGUILayout.BeginHorizontal();

            DrawTab("🚀 Test", Tab.Test);
            DrawTab("📊 Status", Tab.Status);
            DrawTab("📋 History", Tab.History);
            DrawTab("⚙️ Settings", Tab.Settings);

            EditorGUILayout.EndHorizontal();
        }

        private void DrawTab(string label, Tab tab)
        {
            bool isSelected = currentTab == tab;
            GUIStyle style = isSelected ? EditorStyles.toolbarButton : EditorStyles.miniButton;
            
            if (GUILayout.Toggle(isSelected, label, style))
            {
                if (currentTab != tab)
                {
                    currentTab = tab;
                    
                    // Load history when switching to History tab
                    if (tab == Tab.History && recentTests == null)
                    {
                        LoadTestHistory();
                    }
                }
            }
        }

        private void DrawFooter()
        {
            EditorGUILayout.Space(10);
            DrawSeparator();
            
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            
            if (GUILayout.Button("📖 Docs", EditorStyles.miniButton, GUILayout.Width(60)))
            {
                Application.OpenURL(DeffatestConstants.DOCS_URL);
            }
            
            if (GUILayout.Button("🌐 Dashboard", EditorStyles.miniButton, GUILayout.Width(80)))
            {
                Application.OpenURL(DeffatestConstants.DASHBOARD_URL);
            }
            
            GUILayout.Label($"v{DeffatestConstants.PLUGIN_VERSION}", EditorStyles.miniLabel);
            
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(5);
        }

        #endregion

        #region Test Tab

        private void DrawTestTab()
        {
            if (!DeffatestSettings.IsAuthenticated)
            {
                DrawAuthRequired();
                return;
            }

            // Test Type Selection
            EditorGUILayout.LabelField("Test Configuration", subHeaderStyle);
            EditorGUILayout.Space(5);

            DrawCard(() =>
            {
                // Test Type
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Test Type:", GUILayout.Width(100));
                selectedTestTypeIndex = EditorGUILayout.Popup(
                    selectedTestTypeIndex,
                    DeffatestSettings.TestTypeDisplayNames
                );
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Space(5);

                // URL or Build info
                string testType = DeffatestSettings.TestTypeOptions[selectedTestTypeIndex];
                
                if (testType == "web" || testType == "game")
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("URL:", GUILayout.Width(100));
                    testUrl = EditorGUILayout.TextField(testUrl);
                    EditorGUILayout.EndHorizontal();

                    EditorGUILayout.HelpBox(
                        testType == "web" 
                            ? "Enter your local development server URL or any public URL."
                            : "Enter your WebGL build URL or localhost for local testing.",
                        MessageType.Info
                    );
                }
                else if (testType == "mobile")
                {
                    EditorGUILayout.HelpBox(
                        "Click 'Build & Submit' to build your APK and submit for testing.",
                        MessageType.Info
                    );
                }

                EditorGUILayout.Space(5);

                // Duration
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Duration:", GUILayout.Width(100));
                selectedDurationIndex = EditorGUILayout.Popup(
                    selectedDurationIndex,
                    DeffatestSettings.DurationDisplayNames
                );
                EditorGUILayout.EndHorizontal();
            });

            EditorGUILayout.Space(15);

            // Submit Button
            GUI.enabled = !isSubmitting && !string.IsNullOrEmpty(testUrl);
            
            string buttonLabel = isSubmitting 
                ? $"Submitting... {(uploadProgress * 100):F0}%" 
                : "🚀 Start AI Test";

            if (GUILayout.Button(buttonLabel, successButtonStyle, GUILayout.Height(40)))
            {
                SubmitTest();
            }
            
            GUI.enabled = true;

            // Progress bar during submission
            if (isSubmitting)
            {
                EditorGUILayout.Space(5);
                EditorGUI.ProgressBar(
                    EditorGUILayout.GetControlRect(GUILayout.Height(20)),
                    uploadProgress,
                    $"Uploading... {(uploadProgress * 100):F0}%"
                );
            }

            // Error message
            if (!string.IsNullOrEmpty(submissionError))
            {
                EditorGUILayout.Space(5);
                EditorGUILayout.HelpBox(submissionError, MessageType.Error);
            }

            EditorGUILayout.Space(20);

            // Quick Actions
            EditorGUILayout.LabelField("Quick Actions", subHeaderStyle);
            EditorGUILayout.Space(5);

            EditorGUILayout.BeginHorizontal();
            
            if (GUILayout.Button("🔨 Build APK", GUILayout.Height(30)))
            {
                BuildAndSubmitAPK();
            }
            
            if (GUILayout.Button("🌐 Build WebGL", GUILayout.Height(30)))
            {
                BuildWebGL();
            }
            
            EditorGUILayout.EndHorizontal();
        }

        #endregion

        #region Status Tab

        private void DrawStatusTab()
        {
            if (!isTestRunning && string.IsNullOrEmpty(currentTestId))
            {
                EditorGUILayout.HelpBox(
                    "No active test. Submit a test from the Test tab to see live status.",
                    MessageType.Info
                );
                
                if (GUILayout.Button("Go to Test Tab", GUILayout.Height(30)))
                {
                    currentTab = Tab.Test;
                }
                return;
            }

            // Test ID and Status
            EditorGUILayout.LabelField("Current Test", subHeaderStyle);
            EditorGUILayout.Space(5);

            DrawCard(() =>
            {
                // Test ID
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Test ID:", GUILayout.Width(80));
                EditorGUILayout.SelectableLabel(currentTestId, GUILayout.Height(18));
                
                if (GUILayout.Button("📋", GUILayout.Width(25)))
                {
                    GUIUtility.systemCopyBuffer = currentTestId;
                    ShowNotification(new GUIContent("Copied!"), 1);
                }
                EditorGUILayout.EndHorizontal();

                // Status
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Status:", GUILayout.Width(80));
                EditorGUILayout.LabelField(GetStatusWithEmoji(testStatus));
                EditorGUILayout.EndHorizontal();

                // Current action
                if (!string.IsNullOrEmpty(currentAction))
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("Action:", GUILayout.Width(80));
                    EditorGUILayout.LabelField(currentAction, EditorStyles.wordWrappedLabel);
                    EditorGUILayout.EndHorizontal();
                }
            });

            EditorGUILayout.Space(10);

            // Progress bar
            EditorGUILayout.LabelField("Progress", subHeaderStyle);
            EditorGUILayout.Space(5);

            Rect progressRect = EditorGUILayout.GetControlRect(GUILayout.Height(25));
            EditorGUI.ProgressBar(progressRect, testProgress / 100f, $"{testProgress}% Complete");

            EditorGUILayout.Space(15);

            // Bug Summary
            EditorGUILayout.LabelField("Bugs Found", subHeaderStyle);
            EditorGUILayout.Space(5);

            DrawCard(() =>
            {
                if (bugCounts == null)
                {
                    bugCounts = new DeffatestAPIClient.BugCounts();
                }

                EditorGUILayout.BeginHorizontal();
                DrawBugCounter("🔴 Critical", bugCounts.critical, Color.red);
                DrawBugCounter("🟠 High", bugCounts.high, new Color(1f, 0.5f, 0f));
                DrawBugCounter("🟡 Medium", bugCounts.medium, new Color(1f, 0.9f, 0f));
                DrawBugCounter("🟢 Low", bugCounts.low, new Color(0.3f, 0.8f, 0.3f));
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField($"Total: {bugCounts.total} bugs found", EditorStyles.boldLabel);
            });

            EditorGUILayout.Space(15);

            // Actions
            EditorGUILayout.BeginHorizontal();

            if (testStatus == "completed" && !string.IsNullOrEmpty(reportUrl))
            {
                if (GUILayout.Button("📄 View Full Report", successButtonStyle, GUILayout.Height(35)))
                {
                    Application.OpenURL(reportUrl);
                }
            }

            if (isTestRunning && testStatus == "running")
            {
                if (GUILayout.Button("⏹️ Cancel Test", dangerButtonStyle, GUILayout.Height(35)))
                {
                    CancelTest();
                }
            }

            if (!isTestRunning && testStatus == "completed")
            {
                if (GUILayout.Button("🔄 New Test", buttonStyle, GUILayout.Height(35)))
                {
                    currentTab = Tab.Test;
                    currentTestId = "";
                    testStatus = "";
                    testProgress = 0;
                    bugCounts = null;
                }
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawBugCounter(string label, int count, Color color)
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(80));
            
            var originalColor = GUI.color;
            GUI.color = color;
            EditorGUILayout.LabelField(count.ToString(), 
                new GUIStyle(EditorStyles.boldLabel) { fontSize = 24, alignment = TextAnchor.MiddleCenter });
            GUI.color = originalColor;
            
            EditorGUILayout.LabelField(label.Split(' ')[1], 
                new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleCenter });
            
            EditorGUILayout.EndVertical();
        }

        #endregion

        #region History Tab

        private void DrawHistoryTab()
        {
            if (!DeffatestSettings.IsAuthenticated)
            {
                DrawAuthRequired();
                return;
            }

            EditorGUILayout.LabelField("Recent Tests", subHeaderStyle);
            EditorGUILayout.Space(5);

            // Refresh button
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            
            GUI.enabled = !isLoadingHistory;
            if (GUILayout.Button("🔄 Refresh", GUILayout.Width(80)))
            {
                LoadTestHistory();
            }
            GUI.enabled = true;
            
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(10);

            if (isLoadingHistory)
            {
                EditorGUILayout.LabelField("Loading...", EditorStyles.centeredGreyMiniLabel);
                return;
            }

            if (recentTests == null || recentTests.Length == 0)
            {
                EditorGUILayout.HelpBox("No tests found. Submit your first test!", MessageType.Info);
                return;
            }

            // Test list
            foreach (var test in recentTests)
            {
                DrawTestCard(test);
                EditorGUILayout.Space(5);
            }
        }

        private void DrawTestCard(DeffatestAPIClient.TestData test)
        {
            DrawCard(() =>
            {
                EditorGUILayout.BeginHorizontal();
                
                // Status icon
                EditorGUILayout.LabelField(GetStatusEmoji(test.status), GUILayout.Width(25));
                
                // Test info
                EditorGUILayout.BeginVertical();
                EditorGUILayout.LabelField(test.id, EditorStyles.boldLabel);
                EditorGUILayout.LabelField($"{test.type} • {test.duration}", EditorStyles.miniLabel);
                EditorGUILayout.EndVertical();
                
                GUILayout.FlexibleSpace();
                
                // Action button
                if (test.status == "completed")
                {
                    if (GUILayout.Button("View", GUILayout.Width(50)))
                    {
                        Application.OpenURL($"{DeffatestConstants.DASHBOARD_URL}/test/{test.id}");
                    }
                }
                else if (test.status == "running")
                {
                    if (GUILayout.Button("Track", GUILayout.Width(50)))
                    {
                        currentTestId = test.id;
                        isTestRunning = true;
                        currentTab = Tab.Status;
                        StartStatusPolling();
                    }
                }
                
                EditorGUILayout.EndHorizontal();
            });
        }

        #endregion

        #region Settings Tab

        private void DrawSettingsTab()
        {
            EditorGUILayout.LabelField("Authentication", subHeaderStyle);
            EditorGUILayout.Space(5);

            DrawCard(() =>
            {
                if (DeffatestSettings.IsAuthenticated)
                {
                    // Show logged in user
                    EditorGUILayout.LabelField($"✅ Logged in as: {DeffatestSettings.Email}");
                    EditorGUILayout.LabelField($"Plan: {DeffatestSettings.PlanDisplayName}");
                    EditorGUILayout.LabelField($"Tests remaining today: {DeffatestSettings.TestsRemaining}");

                    EditorGUILayout.Space(10);

                    if (GUILayout.Button("🔄 Refresh", GUILayout.Height(25)))
                    {
                        RefreshUserInfo();
                    }

                    EditorGUILayout.Space(5);

                    if (GUILayout.Button("🚪 Logout", dangerButtonStyle, GUILayout.Height(25)))
                    {
                        Logout();
                    }
                }
                else
                {
                    // API Key input
                    EditorGUILayout.LabelField("Enter your API Key:");
                    apiKeyInput = EditorGUILayout.PasswordField(apiKeyInput);

                    EditorGUILayout.Space(5);

                    EditorGUILayout.HelpBox(
                        "Get your API key from: deffatest.online/dashboard/settings/api-keys",
                        MessageType.Info
                    );

                    EditorGUILayout.Space(5);

                    GUI.enabled = !isVerifying && !string.IsNullOrEmpty(apiKeyInput);
                    
                    string verifyLabel = isVerifying ? "Verifying..." : "🔐 Verify API Key";
                    if (GUILayout.Button(verifyLabel, successButtonStyle, GUILayout.Height(35)))
                    {
                        VerifyApiKey();
                    }
                    
                    GUI.enabled = true;

                    if (!string.IsNullOrEmpty(verificationError))
                    {
                        EditorGUILayout.Space(5);
                        EditorGUILayout.HelpBox(verificationError, MessageType.Error);
                    }

                    EditorGUILayout.Space(10);

                    if (GUILayout.Button("🌐 Create Free Account", GUILayout.Height(25)))
                    {
                        Application.OpenURL($"{DeffatestConstants.WEBSITE_URL}/register");
                    }
                }
            });

            EditorGUILayout.Space(15);

            // Default Settings
            EditorGUILayout.LabelField("Default Settings", subHeaderStyle);
            EditorGUILayout.Space(5);

            DrawCard(() =>
            {
                // Default test type
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Default Test Type:", GUILayout.Width(130));
                int typeIndex = DeffatestSettings.GetTestTypeIndex(DeffatestSettings.DefaultTestType);
                int newTypeIndex = EditorGUILayout.Popup(typeIndex, DeffatestSettings.TestTypeDisplayNames);
                if (newTypeIndex != typeIndex)
                {
                    DeffatestSettings.DefaultTestType = DeffatestSettings.TestTypeOptions[newTypeIndex];
                }
                EditorGUILayout.EndHorizontal();

                // Default duration
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Default Duration:", GUILayout.Width(130));
                int durIndex = DeffatestSettings.GetDurationIndex(DeffatestSettings.DefaultDuration);
                int newDurIndex = EditorGUILayout.Popup(durIndex, DeffatestSettings.DurationDisplayNames);
                if (newDurIndex != durIndex)
                {
                    DeffatestSettings.DefaultDuration = DeffatestSettings.DurationOptions[newDurIndex];
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Space(5);

                // Auto-submit
                DeffatestSettings.AutoSubmitOnBuild = EditorGUILayout.Toggle(
                    "Auto-submit after build",
                    DeffatestSettings.AutoSubmitOnBuild
                );

                // Show notifications
                DeffatestSettings.ShowNotifications = EditorGUILayout.Toggle(
                    "Show notifications",
                    DeffatestSettings.ShowNotifications
                );

                // Auto-open report
                DeffatestSettings.AutoOpenReport = EditorGUILayout.Toggle(
                    "Auto-open report on completion",
                    DeffatestSettings.AutoOpenReport
                );

                // Analytics
                DeffatestSettings.AnalyticsEnabled = EditorGUILayout.Toggle(
                    "Send anonymous analytics",
                    DeffatestSettings.AnalyticsEnabled
                );
            });

            EditorGUILayout.Space(15);

            // Advanced
            EditorGUILayout.LabelField("Advanced", subHeaderStyle);
            EditorGUILayout.Space(5);

            DrawCard(() =>
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("API URL:", GUILayout.Width(100));
                DeffatestSettings.BaseURL = EditorGUILayout.TextField(DeffatestSettings.BaseURL);
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("WebSocket:", GUILayout.Width(100));
                DeffatestSettings.WebSocketURL = EditorGUILayout.TextField(DeffatestSettings.WebSocketURL);
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Space(5);

                if (GUILayout.Button("Reset to Defaults"))
                {
                    DeffatestSettings.BaseURL = "https://api.deffatest.online";
                    DeffatestSettings.WebSocketURL = "wss://api.deffatest.online/ws";
                }
            });
        }

        private void DrawAuthRequired()
        {
            EditorGUILayout.Space(20);
            
            EditorGUILayout.BeginVertical();
            GUILayout.FlexibleSpace();
            
            EditorGUILayout.LabelField("🔐 Authentication Required", 
                new GUIStyle(EditorStyles.boldLabel) { alignment = TextAnchor.MiddleCenter, fontSize = 16 });
            
            EditorGUILayout.Space(10);
            
            EditorGUILayout.LabelField("Please authenticate in the Settings tab to use Deffatest.", 
                new GUIStyle(EditorStyles.label) { alignment = TextAnchor.MiddleCenter });
            
            EditorGUILayout.Space(15);
            
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            
            if (GUILayout.Button("Go to Settings", GUILayout.Width(150), GUILayout.Height(30)))
            {
                currentTab = Tab.Settings;
            }
            
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndVertical();
        }

        #endregion

        #region Actions

        private void VerifyApiKey()
        {
            if (string.IsNullOrEmpty(apiKeyInput))
                return;

            if (!DeffatestSettings.ValidateApiKeyFormat(apiKeyInput))
            {
                verificationError = "Invalid API key format. Key should start with 'sk_live_' or 'sk_test_'";
                return;
            }

            isVerifying = true;
            verificationError = "";

            var tempClient = new DeffatestAPIClient(DeffatestSettings.BaseURL, apiKeyInput);

            EditorCoroutineUtility.StartCoroutine(
                tempClient.VerifyApiKey(
                    onSuccess: (userInfo) =>
                    {
                        isVerifying = false;
                        
                        // Save credentials
                        DeffatestSettings.ApiKey = apiKeyInput;
                        DeffatestSettings.SaveUserInfo(
                            userInfo.email,
                            userInfo.username,
                            userInfo.user_id,
                            userInfo.plan_type,
                            userInfo.tests_remaining
                        );

                        // Initialize API client
                        InitializeApiClient();

                        apiKeyInput = "";

                        EditorUtility.DisplayDialog(
                            "Success",
                            $"Welcome, {userInfo.username}!\n\nPlan: {userInfo.plan_type}\nTests remaining: {userInfo.tests_remaining}",
                            "OK"
                        );

                        Repaint();
                    },
                    onError: (error) =>
                    {
                        isVerifying = false;
                        verificationError = error;
                        Repaint();
                    }
                ),
                this
            );
        }

        private void Logout()
        {
            if (EditorUtility.DisplayDialog("Logout", "Are you sure you want to logout?", "Yes", "Cancel"))
            {
                DeffatestSettings.ClearAll();
                apiClient = null;
                DisconnectWebSocket();
                apiKeyInput = "";
                Repaint();
            }
        }

        private void RefreshUserInfo()
        {
            if (apiClient == null) return;

            EditorCoroutineUtility.StartCoroutine(
                apiClient.VerifyApiKey(
                    onSuccess: (userInfo) =>
                    {
                        DeffatestSettings.SaveUserInfo(
                            userInfo.email,
                            userInfo.username,
                            userInfo.user_id,
                            userInfo.plan_type,
                            userInfo.tests_remaining
                        );
                        Repaint();
                        ShowNotification(new GUIContent("User info refreshed!"), 2);
                    },
                    onError: (error) =>
                    {
                        Debug.LogError($"[Deffatest] Failed to refresh user info: {error}");
                    }
                ),
                this
            );
        }

        private void SubmitTest()
        {
            if (apiClient == null)
            {
                submissionError = "Not authenticated";
                return;
            }

            isSubmitting = true;
            submissionError = "";
            uploadProgress = 0f;

            string testType = DeffatestSettings.TestTypeOptions[selectedTestTypeIndex];
            string duration = DeffatestSettings.DurationOptions[selectedDurationIndex];

            EditorCoroutineUtility.StartCoroutine(
                apiClient.SubmitWebTest(
                    testUrl,
                    duration,
                    onSuccess: (testData) =>
                    {
                        isSubmitting = false;
                        
                        currentTestId = testData.id;
                        testStatus = testData.status;
                        testProgress = 0;
                        bugCounts = new DeffatestAPIClient.BugCounts();
                        isTestRunning = true;

                        // Switch to status tab
                        currentTab = Tab.Status;

                        // Connect WebSocket
                        ConnectWebSocket();

                        if (DeffatestSettings.ShowNotifications)
                        {
                            ShowNotification(new GUIContent("Test submitted successfully!"), 3);
                        }

                        Repaint();
                    },
                    onError: (error) =>
                    {
                        isSubmitting = false;
                        submissionError = error;
                        Repaint();
                    }
                ),
                this
            );
        }

        private void BuildAndSubmitAPK()
        {
            EditorUtility.DisplayDialog(
                "Build APK",
                "This will build an Android APK and submit it for testing.\n\n" +
                "Make sure you have:\n" +
                "• Android Build Support installed\n" +
                "• Scenes added to Build Settings\n" +
                "• Keystore configured (for signed builds)",
                "OK"
            );

            // Use DeffatestBuildUtility for actual build
            Debug.Log("[Deffatest] APK build not yet implemented in this version");
        }

        private void BuildWebGL()
        {
            EditorUtility.DisplayDialog(
                "Build WebGL",
                "This will build a WebGL version of your game.\n\n" +
                "After building, deploy to a web server and test using the URL.",
                "OK"
            );

            Debug.Log("[Deffatest] WebGL build helper coming soon");
        }

        private void CancelTest()
        {
            if (apiClient == null || string.IsNullOrEmpty(currentTestId))
                return;

            if (!EditorUtility.DisplayDialog("Cancel Test", "Are you sure you want to cancel this test?", "Yes", "No"))
                return;

            EditorCoroutineUtility.StartCoroutine(
                apiClient.CancelTest(
                    currentTestId,
                    onSuccess: () =>
                    {
                        testStatus = "cancelled";
                        isTestRunning = false;
                        DisconnectWebSocket();
                        ShowNotification(new GUIContent("Test cancelled"), 2);
                        Repaint();
                    },
                    onError: (error) =>
                    {
                        Debug.LogError($"[Deffatest] Failed to cancel test: {error}");
                    }
                ),
                this
            );
        }

        private void LoadTestHistory()
        {
            if (apiClient == null) return;

            isLoadingHistory = true;

            EditorCoroutineUtility.StartCoroutine(
                apiClient.GetTestList(
                    10,
                    onSuccess: (tests) =>
                    {
                        recentTests = tests;
                        isLoadingHistory = false;
                        Repaint();
                    },
                    onError: (error) =>
                    {
                        Debug.LogError($"[Deffatest] Failed to load history: {error}");
                        isLoadingHistory = false;
                        Repaint();
                    }
                ),
                this
            );
        }

        #endregion

        #region WebSocket

        private void ConnectWebSocket()
        {
            if (string.IsNullOrEmpty(currentTestId))
                return;

            DisconnectWebSocket();

            wsClient = new DeffatestWebSocketClient();

            wsClient.OnConnected += () =>
            {
                Debug.Log("[Deffatest] WebSocket connected");
            };

            wsClient.OnProgressUpdate += (progress, action) =>
            {
                testProgress = progress;
                currentAction = action;
                Repaint();
            };

            wsClient.OnBugFound += (bugAlert) =>
            {
                bugCounts = new DeffatestAPIClient.BugCounts
                {
                    critical = bugAlert.critical,
                    high = bugAlert.high,
                    medium = bugAlert.medium,
                    low = bugAlert.low,
                    total = bugAlert.total
                };

                if (DeffatestSettings.ShowNotifications && bugAlert.total > (bugCounts?.total ?? 0))
                {
                    ShowNotification(new GUIContent($"🐛 Bug found: {bugAlert.latest_bug_title}"), 3);
                }

                Repaint();
            };

            wsClient.OnTestComplete += (completeData) =>
            {
                testStatus = completeData.status;
                testProgress = 100;
                isTestRunning = false;
                reportUrl = completeData.report_url;

                bugCounts = new DeffatestAPIClient.BugCounts
                {
                    critical = completeData.critical,
                    high = completeData.high,
                    medium = completeData.medium,
                    low = completeData.low,
                    total = completeData.total
                };

                DisconnectWebSocket();

                if (DeffatestSettings.ShowNotifications)
                {
                    EditorUtility.DisplayDialog(
                        "Test Complete",
                        $"Test finished!\n\n" +
                        $"Bugs found: {completeData.total}\n" +
                        $"  🔴 Critical: {completeData.critical}\n" +
                        $"  🟠 High: {completeData.high}\n" +
                        $"  🟡 Medium: {completeData.medium}\n" +
                        $"  🟢 Low: {completeData.low}",
                        "View Report"
                    );

                    if (DeffatestSettings.AutoOpenReport && !string.IsNullOrEmpty(reportUrl))
                    {
                        Application.OpenURL(reportUrl);
                    }
                }

                Repaint();
            };

            wsClient.OnError += (error) =>
            {
                Debug.LogError($"[Deffatest] WebSocket error: {error}");
            };

            wsClient.Connect(DeffatestSettings.ApiKey, currentTestId);
        }

        private void DisconnectWebSocket()
        {
            if (wsClient != null)
            {
                wsClient.Disconnect();
                wsClient.Dispose();
                wsClient = null;
            }
        }

        private void StartStatusPolling()
        {
            ConnectWebSocket();
        }

        #endregion

        #region Helpers

        private void LoadSettings()
        {
            selectedDurationIndex = DeffatestSettings.GetDurationIndex(DeffatestSettings.DefaultDuration);
            selectedTestTypeIndex = DeffatestSettings.GetTestTypeIndex(DeffatestSettings.DefaultTestType);
        }

        private void InitializeApiClient()
        {
            if (DeffatestSettings.IsAuthenticated)
            {
                apiClient = new DeffatestAPIClient(
                    DeffatestSettings.BaseURL,
                    DeffatestSettings.ApiKey
                );
            }
        }

        private void LoadLogo()
        {
            logoTexture = Resources.Load<Texture2D>("DeffatestLogo");
        }

        private string GetStatusEmoji(string status)
        {
            return status?.ToLower() switch
            {
                "queued" => "⏸️",
                "running" => "⏳",
                "completed" => "✅",
                "failed" => "❌",
                "cancelled" => "⏹️",
                _ => "❓"
            };
        }

        private string GetStatusWithEmoji(string status)
        {
            return $"{GetStatusEmoji(status)} {status ?? "Unknown"}";
        }

        #endregion

        #region Styles

        private void InitializeStyles()
        {
            if (stylesInitialized) return;

            headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 16,
                normal = { textColor = new Color(0.2f, 0.6f, 1f) }
            };

            subHeaderStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 12
            };

            buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold
            };

            successButtonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.2f, 0.8f, 0.2f) }
            };

            dangerButtonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.9f, 0.3f, 0.3f) }
            };

            stylesInitialized = true;
        }

        private void DrawCard(Action content)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.Space(5);
            content?.Invoke();
            EditorGUILayout.Space(5);
            EditorGUILayout.EndVertical();
        }

        private void DrawSeparator()
        {
            Rect rect = EditorGUILayout.GetControlRect(GUILayout.Height(1));
            EditorGUI.DrawRect(rect, new Color(0.5f, 0.5f, 0.5f, 0.3f));
        }

        #endregion
    }
}
