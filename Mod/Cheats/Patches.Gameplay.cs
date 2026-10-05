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
            #region active game patches
            [HarmonyPatch]
            [HarmonyPatch(typeof(CameraManager), "ApplyZoom")]
            public class Camera_ : MelonMod
            {
                //todo: getting kicked due to idle can cause this to stop triggering somehow
                //todo: breaks when switching between offline and online
                private static bool isPatched = false;
                public static void Postfix(CameraManager __instance)
                {
                    if (!isPatched && Settings.cameraZoomUnlock)
                    {
                        //MelonLogger.Msg("[Mod] CameraManager hooked");
                        //MelonLogger.Msg("zoomDefault: " + __instance.zoomDefault.ToString());
                        //MelonLogger.Msg("zoomMin: " + __instance.zoomMin.ToString());
                        //MelonLogger.Msg("reverseZoomDirection: " + __instance.reverseZoomDirection.ToString());
                        __instance.zoomDefault = -52.5f;
                        isPatched = true;
                        MelonLogger.Msg("[LeHud.Hooks]  Camera max zoom patched (3x)");
                        // zoomDefault: -17.5
                        // zoomMin: -7
                    }
                    else if (isPatched && !Settings.cameraZoomUnlock)
                    {
                        //MelonLogger.Msg("[Mod] CameraManager unhooked");
                        __instance.zoomDefault = -17.5f;
                        isPatched = false;
                        MelonLogger.Msg("[LeHud.Hooks]  Camera max zoom unpatched (1x)");
                    }
                }
            }

            [HarmonyPatch]
            [HarmonyPatch(typeof(DMMapZoom), "ZoomOutMinimap")]
            public class DMMapZoom_ZoomOutMinimap : MelonMod
            {
                private static bool isPatched = false;
                public static void Prefix(ref DMMapZoom __instance)
                {
                    if (!isPatched && Settings.minimapZoomUnlock)
                    {
                        //MelonLogger.Msg("DMMapZoom hooked");
                        //MelonLogger.Msg("minimap zoomDefault: " + __instance.maxMinimapZoom.ToString());
                        __instance.maxMinimapZoom = float.MaxValue;
                        isPatched = true;
                        MelonLogger.Msg("[LeHud.Hooks]  minimap max zoom patched ()");
                        // zoomdefault: 37.5
                        // zoommin: 12.5
                        // step size: 5
                    }
                    else if (isPatched && !Settings.minimapZoomUnlock)
                    {
                        //MelonLogger.Msg("[Mod] DMMapZoom unhooked");
                        __instance.maxMinimapZoom = 37.5f;
                        isPatched = false;
                        MelonLogger.Msg("[LeHud.Hooks]  minimap max zoom unpatched (1x)");
                    }
                }
            }

            [HarmonyPatch]
            public class RelayDamageEvents_DamageNumberEvent
            {
                private static System.Reflection.MethodBase? s_target;
                private static bool s_loggedMissing;

                [HarmonyPrepare]
                public static bool Prepare()
                {
                    try
                    {
                        var relayType = TypeLookup.FindType(
                            "Il2Cpp.RelayDamageEvents",
                            "RelayDamageEvents",
                            "Il2CppLE.RelayDamageEvents");
                        if (relayType == null)
                        {
                            LogMissingBinding("type");
                            return false;
                        }

                        var methods = AccessTools.GetDeclaredMethods(relayType);
                        for (int i = 0; i < methods.Count; i++)
                        {
                            var method = methods[i];
                            if (!string.Equals(method.Name, "DamageNumberEvent", StringComparison.Ordinal))
                                continue;

                            var parameters = method.GetParameters();
                            if (parameters.Length == 0)
                                continue;

                            if (!IsNumericParameter(parameters[0].ParameterType))
                                continue;

                            s_target = method;
                            MelonLogger.Msg($"[LeHud.Hooks]  RelayDamageEvents.DamageNumberEvent bound ({parameters.Length} params)");
                            return true;
                        }

                        LogMissingBinding("method");
                        return false;
                    }
                    catch (Exception e)
                    {
                        MelonLogger.Error($"[LeHud.Hooks]  RelayDamageEvents.DamageNumberEvent Prepare error: {e.Message}");
                        return false;
                    }
                }

                [HarmonyTargetMethod]
                public static System.Reflection.MethodBase TargetMethod() => s_target!;

                public static void Prefix(object __instance, object[] __args)
                {
                    try
                    {
                        if (!Settings.enableDpsMeter)
                            return;
                        if (!ObjectManager.IsOfflineMode())
                            return;

                        if (__args == null || __args.Length == 0)
                            return;

                        if (!TryGetDamage(__args[0], out float damage))
                            return;

                        int hitEvents = 0;
                        if (__args.Length > 1)
                        {
                            TryGetHitEventFlags(__args[1], out hitEvents);
                        }

                        DpsMeter.OnDamageEvent(__instance, damage, hitEvents);
                    }
                    catch
                    {
                        // Keep hook non-fatal.
                    }
                }

                private static bool IsNumericParameter(Type type)
                {
                    return type == typeof(float)
                        || type == typeof(double)
                        || type == typeof(int)
                        || type == typeof(uint)
                        || type == typeof(long)
                        || type == typeof(ulong)
                        || type == typeof(short)
                        || type == typeof(ushort)
                        || type == typeof(byte)
                        || type == typeof(sbyte);
                }

                private static bool TryGetDamage(object? value, out float damage)
                {
                    damage = 0f;
                    if (value == null)
                        return false;

                    try
                    {
                        damage = Convert.ToSingle(value);
                        return !float.IsNaN(damage) && !float.IsInfinity(damage);
                    }
                    catch
                    {
                        return false;
                    }
                }

                private static bool TryGetHitEventFlags(object? value, out int flags)
                {
                    flags = 0;
                    if (value == null)
                        return false;

                    try
                    {
                        flags = Convert.ToInt32(value);
                        return true;
                    }
                    catch
                    {
                        try
                        {
                            return int.TryParse(value.ToString(), out flags);
                        }
                        catch
                        {
                            return false;
                        }
                    }
                }

                private static void LogMissingBinding(string phase)
                {
                    if (s_loggedMissing)
                        return;
                    s_loggedMissing = true;
                    MelonLogger.Warning($"[LeHud.Hooks]  RelayDamageEvents.DamageNumberEvent {phase} not found; DPS meter source disabled.");
                }
            }

            [HarmonyPatch]
            public class DamageNumber_Diagnostics
            {
                private static List<System.Reflection.MethodBase>? s_targets;
                private static bool s_loggedMissing;

                [HarmonyPrepare]
                public static bool Prepare()
                {
                    try
                    {
                        var damageNumberType = TypeLookup.FindType(
                            "Il2Cpp.DamageNumber",
                            "DamageNumber",
                            "Il2CppLE.DamageNumber");
                        if (damageNumberType == null)
                        {
                            LogMissingBinding("type");
                            return false;
                        }

                        s_targets = AccessTools.GetDeclaredMethods(damageNumberType)
                            .Where(m =>
                                !m.IsStatic
                                && !m.IsAbstract
                                && (string.Equals(m.Name, "Awake", StringComparison.Ordinal)
                                    || string.Equals(m.Name, "OnDestroy", StringComparison.Ordinal)
                                    || string.Equals(m.Name, "SendPropertiesToRenderer", StringComparison.Ordinal)))
                            .Cast<System.Reflection.MethodBase>()
                            .ToList();

                        if (s_targets.Count == 0)
                        {
                            LogMissingBinding("methods");
                            return false;
                        }

                        var boundNames = string.Join(", ", s_targets.Select(t => t.Name).Distinct());
                        MelonLogger.Msg($"[LeHud.Hooks]  DamageNumber diagnostics hooks bound: {boundNames}");
                        return true;
                    }
                    catch (Exception e)
                    {
                        MelonLogger.Error($"[LeHud.Hooks]  DamageNumber diagnostics Prepare error: {e.Message}");
                        return false;
                    }
                }

                [HarmonyTargetMethods]
                public static IEnumerable<System.Reflection.MethodBase> TargetMethods() => s_targets ?? Enumerable.Empty<System.Reflection.MethodBase>();

                public static void Prefix(object __instance, System.Reflection.MethodBase __originalMethod)
                {
                    if (!Settings.enableDamageNumberDiagnostics
                        && !(Settings.enableDpsMeter && Settings.enableDpsMeterOnlineRaw))
                    {
                        return;
                    }

                    try
                    {
                        DamageNumberDiagnostics.OnPrefix(__instance, __originalMethod);
                    }
                    catch
                    {
                        // Keep diagnostics hooks non-fatal.
                    }
                }

                public static void Postfix(object __instance, System.Reflection.MethodBase __originalMethod)
                {
                    if (!Settings.enableDamageNumberDiagnostics
                        && !(Settings.enableDpsMeter && Settings.enableDpsMeterOnlineRaw))
                    {
                        return;
                    }

                    try
                    {
                        DamageNumberDiagnostics.OnPostfix(__instance, __originalMethod);
                    }
                    catch
                    {
                        // Keep diagnostics hooks non-fatal.
                    }
                }

                private static void LogMissingBinding(string phase)
                {
                    if (s_loggedMissing)
                        return;
                    s_loggedMissing = true;
                    MelonLogger.Warning($"[LeHud.Hooks]  DamageNumber diagnostics {phase} not found.");
                }
            }

            [HarmonyPatch]
            public class DamageNumber_InitDiagnostics
            {
                private static List<System.Reflection.MethodBase>? s_targets;
                private static bool s_loggedMissing;
                private static readonly bool s_enableInitDiagnostics = false;

                [HarmonyPrepare]
                public static bool Prepare()
                {
                    // Disabled for safety: patching DamageNumber.Init currently causes IL2CPP access violations.
                    // Keep the implementation for future controlled experiments.
                    if (!s_enableInitDiagnostics)
                        return false;

                    try
                    {
                        var damageNumberType = TypeLookup.FindType(
                            "Il2Cpp.DamageNumber",
                            "DamageNumber",
                            "Il2CppLE.DamageNumber");
                        if (damageNumberType == null)
                        {
                            LogMissingBinding("type");
                            return false;
                        }

                        s_targets = AccessTools.GetDeclaredMethods(damageNumberType)
                            .Where(m =>
                                !m.IsStatic
                                && !m.IsAbstract
                                && string.Equals(m.Name, "Init", StringComparison.Ordinal)
                                && (m.GetParameters().Length == 4 || m.GetParameters().Length == 10))
                            .Cast<System.Reflection.MethodBase>()
                            .ToList();

                        if (s_targets.Count == 0)
                        {
                            LogMissingBinding("methods");
                            return false;
                        }

                        MelonLogger.Msg($"[LeHud.Hooks]  DamageNumber Init diagnostics hooks bound ({s_targets.Count} overloads)");
                        return true;
                    }
                    catch (Exception e)
                    {
                        MelonLogger.Error($"[LeHud.Hooks]  DamageNumber Init diagnostics Prepare error: {e.Message}");
                        return false;
                    }
                }

                [HarmonyTargetMethods]
                public static IEnumerable<System.Reflection.MethodBase> TargetMethods() => s_targets ?? Enumerable.Empty<System.Reflection.MethodBase>();

                public static void Postfix(object __instance, System.Reflection.MethodBase __originalMethod)
                {
                    try
                    {
                        DamageNumberDiagnostics.OnInitPostfix(__instance, __originalMethod);
                    }
                    catch
                    {
                        // Keep diagnostics hooks non-fatal.
                    }
                }

                private static void LogMissingBinding(string phase)
                {
                    if (s_loggedMissing)
                        return;
                    s_loggedMissing = true;
                    MelonLogger.Warning($"[LeHud.Hooks]  DamageNumber Init diagnostics {phase} not found.");
                }
            }
            #endregion
        }
    }
}
