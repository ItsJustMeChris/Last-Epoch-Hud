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
            #region waypoint patches
            [HarmonyPatch(typeof(UIWaypointStandard), "OnPointerEnter", new Type[] { typeof(UnityEngine.EventSystems.PointerEventData) })]
            internal class WayPointUnlock
            {
                public static void Prefix(UIWaypointStandard __instance, UnityEngine.EventSystems.PointerEventData eventData)
                {
                    //MelonLogger.Msg("[Mod] UIWaypointStandard.OnPointerEnter hooked");

                    if (Settings.useAnyWaypoint && ObjectManager.IsOfflineMode())
                        __instance.isActive = true;
                }
            }

            /*
            [HarmonyPatch]
            public class WaypointManager_WaypointsEnabled_Any : MelonMod
            {
                private static System.Reflection.MethodBase? s_target;
                private static bool s_loggedToggle;

                [HarmonyPrepare]
                public static bool Prepare()
                {
                    try
                    {
                        var t = typeof(WaypointManager);
                        // Scan once without probing missing names to avoid noisy AccessTools warnings
                        var methods = AccessTools.GetDeclaredMethods(t) ?? new List<System.Reflection.MethodInfo>();

                        // Prefer exact method if present
                        s_target = methods.FirstOrDefault(m => m.IsStatic && m.ReturnType == typeof(bool) && m.GetParameters().Length == 0 && string.Equals(m.Name, "WaypointIsEnabled", StringComparison.Ordinal));

                        // Fallback: any static bool, parameterless method whose name contains both "waypoint" and "enable"
                        if (s_target == null)
                        {
                            s_target = methods.FirstOrDefault(m => m.IsStatic && m.ReturnType == typeof(bool) && m.GetParameters().Length == 0 && m.Name.IndexOf("waypoint", StringComparison.OrdinalIgnoreCase) >= 0 && m.Name.IndexOf("enable", StringComparison.OrdinalIgnoreCase) >= 0);
                        }

                        if (s_target == null)
                        {
                            var names = string.Join(", ", methods.Select(m => m.Name).Distinct());
                            MelonLogger.Warning($"[LeHud.Hooks]  WaypointManager.*WaypointsEnabled* not found; available methods: {names}");
                            return false;
                        }

                        MelonLogger.Msg($"[LeHud.Hooks]  WaypointManager global enable hook -> {s_target.Name}");
                        return true;
                    }
                    catch (Exception e)
                    {
                        MelonLogger.Error($"[LeHud.Hooks]  WaypointManager.*WaypointsEnabled* Prepare error: {e.Message}");
                        return false;
                    }
                }

                [HarmonyTargetMethod]
                public static System.Reflection.MethodBase TargetMethod() => s_target!;

                public static void Postfix(ref bool __result)
                {
                    try
                    {
                        if (Settings.useAnyWaypoint)
                        {
                            __result = true;
                            if (!s_loggedToggle)
                            {
                                s_loggedToggle = true;
                                MelonLogger.Msg("[LeHud.Hooks]  WaypointsEnabled() forced TRUE (useAnyWaypoint)");
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        MelonLogger.Error($"[LeHud.Hooks]  WaypointManager.*WaypointsEnabled* Postfix error: {e.Message}");
                    }
                }
            }

            // Instance-level property getter guard to keep UI/controller checks aligned
            [HarmonyPatch]
            public class WaypointManager_WaypointEnabled_PropertyGet : MelonMod
            {
                private static System.Reflection.MethodBase? s_target;

                [HarmonyPrepare]
                public static bool Prepare()
                {
                    try
                    {
                        var t = typeof(WaypointManager);
                        s_target = AccessTools.PropertyGetter(t, "WaypointEnabled")
                                || AccessTools.DeclaredMethod(t, "get_WaypointEnabled");
                        if (s_target == null)
                        {
                            MelonLogger.Warning("[LeHud.Hooks]  WaypointManager.get_WaypointEnabled not found; skipping patch.");
                            return false;
                        }
                        MelonLogger.Msg($"[LeHud.Hooks]  WaypointManager property getter hook -> {s_target.Name}");
                        return true;
                    }
                    catch (Exception e)
                    {
                        MelonLogger.Error($"[LeHud.Hooks]  WaypointManager.get_WaypointEnabled Prepare error: {e.Message}");
                        return false;
                    }
                }

                [HarmonyTargetMethod]
                public static System.Reflection.MethodBase TargetMethod() => s_target!;

                public static void Postfix(ref bool __result)
                {
                    try
                    {
                        if (Settings.useAnyWaypoint)
                            __result = true;
                    }
                    catch (Exception e)
                    {
                        MelonLogger.Error($"[LeHud.Hooks]  WaypointManager.get_WaypointEnabled Postfix error: {e.Message}");
                    }
                }
            }

            [HarmonyPatch(typeof(WaypointManager), "EnableWaypoint")]
            public class WaypointManager_EnableWaypoint : MelonMod
            {
                private static readonly System.Reflection.FieldInfo? waypointEnabledField = AccessTools.Field(typeof(WaypointManager), "waypointEnabled");
                private static readonly System.Reflection.FieldInfo? waypointsEnabledField = AccessTools.Field(typeof(WaypointManager), "waypointsEnabled");
                private static bool loggedMissingFields = false;
                private static bool loggedApplied = false;

                public static void Postfix(WaypointManager __instance)
                {
                    if (!Settings.useAnyWaypoint || __instance == null)
                        return;

                    try
                    {
                        bool anySet = false;
                        if (waypointEnabledField != null)
                        {
                            waypointEnabledField.SetValue(__instance, true);
                            anySet = true;
                        }
                        if (waypointsEnabledField != null)
                        {
                            waypointsEnabledField.SetValue(__instance, true);
                            anySet = true;
                        }

                        if (!anySet && !loggedMissingFields)
                        {
                            loggedMissingFields = true;
                            MelonLogger.Warning("[LeHud.Hooks]  WaypointManager.EnableWaypoint Postfix: enablement fields not found; relying on WaypointsEnabled()/WaypointIsEnabled() overrides only.");
                        }
                        else if (anySet && !loggedApplied)
                        {
                            loggedApplied = true;
                            MelonLogger.Msg("[LeHud.Hooks]  WaypointManager.EnableWaypoint forced fields TRUE");
                        }
                    }
                    catch (Exception e)
                    {
                        MelonLogger.Error($"[LeHud.Hooks]  WaypointManager.EnableWaypoint Postfix error: {e.Message}");
                    }
                }
            }

            [HarmonyPatch(typeof(WaypointCondition), "CheckWaypoint", new Type[] { typeof(CharacterDataTracker), typeof(string) })]
            public class WaypointCondition_CheckWaypoint
            {
                public static void Postfix(CharacterDataTracker dataTracker, string waypointSceneName, ref bool __result)
                {
                    try
                    {
                        if (Settings.useAnyWaypoint)
                            __result = true;
                    }
                    catch (Exception e)
                    {
                        MelonLogger.Error($"[LeHud.Hooks]  WaypointCondition.CheckWaypoint Postfix error: {e.Message}");
                    }
                }
            }

            [HarmonyPatch(typeof(UIWaypointStandard), "LoadWaypointScene")]
            public class UIWaypointStandard_LoadWaypointScene
            {
                public static void Prefix(UIWaypointStandard __instance)
                {
                    try
                    {
                        if (!Settings.useAnyWaypoint)
                            return;

                        // Make sure the UI element is considered active
                        __instance.isActive = true;

                        // Attempt to globally enable via manager instance if available
                        try
                        {
                            var mgr = WaypointManager.getInstance();
                            if (mgr != null)
                            {
                                mgr.EnableWaypoint();
                            }
                        }
                        catch { }
                    }
                    catch (Exception e)
                    {
                        MelonLogger.Error($"[LeHud.Hooks]  UIWaypointStandard.LoadWaypointScene Prefix error: {e.Message}");
                    }
                }
            }

            [HarmonyPatch(typeof(PlayerSync), "SendAttemptWaypoint", new Type[] { typeof(string), typeof(byte) })]
            public class PlayerSync_SendAttemptWaypoint
            {
                private static bool s_loggedOnce;
                public static void Prefix(PlayerSync __instance, string scene, byte gate)
                {
                    try
                    {
                        if (!s_loggedOnce && Settings.useAnyWaypoint)
                        {
                            s_loggedOnce = true;
                            // Minimal one-time trace to confirm path is hit
                            MelonLogger.Msg("[LeHud.Hooks]  PlayerSync.SendAttemptWaypoint observed");
                        }
                    }
                    catch { }
                }
            }
            */
            #endregion
        }
    }
}
