using UnityEngine;
using MelonLoader;
using Il2Cpp;
// using Il2CppDMM;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppLE.UI;
using Il2CppLE.Telemetry;
using Il2CppItemFiltering;
using Il2CppLidgren.Network;
using Il2CppSystem.Net;
using Il2CppSteamworks;
using HarmonyLib;
using System.Linq;
using Mod.Cheats;

using HarmonyPatch = HarmonyLib.HarmonyPatch;
using static MelonLoader.LoaderConfig;
using static Il2Cpp.GroundItemManager;
using Mod.Utils;
using Mod.Game;


namespace Mod.Cheats.Patches
{
    public partial class MapIconPatch
    {
        internal partial class HarmonyPatches
        {
            #region networking / anti-idle patches

            [HarmonyPatch]
            public class EpochInputManager_Awake
            {
                private static System.Reflection.MethodBase? s_target;

                [HarmonyPrepare]
                public static bool Prepare()
                {
                    try
                    {
                        var t = TypeLookup.FindType(
                            "Il2Cpp.EpochInputManager",
                            "EpochInputManager",
                            "Il2CppLE.EpochInputManager",
                            "Il2CppLE.Input.EpochInputManager");
                        if (t == null) return false;
                        s_target = AccessTools.Method(t, "Awake", Type.EmptyTypes);
                        return s_target != null;
                    }
                    catch { return false; }
                }

                [HarmonyTargetMethod]
                public static System.Reflection.MethodBase TargetMethod() => s_target!;

                public static void Postfix(object __instance)
                {
                    try
                    {
                        EpochInputManagerBridge.RegisterKnownInstance(__instance);
                    }
                    catch (Exception e)
                    {
                        MelonLogger.Error($"[LeHud.Hooks]  EpochInputManager.Awake Postfix error: {e.Message}");
                    }
                }
            }

            [HarmonyPatch]
            public class EpochInputManager_OnDestroy
            {
                private static System.Reflection.MethodBase? s_target;

                [HarmonyPrepare]
                public static bool Prepare()
                {
                    try
                    {
                        var t = TypeLookup.FindType(
                            "Il2Cpp.EpochInputManager",
                            "EpochInputManager",
                            "Il2CppLE.EpochInputManager",
                            "Il2CppLE.Input.EpochInputManager");
                        if (t == null) return false;
                        s_target = AccessTools.Method(t, "OnDestroy", Type.EmptyTypes);
                        return s_target != null;
                    }
                    catch { return false; }
                }

                [HarmonyTargetMethod]
                public static System.Reflection.MethodBase TargetMethod() => s_target!;

                public static void Prefix(object __instance)
                {
                    try
                    {
                        EpochInputManagerBridge.ClearKnownInstance(__instance);
                    }
                    catch (Exception e)
                    {
                        MelonLogger.Error($"[LeHud.Hooks]  EpochInputManager.OnDestroy Prefix error: {e.Message}");
                    }
                }
            }

            [HarmonyPatch]
            public class EpochInputManager_CheckIdleInput
            {
                // Keep observer/assist code available, but disabled by default.
                private static readonly bool s_enableObserver = false;
                private static System.Reflection.MethodBase? s_target;

                [HarmonyPrepare]
                public static bool Prepare()
                {
                    if (!s_enableObserver)
                        return false;

                    try
                    {
                        var t = TypeLookup.FindType(
                            "Il2Cpp.EpochInputManager",
                            "EpochInputManager",
                            "Il2CppLE.EpochInputManager",
                            "Il2CppLE.Input.EpochInputManager");
                        if (t == null) return false;
                        s_target = AccessTools.Method(t, "CheckIdleInput", Type.EmptyTypes);
                        return s_target != null;
                    }
                    catch { return false; }
                }

                [HarmonyTargetMethod]
                public static System.Reflection.MethodBase TargetMethod() => s_target!;

                public static void Postfix(object __instance)
                {
                    try
                    {
                        _ = __instance;
                        // EpochInputManagerBridge.OnCheckIdleInputObserved(__instance);
                    }
                    catch (Exception e)
                    {
                        MelonLogger.Error($"[LeHud.Hooks]  EpochInputManager.CheckIdleInput Postfix error: {e.Message}");
                    }
                }
            }

