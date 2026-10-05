#nullable disable
using System;
using System.Collections.Generic;
using HarmonyLib;
using Il2Cpp;
using MelonLoader;
using Mod.Game;
using UnityEngine;

namespace Mod.Cheats.FallenPlugins;

internal static class AutoEnabler
{
    private const int RingPointCount = 48;
    private const float RingLineWidth = 0.12f;
    private const float RingYOffset = 0.15f;
    private static readonly Dictionary<string, string> TargetNames = new()
    {
        ["Shrine Placement Manager"] = "Shrine",
        ["Chest Placement Manager"] = "Chest",
        ["Tomb Reward Chest"] = "Cemetery Chest",
        ["Monolith Reward Chest"] = "Monolith Chest",
        ["Cache Click Listener"] = "Cache",
        ["void portal"] = "Void Portal",
        ["Rune Prison Visuals"] = "Rune Prison",
        ["Time Beast Rift Visuals"] = "Time Beast Rift"
    };

    private sealed class Target
    {
        public long Pointer;
        public WorldObjectClickListener Listener;
        public Transform Transform;
        public string Name;
        public string Type;
        public GameObject Ring;
        public Vector3 LastPosition;
    }

    private static readonly List<Target> Targets = new();
    private static readonly HashSet<long> KnownPointers = new();
    private static MelonPreferences_Category Category;
    public static MelonPreferences_Entry<bool> ShowRings;
    public static MelonPreferences_Entry<bool> DebugLog;
    public static MelonPreferences_Entry<float> Distance;
    public static MelonPreferences_Entry<Color> RingColor;
    public static readonly Dictionary<string, MelonPreferences_Entry<bool>> TypeEnabled = new();
    private static GameObject RingTemplate;
    private static Transform PlayerTransform;
    private static float NextUpdateAt;
    private static float CachedDistance = -1f;
    private static Color CachedColor;
    private static bool CachedShowRings;
    public static string LastEvent { get; private set; } = "Waiting for nearby interactables";
    public static int TrackedCount => Targets.Count;

    public static void Initialize()
    {
        if (Category != null) return;
        Category = MelonPreferences.CreateCategory("ProximityManager", "Auto Enabler");
        Category.SetFilePath("UserData/FallenProximity.cfg");
        ShowRings = Category.CreateEntry("ShowRings", true, "Show proximity rings");
        DebugLog = Category.CreateEntry("DebugLog", false, "Log target tracking and activations");
        Distance = Category.CreateEntry("Distance", 5f, "Activation radius");
        RingColor = Category.CreateEntry("RingColor", new Color(0.1f, 0.8f, 1f, 0.5f), "Ring color");
        foreach (string type in TargetNames.Values)
            if (!TypeEnabled.ContainsKey(type))
                TypeEnabled[type] = Category.CreateEntry($"Enable_{type.Replace(" ", string.Empty)}", true, $"Auto-activate {type}");
        CreateRingTemplate();
    }

    public static IEnumerable<KeyValuePair<string, MelonPreferences_Entry<bool>>> TypeOptions => TypeEnabled;

    public static void ApplySettings()
    {
        if (Category == null) return;
        CachedDistance = -1f;
        UpdateRingsIfNeeded();
    }

    public static void Save()
    {
        try { Category?.SaveToFile(false); } catch { }
    }

    public static void OnSceneInitialized()
    {
        PlayerTransform = null;
        for (int i = Targets.Count - 1; i >= 0; i--)
        {
            Target target = Targets[i];
            if (target.Listener == null || target.Transform == null || !target.Transform.gameObject.activeInHierarchy)
                RemoveTargetAt(i);
        }
    }

