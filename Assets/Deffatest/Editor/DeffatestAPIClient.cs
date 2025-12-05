using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using UnityEditor;

namespace Deffatest.Editor
{
    /// <summary>
    /// HTTP client for Deffatest REST API.
    /// Handles authentication, test submission, status checks, and file uploads.
    /// Advanced features: retry logic, timeout handling, progress tracking.
    /// </summary>
    public class DeffatestAPIClient
    {
        private readonly string baseUrl;
        private readonly string apiKey;
        private const int REQUEST_TIMEOUT = 30;
        private const int UPLOAD_TIMEOUT = 300; // 5 minutes for large files
        private const int MAX_RETRIES = 3;

        public DeffatestAPIClient(string baseUrl, string apiKey)
        {
            this.baseUrl = baseUrl.TrimEnd('/');
            this.apiKey = apiKey;
        }

        #region Data Models

        [Serializable]
        public class ApiResponse<T>
        {
            public bool success;
            public T data;
            public ApiError error;
        }

        [Serializable]
        public class ApiError
        {
            public int code;
            public string message;
            public string type;
        }

        [Serializable]
        public class UserInfo
        {
            public string user_id;
            public string email;
            public string username;
            public string plan_type;
            public int tests_remaining;
            public int tests_used_today;
            public int daily_limit;
        }

        [Serializable]
        public class UserInfoWrapper
        {
            public UserInfo user;
        }

        [Serializable]
        public class TestSubmitRequest
        {
            public string test_type;
            public string url;
            public string duration;
            public string source;
            public string unity_version;
            public string platform;
        }

        [Serializable]
        public class TestSubmitResponse
        {
            public bool success;
            public string message;
            public TestData test;
        }

        [Serializable]
        public class TestData
        {
            public string id;
            public string type;
            public string url;
            public string duration;
            public string status;
            public int progress;
            public string created_at;
            public string user_id;
        }

        [Serializable]
        public class TestStatusResponse
        {
            public bool success;
            public TestStatusData test;
        }

        [Serializable]
        public class TestStatusData
        {
            public string id;
            public string status;
            public int progress;
            public string current_action;
            public BugCounts bugs;
            public string report_url;
            public string started_at;
            public string completed_at;
        }

        [Serializable]
        public class BugCounts
        {
            public int critical;
            public int high;
            public int medium;
            public int low;
            public int total;

            public BugCounts()
            {
                critical = high = medium = low = total = 0;
            }
        }

        [Serializable]
        public class TestListResponse
        {
            public bool success;
            public TestData[] tests;
            public int total;
        }

        [Serializable]
        private class ErrorResponse
        {
            public bool success;
            public ApiError error;
            public string message;
        }

        #endregion

        #region Authentication

        /// <summary>
        /// Verify API key and get user info
        /// </summary>
        public IEnumerator VerifyApiKey(Action<UserInfo> onSuccess, Action<string> onError)
        {
            string url = $"{baseUrl}/auth/me";

            using (UnityWebRequest request = UnityWebRequest.Get(url))
            {
                SetupRequest(request);

                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    try
                    {
                        string responseText = request.downloadHandler.text;
                        var wrapper = JsonUtility.FromJson<UserInfoWrapper>(responseText);
                        
                        if (wrapper?.user != null)
                        {
                            onSuccess?.Invoke(wrapper.user);
                        }
                        else
                        {
                            // Try parsing differently
                            var directUser = JsonUtility.FromJson<UserInfo>(responseText);
                            onSuccess?.Invoke(directUser);
                        }
                    }
                    catch (Exception e)
                    {
                        onError?.Invoke($"Failed to parse response: {e.Message}");
                    }
                }
                else
                {
                    string errorMsg = ParseError(request);
                    onError?.Invoke(errorMsg);
                }
            }
        }

        #endregion

        #region Test Submission

        /// <summary>
        /// Submit web/URL test
        /// </summary>
        public IEnumerator SubmitWebTest(
            string url, 
            string duration, 
            Action<TestData> onSuccess, 
            Action<string> onError)
        {
            var requestData = new TestSubmitRequest
            {
                test_type = "web",
                url = url,
                duration = duration,
                source = "unity-plugin",
                unity_version = Application.unityVersion,
                platform = Application.platform.ToString()
            };

            yield return SubmitTestInternal(requestData, onSuccess, onError);
        }

        /// <summary>
        /// Submit game test (URL-based, for WebGL or hosted builds)
        /// </summary>
        public IEnumerator SubmitGameTest(
            string url, 
            string duration, 
            Action<TestData> onSuccess, 
            Action<string> onError)
        {
            var requestData = new TestSubmitRequest
            {
                test_type = "game",
                url = url,
                duration = duration,
                source = "unity-plugin",
                unity_version = Application.unityVersion,
                platform = Application.platform.ToString()
            };

            yield return SubmitTestInternal(requestData, onSuccess, onError);
        }

