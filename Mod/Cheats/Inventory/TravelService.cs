#nullable disable
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Il2Cpp;
using MelonLoader;
using Mod.Game;
using UnityEngine;

namespace Mod.Cheats.Inventory
{
    // The hardened teleport engine. History (see ARCHAEOLOGY.md):
    //  • NEVER PlayerSync.SendAttemptWaypoint — the server force-disconnects
    //    unless you are physically standing on a waypoint.
    //  • MapPanel.quickTravel(scene) is the game's working inventory quick-
    //    travel path; the other waypoint methods silently return in this build.
    //  • Waypoint data is refreshed through controller APIs only. Never toggle
    //    hidden UI roots: those ancestors can contain the game's MapPanel.
    //  • v1's fallback chain (map-flash + era-tab text-click + searching all
    //    buttons for "VISIT X") is DELETED. Prime-then-retry replaces it.
    internal static class TravelService
    {
        static bool _travelInProgress;
        static bool _primed;
        static bool _primerRunning;
        static bool _unlockUnreadableWarned;
        static float _nextPrimeRetryAt;
        static readonly HashSet<string> _warnedScenes = new();

        public static void EnsurePrimed()
        {
            if (_primed || _primerRunning) return;
            _primerRunning = true;
            MelonCoroutines.Start(PrimeCoroutine());
        }

        // The travel guard spans the whole scene transition (safety rule #3:
        // concurrent travel once summoned EHG's bug reporter) — it is cleared
        // here on scene load, with a timeout failsafe inside TravelCoroutine.
        public static void NotifySceneLoaded()
        {
            _travelInProgress = false;
            _primed = false;
            _nextPrimeRetryAt = 0f;
        }

        public static void Update()
        {
            if (_primed || _primerRunning || Time.time < _nextPrimeRetryAt) return;
            if (!IsPlayableScene()) return;
            _nextPrimeRetryAt = Time.time + 1f;
            EnsurePrimed();
        }

        public static void RequestTravel(string scene)
        {
            if (_travelInProgress)
            {
                Dbg.Log("travel already in progress — click ignored");
                return;
            }
            MelonCoroutines.Start(TravelCoroutine(scene));
        }

        // ── Travel ────────────────────────────────────────────────

        static IEnumerator TravelCoroutine(string scene)
        {
            _travelInProgress = true;
            EnsurePrimed();
            float waited = 0f;
            while (!_primed && waited < 20f)
            {
                if (!_primerRunning) EnsurePrimed();
                yield return new WaitForSeconds(0.25f);
                waited += 0.25f;
            }

            // One scene scan per click, shared by the gate and the lookup
            // (FindObjectsOfType over a big town scene is hitch-prone on Deck).
            UIWaypointController[] controllers = FindControllers();
            RefreshWaypointStates(controllers);
            UIWaypointStandard wp = FindWaypointForScene(controllers, scene);

            // SPEC travel rule 4: waypoint miss → re-run the primer ONCE,
            // retry once (a controller can miss its OnEnable, or a relog can
            // re-instantiate controllers the latched prime never saw).
            if (wp == null)
            {
                Dbg.Log($"'{scene}' not found — re-priming once");
                _primed = false;
                _nextPrimeRetryAt = 0f;
                EnsurePrimed();
                float w2 = 0f;
                while (!_primed && w2 < 20f)
                {
                    if (!_primerRunning) EnsurePrimed();
                    yield return new WaitForSeconds(0.25f);
                    w2 += 0.25f;
                }
                controllers = FindControllers();
                RefreshWaypointStates(controllers);
                wp = FindWaypointForScene(controllers, scene);
            }

            if (wp == null)
            {
                if (_warnedScenes.Add(scene))   // once per scene per session
                    MelonLogger.Warning($"[LEHud] Quick teleport unavailable: waypoint '{scene}' not present after data refresh (controllers={controllers?.Length ?? 0}).");
                _travelInProgress = false;
                yield break;
            }

            // Unlock gate — behave exactly like the map's own locked node:
            // not unlocked → do nothing, leave NO game-state footprint.
            if (!IsUnlocked(controllers, scene, wp))
            {
                // The first gameplay load can finish before the player's
                // waypoint state reaches every hidden era controller. Re-prime
                // once and re-read the native state before treating it as locked.
                _primed = false;
                _nextPrimeRetryAt = 0f;
                EnsurePrimed();
                float w3 = 0f;
                while (!_primed && w3 < 10f)
                {
                    if (!_primerRunning) EnsurePrimed();
                    yield return new WaitForSeconds(0.25f);
                    w3 += 0.25f;
                }
                float unlockWaited = 0f;
                while (unlockWaited < 10f)
                {
                    yield return new WaitForSeconds(0.5f);
                    unlockWaited += 0.5f;
                    controllers = FindControllers();
                    RefreshWaypointStates(controllers);
                    wp = FindWaypointForScene(controllers, scene);
                    if (wp != null && IsUnlocked(controllers, scene, wp)) break;
                }
                if (wp == null || !IsUnlocked(controllers, scene, wp))
                {
                    MelonLogger.Warning($"[LEHud] Quick teleport refused: '{scene}' is locked or the character unlock data is not ready.");
                    _travelInProgress = false;
                    yield break;
                }
            }

            float managerWaited = 0f;
            bool waypointManagerReady = false;
            while (!waypointManagerReady && managerWaited < 10f)
            {
                waypointManagerReady = EnableWaypointForTravel();
                if (waypointManagerReady) break;
                yield return new WaitForSeconds(0.25f);
                managerWaited += 0.25f;
            }
            if (!waypointManagerReady)
            {
                MelonLogger.Warning("[LEHud] Quick teleport cancelled: WaypointManager did not initialize within 10 seconds.");
                _travelInProgress = false;
                yield break;
            }

            bool fired = false;
            try
            {
                InvokeQuickTravel(wp, scene);
                fired = true;
            }
            catch (Exception e)
            {
                MelonLogger.Warning($"[LEHud] Quick teleport to '{scene}' failed: {e.Message}");
            }

            if (!fired)
            {
                _travelInProgress = false;
                yield break;
            }

            // Hold the guard across the transition; NotifySceneLoaded clears
            // it on arrival, the timeout covers a silently failed load.
            float guard = 0f;
            while (_travelInProgress && guard < 10f)
            {
                yield return new WaitForSeconds(0.5f);
                guard += 0.5f;
            }
            _travelInProgress = false;
        }