            // Force game idle flags to false when Simple Anti-Idle is enabled
            [HarmonyPatch(typeof(ClientStateController), "get_IsIdle")]
            public class ClientStateController_IsIdle
            {
                private static bool s_logged;
                public static void Postfix(ref bool __result)
                {
                    try
                    {
                        if (Settings.useSimpleAntiIdle && Settings.forceIsIdleFalseFallback && !ObjectManager.IsOfflineMode())
                        {
                            if (__result)
                            {
                                __result = false;
                                if (!s_logged)
                                {
                                    s_logged = true;
                                    MelonLogger.Msg("[AntiIdle] ClientStateController.IsIdle forced FALSE");
                                }
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        MelonLogger.Error($"[LeHud.Hooks]  ClientStateController.get_IsIdle Postfix error: {e.Message}");
                    }
                }
            }

            [HarmonyPatch(typeof(StateController), "get_IsIdle")]
            public class StateController_IsIdle
            {
                private static bool s_logged;
                public static void Postfix(ref bool __result)
                {
                    try
                    {
                        if (Settings.useSimpleAntiIdle && Settings.forceIsIdleFalseFallback && !ObjectManager.IsOfflineMode())
                        {
                            if (__result)
                            {
                                __result = false;
                                if (!s_logged)
                                {
                                    s_logged = true;
                                    MelonLogger.Msg("[AntiIdle] StateController.IsIdle forced FALSE");
                                }
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        MelonLogger.Error($"[LeHud.Hooks]  StateController.get_IsIdle Postfix error: {e.Message}");
                    }
                }
            }

            // NetPeer heartbeat hooks disabled: fields do not expose IL2CPP accessors
            /*
			[HarmonyPatch(typeof(Il2CppLidgren.Network.NetPeer), "get_m_lastHeartbeat")]
			public class NetPeer_LastHeartbeat_Get
			{
				private static void Postfix(Il2CppLidgren.Network.NetPeer __instance, double __result)
				{
					try
					{
						MelonLogger.Msg($"[Mod] NetPeer getter called: {__result}");
						if (__instance != null)
						{
							AntiIdleSystem.SetNetPeer(__instance);
							AntiIdleSystem.OnHeartbeatRead(__result);
						}
					}
					catch (Exception e)
					{
						MelonLogger.Error($"[Mod] NetPeer.m_lastHeartbeat getter Postfix error: {e.Message}");
					}
				}
			}

			[HarmonyPatch(typeof(Il2CppLidgren.Network.NetPeer), "set_m_lastHeartbeat")]
			public class NetPeer_LastHeartbeat_Set
			{
				private static void Prefix(Il2CppLidgren.Network.NetPeer __instance, double value)
				{
					try
					{
						MelonLogger.Msg($"[Mod] NetPeer setter called: {value}");
						if (__instance != null)
						{
							AntiIdleSystem.SetNetPeer(__instance);
							AntiIdleSystem.OnHeartbeatWrite(value);
						}
					}
					catch (Exception e)
					{
						MelonLogger.Error($"[Mod] NetPeer.m_lastHeartbeat setter Prefix error: {e.Message}");
					}
				}
			}
            */

            // These patches will hook into networking methods to detect and prevent idle timeouts
            [HarmonyPatch(typeof(NetMultiClient), "get_ConnectionStatus")]
            public class NetMultiClient_ConnectionStatus
            {
                private static void Postfix(NetMultiClient __instance, NetConnectionStatus __result)
                {
                    try
                    {
                        if (__instance != null)
                        {
                            // Update references
                            AntiIdleSystem.SetNetMultiClient(__instance);
                            AntiIdleSystem.OnConnectionStatusChanged(__result);

                            // Attempt to capture server connection from property if available
                            try
                            {
                                var type = __instance.GetType();
                                var prop = type.GetProperty("ServerConnection", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                                if (prop != null)
                                {
                                    var conn = prop.GetValue(__instance);
                                    if (conn != null)
                                        AntiIdleSystem.SetServerConnection(conn);
                                }
                                else
                                {
                                    // Fallbacks: try common collections: Connections, m_connections, connection, m_connection
                                    object? candidate = null;

                                    // 1) Properties that look like collections of connections
                                    var props = type.GetProperties(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                                    foreach (var p in props)
                                    {
                                        var n = p.Name.ToLowerInvariant();
                                        if (n.Contains("connection"))
                                        {
                                            try
                                            {
                                                var val = p.GetValue(__instance);
                                                candidate = TryPickSingleConnection(val) ?? candidate;
                                                if (candidate != null) break;
                                            }
                                            catch { }
                                        }
                                    }

                                    // 2) Fields that look like collections of connections
                                    if (candidate == null)
                                    {
                                        var fields = type.GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                                        foreach (var f in fields)
                                        {
                                            var n = f.Name.ToLowerInvariant();
                                            if (n.Contains("connection"))
                                            {
                                                try
                                                {
                                                    var val = f.GetValue(__instance);
                                                    candidate = TryPickSingleConnection(val) ?? candidate;
                                                    if (candidate != null) break;
                                                }
                                                catch { }
                                            }
                                        }
                                    }

                                    if (candidate != null)
                                    {
                                        AntiIdleSystem.SetServerConnection(candidate);
                                    }
                                }
                            }
                            catch { }
                        }
                    }
                    catch (Exception e)
                    {
                        MelonLogger.Error($"[LeHud.Hooks]  NetMultiClient.ConnectionStatus Postfix error: {e.Message}");
                    }
                }

                // Try to pick a single NetConnection from various collection shapes
                private static object? TryPickSingleConnection(object? value)
                {
                    if (value == null) return null;
                    try
                    {
                        var valType = value.GetType();

                        // If it's already a NetConnection, return it
                        if (valType.Name.Contains("NetConnection"))
                            return value;

                        // Handle Il2Cpp arrays
                        if (valType.IsArray)
                        {
                            var arr = value as System.Array;
                            if (arr != null && arr.Length == 1)
                                return arr.GetValue(0);
                            if (arr != null && arr.Length > 0)
                                return arr.GetValue(0); // pick first as best-effort
                        }

                        // Handle generic collections (List<NetConnection>, etc.)
                        var countProp = valType.GetProperty("Count") ?? valType.GetProperty("Length");
                        if (countProp != null)
                        {
                            var countObj = countProp.GetValue(value);
                            int count = 0;
                            try { count = Convert.ToInt32(countObj); } catch { }
                            if (count > 0)
                            {
                                var indexer = valType.GetMethod("get_Item");
                                if (indexer != null)
                                    return indexer.Invoke(value, new object[] { 0 });

                                // Fallback: enumerate via IEnumerable
                                var enumerable = value as System.Collections.IEnumerable;
                                if (enumerable != null)
                                {
                                    foreach (var item in enumerable)
                                        return item; // first
                                }
                            }
                        }

                        // Handle single field/property named similarly to a NetConnection
                        foreach (var p in valType.GetProperties(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic))
                        {
                            if (p.PropertyType.Name.Contains("NetConnection"))
                                return p.GetValue(value);
                        }
                        foreach (var f in valType.GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic))
                        {
                            if (f.FieldType.Name.Contains("NetConnection"))
                                return f.GetValue(value);
                        }
                    }
                    catch { }
                    return null;
                }
            }

			[HarmonyPatch(typeof(NetMultiClient), "Connect", new Type[] { typeof(IPEndPoint), typeof(NetOutgoingMessage) })]
			public class NetMultiClient_Connect
			{
				private static void Prefix(NetMultiClient __instance)
				{
					try
					{
                        _ = __instance;
                        Log.MarkGamePhase("NetMultiClient.Connect");
                        Log.ArmShaDiagnosticsWindow("NetMultiClient.Connect", TimeSpan.FromSeconds(60));
						// Reduced: no verbose prefix log
					}
					catch (Exception e)
					{
						MelonLogger.Error($"[LeHud.Hooks]  NetMultiClient.Connect Prefix error: {e.Message}");
					}
				}

				private static void Postfix(NetMultiClient __instance, NetConnection __result)
				// private static void Postfix(Il2CppLidgren.Network.NetMultiClient __instance, Il2CppLidgren.Network.NetConnection __result)
				{
					try
					{
						if (__result != null)
						{
                            Log.MarkGamePhase("NetMultiClient.Connected");
                            Log.ArmShaDiagnosticsWindow("NetMultiClient.Connected", TimeSpan.FromSeconds(45));
							AntiIdleSystem.SetServerConnection(__result);
							AntiIdleSystem.SetNetMultiClient(__instance);
						}
                        else
                        {
                            Log.MarkGamePhase("NetMultiClient.ConnectFailed");
                        }
					}
					catch (Exception e)
					{
						MelonLogger.Error($"[LeHud.Hooks]  NetMultiClient.Connect Postfix error: {e.Message}");
					}
				}
			}

            [HarmonyPatch(typeof(NetMultiClient), "Disconnect")]
            public class NetMultiClient_Disconnect
            {
                private static void Prefix(NetMultiClient __instance, string byeMessage)
                {
                    try
                    {
                        _ = __instance;
                        var reason = string.IsNullOrWhiteSpace(byeMessage) ? "No reason provided" : byeMessage.Trim();
                        Log.MarkGamePhase($"NetMultiClient.Disconnect:{reason}");
                        MelonLogger.Msg($"[LeHud.Hooks]  NetMultiClient.Disconnect Prefix - Reason: {reason}");

                        // Log disconnect attempt for anti-idle analysis
                        AntiIdleSystem.OnDisconnectAttempted(byeMessage);

                        if (IsLikelyLoginFailureDisconnect(reason))
                        {
                            Log.DumpRecentGameEventsThrottled(
                                key: "game-login-failure-disconnect",
                                reason: $"Disconnect observed: {reason}",
                                minInterval: TimeSpan.FromSeconds(10),
                                maxLines: 100);
                        }
                    }
                    catch (Exception e)
                    {
                        MelonLogger.Error($"[LeHud.Hooks]  NetMultiClient.Disconnect Prefix error: {e.Message}");
                    }
                }

                private static void Postfix(NetMultiClient __instance, string byeMessage)
                {
                    try
                    {
                        _ = __instance;
                        _ = byeMessage;
                        MelonLogger.Msg($"[LeHud.Hooks]  NetMultiClient.Disconnect Postfix - Disconnection completed");

                        // Clear stored references
                        AntiIdleSystem.ClearConnections();
                    }
                    catch (Exception e)
                    {
                        MelonLogger.Error($"[LeHud.Hooks]  NetMultiClient.Disconnect Postfix error: {e.Message}");
                    }
                }

                private static bool IsLikelyLoginFailureDisconnect(string reason)
                {
                    if (string.IsNullOrWhiteSpace(reason))
                        return false;

                    return reason.Contains("returning to menu", StringComparison.OrdinalIgnoreCase)
                        || reason.Contains("timeout", StringComparison.OrdinalIgnoreCase)
                        || reason.Contains("idle", StringComparison.OrdinalIgnoreCase)
                        || reason.Contains("load", StringComparison.OrdinalIgnoreCase)
                        || reason.Contains("character", StringComparison.OrdinalIgnoreCase);
                }
            }

            [HarmonyPatch]
            public class NetMultiClient_SendMessage
            {
                private static System.Reflection.MethodBase? s_target;

                [HarmonyPrepare]
                public static bool Prepare()
                {
                    s_target = AccessTools.GetDeclaredMethods(typeof(NetMultiClient))
                        .FirstOrDefault(method => method.Name == "SendMessage"
                            && method.GetParameters().Select(parameter => parameter.ParameterType)
                                .SequenceEqual(new[] { typeof(NetOutgoingMessage), typeof(NetDeliveryMethod), typeof(int) }));
                    return s_target != null;
                }

                [HarmonyTargetMethod]
                public static System.Reflection.MethodBase TargetMethod() => s_target!;

                private static void Prefix(NetMultiClient __instance, NetOutgoingMessage msg, NetDeliveryMethod method, int sequenceChannel)
                {
                    try
                    {

                        // Track message sending for anti-idle analysis
                        AntiIdleSystem.OnMessageSent(msg, method, sequenceChannel);
                    }
                    catch (Exception e)
                    {
                        MelonLogger.Error($"[LeHud.Hooks]  NetMultiClient.SendMessage Prefix error: {e.Message}");
                    }
                }

                private static void Postfix(NetMultiClient __instance, NetOutgoingMessage msg, NetDeliveryMethod method, int sequenceChannel, NetSendResult __result)
                {
                    try
                    {
                        // Reduced: no per-message logging
                    }
                    catch (Exception e)
                    {
                        MelonLogger.Error($"[LeHud.Hooks]  NetMultiClient.SendMessage Postfix error: {e.Message}");
                    }
                }
            }

            // Hook into individual NetConnection status changes
            [HarmonyPatch(typeof(NetConnection), "get_Status")]
            public class NetConnection_Status
            {
                private static void Postfix(NetConnection __instance, NetConnectionStatus __result)
                {
                    try
                    {
                        if (__instance != null)
                        {
                            // Track individual connection status (reduced logging)
                            AntiIdleSystem.OnNetConnectionStatusChanged(__instance, __result);
                        }
                    }
                    catch (Exception e)
                    {
                        MelonLogger.Error($"[LeHud.Hooks]  NetConnection.Status Postfix error: {e.Message}");
                    }
                }
            }

            // Hook into Steam networking callbacks if available
            [HarmonyPatch(typeof(SteamNetworkingSockets), "ConnectionStatusChanged")]
            public class SteamNetworking_ConnectionStatusChanged
            {
                private static void Prefix(SteamNetworkingSockets __instance, Il2CppSteamworks.Data.SteamNetConnectionStatusChangedCallback_t data)
                {
                    try
                    {
                        _ = __instance;
                        Log.InfoThrottled(LogSource.Hooks, "SteamNetworking.ConnectionStatusChanged", "Steam networking connection status callback observed", TimeSpan.FromSeconds(10));

                        // Track Steam networking status changes
                        AntiIdleSystem.OnSteamConnectionStatusChanged(data);
                    }
                    catch (Exception e)
                    {
                        MelonLogger.Error($"[LeHud.Hooks]  SteamNetworking.ConnectionStatusChanged Prefix error: {e.Message}");
                    }
                }
            }

            // Supplementary hook: ClientNetworkService (game wrapper over networking)
            [HarmonyPatch]
            public class ClientNetworkService_SendClientMessage
            {
                private static System.Reflection.MethodBase? s_target;
                private static bool s_loggedSkip;

                [HarmonyPrepare]
                public static bool Prepare()
                {
                    try
                    {
                        var t = TypeLookup.FindType("Il2CppLE.Networking.Core.Networking.ClientNetworkService");
                        if (t == null) return false;
                        // Generic method: SendClientMessage<T>(T message, NetDeliveryMethod deliveryMethod, int sequenceChannel)
                        s_target = AccessTools.GetDeclaredMethods(t)
                            .FirstOrDefault(m => m.Name == "SendClientMessage" && m.GetParameters().Length == 3);
                        if (s_target == null)
                            return false;

                        // IL2CPP generic methods are unsafe to patch with Harmony here; skip to avoid IL compile errors
                        var mi = s_target as System.Reflection.MethodInfo;
                        if (mi != null && (mi.IsGenericMethodDefinition || mi.ContainsGenericParameters))
                        {
                            if (!s_loggedSkip)
                            {
                                s_loggedSkip = true;
                                MelonLogger.Warning("[LeHud.Hooks]  Skipping ClientNetworkService.SendClientMessage<T> patch (generic IL2CPP method)");
                            }
                            return false;
                        }
                        return true;
                    }
                    catch { return false; }
                }

                [HarmonyTargetMethod]
                public static System.Reflection.MethodBase TargetMethod() => s_target!;

                public static void Prefix(object __instance, [HarmonyArgument(0)] object message, [HarmonyArgument(1)] object deliveryMethod, [HarmonyArgument(2)] int sequenceChannel)
                {
                    try
                    {
                        Log.CaptureNetworkBreadcrumb(
                            stage: "ClientNetworkService.SendClientMessage.Prefix",
                            details: $"delivery={deliveryMethod?.ToString() ?? "null"} channel={sequenceChannel}",
                            minInterval: TimeSpan.FromMilliseconds(250));
                        Log.CaptureNetworkBreadcrumb(
                            stage: "ClientNetworkService.SendClientMessage.Message",
                            payload: message,
                            minInterval: TimeSpan.FromMilliseconds(120));
                        AntiIdleSystem.SetClientNetworkService(__instance);
                        AntiIdleSystem.OnMessageSent(message, deliveryMethod ?? "null", sequenceChannel);
                    }
                    catch (Exception e)
                    {
                        MelonLogger.Error($"[LeHud.Hooks]  ClientNetworkService.SendClientMessage Prefix error: {e.Message}");
                    }
                }
            }

            [HarmonyPatch]
            public class ClientNetworkService_SendMessageBuffer
            {
                private static System.Reflection.MethodBase? s_target;

                [HarmonyPrepare]
                public static bool Prepare()
                {
                    try
                    {
                        var t = TypeLookup.FindType("Il2CppLE.Networking.Core.Networking.ClientNetworkService");
                        if (t == null) return false;
                        // SendMessageBuffer(MessageKey key, ReadOnlySpan<byte> data, NetDeliveryMethod method, int channel)
                        s_target = AccessTools.GetDeclaredMethods(t)
                            .FirstOrDefault(m => m.Name == "SendMessageBuffer" && m.GetParameters().Length == 4);
                        if (s_target != null)
                        {
                            // Log the parameter type for messageKey for diagnostics
                            var param = s_target.GetParameters().FirstOrDefault();
                            if (param != null)
                            {
                                MelonLogger.Msg($"[LeHud.Hooks]  ClientNetworkService.SendMessageBuffer: messageKey param type: {param.ParameterType.FullName}");
                            }
                            else
                            {
                                MelonLogger.Msg("[LeHud.Hooks]  ClientNetworkService.SendMessageBuffer: messageKey param not found.");
                            }
                        }

                        return s_target != null;
                    }
                    catch { return false; }
                }

                [HarmonyTargetMethod]
                public static System.Reflection.MethodBase TargetMethod() => s_target!;

                public static void Prefix(object __instance, [HarmonyArgument(0)] object key, [HarmonyArgument(2)] object method, [HarmonyArgument(3)] int channel)
                {
                    try
                    {
                        Log.CaptureNetworkBreadcrumb(
                            stage: "ClientNetworkService.SendMessageBuffer.Prefix",
                            details: $"method={method?.ToString() ?? "null"} channel={channel}",
                            minInterval: TimeSpan.FromMilliseconds(250));
                        Log.CaptureNetworkBreadcrumb(
                            stage: "ClientNetworkService.SendMessageBuffer.Key",
                            payload: key,
                            minInterval: TimeSpan.FromMilliseconds(120));
                        AntiIdleSystem.SetClientNetworkService(__instance);
                        AntiIdleSystem.OnMessageSent(key, method ?? "null", channel);
                        AntiIdleSystem.OnWrapperBufferSent(key, method ?? "null", channel);
                    }
                    catch (Exception e)
                    {
                        MelonLogger.Error($"[LeHud.Hooks]  ClientNetworkService.SendMessageBuffer Prefix error: {e.Message}");
                    }
                }
            }

            // Observe wrapper receive paths, if available
            [HarmonyPatch]
            public class ClientNetworkService_OnReceiveNetworkMessage
            {
                private static System.Reflection.MethodBase? s_target;
                [HarmonyPrepare]
                public static bool Prepare()
                {
                    try
                    {
                        var t = TypeLookup.FindType("Il2CppLE.Networking.Core.Networking.ClientNetworkService");
                        if (t == null) return false;
                        s_target = AccessTools.GetDeclaredMethods(t)
                            .FirstOrDefault(m => m.Name.IndexOf("Receive", StringComparison.OrdinalIgnoreCase) >= 0 && m.GetParameters().Length >= 1);
                        return s_target != null;
                    }
                    catch { return false; }
                }
                [HarmonyTargetMethod]
                public static System.Reflection.MethodBase TargetMethod() => s_target!;
                public static void Prefix(object __instance, object __0)
                {
                    try
                    {
                        _ = __instance;
                        Log.MarkGamePhase("ClientNetworkService.Receive");
                        Log.CaptureNetworkBreadcrumb(
                            stage: $"ClientNetworkService.{s_target?.Name ?? "Receive*"}.Prefix",
                            payload: __0,
                            minInterval: TimeSpan.FromMilliseconds(30));
                        AntiIdleSystem.OnWrapperReceive(__0);
                    }
                    catch { }
                }

                public static void Postfix(object __instance, object __0)
                {
                    try
                    {
                        _ = __instance;
                        Log.CaptureNetworkBreadcrumb(
                            stage: $"ClientNetworkService.{s_target?.Name ?? "Receive*"}.Postfix",
                            payload: __0,
                            minInterval: TimeSpan.FromMilliseconds(50));
                    }
                    catch { }
                }
            }

            [HarmonyPatch]
            public class ClientNetworkService_OnProcessedIncomingMessage
            {
                private static System.Reflection.MethodBase? s_target;
                [HarmonyPrepare]
                public static bool Prepare()
                {
                    try
                    {
                        var t = TypeLookup.FindType("Il2CppLE.Networking.Core.Networking.ClientNetworkService");
                        if (t == null) return false;
                        s_target = AccessTools.GetDeclaredMethods(t)
                            .FirstOrDefault(m => m.Name.IndexOf("Processed", StringComparison.OrdinalIgnoreCase) >= 0 && m.Name.IndexOf("IncomingMessage", StringComparison.OrdinalIgnoreCase) >= 0);
                        return s_target != null;
                    }
                    catch { return false; }
                }
                [HarmonyTargetMethod]
                public static System.Reflection.MethodBase TargetMethod() => s_target!;
                public static void Prefix(object __instance, object __0)
                {
                    try
                    {
                        _ = __instance;
                        Log.MarkGamePhase("ClientNetworkService.ProcessedIncomingMessage");
                        Log.CaptureNetworkBreadcrumb(
                            stage: "ClientNetworkService.ProcessedIncomingMessage.Prefix",
                            payload: __0,
                            minInterval: TimeSpan.FromMilliseconds(30));
                        AntiIdleSystem.OnWrapperReceive(__0);
                    }
                    catch { }
                }

                public static void Postfix(object __instance, object __0)
                {
                    try
                    {
                        _ = __instance;
                        Log.CaptureNetworkBreadcrumb(
                            stage: "ClientNetworkService.ProcessedIncomingMessage.Postfix",
                            payload: __0,
                            minInterval: TimeSpan.FromMilliseconds(50));
                    }
                    catch { }
                }
            }

            // Focused hook for the method seen in SHA probe stacks.
            [HarmonyPatch]
            public class ClientNetworkService_ReceiveCallback
            {
                private static System.Reflection.MethodBase? s_target;
                private static bool s_loggedBinding;

                [HarmonyPrepare]
                public static bool Prepare()
                {
                    try
                    {
                        var t = TypeLookup.FindType("Il2CppLE.Networking.Core.Networking.ClientNetworkService");
                        if (t == null) return false;

                        s_target = AccessTools.GetDeclaredMethods(t)
                            .FirstOrDefault(m => string.Equals(m.Name, "ReceiveCallback", StringComparison.Ordinal));
                        if (s_target == null)
                            return false;

                        if (!s_loggedBinding)
                        {
                            s_loggedBinding = true;
                            var ps = s_target.GetParameters();
                            MelonLogger.Msg($"[LeHud.Hooks]  ClientNetworkService.ReceiveCallback bound (params={ps.Length})");
                        }

                        return true;
                    }
                    catch
                    {
                        return false;
                    }
                }

                [HarmonyTargetMethod]
                public static System.Reflection.MethodBase TargetMethod() => s_target!;

                public static void Prefix(object __instance, object[] __args)
                {
                    try
                    {
                        if (!Settings.enableNetworkDiagnostics)
                            return;

                        _ = __instance;
                        Log.MarkGamePhase("ClientNetworkService.ReceiveCallback");
                        object? firstArg = (__args != null && __args.Length > 0) ? __args[0] : null;
                        Log.CaptureNetworkBreadcrumb(
                            stage: "ClientNetworkService.ReceiveCallback.Prefix",
                            payload: firstArg,
                            minInterval: TimeSpan.FromMilliseconds(20));
                        if (Log.IsShaDiagnosticsWindowActive())
                        {
                            Log.CaptureNetworkBreadcrumb(
                                stage: "ClientNetworkService.ReceiveCallback.Args",
                                details: SummarizeArgs(__instance, __args),
                                minInterval: TimeSpan.FromMilliseconds(60));
                        }
                    }
                    catch { }
                }

                public static void Postfix(object __instance, object[] __args)
                {
                    try
                    {
                        if (!Settings.enableNetworkDiagnostics)
                            return;

                        _ = __instance;
                        object? firstArg = (__args != null && __args.Length > 0) ? __args[0] : null;
                        Log.CaptureNetworkBreadcrumb(
                            stage: "ClientNetworkService.ReceiveCallback.Postfix",
                            payload: firstArg,
                            minInterval: TimeSpan.FromMilliseconds(35));
                    }
                    catch { }
                }

                private static string SummarizeArgs(object instance, object[]? args)
                {
                    var sb = new System.Text.StringBuilder(280);
                    sb.Append("instance=").Append(Log.DescribePayloadForDiagnostics(instance));
                    if (args == null || args.Length == 0)
                    {
                        sb.Append(" | args=<none>");
                        return sb.ToString();
                    }

                    int max = Math.Min(args.Length, 6);
                    sb.Append(" | argc=").Append(args.Length);
                    for (int i = 0; i < max; i++)
                    {
                        sb.Append(" | arg").Append(i).Append('=').Append(Log.DescribePayloadForDiagnostics(args[i]));
                    }
                    if (args.Length > max)
                    {
                        sb.Append(" | ...");
                    }

                    return sb.ToString();
                }
            }

            // Additional receive-side processing hooks to add breadcrumbs around decode/dispatch methods.
            [HarmonyPatch]
            public class ClientNetworkService_MessageProcessing
            {
                private static List<System.Reflection.MethodBase>? s_targets;
                private static bool s_loggedBinding;

                [HarmonyPrepare]
                public static bool Prepare()
                {
                    try
                    {
                        var t = TypeLookup.FindType("Il2CppLE.Networking.Core.Networking.ClientNetworkService");
                        if (t == null) return false;

                        var keywords = new[] { "Deserialize", "Decode", "Process", "Handle", "Dispatch" };
                        s_targets = AccessTools.GetDeclaredMethods(t)
                            .Where(m => !m.IsAbstract
                                && !m.IsGenericMethod
                                && !m.ContainsGenericParameters
                                && m.GetParameters().Length > 0
                                && keywords.Any(k => m.Name.IndexOf(k, StringComparison.OrdinalIgnoreCase) >= 0))
                            .Cast<System.Reflection.MethodBase>()
                            .Take(10)
                            .ToList();

                        if (s_targets.Count == 0)
                            return false;

                        if (!s_loggedBinding)
                        {
                            s_loggedBinding = true;
                            var names = string.Join(", ", s_targets.Select(m => m.Name).Distinct());
                            MelonLogger.Msg($"[LeHud.Hooks]  ClientNetworkService processing hooks bound: {names}");
                        }

                        return true;
                    }
                    catch
                    {
                        return false;
                    }
                }

                [HarmonyTargetMethods]
                public static IEnumerable<System.Reflection.MethodBase> TargetMethods() => s_targets ?? Enumerable.Empty<System.Reflection.MethodBase>();

                public static void Prefix(object __instance, object[] __args, System.Reflection.MethodBase __originalMethod)
                {
                    try
                    {
                        if (!Settings.enableNetworkDiagnostics || !Log.IsShaDiagnosticsWindowActive())
                            return;

                        _ = __instance;
                        string method = __originalMethod?.Name ?? "UnknownMethod";
                        Log.MarkGamePhase($"ClientNetworkService.{method}");
                        Log.CaptureNetworkBreadcrumb(
                            stage: $"ClientNetworkService.{method}.PrefixArgs",
                            details: SummarizeArgs(__args),
                            minInterval: TimeSpan.FromMilliseconds(35));
                    }
                    catch { }
                }

                public static void Postfix(object __instance, object[] __args, System.Reflection.MethodBase __originalMethod)
                {
                    try
                    {
                        if (!Settings.enableNetworkDiagnostics || !Log.IsShaDiagnosticsWindowActive())
                            return;

                        _ = __instance;
                        _ = __args;
                        string method = __originalMethod?.Name ?? "UnknownMethod";
                        Log.CaptureNetworkBreadcrumb(
                            stage: $"ClientNetworkService.{method}.Postfix",
                            details: "ok",
                            minInterval: TimeSpan.FromMilliseconds(50));
                    }
                    catch { }
                }

                private static string SummarizeArgs(object[]? args)
                {
                    if (args == null || args.Length == 0)
                        return "args=<none>";

                    var sb = new System.Text.StringBuilder(220);
                    int max = Math.Min(args.Length, 4);
                    sb.Append("argc=").Append(args.Length);
                    for (int i = 0; i < max; i++)
                    {
                        sb.Append(" | arg").Append(i).Append('=').Append(Log.DescribePayloadForDiagnostics(args[i]));
                    }
                    if (args.Length > max)
                    {
                        sb.Append(" | ...");
                    }
                    return sb.ToString();
                }
            }

            // Lower-level Lidgren ingress probes were disabled due IL2CPP runtime instability.
            #endregion
        }
    }
}
