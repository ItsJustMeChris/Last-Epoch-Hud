#nullable disable
using System;
using System.Collections.Generic;
using Il2Cpp;
using MelonLoader;
using Mod.Game;
using UnityEngine;
using UnityEngine.UI;

namespace Mod.Cheats.CooldownTracker
{
    internal class SlotData
    {
        public int    SlotIndex;
        public string GameBoundKey;     // null = still probing, "" = gave up
        public Sprite Icon;
        public Image  CooldownBar;
        public float  Fill;
        public bool   OnCooldown;
        public bool   CooldownStateArmed;
        public float  CooldownObservedSince;
        public AbilityBarIcon Source;

        // Render cache — rebuilt only when the resolved label changes, so the
        // per-frame draw path allocates nothing.
        public string RawLabel;
        public string DisplayLabel = "";
        public bool   TwoLine;
        public bool   BadgeIsFace;     // label is a face button of the active mode
        public Color  BadgeColor;      // its pad colour when BadgeIsFace

        public float NextHotkeyRetry;
        public int   HotkeyTries;
    }

    // Thread-safe registry of action-bar slots, fed by the Harmony patches.
    // Slots whose source object has been destroyed (quit to menu, scene torn
    // down) are pruned on the next tick — nothing stale ever reaches the
    // renderer, so no more ghost icons on the login screen.
    internal static class SlotRegistry
    {
        public const int   SlotCount      = 7;   // 0-5 skills, 6 = evade
        const int   MaxHotkeyTries = 6;
        const float HotkeyRetrySec = 2f;

        static readonly List<SlotData> _slots = new();
        static readonly object _lock = new();
        static bool _liveReplacementWarned;
        static float _nextPlayerScanAt;

        public static int Count { get { lock (_lock) return _slots.Count; } }

        public static void Register(AbilityBarIcon icon)
        {
            if (icon == null) return;
            try
            {
                int idx = icon.abilityNumber;
                if ((uint)idx >= SlotCount) return;
                if (!BelongsToLocalPlayer(icon)) return;
                lock (_lock)
                {
                    var current = _slots.Find(x => x.SlotIndex == idx);
                    if (current != null && current.Source != null && SameNativeInstance(current.Source, icon))
                        return;
                }
                var s = new SlotData
                {
                    SlotIndex    = idx,
                    GameBoundKey = HotkeyReader.TryRead(icon),
                    Icon         = ReadSprite(icon),
                    CooldownBar  = icon.cooldownBar,
                    OnCooldown   = false,
                    CooldownStateArmed = !icon.cooldownBarActive,
                    CooldownObservedSince = 0f,
                    Source       = icon,
                };
                RefreshLabel(s);
                bool differentLiveSource = false;
                bool firstLiveReplacement = false;
                lock (_lock)
                {
                    var existing = _slots.Find(x => x.SlotIndex == idx);
                    try
                    {
                        differentLiveSource = existing != null && existing.Source != null &&
                            !SameNativeInstance(existing.Source, icon);
                    }
                    catch { }
                    if (differentLiveSource && !_liveReplacementWarned)
                    {
                        _liveReplacementWarned = true;
                        firstLiveReplacement = true;
                    }
                    _slots.RemoveAll(x => x.SlotIndex == idx);
                    int at = _slots.FindIndex(x => x.SlotIndex > idx);
                    if (at < 0) _slots.Add(s); else _slots.Insert(at, s);
                }
                if (differentLiveSource)
                {
                    Dbg.Log($"slot #{idx} re-registered from a different live AbilityBarIcon");
                    if (firstLiveReplacement)
                        MelonLogger.Warning($"slot #{idx} re-registered from a different live AbilityBarIcon — a second action bar may exist; report this line");
                }
                Dbg.Log($"slot #{idx} registered (key={s.GameBoundKey ?? "?"})");
                DumpIconHierarchy(icon, idx);
            }
            catch (Exception ex) { MelonLogger.Warning("RegisterSlot: " + ex.Message); }
        }