        static void InvokeQuickTravel(UIWaypointStandard waypoint, string scene)
        {
            MapPanel panel = waypoint._mapPanel;
            if (panel == null)
                panel = UnityEngine.Object.FindObjectOfType<MapPanel>(true);
            if (panel == null)
                throw new InvalidOperationException("MapPanel instance was not found");

            waypoint._mapPanel = panel;
            MethodInfo method = AccessTools.Method(typeof(MapPanel), "quickTravel", new[] { typeof(string) });
            if (method == null) throw new MissingMethodException("MapPanel.quickTravel(string)");
            method.Invoke(panel, new object[] { scene });
        }

        static UIWaypointController[] FindControllers()
        {
            try { return UnityEngine.Object.FindObjectsOfType<UIWaypointController>(true); }
            catch { return null; }
        }

        static bool IsPlayableScene()
        {
            return ObjectManager.HasPlayer();
        }

        // ── Unlock gate ───────────────────────────────────────────
        // True only when an era controller positively lists the scene as unlocked.

        static bool IsUnlocked(UIWaypointController[] all, string scene, UIWaypointStandard waypoint)
        {
            try
            {
                if (waypoint.alwaysUnlocked || waypoint.isActive || waypoint.playerIsHere) return true;
            }
            catch { }

            bool readAnything = false;
            if (all != null)
            {
                foreach (UIWaypointController ctrl in all)
                {
                    try
                    {
                        var unlocked = ctrl.unlockedScenes;
                        if (unlocked == null) continue;
                        int n = unlocked.Count;
                        readAnything = true;
                        for (int i = 0; i < n; i++)
                        {
                            string unlockedScene = unlocked[i] ?? "";
                            if (unlockedScene == scene ||
                                (waypoint != null && unlockedScene == waypoint.sceneName))
                                return true;
                        }
                    }
                    catch { }
                }
            }

            // Recent game builds can expose unlock strings that do not use
            // the scene IDs passed to this menu. Fall back to the native
            // waypoint's own unlocked/clickable state rather than blocking
            // every destination on a string mismatch.
            if (waypoint != null)
            {
                try
                {
                    if (waypoint.alwaysUnlocked) return true;
                    var button = waypoint.waypointButton;
                    if (button != null && button.interactable) return true;
                }
                catch { }
            }

            if (!readAnything)
            {
                // Council A1: unreadable unlock data fails closed instead of authorizing travel.
                if (!_unlockUnreadableWarned)
                {
                    _unlockUnreadableWarned = true;
                    Dbg.Log("unlock data unreadable — travel refused");
                }
                return false;
            }
            return false;
        }

