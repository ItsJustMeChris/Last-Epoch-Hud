using MelonLoader;
using UnityEngine;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using HarmonyLib;
using Il2Cpp;
using Mod.Utils;

namespace Mod.Cheats
{
    /// <summary>
    /// Handles anti-idle timeout detection and prevention for Last Epoch
    /// </summary>
    internal static partial class AntiIdleSystem
    {
        private const bool VerboseHeartbeatLogs = false; // Toggle to re-enable detailed heartbeat write logs
        private const bool VerboseStatusLogs = false; // Toggle to re-enable status/info logs
        private const bool VerboseKeepaliveLogs = false; // Toggle to log successful synthetic keepalives
        private const bool VerboseActionLogs = false; // Toggle to log legacy action start/complete messages
        private const bool VerboseSuppressionLogs = false; // Toggle to log activity suppression and suppressed notifies
        #region Fields and Properties
        
        // Networking references (will be set by patches)
        private static object? _netMultiClient;
        private static object? _serverConnection;
        private static object? _netPeer;
        private static object? _clientNetworkService;
        // State tracking
        private static bool _isInitialized = false;
        private static float _lastStatusCheck = 0f;
        private static float _lastHeartbeat = 0f;
        private static float _lastAntiIdleAction = 0f;
        // private static float _lastSyntheticKeepAlive = 0f;
        private static float _nextInputNotifyAt = 0f;
        private static bool _loggedNoSendMethod;
        
        // Cache last observed delivery + channel from real traffic
        private static object? _lastDeliveryMethodObj;
        private static int _lastSequenceChannel = 0;
        private static float _lastDeliveryCaptureTime = 0f;
#pragma warning disable CS0414 // Naming Styles
        private static bool _isSendingSyntheticKeepalive = false; // TODO: Remove this
#pragma warning restore CS0414 // Naming Styles

        // Wrapper traffic observation
        private static float _lastWrapperSendTime = 0f;
        private static float _lastWrapperReceiveTime = 0f;
        private static readonly Dictionary<string, int> _wrapperKeyCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        private static float _lastWrapperKeySummaryTime = 0f;
        
        // Configuration
        private const float STATUS_CHECK_INTERVAL = 5f;    // Check connection status every 5 seconds
        private const float HEARTBEAT_INTERVAL = 30f;      // Send heartbeat every 30 seconds
        // Status tracking
        private static string? _lastConnectionStatus;
        private static int _consecutiveIdleDetections = 0;
        private static readonly List<string> _recentDisconnectReasons = new List<string>();
        private static double _lastHeartbeatValue = double.NaN;
        private static bool _isConnected = false;

        // Activity suppression
        private static float _suppressSyntheticUntil = 0f;
        private static float _lastInputCheck = 0f;
        private const float INPUT_CHECK_INTERVAL = 0.25f; // 4x per second
        private static Vector3 _lastMousePos;
        private static bool _lastMousePosInitialized = false;
        private static string _lastSuppressionReason = "unknown"; // TODO: Remove this
        
        #endregion
        
        #region Public Interface
        
        /// <summary>
        /// Returns true when only the Simple Anti-Idle (server input notify) mode is active.
        /// </summary>
        private static bool IsSimpleOnly => Settings.useSimpleAntiIdle && !Settings.useAntiIdle;
        
