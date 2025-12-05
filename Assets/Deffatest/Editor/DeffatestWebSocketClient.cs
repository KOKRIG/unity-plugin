using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Deffatest.Editor
{
    /// <summary>
    /// WebSocket client for real-time test updates.
    /// Uses native .NET WebSockets (Unity 2019.3+).
    /// Provides live progress, bug alerts, and completion notifications.
    /// </summary>
    public class DeffatestWebSocketClient : IDisposable
    {
        private ClientWebSocket webSocket;
        private CancellationTokenSource cancellationSource;
        private string testId;
        private string apiKey;
        private bool isConnected = false;
        private bool isDisposed = false;
        
        // Message queue for main thread processing
        private readonly Queue<WebSocketMessage> messageQueue = new Queue<WebSocketMessage>();
        private readonly object queueLock = new object();

        #region Events

        /// <summary>Fired when WebSocket connects successfully</summary>
        public event Action OnConnected;
        
        /// <summary>Fired when WebSocket disconnects</summary>
        public event Action<string> OnDisconnected;
        
        /// <summary>Fired on progress update (0-100)</summary>
        public event Action<int, string> OnProgressUpdate;
        
        /// <summary>Fired when new bugs are found</summary>
        public event Action<BugAlert> OnBugFound;
        
        /// <summary>Fired when test completes</summary>
        public event Action<TestCompleteData> OnTestComplete;
        
        /// <summary>Fired on screenshot captured (base64)</summary>
        public event Action<string, string> OnScreenshotCaptured;
        
        /// <summary>Fired on any error</summary>
        public event Action<string> OnError;

        #endregion

        #region Data Models

        [Serializable]
        public class WebSocketMessage
        {
            public string type;
            public string data;
            public long timestamp;
        }

        [Serializable]
        public class ProgressData
        {
            public int progress;
            public string current_action;
            public string elapsed;
            public string remaining;
        }

        [Serializable]
        public class BugAlert
        {
            public int critical;
            public int high;
            public int medium;
            public int low;
            public int total;
            public string latest_bug_title;
            public string latest_bug_severity;
        }

        [Serializable]
        public class TestCompleteData
        {
            public string test_id;
            public string status;
            public int critical;
            public int high;
            public int medium;
            public int low;
            public int total;
            public string report_url;
            public string duration;
        }

        [Serializable]
        public class ScreenshotData
        {
            public string test_id;
            public string screenshot_base64;
            public string description;
        }

        #endregion

        #region Connection

        /// <summary>
        /// Connect to WebSocket for test updates
        /// </summary>
        public async void Connect(string apiKey, string testId)
        {
            if (isConnected || isDisposed)
            {
                Debug.LogWarning("[Deffatest] WebSocket already connected or disposed");
                return;
            }

            this.apiKey = apiKey;
            this.testId = testId;

            try
            {
                webSocket = new ClientWebSocket();
                cancellationSource = new CancellationTokenSource();

                // Set authorization header
                webSocket.Options.SetRequestHeader("Authorization", $"Bearer {apiKey}");
                webSocket.Options.SetRequestHeader("X-Source", "unity-plugin");
                webSocket.Options.KeepAliveInterval = TimeSpan.FromSeconds(30);

                string wsUrl = $"{DeffatestSettings.WebSocketURL}?token={apiKey}";
                Uri uri = new Uri(wsUrl);

                Debug.Log($"[Deffatest] Connecting to WebSocket: {uri.Host}");

                await webSocket.ConnectAsync(uri, cancellationSource.Token);

                if (webSocket.State == WebSocketState.Open)
                {
                    isConnected = true;
                    Debug.Log("[Deffatest] WebSocket connected successfully");
                    
                    // Subscribe to test updates
                    await SendMessage(new { type = "join_test_room", test_id = testId });
                    
                    // Notify listeners
                    EnqueueEvent(() => OnConnected?.Invoke());

                    // Start receiving messages
                    _ = ReceiveLoop();
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[Deffatest] WebSocket connection failed: {e.Message}");
                EnqueueEvent(() => OnError?.Invoke(e.Message));
            }
        }

        /// <summary>
        /// Disconnect from WebSocket
        /// </summary>
        public async void Disconnect()
        {
            if (!isConnected || webSocket == null)
                return;

            try
            {
                isConnected = false;
                
                if (webSocket.State == WebSocketState.Open)
                {
                    // Leave test room
                    await SendMessage(new { type = "leave_test_room", test_id = testId });
                    
                    await webSocket.CloseAsync(
                        WebSocketCloseStatus.NormalClosure,
                        "Client disconnecting",
                        CancellationToken.None
                    );
                }

                Debug.Log("[Deffatest] WebSocket disconnected");
                EnqueueEvent(() => OnDisconnected?.Invoke("Client disconnected"));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Deffatest] WebSocket disconnect error: {e.Message}");
            }
            finally
            {
                cancellationSource?.Cancel();
            }
        }

        #endregion

        #region Message Handling

        private async Task SendMessage(object message)
        {
            if (webSocket?.State != WebSocketState.Open)
                return;

            try
            {
                string json = JsonUtility.ToJson(message);
                byte[] bytes = Encoding.UTF8.GetBytes(json);
                
                await webSocket.SendAsync(
                    new ArraySegment<byte>(bytes),
                    WebSocketMessageType.Text,
                    true,
                    cancellationSource.Token
                );
            }
            catch (Exception e)
            {
                Debug.LogError($"[Deffatest] Failed to send WebSocket message: {e.Message}");
            }
        }

        private async Task ReceiveLoop()
        {
            byte[] buffer = new byte[8192];

            try
            {
                while (isConnected && webSocket?.State == WebSocketState.Open)
                {
                    var result = await webSocket.ReceiveAsync(
                        new ArraySegment<byte>(buffer),
                        cancellationSource.Token
                    );

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        Debug.Log("[Deffatest] WebSocket closed by server");
                        EnqueueEvent(() => OnDisconnected?.Invoke("Server closed connection"));
                        break;
                    }

                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        string message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                        ProcessMessage(message);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Normal cancellation
            }
            catch (Exception e)
            {
                if (isConnected)
                {
                    Debug.LogError($"[Deffatest] WebSocket receive error: {e.Message}");
                    EnqueueEvent(() => OnError?.Invoke(e.Message));
                }
            }
            finally
            {
                isConnected = false;
            }
        }

        private void ProcessMessage(string rawMessage)
        {
            try
            {
                var message = JsonUtility.FromJson<WebSocketMessage>(rawMessage);
                
                if (message == null || string.IsNullOrEmpty(message.type))
                {
                    Debug.LogWarning($"[Deffatest] Invalid WebSocket message: {rawMessage}");
                    return;
                }

                switch (message.type)
                {
                    case "progress":
                    case "test_progress":
                        HandleProgressMessage(message.data ?? rawMessage);
                        break;

                    case "bugs_found":
                    case "bug_found":
                    case "bug_detected":
                        HandleBugMessage(message.data ?? rawMessage);
                        break;

                    case "test_complete":
                    case "test_completed":
                        HandleCompleteMessage(message.data ?? rawMessage);
                        break;

                    case "screenshot":
                    case "screenshot_captured":
                        HandleScreenshotMessage(message.data ?? rawMessage);
                        break;

                    case "pong":
                    case "heartbeat":
                        // Ignore heartbeat responses
                        break;

                    case "error":
                        Debug.LogError($"[Deffatest] Server error: {message.data}");
                        EnqueueEvent(() => OnError?.Invoke(message.data));
                        break;

                    default:
                        Debug.Log($"[Deffatest] Unknown message type: {message.type}");
                        break;
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[Deffatest] Failed to process WebSocket message: {e.Message}");
            }
        }

        private void HandleProgressMessage(string data)
        {
            try
            {
                var progressData = JsonUtility.FromJson<ProgressData>(data);
                EnqueueEvent(() => OnProgressUpdate?.Invoke(
                    progressData.progress, 
                    progressData.current_action ?? ""
                ));
            }
            catch (Exception e)
            {
                Debug.LogError($"[Deffatest] Failed to parse progress: {e.Message}");
            }
        }

        private void HandleBugMessage(string data)
        {
            try
            {
                var bugAlert = JsonUtility.FromJson<BugAlert>(data);
                EnqueueEvent(() => OnBugFound?.Invoke(bugAlert));
            }
            catch (Exception e)
            {
                Debug.LogError($"[Deffatest] Failed to parse bug alert: {e.Message}");
            }
        }

        private void HandleCompleteMessage(string data)
        {
            try
            {
                var completeData = JsonUtility.FromJson<TestCompleteData>(data);
                EnqueueEvent(() => OnTestComplete?.Invoke(completeData));
            }
            catch (Exception e)
            {
                Debug.LogError($"[Deffatest] Failed to parse completion: {e.Message}");
            }
        }

        private void HandleScreenshotMessage(string data)
        {
            try
            {
                var screenshotData = JsonUtility.FromJson<ScreenshotData>(data);
                EnqueueEvent(() => OnScreenshotCaptured?.Invoke(
                    screenshotData.screenshot_base64,
                    screenshotData.description
                ));
            }
            catch (Exception e)
            {
                Debug.LogError($"[Deffatest] Failed to parse screenshot: {e.Message}");
            }
        }

        #endregion

        #region Main Thread Queue

        private void EnqueueEvent(Action action)
        {
            lock (queueLock)
            {
                messageQueue.Enqueue(new WebSocketMessage { type = "event" });
            }
            
            // Use EditorApplication.delayCall to invoke on main thread
            UnityEditor.EditorApplication.delayCall += () => action?.Invoke();
        }

        /// <summary>
        /// Process queued messages on main thread.
        /// Call this from OnGUI or Update.
        /// </summary>
        public void ProcessQueue()
        {
            lock (queueLock)
            {
                messageQueue.Clear(); // Events are processed via delayCall
            }
        }

        #endregion

        #region Status

        public bool IsConnected => isConnected && webSocket?.State == WebSocketState.Open;

        public WebSocketState State => webSocket?.State ?? WebSocketState.None;

        #endregion

        #region Dispose

        public void Dispose()
        {
            if (isDisposed) return;
            isDisposed = true;

            Disconnect();
            
            cancellationSource?.Dispose();
            webSocket?.Dispose();
            
            webSocket = null;
            cancellationSource = null;
        }

        #endregion
    }
}