        static void RefreshWaypointStates(UIWaypointController[] all)
        {
            if (all == null) return;
            MonolithProgressManager monolithProgress = null;
            try { monolithProgress = UnityEngine.Object.FindObjectOfType<MonolithProgressManager>(true); } catch { }
            var localUnlockedScenes = GetLocalUnlockedScenes();

            foreach (UIWaypointController controller in all)
            {
                try
                {
                    controller.GetAllWaypoints();
                    var unlockedScenes = localUnlockedScenes ?? controller.unlockedScenes;
                    var waypoints = controller.waypointsInMenu;
                    if (unlockedScenes == null || waypoints == null) continue;
                    for (int i = 0; i < waypoints.Count; i++)
                    {
                        try
                        {
                            UIWaypoint waypoint = waypoints[i];
                            if (waypoint != null)
                                waypoint.CheckWaypoint(unlockedScenes, monolithProgress);
                        }
                        catch { }
                    }
                }
                catch { }
            }
        }

        static Il2CppSystem.Collections.Generic.List<string> GetLocalUnlockedScenes()
        {
            try
            {
                GameObject localPlayer = ObjectManager.GetLocalPlayer();
                var trackers = CharacterDataTracker.all;
                if (localPlayer == null || trackers == null) return null;

                for (int i = 0; i < trackers.Count; i++)
                {
                    try
                    {
                        CharacterDataTracker tracker = trackers[i];
                        if (tracker == null || tracker.actor == null) continue;
                        GameObject owner = tracker.actor.gameObject;
                        if (owner != null &&
                            (ReferenceEquals(owner, localPlayer) || owner.Pointer == localPlayer.Pointer))
                            return tracker.getUnlockedScenes();
                    }
                    catch { }
                }
            }
            catch { }
            return null;
        }

        static bool EnableWaypointForTravel()
        {
            try
            {
                var manager = WaypointManager.instance;
                if (manager == null) return false;
                manager.WaypointEnabled = true;
                manager.EnableWaypoint();
                Dbg.Log("enabled current-zone waypoint travel after destination passed unlock check");
                return true;
            }
            catch (Exception e)
            {
                MelonLogger.Warning($"could not enable waypoint travel state: {e.Message}");
                return false;
            }
        }

        // ── Waypoint lookup ───────────────────────────────────────
        // Search ALL controllers — FindObjectOfType (singular) was v1's
        // original only-End-of-Time-works bug.

        static UIWaypointStandard FindWaypointForScene(UIWaypointController[] all, string targetScene)
        {
            if (all == null) return null;
            foreach (UIWaypointController ctrl in all)
            {
                int count = 0;
                try { count = ctrl.waypointsInMenu?.Count ?? 0; } catch { }
                for (int i = 0; i < count; i++)
                {
                    try
                    {
                        UIWaypointStandard w = ctrl.waypointsInMenu[i]?.TryCast<UIWaypointStandard>();
                        if (w != null && (w.sceneName ?? "") == targetScene)
                            return w;
                    }
                    catch { }
                }
            }
            return null;
        }

        static bool _dumped;

        // One-time list of every native waypoint scene id, so new destinations can be added reliably.
        static void DumpWaypointScenes(UIWaypointController[] all)
        {
            if (_dumped) return;
            _dumped = true;
            try
            {
                var names = new List<string>();
                foreach (UIWaypointController ctrl in all)
                {
                    int count = ctrl.waypointsInMenu?.Count ?? 0;
                    for (int i = 0; i < count; i++)
                    {
                        UIWaypointStandard w = ctrl.waypointsInMenu[i]?.TryCast<UIWaypointStandard>();
                        if (w != null && !string.IsNullOrEmpty(w.sceneName) && !names.Contains(w.sceneName))
                            names.Add(w.sceneName);
                    }
                }
                MelonLogger.Msg("[LEHud] Waypoint scenes: " + string.Join(", ", names));
            }
            catch { }
        }

        // Data-only initialization. Never activate controllers/ancestors:
        // those UI roots can contain MapPanel and toggling them opens the map.

        static IEnumerator PrimeCoroutine()
        {
            float waited = 0f;
            while (waited < 30f)
            {
                if (IsPlayableScene()) break;
                yield return new WaitForSeconds(0.5f);
                waited += 0.5f;
            }
            UIWaypointController[] all = FindControllers();
            RefreshWaypointStates(all);

            bool haveWaypointData = false;
            if (all != null)
            {
                foreach (UIWaypointController controller in all)
                {
                    try
                    {
                        if (controller.waypointsInMenu != null && controller.waypointsInMenu.Count > 0)
                        {
                            haveWaypointData = true;
                            break;
                        }
                    }
                    catch { }
                }
            }

            _primed = haveWaypointData;
            _primerRunning = false;
            if (_primed)
            {
                Dbg.Log($"data refresh: waypoint data available from {all.Length} controllers");
                DumpWaypointScenes(all);
            }
            else
            {
                _nextPrimeRetryAt = Time.time + 1f;
                Dbg.Log($"data refresh: no waypoint data in {all?.Length ?? 0} controllers — retry scheduled");
            }
        }
    }
}