        /// <summary>
        /// Called from the mod's OnUpdate to perform anti-idle checks and actions
        /// </summary>
        public static void OnUpdate()
        {
            // Check if anti-idle is enabled
            if (!Settings.useAntiIdle && !Settings.useSimpleAntiIdle)
                return;
                
            if (!_isInitialized)
            {
                Initialize();
                return;
            }
            
            try
            {
                // Lightweight input/activity detection (suppresses synthetic keepalive only)
                DetectUserActivity();

                // Simple Anti-Idle UI pulse/notify
                if (Settings.useSimpleAntiIdle)
                {
                    if (_nextInputNotifyAt <= 0f)
                        ScheduleNextInputNotify();

                    if (Time.time >= _nextInputNotifyAt)
                    {
                        if (IsInputNotifyAllowed())
                        {
                            TryServerInputNotify();
                            ScheduleNextInputNotify();
                        }
                        else
                        {
                            // If suppressed, push next attempt beyond suppression window; otherwise short backoff
                            float remaining = GetSuppressionSecondsRemaining();
                            if (remaining > 0f)
                            {
                                if (VerboseSuppressionLogs)
#pragma warning disable CS0162 // Unreachable code detected
                                    MelonLogger.Msg($"[AntiIdle] Input notify suppressed ({_lastSuppressionReason}), remaining {remaining:F0}s");
#pragma warning restore CS0162 // Unreachable code detected
                                _nextInputNotifyAt = Time.time + Mathf.Max(5f, remaining + UnityEngine.Random.Range(1f, 3f));
                            }
                            else
                            {
                                // Not allowed for another reason (feature disabled mid-flight); back off a bit
                                _nextInputNotifyAt = Time.time + 15f;
                            }
                        }
                    }
                }

                // Skip legacy heartbeat/status/action when in Simple-only mode
                if (!IsSimpleOnly)
                {
                    // Check connection status periodically
                    if (Time.time - _lastStatusCheck >= STATUS_CHECK_INTERVAL)
                    {
                        CheckConnectionStatus();
                        _lastStatusCheck = Time.time;
                    }
                    
                    // Quiet heartbeat probe (local timer reset only)
                    if (Time.time - _lastHeartbeat >= HEARTBEAT_INTERVAL)
                    {
                        SendHeartbeat();
                        _lastHeartbeat = Time.time;
                    }
                    
                    // Perform periodic anti-idle action (legacy heartbeat reset path)
                    if (Settings.useAntiIdle)
                    {
                        var interval = Mathf.Max(Settings.antiIdleInterval, 10f); // Minimum 10 seconds
                        if (Time.time - _lastAntiIdleAction >= interval)
                        {
                            PerformAntiIdleAction();
                            _lastAntiIdleAction = Time.time;
                        }
                    }
                }

                // Legacy synthetic keepalive SENDER disabled.
                // The suppression window and read-only observation hooks remain for future research.
                // if (Settings.useSyntheticKeepAlive && !Settings.useSimpleAntiIdle) { /* disabled */ }

                // Periodic wrapper key usage summary (every ~60s)
                if (Time.time - _lastWrapperKeySummaryTime >= 60f && _wrapperKeyCounts.Count > 0)
                {
                    _lastWrapperKeySummaryTime = Time.time;
                    try
                    {
                        string? topKey = null;
                        int topCount = 0;
                        foreach (var kv in _wrapperKeyCounts)
                        {
                            if (kv.Value > topCount)
                            {
                                topKey = kv.Key;
                                topCount = kv.Value;
                            }
                        }

                        if (VerboseKeepaliveLogs && topKey != null)
#pragma warning disable CS0162 // Unreachable code detected
                            MelonLogger.Msg($"[AntiIdle] Wrapper traffic: top={topKey} count={topCount} (window ~60s)");
#pragma warning restore CS0162 // Unreachable code detected

                        _wrapperKeyCounts.Clear();
                    }
                    catch { }
                }
            }
            catch (Exception e)
            {
                MelonLogger.Error($"AntiIdleSystem.OnUpdate error: {e.Message}");
            }
        }
        
        /// <summary>
        /// Called by the mod on scene change to suppress keepalives for a short period
        /// </summary>
        public static void OnSceneChanged()
        {
            try
            {
                RegisterActivity(Settings.sceneChangeSuppressionSeconds, "scene-change");
            }
            catch (Exception e)
            {
                MelonLogger.Error($"AntiIdleSystem.OnSceneChanged error: {e.Message}");
            }
        }
        
        /// <summary>
        /// Called by wrapper when a MessageBuffer is sent
        /// </summary>
        public static void OnWrapperBufferSent(object messageKey, object method, int channel)
        {
            try
            {
                string keyName = messageKey?.ToString() ?? "<null>";
                string methodName = method?.ToString() ?? "<null>";
                string composite = $"key={keyName}|method={methodName}|ch={channel}";
                if (_wrapperKeyCounts.TryGetValue(composite, out var count))
                    _wrapperKeyCounts[composite] = count + 1;
                else
                    _wrapperKeyCounts[composite] = 1;
                _lastWrapperSendTime = Time.time;
            }
            catch (Exception e)
            {
                MelonLogger.Error($"AntiIdleSystem.OnWrapperBufferSent error: {e.Message}");
            }
        }
        
