using UnityEngine;
using MelonLoader;
using Il2Cpp;
// using Il2CppDMM;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppLE.UI;
using Il2CppLE.Telemetry;
using Il2CppLE.Telemetry.Logger;
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
            #region security / detection patches
            [HarmonyPatch(typeof(UIBase), "Awake")]
            public class UIBase_Awake : MelonMod
            {
                public static void Prefix(ref UIBase __instance)
                {
                    Log.MarkGamePhase("UIBase.Awake");
                    MelonLogger.Msg("[LeHud.Hooks]  UIBase.Awake hooked. Disabling bug submission button");
                    //__instance.gameObject.SetActive(false);
                    if (__instance != null)
                    {
                        AutoDisconnect.SetUIBase(__instance);
                        BugReportUiDisabler.TryDisableBugReportUi(__instance, "UIBase.Awake");
                        // AntiIdleSystem.SetUIBase(__instance); // UI pulse disabled; keep for future use
                    }
                }
            }

            [HarmonyPatch(typeof(CharacterSelect), "Awake")]
            public class CharacterSelect_ : MelonMod
            {
                public static void Prefix(ref CharacterSelect __instance)
                {
                    Log.MarkGamePhase("CharacterSelect.Awake");
                    MelonLogger.Msg("[LeHud.Hooks]  CharacterSelect.Awake hooked. Disabling bug submission button");
                    BugReportUiDisabler.TryDisableBugReportUi(__instance, "CharacterSelect.Awake");
                }
            }

            [HarmonyPatch(typeof(UIBase), "OpenBugReportPanel")]
            public class UIBase_OpenBugReportPanel : MelonMod
            {
                public static bool Prefix(ref UIBase __instance)
                {
                    BugReportUiDisabler.LogOnce("UIBase.OpenBugReportPanel:block", "[LeHud.Hooks]  UIBase.OpenBugReportPanel hooked and blocked.");
                    BugReportUiDisabler.TryDisableBugReportUi(__instance, "UIBase.OpenBugReportPanel");
                    return false;
                }
            }

            [HarmonyPatch(typeof(UIBase), "IsBugReportPanelOpen")]
            public class UIBase_IsBugReportPanelOpen : MelonMod
            {
                public static void Postfix(ref UIBase __instance, ref bool __result)
                {
                    BugReportUiDisabler.TryDisableBugReportUi(__instance, "UIBase.IsBugReportPanelOpen");
                    __result = false;
                }
            }

            [HarmonyPatch]
            public class ControllerButtonHoldManager_StartBugReportButtonHold : MelonMod
            {
                private static System.Reflection.MethodBase? s_target;

                [HarmonyPrepare]
                public static bool Prepare()
                {
                    try
                    {
                        var controllerHoldManagerType = TypeLookup.FindType(
                            "Il2Cpp.ControllerButtonHoldManager",
                            "ControllerButtonHoldManager");
                        if (controllerHoldManagerType == null)
                            return false;

                        s_target = AccessTools.GetDeclaredMethods(controllerHoldManagerType)
                            .FirstOrDefault(m => string.Equals(m.Name, "StartBugReportButtonHold", StringComparison.Ordinal));
                        if (s_target == null)
                            return false;

                        MelonLogger.Msg("[LeHud.Hooks]  ControllerButtonHoldManager.StartBugReportButtonHold hook bound.");
                        return true;
                    }
                    catch (Exception e)
                    {
                        MelonLogger.Error($"[LeHud.Hooks]  ControllerButtonHoldManager.StartBugReportButtonHold Prepare error: {e.Message}");
                        return false;
                    }
                }

                [HarmonyTargetMethod]
                public static System.Reflection.MethodBase TargetMethod() => s_target!;

                public static bool Prefix(object __instance)
                {
                    BugReportUiDisabler.TryDisableBugReportUi(__instance, "ControllerButtonHoldManager.StartBugReportButtonHold");
                    BugReportUiDisabler.LogOnce(
                        key: "ControllerButtonHoldManager.StartBugReportButtonHold:block",
                        message: "[LeHud.Hooks]  ControllerButtonHoldManager.StartBugReportButtonHold hooked and blocked.");
                    return false;
                }
            }

            [HarmonyPatch]
            public class MainMenuPanel_BugReportHooks : MelonMod
            {
                private static List<System.Reflection.MethodBase>? s_targets;
                // Quiet lookups: AccessTools logs a warning per miss, and the game's member is named _bugReportButton.
                private const System.Reflection.BindingFlags AnyMember = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;

                private static System.Reflection.FieldInfo? FindBugButtonField(Type type)
                    => type.GetField("_bugReportButton", AnyMember) ?? type.GetField("bugReportButton", AnyMember);

                private static System.Reflection.MethodInfo? FindBugButtonGetter(Type type)
                    => (type.GetProperty("_bugReportButton", AnyMember) ?? type.GetProperty("bugReportButton", AnyMember))?.GetGetMethod(true);

                private static System.Reflection.FieldInfo? s_bugReportButtonField;
                private static System.Reflection.MethodInfo? s_bugReportButtonGetter;

                [HarmonyPrepare]
                public static bool Prepare()
                {
                    try
                    {
                        var mainMenuPanelType = TypeLookup.FindType("Il2CppLE.UI.PanelSystem.MainMenuPanel");
                        if (mainMenuPanelType == null)
                            return false;

                        s_targets = AccessTools.GetDeclaredMethods(mainMenuPanelType)
                            .Where(m => !m.IsStatic
                                && (m.Name.IndexOf("BugReport", StringComparison.OrdinalIgnoreCase) >= 0
                                    || string.Equals(m.Name, "Awake", StringComparison.Ordinal)
                                    || string.Equals(m.Name, "Start", StringComparison.Ordinal)
                                    || string.Equals(m.Name, "OnEnable", StringComparison.Ordinal)
                                    || string.Equals(m.Name, "Open", StringComparison.Ordinal)
                                    || string.Equals(m.Name, "Initialize", StringComparison.Ordinal)))
                            .Cast<System.Reflection.MethodBase>()
                            .ToList();

                        // Hiding the button from inside the panel's own Awake/OnEnable/Open hooks coincided with
                        // stack-overflow crashes on Escape/quit, so the lookup stays disabled (it never matched before).
                        s_bugReportButtonField = null;
                        s_bugReportButtonGetter = null;

                        if (s_targets.Count == 0)
                            return false;

                        MelonLogger.Msg($"[LeHud.Hooks]  MainMenuPanel bug-report hooks bound: {string.Join(", ", s_targets.Select(t => t.Name).Distinct())}");
                        return true;
                    }
                    catch (Exception e)
                    {
                        MelonLogger.Error($"[LeHud.Hooks]  MainMenuPanel bug-report hook Prepare error: {e.Message}");
                        return false;
                    }
                }

                [HarmonyTargetMethods]
                public static IEnumerable<System.Reflection.MethodBase> TargetMethods() => s_targets ?? Enumerable.Empty<System.Reflection.MethodBase>();

                public static void Postfix(object __instance, System.Reflection.MethodBase __originalMethod)
                {
                    string source = $"MainMenuPanel.{__originalMethod?.Name ?? "Unknown"}";
                    TryDisableMainMenuBugButton(__instance, source);
                    BugReportUiDisabler.TryDisableBugReportUi(__instance, source);
                }

                private static void TryDisableMainMenuBugButton(object? instance, string source)
                {
                    if (instance == null)
                        return;

                    try
                    {
                        object? bugButton = null;
                        if (s_bugReportButtonField != null)
                        {
                            bugButton = s_bugReportButtonField.GetValue(instance);
                        }
                        else if (s_bugReportButtonGetter != null)
                        {
                            bugButton = s_bugReportButtonGetter.Invoke(instance, null);
                        }

                        if (bugButton == null)
                            return;

                        if (bugButton is Component component && component.gameObject != null)
                        {
                            component.gameObject.SetActive(false);
                            BugReportUiDisabler.LogOnce(
                                key: $"{source}:MainMenuPanel.bugReportButton:hidden-fastpath",
                                message: $"[LeHud.Hooks]  {source} hid MainMenuPanel.bugReportButton.");
                            return;
                        }

                        if (bugButton is GameObject gameObject)
                        {
                            gameObject.SetActive(false);
                            BugReportUiDisabler.LogOnce(
                                key: $"{source}:MainMenuPanel.bugReportButton.go:hidden-fastpath",
                                message: $"[LeHud.Hooks]  {source} hid MainMenuPanel.bugReportButton.gameObject.");
                        }
                    }
                    catch
                    {
                        // Keep hook non-fatal; reflection fallback handles version variance.
                    }
                }
            }

            [HarmonyPatch]
            public class PanelSystem_BugReportHooks : MelonMod
            {
                private static List<System.Reflection.MethodBase>? s_targets;

                [HarmonyPrepare]
                public static bool Prepare()
                {
                    try
                    {
                        var panelSystemType = TypeLookup.FindType("Il2CppLE.UI.PanelSystem.PanelSystem");
                        if (panelSystemType == null)
                            return false;

                        s_targets = AccessTools.GetDeclaredMethods(panelSystemType)
                            .Where(m => !m.IsStatic
                                && (m.Name.IndexOf("BugReport", StringComparison.OrdinalIgnoreCase) >= 0
                                    || string.Equals(m.Name, "Awake", StringComparison.Ordinal)
                                    || string.Equals(m.Name, "Start", StringComparison.Ordinal)
                                    || string.Equals(m.Name, "OnEnable", StringComparison.Ordinal)
                                    || string.Equals(m.Name, "Open", StringComparison.Ordinal)
                                    || string.Equals(m.Name, "Initialize", StringComparison.Ordinal)))
                            .Cast<System.Reflection.MethodBase>()
                            .ToList();

                        if (s_targets.Count == 0)
                            return false;

                        MelonLogger.Msg($"[LeHud.Hooks]  PanelSystem bug-report hooks bound: {string.Join(", ", s_targets.Select(t => t.Name).Distinct())}");
                        return true;
                    }
                    catch (Exception e)
                    {
                        MelonLogger.Error($"[LeHud.Hooks]  PanelSystem bug-report hook Prepare error: {e.Message}");
                        return false;
                    }
                }

                [HarmonyTargetMethods]
                public static IEnumerable<System.Reflection.MethodBase> TargetMethods() => s_targets ?? Enumerable.Empty<System.Reflection.MethodBase>();

                public static void Postfix(object __instance, System.Reflection.MethodBase __originalMethod)
                {
                    BugReportUiDisabler.TryDisableBugReportUi(__instance, $"PanelSystem.{__originalMethod?.Name ?? "Unknown"}");
                }
            }

            [HarmonyPatch]
            public class BugReportPanel_OpenMethods : MelonMod
            {
                private static List<System.Reflection.MethodBase>? s_targets;

                [HarmonyPrepare]
                public static bool Prepare()
                {
                    try
                    {
                        var bugReportPanelType = TypeLookup.FindType("Il2CppLE.UI.PanelSystem.BugReportPanel");
                        if (bugReportPanelType == null)
                            return false;

                        s_targets = AccessTools.GetDeclaredMethods(bugReportPanelType)
                            .Where(m => !m.IsStatic
                                && !m.IsAbstract
                                && (m.Name.IndexOf("Open", StringComparison.OrdinalIgnoreCase) >= 0
                                    || m.Name.IndexOf("Show", StringComparison.OrdinalIgnoreCase) >= 0)
                                && m.Name.IndexOf("Close", StringComparison.OrdinalIgnoreCase) < 0)
                            .Cast<System.Reflection.MethodBase>()
                            .ToList();

                        if (s_targets.Count == 0)
                            return false;

                        MelonLogger.Msg($"[LeHud.Hooks]  BugReportPanel open hooks bound: {string.Join(", ", s_targets.Select(t => t.Name).Distinct())}");
                        return true;
                    }
                    catch (Exception e)
                    {
                        MelonLogger.Error($"[LeHud.Hooks]  BugReportPanel open hook Prepare error: {e.Message}");
                        return false;
                    }
                }

                [HarmonyTargetMethods]
                public static IEnumerable<System.Reflection.MethodBase> TargetMethods() => s_targets ?? Enumerable.Empty<System.Reflection.MethodBase>();

                public static bool Prefix(object __instance, System.Reflection.MethodBase __originalMethod)
                {
                    string methodName = __originalMethod?.Name ?? "Unknown";
                    BugReportUiDisabler.TryDisableBugReportUi(__instance, $"BugReportPanel.{methodName}");
                    BugReportUiDisabler.LogOnce(
                        key: $"BugReportPanel.{methodName}:blocked",
                        message: $"[LeHud.Hooks]  BugReportPanel.{methodName} hooked and blocked.");
                    return false;
                }
            }

            [HarmonyPatch(typeof(BugSubmitter), "Submit")]
            public class BugSubmitter_Submit : MelonMod
            {
                public static bool Prefix(ref BugSubmitter __instance)
                {
                    MelonLogger.Msg("[LeHud.Hooks]  BugSubmitter.Submit hooked and blocked.");
                    if (__instance != null && __instance.gameObject != null)
                        __instance.gameObject.SetActive(false);
                    return false;
                }
            }

            [HarmonyPatch(typeof(BugSubmitter), "ShowSubmitPanel")]
            public class BugSubmitter_ShowSubmitPanel : MelonMod
            {
                public static bool Prefix(ref BugSubmitter __instance)
                {
                    MelonLogger.Msg("[LeHud.Hooks]  BugSubmitter.ShowSubmitPanel hooked and blocked.");
                    if (__instance != null && __instance.btn_Submit != null && __instance.btn_Submit.gameObject != null)
                        __instance.btn_Submit.gameObject.SetActive(false);
                    return false;
                }
            }

            [HarmonyPatch(typeof(ClientLogHandler), nameof(ClientLogHandler.LogFormat),
                typeof(LogType), typeof(UnityEngine.Object), typeof(string), typeof(Il2CppReferenceArray<Il2CppSystem.Object>))]
            public class ClientLogHandler_LogFormat : MelonMod
            {
                public static bool Prefix(LogType logType, UnityEngine.Object context, string format, Il2CppReferenceArray<Il2CppSystem.Object> args)
                {
                    Log.GameLog(logType, context?.name, format, ExtractArgs(args));

                    return false;
                }

                private static IReadOnlyList<string>? ExtractArgs(Il2CppReferenceArray<Il2CppSystem.Object> args)
                {
                    if (args == null || args.Length == 0)
                        return null;

                    var output = new string[args.Length];
                    for (int i = 0; i < args.Length; i++)
                    {
                        try
                        {
                            output[i] = args[i]?.ToString() ?? "null";
                        }
                        catch (Exception ex)
                        {
                            output[i] = $"<arg[{i}] {ex.GetType().Name}>";
                        }
                    }

                    return output;
                }
            }

            [HarmonyPatch(typeof(ClientLogHandler), nameof(ClientLogHandler.LogException),
                typeof(Il2CppSystem.Exception), typeof(UnityEngine.Object))]
            public class ClientLogHandler_LogException : MelonMod
            {
                public static bool Prefix(Il2CppSystem.Exception exception, UnityEngine.Object context)
                {
                    string exceptionText = exception?.ToString() ?? "null";
                    Log.GameException(context?.name, exceptionText);

                    if (Log.IsLikelyLoginFailureException(exceptionText))
                    {
                        Log.DumpRecentGameEventsThrottled(
                            key: "game-login-failure-exception",
                            reason: "Likely login/load exception observed",
                            minInterval: TimeSpan.FromSeconds(15),
                            maxLines: 80);
                    }
                    return false;
                }
            }

            [HarmonyPatch(typeof(AccountSupport), "GetLogsZip")]
            public class AccountSupport_GetLogsZip : MelonMod
            {
                public static bool Prefix(AccountSupport __instance)
                {
                    MelonLogger.Msg("[LeHud.Hooks]  AccountSupport_GetLogsZip hooked and blocked.");
                    return false;
                }
            }

            //[HarmonyPatch(typeof(CharacterSelectPanelUI), "OpenBugReport")]
            //public class CharacterSelectPanelUI_OpenBugReport : MelonMod
            //{
            //	public static void Prefix(ref CharacterSelectPanelUI __instance)
            //	{
            //		MelonLogger.Msg("[Mod] CharacterSelectPanelUI.OpenBugReport hooked. Disabling bug submission");
            //		//__instance.gameObject.SetActive(false);
            //		return;
            //	}
            //}

            //[HarmonyPatch(typeof(LandingZonePanel), "OnAwake")]
            //public class LandingZonePanel_ : MelonMod
            //{
            //	public static void Prefix(ref LandingZonePanel __instance)
            //	{
            //		MelonLogger.Msg("[Mod] LandingZonePanel.OnAwake hooked");
            //	}
            //}
            #endregion
        }
    }
}
