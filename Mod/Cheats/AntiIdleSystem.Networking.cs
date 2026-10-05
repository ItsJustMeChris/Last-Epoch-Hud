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
    internal static partial class AntiIdleSystem
    {
        #region Heartbeat Management

        // Cached reflection bindings for heartbeat reset (resolved once per client type)
        private static bool _hbCacheResolved;
        private static Type? _hbCacheForType;
        private static FieldInfo? _hbField;           // m_lastHeartbeat on client
        private static PropertyInfo? _hbProp;          // m_lastHeartbeat property on client
        private static FieldInfo? _hbAltField;         // any double field containing "heartbeat"
        private static FieldInfo? _hbNetPeerField;     // field on client whose type contains "NetPeer"
        private static FieldInfo? _hbNpField;          // m_lastHeartbeat on NetPeer
        private static PropertyInfo? _hbNpProp;        // m_lastHeartbeat property on NetPeer
        private static MethodInfo[]? _hbMethods;       // heartbeat/keepalive/ping methods
        private static MethodInfo[]? _hbSetters;       // setter methods with heartbeat in name

        private static double ReadAsDouble(object? value)
        {
            if (value == null) return double.NaN;
            try { return Convert.ToDouble(value); } catch { return double.NaN; }
        }

        private static FieldInfo? FindFieldRecursive(Type type, string name)
        {
            for (var t = type; t != null; t = t.BaseType!)
            {
                var fi = t.GetField(name, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                if (fi != null) return fi;
            }
            return null;
        }

        private static List<FieldInfo> GetAllInstanceFields(Type type)
        {
            var result = new List<FieldInfo>();
            for (var t = type; t != null; t = t.BaseType!)
            {
                foreach (var f in t.GetFields(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                    result.Add(f);
            }
            return result;
        }

        private static void ResolveHeartbeatBindings(Type clientType)
        {
            if (_hbCacheResolved && _hbCacheForType == clientType) return;
            _hbCacheResolved = true;
            _hbCacheForType = clientType;

            _hbField = FindFieldRecursive(clientType, "m_lastHeartbeat");
            _hbProp = clientType.GetProperty("m_lastHeartbeat", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);

            var allFields = GetAllInstanceFields(clientType);

            _hbAltField = null;
            _hbNetPeerField = null;
            for (int i = 0; i < allFields.Count; i++)
            {
                var f = allFields[i];
                if (_hbAltField == null && f.FieldType == typeof(double) && f.Name.IndexOf("heartbeat", StringComparison.OrdinalIgnoreCase) >= 0)
                    _hbAltField = f;
                if (_hbNetPeerField == null && f.FieldType.Name.Contains("NetPeer"))
                    _hbNetPeerField = f;
            }

            // Resolve NetPeer-level heartbeat bindings if we found the peer field
            _hbNpField = null;
            _hbNpProp = null;
            if (_hbNetPeerField != null)
            {
                var peerType = _hbNetPeerField.FieldType;
                _hbNpField = FindFieldRecursive(peerType, "m_lastHeartbeat");
                _hbNpProp = peerType.GetProperty("m_lastHeartbeat", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
            }

            // Resolve heartbeat/keepalive methods
            var methods = clientType.GetMethods();
            var hbMethodList = new List<MethodInfo>();
            var setterList = new List<MethodInfo>();
            for (int i = 0; i < methods.Length; i++)
            {
                var m = methods[i];
                var nameLower = m.Name.ToLowerInvariant();
                if ((nameLower.Contains("heartbeat") || nameLower.Contains("keepalive") || nameLower.Contains("ping"))
                    && !nameLower.StartsWith("get_"))
                {
                    hbMethodList.Add(m);
                }
                if (nameLower.Contains("set") && nameLower.Contains("heartbeat"))
                {
                    setterList.Add(m);
                }
            }
            _hbMethods = hbMethodList.Count > 0 ? hbMethodList.ToArray() : null;
            _hbSetters = setterList.Count > 0 ? setterList.ToArray() : null;
        }

        private static void ResetHeartbeatTimer()
        {
            try
            {
                if (_netMultiClient == null) return;

                var clientType = _netMultiClient.GetType();
                ResolveHeartbeatBindings(clientType);

                bool resetSucceeded = false;
                var nowSeconds = GetNetworkNowSeconds();

                // Try direct field
                if (_hbField != null)
                {
                    try
                    {
                        _hbField.SetValue(_netMultiClient, nowSeconds);
                        var after = ReadAsDouble(_hbField.GetValue(_netMultiClient));
                        resetSucceeded = !double.IsNaN(after) && Math.Abs(after - nowSeconds) <= 1.5;
                    }
                    catch { }
                }

                // Try direct property
                if (!resetSucceeded && _hbProp != null && _hbProp.CanWrite)
                {
                    try
                    {
                        _hbProp.SetValue(_netMultiClient, nowSeconds);
                        var after = ReadAsDouble(_hbProp.GetValue(_netMultiClient));
                        resetSucceeded = !double.IsNaN(after) && Math.Abs(after - nowSeconds) <= 1.5;
                    }
                    catch { }
                }

                // Try alternative heartbeat field
                if (!resetSucceeded && _hbAltField != null)
                {
                    try
                    {
                        _hbAltField.SetValue(_netMultiClient, nowSeconds);
                        var after = ReadAsDouble(_hbAltField.GetValue(_netMultiClient));
                        resetSucceeded = !double.IsNaN(after) && Math.Abs(after - nowSeconds) <= 1.5;
                    }
                    catch { }
                }

                // Try NetPeer heartbeat
                if (!resetSucceeded && _hbNetPeerField != null)
                {
                    try
                    {
                        var netPeerInstance = _hbNetPeerField.GetValue(_netMultiClient);
                        if (netPeerInstance != null)
                        {
                            SetNetPeer(netPeerInstance);
                            if (_hbNpField != null)
                            {
                                _hbNpField.SetValue(netPeerInstance, nowSeconds);
                                var after = ReadAsDouble(_hbNpField.GetValue(netPeerInstance));
                                resetSucceeded = !double.IsNaN(after) && Math.Abs(after - nowSeconds) <= 1.5;
                            }
                            else if (_hbNpProp != null && _hbNpProp.CanWrite)
                            {
                                _hbNpProp.SetValue(netPeerInstance, nowSeconds);
                                var after = ReadAsDouble(_hbNpProp.GetValue(netPeerInstance));
                                resetSucceeded = !double.IsNaN(after) && Math.Abs(after - nowSeconds) <= 1.5;
                            }
                        }
                    }
                    catch { }
                }

                // Try heartbeat/keepalive/ping methods
                if (!resetSucceeded && _hbMethods != null)
                {
                    for (int i = 0; i < _hbMethods.Length; i++)
                    {
                        try
                        {
                            _hbMethods[i].Invoke(_netMultiClient, null);
                            resetSucceeded = true;
                            break;
                        }
                        catch { }
                    }
                }

                // Try setter methods with heartbeat in name
                if (!resetSucceeded && _hbSetters != null)
                {
                    for (int i = 0; i < _hbSetters.Length; i++)
                    {
                        try
                        {
                            var ps = _hbSetters[i].GetParameters();
                            if (ps.Length == 1 && ps[0].ParameterType == typeof(double))
                            {
                                _hbSetters[i].Invoke(_netMultiClient, new object[] { nowSeconds });
                                resetSucceeded = true;
                                break;
                            }
                            else if (ps.Length == 1 && ps[0].ParameterType == typeof(float))
                            {
                                _hbSetters[i].Invoke(_netMultiClient, new object[] { (float)nowSeconds });
                                resetSucceeded = true;
                                break;
                            }
                            else if (ps.Length == 0)
                            {
                                _hbSetters[i].Invoke(_netMultiClient, null);
                                resetSucceeded = true;
                                break;
                            }
                        }
                        catch { }
                    }
                }

                TryResetTimeoutOnConnection(nowSeconds);
            }
            catch (Exception e)
            {
                MelonLogger.Error($"AntiIdleSystem.ResetHeartbeatTimer error: {e.Message}");
            }
        }

        private static void TryResetTimeoutOnConnection(double nowSeconds)
        {
            try
            {
                if (_serverConnection == null) return;
                var connType = _serverConnection.GetType();

                // Extend any timeout deadline fields we can find
                foreach (var f in connType.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public))
                {
                    var name = f.Name.ToLowerInvariant();
                    if (f.FieldType == typeof(double) || f.FieldType == typeof(float))
                    {
                        // timeout-like deadlines: push out by 300s
                        if (name.Contains("timeout") || name.Contains("deadline") || name.Contains("idle"))
                        {
                            try
                            {
                                var before = Convert.ToDouble(f.GetValue(_serverConnection));
                                var target = nowSeconds + 300.0;
                                if (f.FieldType == typeof(double)) f.SetValue(_serverConnection, target);
                                else f.SetValue(_serverConnection, (float)target);
                                var after = Convert.ToDouble(f.GetValue(_serverConnection));
                                // Quiet log to avoid spam; only log significant changes
                                if (Math.Abs(after - before) > 1.0)
                                    MelonLogger.Msg($"[AntiIdle] Timeout field '{f.Name}' adjusted: {before:F1} -> {after:F1}");
                            }
                            catch { }
                        }

                        // last-heard/last-activity markers: set to now
                        if (name.Contains("last") && (name.Contains("heard") || name.Contains("receive") || name.Contains("activity") || name.Contains("seen")))
                        {
                            try
                            {
                                if (f.FieldType == typeof(double)) f.SetValue(_serverConnection, nowSeconds);
                                else f.SetValue(_serverConnection, (float)nowSeconds);
                            }
                            catch { }
                        }
                    }
                }

                // Try common properties too
                foreach (var p in connType.GetProperties(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public))
                {
                    var name = p.Name.ToLowerInvariant();
                    if (!p.CanWrite) continue;
                    var pt = p.PropertyType;
                    if (pt == typeof(double) || pt == typeof(float))
                    {
                        try
                        {
                            if (name.Contains("timeout") || name.Contains("deadline") || name.Contains("idle"))
                            {
                                var target = nowSeconds + 300.0;
                                if (pt == typeof(double)) p.SetValue(_serverConnection, target);
                                else p.SetValue(_serverConnection, (float)target);
                            }
                            if (name.Contains("last") && (name.Contains("heard") || name.Contains("receive") || name.Contains("activity") || name.Contains("seen")))
                            {
                                if (pt == typeof(double)) p.SetValue(_serverConnection, nowSeconds);
                                else p.SetValue(_serverConnection, (float)nowSeconds);
                            }
                        }
                        catch { }
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"AntiIdleSystem.TryResetTimeoutOnConnection error: {ex.Message}");
            }
        }

        private static void SendSyntheticKeepAlive()
        {
            try
            {
                if (_netMultiClient == null || !_isConnected) return;
                var clientType = _netMultiClient.GetType();

                // Create an empty user message via CreateMessage if available
                object? msg = null;
                var createMsgNoArg = clientType.GetMethod("CreateMessage", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
                var createMsgInt = clientType.GetMethod("CreateMessage", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new[] { typeof(int) }, null);
                if (createMsgNoArg != null)
                    msg = createMsgNoArg.Invoke(_netMultiClient, null);
                else if (createMsgInt != null)
                    msg = createMsgInt.Invoke(_netMultiClient, new object[] { 1 }); // reserve 1 byte

                // Try via NetPeer if client couldn't create message
                if (msg == null)
                {
                    var peerField = clientType.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                        .FirstOrDefault(f => f.FieldType.Name.Contains("NetPeer"));
                    var peer = peerField?.GetValue(_netMultiClient);
                    if (peer != null)
                    {
                        var peerType = peer.GetType();
                        var pCreateMsgNoArg = peerType.GetMethod("CreateMessage", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
                        var pCreateMsgInt = peerType.GetMethod("CreateMessage", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new[] { typeof(int) }, null);
                        if (pCreateMsgNoArg != null) msg = pCreateMsgNoArg.Invoke(peer, null);
                        else if (pCreateMsgInt != null) msg = pCreateMsgInt.Invoke(peer, new object[] { 1 });
                    }
                }

                if (msg == null) return;

                // Write a tiny payload to avoid empty-message drops (best-effort)
                try
                {
                    var writeByte = msg.GetType().GetMethod("Write", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new[] { typeof(byte) }, null);
                    writeByte?.Invoke(msg, new object[] { (byte)0 });
                }
                catch { }

                bool sent = false;
                string? lastError = null;

                // Preferred delivery: use last observed if compatible; else ReliableUnordered; fallback to Unreliable
                object? deliveryPreferred = _lastDeliveryMethodObj;
                object? deliveryReliable = null;
                object? deliveryUnreliable = null;
                try
                {
                    var deliveryEnum = clientType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                        .FirstOrDefault(m => m.Name == "SendMessage" && m.GetParameters().Length == 3)?.GetParameters()[1].ParameterType;
                    if (deliveryEnum != null && deliveryEnum.IsEnum)
                    {
                        foreach (var name in Enum.GetNames(deliveryEnum))
                        {
                            if (name.Equals("ReliableUnordered", StringComparison.OrdinalIgnoreCase)) deliveryReliable = Enum.Parse(deliveryEnum, name);
                            if (name.Equals("Unreliable", StringComparison.OrdinalIgnoreCase)) deliveryUnreliable = Enum.Parse(deliveryEnum, name);
                        }
                        if (deliveryPreferred != null && deliveryPreferred.GetType() != deliveryEnum)
                        {
                            // Incompatible cached enum: ignore
                            deliveryPreferred = null;
                        }
                    }
                }
                catch { }

                // Try direct NetConnection.SendMessage first, if available
                try
                {
                    _isSendingSyntheticKeepalive = true;
                    if (_serverConnection != null)
                    {
                        var connType = _serverConnection.GetType();
                        var connSend = connType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                            .FirstOrDefault(m => m.Name == "SendMessage" && m.GetParameters().Length == 3);
                        if (connSend != null)
                        {
                            var ps = connSend.GetParameters();
                            var deliveryType = ps[1].ParameterType;
                            object delivery = (deliveryPreferred != null && deliveryPreferred.GetType() == deliveryType)
                                ? deliveryPreferred
                                : (deliveryReliable ?? (deliveryType.IsEnum ? Enum.Parse(deliveryType, "ReliableUnordered") : (object)0));
                            var channel = (_lastDeliveryCaptureTime > 0f) ? _lastSequenceChannel : 0;
                            var result = connSend.Invoke(_serverConnection, new object[] { msg, delivery, channel });
                            var ok = result?.ToString()?.Contains("Sent", StringComparison.OrdinalIgnoreCase) == true || result?.ToString()?.Contains("Queued", StringComparison.OrdinalIgnoreCase) == true;
                            sent = ok;
                            if (!ok)
                                lastError = $"ConnSend result={result?.ToString() ?? "null"}";
                        }
                    }
                }
                catch (Exception ex)
                {
                    lastError = $"ConnSend error: {ex.Message}";
                }
                finally { _isSendingSyntheticKeepalive = false; }

                // Try 4-arg overload first: SendMessage(msg, connection, method, channel)
                try
                {
                    _isSendingSyntheticKeepalive = true;
                    var send4 = clientType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                        .FirstOrDefault(m => m.Name == "SendMessage" && m.GetParameters().Length == 4 && m.GetParameters()[1].ParameterType.Name.Contains("NetConnection"));
                    if (!sent && send4 != null && _serverConnection != null)
                    {
                        var ps = send4.GetParameters();
                        var channel = (_lastDeliveryCaptureTime > 0f) ? _lastSequenceChannel : 0;
                        var paramDeliveryType = ps[2].ParameterType;
                        var delivery = (deliveryPreferred != null && deliveryPreferred.GetType() == paramDeliveryType)
                            ? deliveryPreferred
                            : (deliveryReliable ?? deliveryUnreliable ?? paramDeliveryType.GetEnumValues().GetValue(0));
                        var result = send4.Invoke(_netMultiClient, new object[] { msg, _serverConnection, delivery!, channel });
                        var ok = result?.ToString()?.Contains("Sent", StringComparison.OrdinalIgnoreCase) == true || result?.ToString()?.Contains("Queued", StringComparison.OrdinalIgnoreCase) == true;
                        sent = ok;
                        if (!ok)
                            lastError = $"Send4 result={result?.ToString() ?? "null"}";
                    }
                }
                catch (Exception ex)
                {
                    lastError = $"Send4 error: {ex.Message}";
                }
                finally { _isSendingSyntheticKeepalive = false; }

                // Try 3-arg overload: SendMessage(msg, method, channel)
                if (!sent)
                {
                    try
                    {
                        _isSendingSyntheticKeepalive = true;
                        var send3 = clientType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                            .FirstOrDefault(m => m.Name == "SendMessage" && m.GetParameters().Length == 3);
                        if (send3 != null)
                        {
                            var ps = send3.GetParameters();
                            var channel = (_lastDeliveryCaptureTime > 0f) ? _lastSequenceChannel : 0;
                            var paramDeliveryType = ps[1].ParameterType;
                            var delivery = (deliveryPreferred != null && deliveryPreferred.GetType() == paramDeliveryType)
                                ? deliveryPreferred
                                : (deliveryReliable ?? deliveryUnreliable ?? paramDeliveryType.GetEnumValues().GetValue(0));
                            var result = send3.Invoke(_netMultiClient, new object[] { msg, delivery!, channel });
                            var ok = result?.ToString()?.Contains("Sent", StringComparison.OrdinalIgnoreCase) == true || result?.ToString()?.Contains("Queued", StringComparison.OrdinalIgnoreCase) == true;
                            sent = ok;
                            if (!ok)
                                lastError = $"Send3 result={result?.ToString() ?? "null"}";
                        }
                    }
                    catch (Exception ex)
                    {
                        lastError = $"Send3 error: {ex.Message}";
                    }
                    finally { _isSendingSyntheticKeepalive = false; }
                }

                // Peer-level send as last resort
                if (!sent)
                {
                    try
                    {
                        _isSendingSyntheticKeepalive = true;
                        var peerField = clientType.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                            .FirstOrDefault(f => f.FieldType.Name.Contains("NetPeer"));
                        var peer = peerField?.GetValue(_netMultiClient);
                        if (peer != null)
                        {
                            var peerType = peer.GetType();
                            var sendPeer = peerType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                                .FirstOrDefault(m => m.Name == "SendMessage" && m.GetParameters().Length >= 3);
                            if (sendPeer != null)
                            {
                                var ps = sendPeer.GetParameters();
                                object delivery;
                                if (_lastDeliveryCaptureTime > 0f && _lastDeliveryMethodObj != null && ps[1].ParameterType.IsEnum && _lastDeliveryMethodObj.GetType() == ps[1].ParameterType)
                                    delivery = _lastDeliveryMethodObj;
                                else
                                    delivery = ps[1].ParameterType.IsEnum ? Enum.Parse(ps[1].ParameterType, "ReliableUnordered") : (object)0;
                                int channel = (_lastDeliveryCaptureTime > 0f) ? _lastSequenceChannel : 0;
                                object? result;
                                if (ps.Length == 3)
                                    result = sendPeer.Invoke(peer, new object[] { msg, delivery, channel });
                                else if (ps.Length == 4 && ps[1].ParameterType.Name.Contains("NetConnection") && _serverConnection != null)
                                    result = sendPeer.Invoke(peer, new object[] { msg, _serverConnection, delivery, channel });
                                else
                                    result = sendPeer.Invoke(peer, new object[] { msg, delivery, channel });

                                var ok = result?.ToString()?.Contains("Sent", StringComparison.OrdinalIgnoreCase) == true || result?.ToString()?.Contains("Queued", StringComparison.OrdinalIgnoreCase) == true;
                                sent = ok;
                                if (!ok)
                                    lastError = $"SendPeer result={result?.ToString() ?? "null"}";
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        lastError = $"SendPeer error: {ex.Message}";
                    }
                    finally { _isSendingSyntheticKeepalive = false; }
                }

                // As a final fallback, try invoking Ping/KeepAlive methods on connection or peer
                if (!sent)
                {
                    try
                    {
                        bool invoked = false;
                        if (_serverConnection != null)
                        {
                            var ct = _serverConnection.GetType();
                            var ping = ct.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                                .FirstOrDefault(m => m.Name.IndexOf("ping", StringComparison.OrdinalIgnoreCase) >= 0 || m.Name.IndexOf("keepalive", StringComparison.OrdinalIgnoreCase) >= 0);
                            if (ping != null)
                            {
                                ping.Invoke(_serverConnection, null);
                                invoked = true;
                            }
                        }
                        if (!invoked && _netPeer != null)
                        {
                            var pt = _netPeer.GetType();
                            var ping = pt.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                                .FirstOrDefault(m => m.Name.IndexOf("ping", StringComparison.OrdinalIgnoreCase) >= 0 || m.Name.IndexOf("keepalive", StringComparison.OrdinalIgnoreCase) >= 0);
                            ping?.Invoke(_netPeer, null);
                        }
                    }
                    catch { }
                }

                if (!sent && lastError != null)
                {
                    MelonLogger.Warning($"[AntiIdle] Keepalive send failed: {lastError}");
                }
                else if (sent && VerboseKeepaliveLogs)
                {
                    MelonLogger.Msg("[AntiIdle] Keepalive sent successfully");
                }

                // Also reset heartbeat right after
                ResetHeartbeatTimer();
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"AntiIdleSystem.SendSyntheticKeepAlive error: {ex.Message}");
            }
        }

        #endregion

        #region State Parsing Methods

        private static void ParseNetworkingState()
        {
            try
            {
                // Minimal networking snapshot (quiet): update internal heartbeat tracking only
                if (_netMultiClient != null)
                {
                    var clientType = _netMultiClient.GetType();
                    var lastHeartbeatField = clientType.GetField("m_lastHeartbeat");
                    if (lastHeartbeatField != null)
                    {
                        var lastHeartbeatValue = lastHeartbeatField.GetValue(_netMultiClient);
                        if (lastHeartbeatValue != null)
                        {
                            _lastHeartbeatValue = Convert.ToDouble(lastHeartbeatValue);
                        }
                    }
                }
                // Do not log NetMultiClient/ServerConnection here; only on status change
            }
            catch (Exception e)
            {
                MelonLogger.Error($"AntiIdleSystem.ParseNetworkingState error: {e.Message}");
            }
        }

        // UI/Player state logging removed to reduce noise

        #region NetPeer Integration

        /// <summary>
        /// Called by NetPeer patches to set the NetPeer instance
        /// </summary>
        public static void SetNetPeer(object netPeer)
        {
            try
            {
                _netPeer = netPeer;
            }
            catch (Exception e)
            {
                MelonLogger.Error($"AntiIdleSystem.SetNetPeer error: {e.Message}");
            }
        }

        /// <summary>
        /// Called by NetPeer getter patch when heartbeat is read
        /// </summary>
        public static void OnHeartbeatRead(double value)
        {
            try
            {
                // Log significant heartbeat changes
                if (!double.IsNaN(_lastHeartbeatValue) && Math.Abs(value - _lastHeartbeatValue) > 10.0)
                {
                    MelonLogger.Msg($"[AntiIdle] Heartbeat read: {value:F2} (Change: {value - _lastHeartbeatValue:F2})");
                }
                _lastHeartbeatValue = value;
            }
            catch (Exception e)
            {
                MelonLogger.Error($"AntiIdleSystem.OnHeartbeatRead error: {e.Message}");
            }
        }

        /// <summary>
        /// Called by NetPeer setter patch when heartbeat is written
        /// </summary>
        public static void OnHeartbeatWrite(double value)
        {
            try
            {
                // Log significant heartbeat writes
                if (!double.IsNaN(_lastHeartbeatValue) && Math.Abs(value - _lastHeartbeatValue) > 10.0)
                {
                    MelonLogger.Msg($"[AntiIdle] Heartbeat written: {value:F2} (Change: {value - _lastHeartbeatValue:F2})");
                }
                _lastHeartbeatValue = value;
            }
            catch (Exception e)
            {
                MelonLogger.Error($"AntiIdleSystem.OnHeartbeatWrite error: {e.Message}");
            }
        }

        #endregion

        // Detects user activity cheaply and sets suppression window for synthetic keepalive
        private static void DetectUserActivity()
        {
            try
            {
                if (!Settings.suppressKeepAliveOnActivity) return;
                if (!Application.isFocused) return;

                if (!_lastMousePosInitialized)
                {
                    _lastMousePos = Input.mousePosition;
                    _lastMousePosInitialized = true;
                }

                if (Time.time - _lastInputCheck < INPUT_CHECK_INTERVAL)
                    return;
                _lastInputCheck = Time.time;

                bool activityDetected = false;

                // Any key or mouse button this frame
                if (Input.anyKeyDown)
                    activityDetected = true;
                else if (Input.GetMouseButton(0) || Input.GetMouseButton(1) || Input.GetMouseButton(2))
                    activityDetected = true;
                else
                {
                    // Mouse moved significantly
                    var pos = Input.mousePosition;
                    if ((_lastMousePos - pos).sqrMagnitude > 4f)
                        activityDetected = true;
                    _lastMousePos = pos;
                }

                if (activityDetected)
                {
                    RegisterActivity(Settings.activitySuppressionSeconds, "input");
                }
            }
            catch (Exception e)
            {
                MelonLogger.Error($"AntiIdleSystem.DetectUserActivity error: {e.Message}");
            }
        }

        // Extends suppression window for synthetic keepalive
        private static void RegisterActivity(float seconds, string reason = "activity")
        {
            if (seconds <= 0f) return;
            var until = Time.time + seconds;
            if (until > _suppressSyntheticUntil)
            {
                _suppressSyntheticUntil = until;
                _lastSuppressionReason = reason;
                if (VerboseSuppressionLogs)
                {
#pragma warning disable CS0162 // Unreachable code detected
                    var remaining = _suppressSyntheticUntil - Time.time;
                    MelonLogger.Msg($"[AntiIdle] Suppression registered: reason={reason}, duration={seconds:F0}s, remaining={remaining:F0}s");
#pragma warning restore CS0162 // Unreachable code detected
                }
            }
        }

        #endregion
    }
}