        /// <summary>
        /// Called by patches when connection status changes
        /// </summary>
        public static void OnConnectionStatusChanged(object? status)
        {
            try
            {
                if (IsSimpleOnly)
                {
                    // In Simple-only mode we do not engage legacy status-based actions
                    var statusStringSimple = status?.ToString() ?? "Unknown";
                    _lastConnectionStatus = statusStringSimple;
                    _isConnected = string.Equals(statusStringSimple, "Connected", StringComparison.OrdinalIgnoreCase);
                    return;
                }

                var statusString = status?.ToString() ?? "Unknown";
                
                // Only log if status actually changed
                if (_lastConnectionStatus != statusString)
                {
                    if (VerboseStatusLogs)
#pragma warning disable CS0162 // Unreachable code detected
                        MelonLogger.Msg($"[AntiIdle] Connection status changed from '{_lastConnectionStatus ?? "None"}' to '{statusString}'");
#pragma warning restore CS0162 // Unreachable code detected
                    _lastConnectionStatus = statusString;
                    _isConnected = string.Equals(statusString, "Connected", StringComparison.OrdinalIgnoreCase);
                    
                    // Also emit a one-time snapshot of key objects on status change only
                    if (VerboseStatusLogs)
                    {
#pragma warning disable CS0162 // Unreachable code detected
                        if (_netMultiClient != null)
                        {
                            var clientType = _netMultiClient.GetType();
                            MelonLogger.Msg($"[AntiIdle] NetMultiClient: {clientType.Name}");
                        }
                        else
                        {
                            MelonLogger.Msg("[AntiIdle] NetMultiClient: null");
                        }
#pragma warning restore CS0162 // Unreachable code detected
                    }

                    if (VerboseStatusLogs)
                    {
#pragma warning disable CS0162 // Unreachable code detected
                        if (_serverConnection != null)
                        {
                            var connType = _serverConnection.GetType();
                            MelonLogger.Msg($"[AntiIdle] ServerConnection: {connType.Name}");
                        }
                        else
                        {
                            MelonLogger.Msg("[AntiIdle] ServerConnection: null");
                        }
#pragma warning restore CS0162 // Unreachable code detected
                    }
                    
                    // Treat status transitions as activity (short suppression)
                    // if (_isConnected)
                    //     RegisterActivity(Mathf.Min(Settings.networkActivitySuppressionSeconds, 30f), "status-change");
                    
                    // Check if this is a concerning status
                    if (IsConcerningStatus(statusString))
                    {
                        _consecutiveIdleDetections++;
                        MelonLogger.Warning($"[AntiIdle] Concerning connection status detected: {statusString} (Count: {_consecutiveIdleDetections})");
                        
                        // Trigger immediate anti-idle action
                        PerformAntiIdleAction();
                    }
                    else
                    {
                        // Reset counter if status is good
                        _consecutiveIdleDetections = 0;
                    }
                }
            }
            catch (Exception e)
            {
                MelonLogger.Error($"AntiIdleSystem.OnConnectionStatusChanged error: {e.Message}");
            }
        }
        
        /// <summary>
        /// Called by patches when a disconnect is attempted
        /// </summary>
        public static void OnDisconnectAttempted(string? reason)
        {
            try
            {
                if (IsSimpleOnly)
                    return;

                MelonLogger.Warning($"[AntiIdle] Disconnect attempted - Reason: {reason ?? "No reason provided"}");
                
                // Track recent disconnect reasons for analysis
                _recentDisconnectReasons.Add($"{DateTime.Now:HH:mm:ss} - {reason ?? "Unknown"}");
                
                // Keep only last 10 reasons
                if (_recentDisconnectReasons.Count > 10)
                {
                    _recentDisconnectReasons.RemoveAt(0);
                }
                
                // If this looks like an idle timeout, trigger immediate action
                if (IsIdleTimeoutReason(reason))
                {
                    MelonLogger.Warning("[AntiIdle] Idle timeout detected! Triggering immediate anti-idle action.");
                    PerformAntiIdleAction();
                }
            }
            catch (Exception e)
            {
                MelonLogger.Error($"AntiIdleSystem.OnDisconnectAttempted error: {e.Message}");
            }
        }
        