    public static void Update()
    {
        if (Category == null || Time.unscaledTime < NextUpdateAt) return;
        NextUpdateAt = Time.unscaledTime + 0.25f;

        try
        {
            GameObject localPlayer = ObjectManager.GetLocalPlayer();
            if (localPlayer == null)
            {
                PlayerTransform = null;
                return;
            }
            if (PlayerTransform == null || PlayerTransform.gameObject == null || PlayerTransform.gameObject.Pointer != localPlayer.Pointer)
                PlayerTransform = localPlayer.transform;
        }
        catch { PlayerTransform = null; }

        if (PlayerTransform == null) return;
        UpdateRingsIfNeeded();

        float limit = Mathf.Pow(Mathf.Clamp(Distance.Value, 1f, 10f), 2f);
        Vector3 playerPosition = PlayerTransform.position;
        for (int i = Targets.Count - 1; i >= 0; i--)
        {
            Target target = Targets[i];
            if (!IsTargetAlive(target, i)) continue;
            if (target.Ring != null && Vector3.SqrMagnitude(target.LastPosition - target.Transform.position) > 0.01f)
            {
                target.LastPosition = target.Transform.position;
                SnapRing(target.Ring.transform, target.LastPosition);
            }
            if (!TypeEnabled.TryGetValue(target.Type, out var enabled) || !enabled.Value) continue;

            Vector3 delta = target.Transform.position - playerPosition;
            float effectiveLimit = target.Name.Contains("Frontend", StringComparison.OrdinalIgnoreCase)
                ? Mathf.Min(limit, 25f) : limit;
            if (delta.x * delta.x + delta.z * delta.z > effectiveLimit) continue;

            try
            {
                target.Listener.ObjectClick(target.Transform.gameObject, true);
                LastEvent = $"Activated {target.Type}: {target.Name}";
                if (DebugLog.Value) MelonLogger.Msg($"[LEHud] Auto Enabler {LastEvent}");
            }
            catch (Exception e)
            {
                MelonLogger.Warning($"[LEHud] Auto Enabler could not activate {target.Type}: {e.Message}");
            }
            RemoveTargetAt(i);
        }
    }

    private static void UpdateRingsIfNeeded()
    {
        float distance = Distance.Value;
        Color color = RingColor.Value;
        bool visible = ShowRings.Value;
        if (Mathf.Approximately(distance, CachedDistance) && color == CachedColor && visible == CachedShowRings) return;
        CachedDistance = distance;
        CachedColor = color;
        CachedShowRings = visible;
        foreach (Target target in Targets)
        {
            if (!visible)
            {
                if (target.Ring != null) UnityEngine.Object.Destroy(target.Ring);
                target.Ring = null;
            }
            else if (target.Ring == null && target.Transform != null)
                target.Ring = CreateRing(target.Transform.gameObject, target.Name);
            else if (target.Ring != null)
                UpdateRing(target.Ring, target.Name, distance, color);
        }
    }

    private static bool IsTargetAlive(Target target, int index)
    {
        try
        {
            if (target.Listener != null && target.Listener.Pointer != IntPtr.Zero &&
                target.Transform != null && target.Transform.Pointer != IntPtr.Zero &&
                target.Transform.gameObject.activeInHierarchy) return true;
        }
        catch { }
        RemoveTargetAt(index);
        return false;
    }

    private static void RemoveTargetAt(int index)
    {
        Target target = Targets[index];
        if (target.Ring != null) UnityEngine.Object.Destroy(target.Ring);
        KnownPointers.Remove(target.Pointer);
        Targets.RemoveAt(index);
    }

    private static void Register(WorldObjectClickListener listener)
    {
        try
        {
            if (listener == null || listener.Pointer == IntPtr.Zero) return;
            long pointer = listener.Pointer.ToInt64();
            if (KnownPointers.Contains(pointer)) return;
            GameObject go = listener.gameObject;
            if (go == null || !TryGetTargetType(go, out string type)) return;
            KnownPointers.Add(pointer);
            Targets.Add(new Target
            {
                Pointer = pointer,
                Listener = listener,
                Transform = go.transform,
                Name = go.name ?? string.Empty,
                Type = type,
                LastPosition = go.transform.position,
                Ring = ShowRings.Value ? CreateRing(go, go.name) : null
            });
            LastEvent = $"Tracking {type}: {go.name}";
            if (DebugLog.Value) MelonLogger.Msg($"[LEHud] Auto Enabler {LastEvent}");
        }
        catch (Exception e) { MelonLogger.Warning($"[LEHud] Auto Enabler target registration failed: {e.Message}"); }
    }

