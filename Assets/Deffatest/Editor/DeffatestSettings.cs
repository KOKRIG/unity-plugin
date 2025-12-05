using UnityEngine;
using UnityEditor;
using System;
using System.Security.Cryptography;
using System.Text;

namespace Deffatest.Editor
{
    /// <summary>
    /// Secure persistent settings for Deffatest Unity Plugin.
    /// API keys are encrypted before storage in EditorPrefs.
    /// More advanced than VSCode - includes encryption and validation.
    /// </summary>
    public static class DeffatestSettings
    {
        // EditorPrefs keys
        private const string API_KEY_PREF = "Deffatest_APIKey_Encrypted";
        private const string DEFAULT_DURATION_PREF = "Deffatest_DefaultDuration";
        private const string DEFAULT_TYPE_PREF = "Deffatest_DefaultType";
        private const string AUTO_SUBMIT_PREF = "Deffatest_AutoSubmit";
        private const string BASE_URL_PREF = "Deffatest_BaseURL";
        private const string WS_URL_PREF = "Deffatest_WebSocketURL";
        private const string EMAIL_PREF = "Deffatest_Email";
        private const string USERNAME_PREF = "Deffatest_Username";
        private const string PLAN_PREF = "Deffatest_Plan";
        private const string TESTS_REMAINING_PREF = "Deffatest_TestsRemaining";
        private const string USER_ID_PREF = "Deffatest_UserId";
        private const string LAST_VERIFIED_PREF = "Deffatest_LastVerified";
        private const string SHOW_NOTIFICATIONS_PREF = "Deffatest_ShowNotifications";
        private const string AUTO_OPEN_REPORT_PREF = "Deffatest_AutoOpenReport";
        private const string ANALYTICS_ENABLED_PREF = "Deffatest_AnalyticsEnabled";
        
        // Encryption key (derived from machine-specific data)
        private static readonly byte[] EntropyBytes = Encoding.UTF8.GetBytes(
            SystemInfo.deviceUniqueIdentifier.Substring(0, 16)
        );

        #region API Key (Encrypted)
        
        /// <summary>
        /// Encrypted API key storage. Uses machine-specific entropy.
        /// </summary>
        public static string ApiKey
        {
            get
            {
                string encrypted = EditorPrefs.GetString(API_KEY_PREF, "");
                if (string.IsNullOrEmpty(encrypted)) return "";
                
                try
                {
                    return DecryptString(encrypted);
                }
                catch
                {
                    // If decryption fails, clear corrupted data
                    EditorPrefs.DeleteKey(API_KEY_PREF);
                    return "";
                }
            }
            set
            {
                if (string.IsNullOrEmpty(value))
                {
                    EditorPrefs.DeleteKey(API_KEY_PREF);
                }
                else
                {
                    string encrypted = EncryptString(value);
                    EditorPrefs.SetString(API_KEY_PREF, encrypted);
                }
            }
        }

        #endregion

        #region User Info (Cached after verification)

        public static string Email
        {
            get => EditorPrefs.GetString(EMAIL_PREF, "");
            set => EditorPrefs.SetString(EMAIL_PREF, value);
        }

        public static string Username
        {
            get => EditorPrefs.GetString(USERNAME_PREF, "");
            set => EditorPrefs.SetString(USERNAME_PREF, value);
        }

        public static string UserId
        {
            get => EditorPrefs.GetString(USER_ID_PREF, "");
            set => EditorPrefs.SetString(USER_ID_PREF, value);
        }

        public static string Plan
        {
            get => EditorPrefs.GetString(PLAN_PREF, "free");
            set => EditorPrefs.SetString(PLAN_PREF, value);
        }

        public static int TestsRemaining
        {
            get => EditorPrefs.GetInt(TESTS_REMAINING_PREF, 0);
            set => EditorPrefs.SetInt(TESTS_REMAINING_PREF, value);
        }

        public static DateTime LastVerified
        {
            get
            {
                string ticks = EditorPrefs.GetString(LAST_VERIFIED_PREF, "0");
                return new DateTime(long.Parse(ticks));
            }
            set => EditorPrefs.SetString(LAST_VERIFIED_PREF, value.Ticks.ToString());
        }

        #endregion

        #region Default Settings

        public static string DefaultDuration
        {
            get => EditorPrefs.GetString(DEFAULT_DURATION_PREF, "2h");
            set => EditorPrefs.SetString(DEFAULT_DURATION_PREF, value);
        }

        public static string DefaultTestType
        {
            get => EditorPrefs.GetString(DEFAULT_TYPE_PREF, "game");
            set => EditorPrefs.SetString(DEFAULT_TYPE_PREF, value);
        }

        public static bool AutoSubmitOnBuild
        {
            get => EditorPrefs.GetBool(AUTO_SUBMIT_PREF, false);
            set => EditorPrefs.SetBool(AUTO_SUBMIT_PREF, value);
        }

        public static bool ShowNotifications
        {
            get => EditorPrefs.GetBool(SHOW_NOTIFICATIONS_PREF, true);
            set => EditorPrefs.SetBool(SHOW_NOTIFICATIONS_PREF, value);
        }

        public static bool AutoOpenReport
        {
            get => EditorPrefs.GetBool(AUTO_OPEN_REPORT_PREF, true);
            set => EditorPrefs.SetBool(AUTO_OPEN_REPORT_PREF, value);
        }

        public static bool AnalyticsEnabled
        {
            get => EditorPrefs.GetBool(ANALYTICS_ENABLED_PREF, true);
            set => EditorPrefs.SetBool(ANALYTICS_ENABLED_PREF, value);
        }