        /// <summary>
        /// Called by patches when a message is sent
        /// </summary>
        public static void OnMessageSent(object message, object deliveryMethod, int sequenceChannel)
        {
            try
            {
                // Reset idle detection counter when we send messages
                if (_consecutiveIdleDetections > 0)
                {
                    MelonLogger.Msg($"[AntiIdle] Message sent - resetting idle detection counter");
                    _consecutiveIdleDetections = 0;
                }

                // Network traffic implies activity; suppress synthetic keepalive briefly
                // if (!_isSendingSyntheticKeepalive)
                //     RegisterActivity(Settings.networkActivitySuppressionSeconds, "network-send");

                // Capture delivery + channel to reuse for synthetic keepalive
                _lastDeliveryMethodObj = deliveryMethod;
                _lastSequenceChannel = sequenceChannel;
                _lastDeliveryCaptureTime = Time.time;

                // Timestamp wrapper send
                _lastWrapperSendTime = Time.time;
            }
            catch (Exception e)
            {
                MelonLogger.Error($"AntiIdleSystem.OnMessageSent error: {e.Message}");
            }
        }
        
        /// <summary>
        /// Called by wrapper when a network message is received.
        /// </summary>
        public static void OnWrapperReceive(object incomingMessage)
        {
            try
            {
                _lastWrapperReceiveTime = Time.time;
            }
            catch (Exception e)
            {
                MelonLogger.Error($"AntiIdleSystem.OnWrapperReceive error: {e.Message}");
            }
        }
        
        /// <summary>
        /// Called by patches when NetConnection status changes
        /// </summary>
        public static void OnNetConnectionStatusChanged(object connection, object status)
        {
            try
            {
                var statusString = status?.ToString() ?? "Unknown";
                if (VerboseStatusLogs)
#pragma warning disable CS0162 // Unreachable code detected
                    MelonLogger.Msg($"[AntiIdle] NetConnection status: {statusString}");
#pragma warning restore CS0162 // Unreachable code detected
                
                // Handle the same way as main connection status
                OnConnectionStatusChanged(status);
            }
            catch (Exception e)
            {
                MelonLogger.Error($"AntiIdleSystem.OnNetConnectionStatusChanged error: {e.Message}");
            }
        }
        
        /// <summary>
        /// Called by patches when Steam connection status changes
        /// </summary>
        public static void OnSteamConnectionStatusChanged(object data)
        {
            try
            {
                MelonLogger.Msg("[AntiIdle] Steam connection status changed");
                // TODO: Parse Steam networking data if needed
            }
            catch (Exception e)
            {
                MelonLogger.Error($"AntiIdleSystem.OnSteamConnectionStatusChanged error: {e.Message}");
            }
        }
        
        /// <summary>
        /// Called when the in-game settings menu is opened to register user presence.
        /// </summary>
        public static void OnMenuOpened()
        {
            try
            {
                if (!Settings.suppressKeepAliveOnActivity)
                    return;
                RegisterActivity(Settings.activitySuppressionSeconds, "menu");
            }
            catch (Exception e)
            {
                MelonLogger.Error($"AntiIdleSystem.OnMenuOpened error: {e.Message}");
            }
        }
        
        /// <summary>
        /// Called when the in-game settings menu is closed to register user presence.
        /// </summary>
        public static void OnMenuClosed()
        {
            try
            {
                if (!Settings.suppressKeepAliveOnActivity)
                    return;
                RegisterActivity(Settings.activitySuppressionSeconds, "menu");
            }
            catch (Exception e)
            {
                MelonLogger.Error($"AntiIdleSystem.OnMenuClosed error: {e.Message}");
            }
        }
        
        #endregion
        
        #region Patch Integration Methods
        
        /// <summary>
        /// Set the NetMultiClient instance (called by patches)
        /// </summary>
        public static void SetNetMultiClient(object netMultiClient)
        {
            try
            {
                if (_netMultiClient != netMultiClient)
                {
                    _netMultiClient = netMultiClient;
                    MelonLogger.Msg("[AntiIdle] NetMultiClient instance set");
                }
            }
            catch (Exception e)
            {
                MelonLogger.Error($"AntiIdleSystem.SetNetMultiClient error: {e.Message}");
            }
        }
        
        /// <summary>
        /// Set the server connection (called by patches)
        /// </summary>
        public static void SetServerConnection(object connection)
        {
            try
            {
                if (_serverConnection != connection)
                {
                    _serverConnection = connection;
                    MelonLogger.Msg("[AntiIdle] Server connection set");
                }
            }
            catch (Exception e)
            {
                MelonLogger.Error($"AntiIdleSystem.SetServerConnection error: {e.Message}");
            }
        }
        