    private static bool TryGetTargetType(GameObject go, out string type)
    {
        for (Transform current = go.transform; current != null; current = current.parent)
        {
            string name = current.name;
            foreach (var keyword in TargetNames)
            {
                if (name.Contains(keyword.Key, StringComparison.OrdinalIgnoreCase))
                {
                    type = keyword.Value;
                    return true;
                }
            }
        }
        type = string.Empty;
        return false;
    }

    private static void CreateRingTemplate()
    {
        if (RingTemplate != null) return;
        RingTemplate = new GameObject("LEHud_AutoEnablerRingTemplate");
        RingTemplate.SetActive(false);
        UnityEngine.Object.DontDestroyOnLoad(RingTemplate);
        LineRenderer line = RingTemplate.AddComponent<LineRenderer>();
        line.useWorldSpace = false;
        line.widthMultiplier = 0.12f;
        line.positionCount = RingPointCount;
        line.loop = true;
        line.castShadows = false;
        line.material = new Material(Shader.Find("UI/Default"));
        line.material.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always);
        line.material.renderQueue = 3500;
        UpdateRing(line, string.Empty, 5f, RingColor.Value);
    }

    private static GameObject CreateRing(GameObject target, string name)
    {
        try
        {
            if (!ShowRings.Value || RingTemplate == null) return null;
            GameObject ring = UnityEngine.Object.Instantiate(RingTemplate);
            ring.name = "LEHud_AutoEnablerRing";
            ring.transform.SetParent(null);
            var line = ring.GetComponent<LineRenderer>();
            UpdateRing(line, name, Distance.Value, RingColor.Value);
            ring.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            SnapRing(ring.transform, target.transform.position);
            ring.SetActive(true);
            return ring;
        }
        catch { return null; }
    }

    private static void UpdateRing(GameObject ring, string name, float radius, Color color)
    {
        if (ring == null) return;
        UpdateRing(ring.GetComponent<LineRenderer>(), name, radius, color);
    }

    private static void UpdateRing(LineRenderer line, string name, float radius, Color color)
    {
        if (line == null) return;
        line.material.color = color;
        float drawRadius = name.Contains("Frontend", StringComparison.OrdinalIgnoreCase) ? Mathf.Min(radius, 5f) : radius;
        for (int i = 0; i < RingPointCount; i++)
        {
            float angle = 2f * Mathf.PI * i / RingPointCount;
            line.SetPosition(i, new Vector3(drawRadius * Mathf.Cos(angle), drawRadius * Mathf.Sin(angle), 0f));
        }
    }

    private static void SnapRing(Transform ring, Vector3 position)
    {
        Vector3 rayStart = position + Vector3.up * 3f;
        ring.position = Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, 6f, (1 << 0) | (1 << 11) | (1 << 14))
            ? hit.point + Vector3.up * 0.15f : position + Vector3.up * 0.15f;
    }

    [HarmonyPatch(typeof(InteractableListener), nameof(InteractableListener.Awake))]
    private static class InteractableAwakePatch
    {
        [HarmonyPostfix]
        private static void Postfix(InteractableListener __instance)
        {
            try
            {
                if (__instance == null) return;
                WorldObjectClickListener listener = __instance.TryCast<WorldObjectClickListener>();
                if (listener != null) Register(listener);
            }
            catch { }
        }
    }

    [HarmonyPatch(typeof(WorldObjectClickListener), nameof(WorldObjectClickListener.OnEnable))]
    private static class WorldObjectEnabledPatch
    {
        [HarmonyPostfix]
        private static void Postfix(WorldObjectClickListener __instance) => Register(__instance);
    }
}