        #endregion

        #region API URLs

        public static string BaseURL
        {
            get => EditorPrefs.GetString(BASE_URL_PREF, "https://api.deffatest.online");
            set => EditorPrefs.SetString(BASE_URL_PREF, value);
        }

        public static string WebSocketURL
        {
            get => EditorPrefs.GetString(WS_URL_PREF, "wss://api.deffatest.online/ws");
            set => EditorPrefs.SetString(WS_URL_PREF, value);
        }

        #endregion

        #region Validation

        /// <summary>
        /// Check if user is authenticated with valid API key
        /// </summary>
        public static bool IsAuthenticated
        {
            get
            {
                string key = ApiKey;
                return !string.IsNullOrEmpty(key) && ValidateApiKeyFormat(key);
            }
        }

        /// <summary>
        /// Validate API key format (sk_live_... or sk_test_...)
        /// </summary>
        public static bool ValidateApiKeyFormat(string apiKey)
        {
            if (string.IsNullOrEmpty(apiKey)) return false;
            return apiKey.StartsWith("sk_live_") || apiKey.StartsWith("sk_test_");
        }

        /// <summary>
        /// Check if verification has expired (24 hours)
        /// </summary>
        public static bool IsVerificationExpired
        {
            get
            {
                if (LastVerified == DateTime.MinValue) return true;
                return (DateTime.Now - LastVerified).TotalHours > 24;
            }
        }

        /// <summary>
        /// Get plan display name with emoji
        /// </summary>
        public static string PlanDisplayName
        {
            get
            {
                return Plan.ToLower() switch
                {
                    "free" => "🆓 Free",
                    "pro" => "⭐ Pro",
                    "chaos" => "🔥 Chaos",
                    "enterprise" => "🏢 Enterprise",
                    _ => Plan
                };
            }
        }

        /// <summary>
        /// Get daily test limits based on plan
        /// </summary>
        public static int DailyTestLimit
        {
            get
            {
                return Plan.ToLower() switch
                {
                    "free" => 3,
                    "pro" => 33,
                    "chaos" => 100,
                    "enterprise" => 1000,
                    _ => 3
                };
            }
        }

        #endregion

        #region Actions

        /// <summary>
        /// Clear all settings (logout)
        /// </summary>
        public static void ClearAll()
        {
            EditorPrefs.DeleteKey(API_KEY_PREF);
            EditorPrefs.DeleteKey(EMAIL_PREF);
            EditorPrefs.DeleteKey(USERNAME_PREF);
            EditorPrefs.DeleteKey(USER_ID_PREF);
            EditorPrefs.DeleteKey(PLAN_PREF);
            EditorPrefs.DeleteKey(TESTS_REMAINING_PREF);
            EditorPrefs.DeleteKey(LAST_VERIFIED_PREF);
            
            Debug.Log("[Deffatest] User logged out, all credentials cleared.");
        }

        /// <summary>
        /// Save user info after successful verification
        /// </summary>
        public static void SaveUserInfo(string email, string username, string userId, string plan, int testsRemaining)
        {
            Email = email;
            Username = username;
            UserId = userId;
            Plan = plan;
            TestsRemaining = testsRemaining;
            LastVerified = DateTime.Now;
        }

        #endregion

        #region Encryption Helpers

        private static string EncryptString(string plainText)
        {
            try
            {
                byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
                
                // Use simple XOR encryption with entropy (works cross-platform)
                byte[] encrypted = new byte[plainBytes.Length];
                for (int i = 0; i < plainBytes.Length; i++)
                {
                    encrypted[i] = (byte)(plainBytes[i] ^ EntropyBytes[i % EntropyBytes.Length]);
                }
                
                return Convert.ToBase64String(encrypted);
            }
            catch (Exception e)
            {
                Debug.LogError($"[Deffatest] Encryption failed: {e.Message}");
                return "";
            }
        }

        private static string DecryptString(string encryptedText)
        {
            try
            {
                byte[] encryptedBytes = Convert.FromBase64String(encryptedText);
                
                // Reverse XOR
                byte[] decrypted = new byte[encryptedBytes.Length];
                for (int i = 0; i < encryptedBytes.Length; i++)
                {
                    decrypted[i] = (byte)(encryptedBytes[i] ^ EntropyBytes[i % EntropyBytes.Length]);
                }
                
                return Encoding.UTF8.GetString(decrypted);
            }
            catch (Exception e)
            {
                Debug.LogError($"[Deffatest] Decryption failed: {e.Message}");
                throw;
            }
        }

        #endregion

        #region Duration Helpers

        public static readonly string[] DurationOptions = { "30m", "1h", "2h", "6h", "12h" };
        public static readonly string[] DurationDisplayNames = { "30 minutes", "1 hour", "2 hours", "6 hours", "12 hours" };

        public static int GetDurationIndex(string duration)
        {
            int index = Array.IndexOf(DurationOptions, duration);
            return index >= 0 ? index : 2; // Default to 2h
        }

        public static readonly string[] TestTypeOptions = { "web", "mobile", "game" };
        public static readonly string[] TestTypeDisplayNames = { "Web (URL)", "Mobile (APK/IPA)", "Game (Build)" };

        public static int GetTestTypeIndex(string testType)
        {
            int index = Array.IndexOf(TestTypeOptions, testType);
            return index >= 0 ? index : 2; // Default to game
        }

        #endregion
    }
}