        /// <summary>
        /// Set the ClientNetworkService wrapper (called by patches)
        /// </summary>
        public static void SetClientNetworkService(object service)
        {
            try
            {
                if (_clientNetworkService != service)
                {
                    _clientNetworkService = service;
                    MelonLogger.Msg("[AntiIdle] ClientNetworkService set");
                }
            }
            catch (Exception e)
            {
                MelonLogger.Error($"AntiIdleSystem.SetClientNetworkService error: {e.Message}");
            }
        }

        /// <summary>
        /// Clear stored connections (called by patches)
        /// </summary>
        public static void ClearConnections()
        {
            try
            {
                _netMultiClient = null;
                _serverConnection = null;
                _lastConnectionStatus = null;
                _consecutiveIdleDetections = 0;
                MelonLogger.Msg("[AntiIdle] Connections cleared");
            }
            catch (Exception e)
            {
                MelonLogger.Error($"AntiIdleSystem.ClearConnections error: {e.Message}");
            }
        }
        
        #endregion
        
        #region Private Implementation
        
        private static void Initialize()
        {
            try
            {
                MelonLogger.Msg("[AntiIdle] Initializing Anti-Idle System");
                _isInitialized = true;
                _lastStatusCheck = Time.time;
                _lastHeartbeat = Time.time;
                _lastAntiIdleAction = Time.time;
                _lastMousePosInitialized = false;
                if (Settings.useSimpleAntiIdle)
                {
                    _nextInputNotifyAt = Time.time + UnityEngine.Random.Range(25f, 45f);
                    MelonLogger.Msg("[AntiIdle] Server input notify scheduled shortly (~25-45s)");
                }
            }
            catch (Exception e)
            {
                MelonLogger.Error($"AntiIdleSystem.Initialize error: {e.Message}");
            }
        }
        