        // Managed wrappers can differ for one IL2CPP object, so compare native identity too.
        // council 2026-09-11 #2
        static bool SameNativeInstance(AbilityBarIcon a, AbilityBarIcon b) =>
            ReferenceEquals(a, b) || a.Pointer == b.Pointer;

        static bool BelongsToLocalPlayer(AbilityBarIcon icon)
        {
            try
            {
                GameObject owner = icon.player;
                GameObject local = ObjectManager.GetLocalPlayer();
                return owner != null && local != null && SameNativeInstance(owner, local);
            }
            catch { return false; }
        }

        static bool SameNativeInstance(GameObject a, GameObject b) =>
            ReferenceEquals(a, b) || a.Pointer == b.Pointer;

        public static void OnSceneInitialized()
        {
            lock (_lock) _slots.Clear();
            _nextPlayerScanAt = 0f;
        }

        static void ScanLocalPlayerSlots(float now)
        {
            if (now < _nextPlayerScanAt) return;
            _nextPlayerScanAt = now + 0.5f;
            try
            {
                var icons = AbilityBarIcon.all;
                if (icons == null) return;
                for (int i = 0; i < icons.Count; i++)
                {
                    AbilityBarIcon icon = icons[i];
                    if (icon != null && BelongsToLocalPlayer(icon)) Register(icon);
                }
            }
            catch { }
        }

        // The skill artwork, preferring an override sprite when the game set one.
        static Sprite ReadSprite(AbilityBarIcon icon)
        {
            var img = icon.icon;
            if (img == null) return null;
            var ov = img.overrideSprite;
            return ov != null ? ov : img.sprite;
        }

        // Opt-in (DebugLog pref): list every child Image of an action-bar slot
        // with sprite/atlas details. This is the evidence trail for icon-art
        // issues — flip DebugLog on, load a zone, read the Melon log.
        static void DumpIconHierarchy(AbilityBarIcon icon, int idx)
        {
            if (Prefs.DebugLog == null || !Prefs.DebugLog.Value) return;
            try
            {
                var images = icon.GetComponentsInChildren<Image>(true);
                MelonLogger.Msg($"[debug] slot #{idx}: {images.Length} child Image(s)");
                foreach (var im in images)
                {
                    if (im == null) continue;
                    try
                    {
                        var sp = im.overrideSprite != null ? im.overrideSprite : im.sprite;
                        MelonLogger.Msg(sp != null
                            ? $"[debug]   {im.name}: sprite={sp.name} tex={(sp.texture != null ? sp.texture.name : "?")} packed={sp.packed} rect={sp.rect} texRect={SafeTexRect(sp)}"
                            : $"[debug]   {im.name}: no sprite");
                    }
                    catch { }
                }
            }
            catch { }
        }

        static string SafeTexRect(Sprite sp)
        {
            try { return sp.textureRect.ToString(); } catch { return "n/a"; }
        }

        public static void SetCooldown(AbilityBarIcon icon, bool on)
        {
            if (icon == null) return;
            try
            {
                int idx = icon.abilityNumber;
                if ((uint)idx >= SlotCount) return;
                lock (_lock)
                    foreach (var s in _slots)
                        if (s.SlotIndex == idx && SameNativeInstance(s.Source, icon))
                        {
                            if (on)
                            {
                                if (s.CooldownStateArmed)
                                {
                                    s.OnCooldown = true;
                                    s.CooldownObservedSince = Time.time;
                                }
                            }
                            else
                            {
                                s.CooldownStateArmed = true;
                                s.OnCooldown = false;
                                s.CooldownObservedSince = 0f;
                                s.Fill = 0f;
                            }
                            break;
                        }
            }
            catch { }
        }