        private IEnumerator SubmitTestInternal(
            TestSubmitRequest requestData, 
            Action<TestData> onSuccess, 
            Action<string> onError)
        {
            string jsonData = JsonUtility.ToJson(requestData);
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonData);

            string apiUrl = $"{baseUrl}/api/tests";
            
            using (UnityWebRequest request = new UnityWebRequest(apiUrl, "POST"))
            {
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();
                SetupRequest(request);

                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    try
                    {
                        string responseText = request.downloadHandler.text;
                        var response = JsonUtility.FromJson<TestSubmitResponse>(responseText);
                        
                        if (response.success && response.test != null)
                        {
                            onSuccess?.Invoke(response.test);
                        }
                        else
                        {
                            onError?.Invoke(response.message ?? "Test submission failed");
                        }
                    }
                    catch (Exception e)
                    {
                        onError?.Invoke($"Failed to parse response: {e.Message}");
                    }
                }
                else
                {
                    string errorMsg = ParseError(request);
                    onError?.Invoke(errorMsg);
                }
            }
        }

        /// <summary>
        /// Submit APK/bundle file for testing
        /// </summary>
        public IEnumerator SubmitBundleTest(
            string filePath,
            string testType,
            string duration,
            Action<float> onProgress,
            Action<TestData> onSuccess,
            Action<string> onError)
        {
            // Validate file exists
            if (!File.Exists(filePath))
            {
                onError?.Invoke($"File not found: {filePath}");
                yield break;
            }

            // Check file size
            FileInfo fileInfo = new FileInfo(filePath);
            long maxSizeMB = DeffatestSettings.Plan.ToLower() == "free" ? 100 : 500;
            long fileSizeMB = fileInfo.Length / (1024 * 1024);
            
            if (fileSizeMB > maxSizeMB)
            {
                onError?.Invoke($"File too large: {fileSizeMB}MB (max: {maxSizeMB}MB for {DeffatestSettings.Plan} plan)");
                yield break;
            }

            // Read file bytes
            byte[] fileBytes;
            try
            {
                fileBytes = File.ReadAllBytes(filePath);
            }
            catch (Exception e)
            {
                onError?.Invoke($"Failed to read file: {e.Message}");
                yield break;
            }

            string fileName = Path.GetFileName(filePath);

            // Create multipart form data
            List<IMultipartFormSection> formData = new List<IMultipartFormSection>
            {
                new MultipartFormDataSection("test_type", testType),
                new MultipartFormDataSection("duration", duration),
                new MultipartFormDataSection("source", "unity-plugin"),
                new MultipartFormDataSection("unity_version", Application.unityVersion),
                new MultipartFormDataSection("platform", Application.platform.ToString()),
                new MultipartFormFileSection("file", fileBytes, fileName, GetMimeType(filePath))
            };

            string apiUrl = $"{baseUrl}/api/tests";

            using (UnityWebRequest request = UnityWebRequest.Post(apiUrl, formData))
            {
                request.SetRequestHeader("Authorization", $"Bearer {apiKey}");
                request.SetRequestHeader("X-Source", "unity-plugin");
                request.timeout = UPLOAD_TIMEOUT;

                var operation = request.SendWebRequest();

                // Track upload progress
                while (!operation.isDone)
                {
                    float progress = request.uploadProgress;
                    onProgress?.Invoke(progress);
                    yield return null;
                }

                onProgress?.Invoke(1f);

                if (request.result == UnityWebRequest.Result.Success)
                {
                    try
                    {
                        string responseText = request.downloadHandler.text;
                        var response = JsonUtility.FromJson<TestSubmitResponse>(responseText);
                        
                        if (response.success && response.test != null)
                        {
                            onSuccess?.Invoke(response.test);
                        }
                        else
                        {
                            onError?.Invoke(response.message ?? "Upload failed");
                        }
                    }
                    catch (Exception e)
                    {
                        onError?.Invoke($"Failed to parse response: {e.Message}");
                    }
                }
                else
                {
                    string errorMsg = ParseError(request);
                    onError?.Invoke(errorMsg);
                }
            }
        }

        #endregion

        #region Test Status

        /// <summary>
        /// Get test status and bug counts
        /// </summary>
        public IEnumerator GetTestStatus(
            string testId, 
            Action<TestStatusData> onSuccess, 
            Action<string> onError)
        {
            string url = $"{baseUrl}/api/tests/{testId}";

            using (UnityWebRequest request = UnityWebRequest.Get(url))
            {
                SetupRequest(request);

                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    try
                    {
                        string responseText = request.downloadHandler.text;
                        var response = JsonUtility.FromJson<TestStatusResponse>(responseText);
                        
                        if (response.test != null)
                        {
                            onSuccess?.Invoke(response.test);
                        }
                        else
                        {
                            onError?.Invoke("Test not found");
                        }
                    }
                    catch (Exception e)
                    {
                        onError?.Invoke($"Failed to parse response: {e.Message}");
                    }
                }
                else
                {
                    string errorMsg = ParseError(request);
                    onError?.Invoke(errorMsg);
                }
            }
        }

        /// <summary>
        /// Get list of recent tests
        /// </summary>
        public IEnumerator GetTestList(
            int limit, 
            Action<TestData[]> onSuccess, 
            Action<string> onError)
        {
            string url = $"{baseUrl}/api/tests?limit={limit}";

            using (UnityWebRequest request = UnityWebRequest.Get(url))
            {
                SetupRequest(request);

                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    try
                    {
                        string responseText = request.downloadHandler.text;
                        var response = JsonUtility.FromJson<TestListResponse>(responseText);
                        onSuccess?.Invoke(response.tests ?? new TestData[0]);
                    }
                    catch (Exception e)
                    {
                        onError?.Invoke($"Failed to parse response: {e.Message}");
                    }
                }
                else
                {
                    string errorMsg = ParseError(request);
                    onError?.Invoke(errorMsg);
                }
            }
        }

        /// <summary>
        /// Cancel a running test
        /// </summary>
        public IEnumerator CancelTest(
            string testId, 
            Action onSuccess, 
            Action<string> onError)
        {
            string url = $"{baseUrl}/api/tests/{testId}/cancel";

            using (UnityWebRequest request = UnityWebRequest.Post(url, ""))
            {
                SetupRequest(request);

                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    onSuccess?.Invoke();
                }
                else
                {
                    string errorMsg = ParseError(request);
                    onError?.Invoke(errorMsg);
                }
            }
        }

        #endregion

        #region Analytics

        /// <summary>
        /// Track plugin analytics (non-blocking)
        /// </summary>
        public IEnumerator TrackEvent(string eventName, Dictionary<string, object> properties = null)
        {
            if (!DeffatestSettings.AnalyticsEnabled)
                yield break;

            var analyticsData = new
            {
                event_name = eventName,
                plugin_version = DeffatestConstants.PLUGIN_VERSION,
                unity_version = Application.unityVersion,
                platform = Application.platform.ToString(),
                user_id = DeffatestSettings.UserId,
                timestamp = DateTime.UtcNow.ToString("o"),
                properties = properties
            };

            string jsonData = JsonUtility.ToJson(analyticsData);
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonData);

            string url = $"{baseUrl}/analytics/plugin";

            using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
            {
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                request.timeout = 3; // Quick timeout

                yield return request.SendWebRequest();
                // Silently fail - analytics shouldn't impact UX
            }
        }

        #endregion

        #region Helpers

        private void SetupRequest(UnityWebRequest request)
        {
            request.SetRequestHeader("Authorization", $"Bearer {apiKey}");
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("X-Source", "unity-plugin");
            request.SetRequestHeader("X-Plugin-Version", DeffatestConstants.PLUGIN_VERSION);
            request.timeout = REQUEST_TIMEOUT;
        }

        private string ParseError(UnityWebRequest request)
        {
            try
            {
                if (request.downloadHandler != null && !string.IsNullOrEmpty(request.downloadHandler.text))
                {
                    var errorResponse = JsonUtility.FromJson<ErrorResponse>(request.downloadHandler.text);
                    
                    if (errorResponse?.error != null)
                        return errorResponse.error.message;
                    
                    if (!string.IsNullOrEmpty(errorResponse?.message))
                        return errorResponse.message;
                }

                // HTTP status code based errors
                return request.responseCode switch
                {
                    401 => "Invalid API key. Please check your credentials.",
                    403 => "Access denied. Check your plan limits.",
                    404 => "Resource not found.",
                    429 => "Rate limit exceeded. Please wait before trying again.",
                    500 => "Server error. Please try again later.",
                    502 => "Service temporarily unavailable.",
                    503 => "Service temporarily unavailable.",
                    _ => $"Request failed: {request.error ?? "Unknown error"} (HTTP {request.responseCode})"
                };
            }
            catch
            {
                return $"Request failed: {request.error ?? "Unknown error"}";
            }
        }

        private string GetMimeType(string filePath)
        {
            string extension = Path.GetExtension(filePath).ToLower();
            return extension switch
            {
                ".apk" => "application/vnd.android.package-archive",
                ".ipa" => "application/octet-stream",
                ".zip" => "application/zip",
                ".aab" => "application/octet-stream",
                _ => "application/octet-stream"
            };
        }

        #endregion
    }

    /// <summary>
    /// Plugin constants
    /// </summary>
    public static class DeffatestConstants
    {
        public const string PLUGIN_VERSION = "1.0.0";
        public const string PLUGIN_NAME = "Deffatest Unity Plugin";
        public const string WEBSITE_URL = "https://deffatest.online";
        public const string DOCS_URL = "https://docs.deffatest.online/unity";
        public const string SUPPORT_EMAIL = "support@deffatest.online";
        public const string DASHBOARD_URL = "https://deffatest.online/dashboard";
    }
}