        // Helper: try to get Lidgren NetTime.Now; fallback to Unity time
        private static double GetNetworkNowSeconds()
        {
            try
            {
                var asm = _netMultiClient?.GetType().Assembly;
                if (asm == null)
                {
                    foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        try 
                        { 
                            var asmName = a.GetName()?.Name; 
                            if (!string.IsNullOrEmpty(asmName) && asmName.Contains("Lidgren", StringComparison.OrdinalIgnoreCase)) 
                            { 
                                asm = a; 
                                break; 
                            } 
                        }
                        catch { }
                    }
                }
                var netTimeType = asm?.GetType("Il2CppLidgren.Network.NetTime") ?? asm?.GetType("Lidgren.Network.NetTime");
                if (netTimeType != null)
                {
                    var nowProp = netTimeType.GetProperty("Now", BindingFlags.Public | BindingFlags.Static);
                    if (nowProp != null)
                    {
                        var val = nowProp.GetValue(null);
                        return Convert.ToDouble(val);
                    }
                    var getNow = netTimeType.GetMethod("get_Now", BindingFlags.Public | BindingFlags.Static);
                    if (getNow != null)
                    {
                        var val = getNow.Invoke(null, null);
                        return Convert.ToDouble(val);
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Msg($"[AntiIdle] NetTime.Now unavailable ({ex.Message}) - using Unity time");
            }
            try { return (double)Time.realtimeSinceStartup; } catch { }
            return DateTime.UtcNow.TimeOfDay.TotalSeconds;
        }
        
        private static void CheckConnectionStatus()
        {
            try
            {
                if (IsSimpleOnly)
                    return;
                // Lightweight periodic check
                
                // Parse networking state during status check
                ParseNetworkingState();
                
                // If we have too many consecutive idle detections, force an action
                if (_consecutiveIdleDetections >= 3)
                {
                    MelonLogger.Warning($"[AntiIdle] Multiple idle detections ({_consecutiveIdleDetections}) - forcing anti-idle action");
                    PerformAntiIdleAction();
                    _consecutiveIdleDetections = 0; // Reset after action
                }
            }
            catch (Exception e)
            {
                MelonLogger.Error($"AntiIdleSystem.CheckConnectionStatus error: {e.Message}");
            }
        }
        
        private static void SendHeartbeat()
        {
            try
            {
                if (IsSimpleOnly)
                    return;
                // Quiet heartbeat call
                
                // Try to reset the heartbeat timer
                ResetHeartbeatTimer();
            }
            catch (Exception e)
            {
                MelonLogger.Error($"AntiIdleSystem.SendHeartbeat error: {e.Message}");
            }
        }
        
        private static void PerformAntiIdleAction()
        {
            try
            {
                if (!Settings.useAntiIdle)
                    return;

                if (VerboseActionLogs)
#pragma warning disable CS0162 // Unreachable code detected
                    MelonLogger.Msg("[AntiIdle] Action: heartbeat reset + status snapshot");
#pragma warning restore CS0162 // Unreachable code detected

                // Try to reset the heartbeat timer by calling the game's internal heartbeat method
                ResetHeartbeatTimer();
                
                // Brief networking snapshot
                ParseNetworkingState();
                if (VerboseActionLogs)
#pragma warning disable CS0162 // Unreachable code detected
                    MelonLogger.Msg("[AntiIdle] Action complete");
#pragma warning restore CS0162 // Unreachable code detected
            }
            catch (Exception e)
            {
                MelonLogger.Error($"AntiIdleSystem.PerformAntiIdleAction error: {e.Message}");
            }
        }
        
        private static bool IsConcerningStatus(string status)
        {
            if (string.IsNullOrEmpty(status)) return false;
            
            var statusLower = status.ToLowerInvariant();
            
            // Check for concerning status keywords
            return statusLower.Contains("idle") ||
                   statusLower.Contains("disconnect") ||
                   statusLower.Contains("timeout") ||
                   statusLower.Contains("inactive") ||
                   statusLower.Contains("dropped");
        }
        
        private static bool IsIdleTimeoutReason(string? reason)
        {
            if (string.IsNullOrEmpty(reason)) return false;
            
            var reasonLower = reason.ToLowerInvariant();
            
            // Check for idle timeout keywords in disconnect reasons
            return reasonLower.Contains("idle") ||
                   reasonLower.Contains("timeout") ||
                   reasonLower.Contains("inactive") ||
                   reasonLower.Contains("no activity") ||
                   reasonLower.Contains("afk");
        }
        
        #endregion
        
        #region Debug and Logging
        
        /// <summary>
        /// Get debug information about the anti-idle system
        /// </summary>
        public static string GetDebugInfo()
        {
            return $"[AntiIdle] Status: Initialized={_isInitialized}, " +
                   $"NetMultiClient={_netMultiClient != null}, " +
                   $"ServerConnection={_serverConnection != null}, " +
                   $"ClientNetworkService={_clientNetworkService != null}, " +
                   $"LastStatus={_lastConnectionStatus ?? "None"}, " +
                   $"IdleDetections={_consecutiveIdleDetections}, " +
                   $"RecentDisconnects={_recentDisconnectReasons.Count}, " +
                   $"Suppressed={(Settings.suppressKeepAliveOnActivity && Time.time < _suppressSyntheticUntil)}, " +
                   $"LastWrapperSend={(Time.time - _lastWrapperSendTime):F1}s ago, LastWrapperRecv={(Time.time - _lastWrapperReceiveTime):F1}s ago";
        }
        
        #endregion
        
        #region Simple Input Notify

        private static void ScheduleNextInputNotify()
        {
            // Tie to the same interval but allow independent jitter; minimum 30s
            var baseInterval = Mathf.Max(Settings.simpleAntiIdleInterval, 60f);
            var jitter = UnityEngine.Random.Range(-8f, 8f);
            _nextInputNotifyAt = Time.time + Mathf.Max(30f, baseInterval * 0.75f + jitter);
            MelonLogger.Msg($"[AntiIdle] Next server input notify in {(_nextInputNotifyAt - Time.time):F0}s");
        }

        private static bool IsInputNotifyAllowed()
        {
            if (!Settings.useSimpleAntiIdle) return false;
            if (Settings.suppressKeepAliveOnActivity && Time.time < _suppressSyntheticUntil) return false;
            return true;
        }

        private static float GetSuppressionSecondsRemaining()
        {
            if (!Settings.suppressKeepAliveOnActivity) return 0f;
            return Mathf.Max(0f, _suppressSyntheticUntil - Time.time);
        }

        private static void TryServerInputNotify()
        {
            try
            {
                if (EpochInputManagerBridge.TrySendInputActionPerformed())
                {
                    MelonLogger.Msg("[AntiIdle] Server input notify invoked via EpochInputManager");
                    return;
                }

                if (!_loggedNoSendMethod)
                {
                    _loggedNoSendMethod = true;
                    MelonLogger.Warning("[AntiIdle] Input notify method not found (EpochInputManager)");
                }
            }
            catch (Exception e)
            {
                MelonLogger.Error($"[AntiIdle] ServerInputNotify error: {e.Message}");
            }
        }

        #endregion

    }
}