        // 20 Hz heartbeat: refresh fill state, retry hotkey probe with backoff,
        // rebuild label caches, and prune slots whose Unity objects died.
        public static void Tick(float now)
        {
            ScanLocalPlayerSlots(now);
            lock (_lock)
            {
                for (int i = _slots.Count - 1; i >= 0; i--)
                {
                    var s = _slots[i];
                    bool alive;
                    try { alive = s.Source != null; }   // Unity-overloaded ==, true null for destroyed objects
                    catch { alive = false; }
                    if (!alive) { _slots.RemoveAt(i); continue; }
                    if (!BelongsToLocalPlayer(s.Source)) { _slots.RemoveAt(i); continue; }

                    try
                    {
                        // Refresh every tick — keeps icons correct after the
                        // player swaps a skill into an existing slot.
                        var sp = ReadSprite(s.Source);
                        if (sp != null) s.Icon = sp;
                        if (s.CooldownBar == null) s.CooldownBar = s.Source.cooldownBar;

                        if (s.CooldownBar != null)
                        {
                            s.Fill = s.CooldownBar.fillAmount;
                            bool active = s.Source.cooldownBarActive;
                            if (!active && s.Fill > 0.01f) active = true;
                            if (active && s.Fill < 0.005f) active = false;
                            if (!active)
                            {
                                s.CooldownStateArmed = true;
                                s.CooldownObservedSince = 0f;
                                s.OnCooldown = false;
                            }
                            else if (s.CooldownStateArmed)
                            {
                                if (!s.OnCooldown) s.CooldownObservedSince = now;
                                s.OnCooldown = true;
                            }
                            else
                            {
                                s.OnCooldown = false;
                            }
                        }

                        if (s.GameBoundKey == null && now >= s.NextHotkeyRetry)
                        {
                            var k = HotkeyReader.TryRead(s.Source);
                            if (k != null)                            s.GameBoundKey = k;
                            else if (++s.HotkeyTries >= MaxHotkeyTries) s.GameBoundKey = "";
                            else                                      s.NextHotkeyRetry = now + HotkeyRetrySec;
                        }

                        RefreshLabel(s);
                    }
                    catch { _slots.RemoveAt(i); }   // dead Il2Cpp reference mid-read
                }
            }
        }

        internal static void RefreshLabel(SlotData s)
        {
            string lbl = ButtonLabels.Resolve(s.SlotIndex, s.GameBoundKey);
            // Face identity is mode-dependent, so recompute even when the
            // label string itself is unchanged (auto-detect can flip modes).
            s.BadgeIsFace = ButtonLabels.TryGetFaceColor(
                ButtonLabels.GetModeIndex(), lbl, out var fc);
            s.BadgeColor = fc;
            if (lbl == s.RawLabel) return;
            s.RawLabel = lbl;
            int sp = lbl.IndexOf(' ');
            s.TwoLine = sp > 0 && sp < lbl.Length - 1;
            s.DisplayLabel = s.TwoLine
                ? lbl.Substring(0, sp) + "\n" + lbl.Substring(sp + 1)
                : lbl;
        }

        public static void SnapshotAll(List<SlotData> buf)
        {
            buf.Clear();
            lock (_lock)
                foreach (var s in _slots) buf.Add(s);
        }

        // Only slots the renderer should draw: enabled, on cooldown, visible fill.
        public static void SnapshotActive(List<SlotData> buf)
        {
            buf.Clear();
            lock (_lock)
                foreach (var s in _slots)
                    if (s.CooldownStateArmed && s.OnCooldown && s.Fill > 0.005f && Time.time - s.CooldownObservedSince >= 0.35f
                        && Prefs.IsSlotEnabled(s.SlotIndex))
                        buf.Add(s);
        }

        // Every enabled slot regardless of cooldown — the Move-mode preview.
        public static void SnapshotEnabled(List<SlotData> buf)
        {
            buf.Clear();
            lock (_lock)
                foreach (var s in _slots)
                    if (Prefs.IsSlotEnabled(s.SlotIndex))
                        buf.Add(s);
        }
    }

    internal static class Dbg
    {
        public static void Log(string msg)
        {
            if (Prefs.DebugLog != null && Prefs.DebugLog.Value)
                MelonLogger.Msg("[debug] " + msg);
        }
    }
}
