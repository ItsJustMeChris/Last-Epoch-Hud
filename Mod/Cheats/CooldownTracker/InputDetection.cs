#nullable disable
using System;
using System.Linq;
using Il2Cpp;
using UnityEngine;
using UnityEngine.UI;

namespace Mod.Cheats.CooldownTracker
{
    internal enum CtrlLayout { Xbox, PlayStation }

    // Watches raw device activity to decide whether the player is currently on
    // keyboard/mouse or a controller, and which controller family it is.
    // Reads UnityEngine.Input directly; this mod installs no Input patches.
    // council 2026-09-11 #5
    internal static class InputTracker
    {
        static float    _lastCtrl  = -999f;
        static float    _lastKb    = -999f;
        static float    _namesNext = 0f;
        static string[] _joyNames  = Array.Empty<string>();

        public static bool       IsControllerActive { get; private set; }
        public static CtrlLayout DetectedLayout     { get; private set; } = CtrlLayout.Xbox;

        const int JoyFirst = (int)KeyCode.JoystickButton0;
        const int JoyLast  = (int)KeyCode.JoystickButton19;

        public static void Update()
        {
            float t = Time.time;
            if (t >= _namesNext)
            {
                _namesNext = t + 5f;
                try   { _joyNames = Input.GetJoystickNames() ?? Array.Empty<string>(); }
                catch { _joyNames = Array.Empty<string>(); }

                DetectedLayout = CtrlLayout.Xbox;
                foreach (var n in _joyNames)
                    if (n != null &&
                        (n.IndexOf("DualSense",           StringComparison.OrdinalIgnoreCase) >= 0 ||
                         n.IndexOf("DualShock",           StringComparison.OrdinalIgnoreCase) >= 0 ||
                         n.IndexOf("PlayStation",         StringComparison.OrdinalIgnoreCase) >= 0 ||
                         n.IndexOf("Wireless Controller", StringComparison.OrdinalIgnoreCase) >= 0))
                    { DetectedLayout = CtrlLayout.PlayStation; break; }

                // Before any real input has been seen, presence of a pad decides.
                if (_lastCtrl < -990f && _lastKb < -990f)
                    IsControllerActive = _joyNames.Any(n => !string.IsNullOrEmpty(n));
            }

            if (Input.anyKeyDown)
            {
                bool joyBtn = false;
                for (int k = JoyFirst; k <= JoyLast; k++)
                    if (Input.GetKeyDown((KeyCode)k)) { joyBtn = true; break; }
                if (joyBtn) { _lastCtrl = t; IsControllerActive = true;  }
                else        { _lastKb   = t; IsControllerActive = false; }
            }
            try
            {
                if (Mathf.Abs(Input.GetAxisRaw("Mouse X")) > 0.01f ||
                    Mathf.Abs(Input.GetAxisRaw("Mouse Y")) > 0.01f)
                { _lastKb = t; if (_lastCtrl < t - 1f) IsControllerActive = false; }
            }
            catch { }
        }
    }

    // Default button names per input mode, resolution of the label that
    // actually gets drawn for a slot (custom > game-bound key > default),
    // and the face-button colour identities shared by the overhead badges
    // and the glyph picker.
    internal static class ButtonLabels
    {
        // 7 entries: slots 0-5 (skills) + slot 6 (evade/dodge)
        public static readonly string[] Xbox        = { "X",  "Y",  "RB", "LT", "L",  "RT", "B"     };
        public static readonly string[] PlayStation = { "□",  "△",  "R1", "L2", "L3", "R2", "○"     };
        public static readonly string[] Keyboard    = { "Q",  "W",  "E",  "R",  "RMB","T",  "Space" };

        // Face buttons drawn in their real-world pad colours.
        public static readonly string[] XboxFaceSyms = { "A", "B", "X", "Y" };
        public static readonly Color[]  XboxFaceCols =
        {
            new(0.15f, 0.80f, 0.28f),   // A – green
            new(0.90f, 0.20f, 0.15f),   // B – red
            new(0.22f, 0.45f, 0.95f),   // X – blue
            new(0.95f, 0.78f, 0.05f),   // Y – gold
        };
        public static readonly string[] PsFaceSyms = { "△", "□", "○", "✕" };
        public static readonly Color[]  PsFaceCols =
        {
            new(0.30f, 0.85f, 0.55f),   // △ Triangle – green
            new(0.95f, 0.48f, 0.70f),   // □ Square   – pink
            new(0.95f, 0.28f, 0.28f),   // ○ Circle   – red
            new(0.38f, 0.52f, 0.96f),   // ✕ Cross    – blue
        };

        // True when `label` is a face button of the given mode; out = its colour.
        public static bool TryGetFaceColor(int modeIdx, string label, out Color c)
        {
            c = default;
            if (string.IsNullOrEmpty(label)) return false;
            string[] syms; Color[] cols;
            if      (modeIdx == 1) { syms = XboxFaceSyms; cols = XboxFaceCols; }
            else if (modeIdx == 2) { syms = PsFaceSyms;   cols = PsFaceCols;   }
            else return false;
            for (int i = 0; i < syms.Length; i++)
                if (string.Equals(label, syms[i], StringComparison.OrdinalIgnoreCase))
                { c = cols[i]; return true; }
            return false;
        }

        // modeIdx: 0=Keyboard  1=Xbox  2=PlayStation
        public static int GetModeIndex()
        {
            switch (Prefs.InputMode.Value)
            {
                case 1: return 0;
                case 2: return 1;
                case 3: return 2;
                default:
                    if (!InputTracker.IsControllerActive) return 0;
                    CtrlLayout lay =
                        Prefs.CtrlLayout.Value == 1 ? CtrlLayout.Xbox :
                        Prefs.CtrlLayout.Value == 2 ? CtrlLayout.PlayStation :
                        InputTracker.DetectedLayout;
                    return lay == CtrlLayout.PlayStation ? 2 : 1;
            }
        }

        static string[] TableFor(int mi) =>
            mi == 2 ? PlayStation : mi == 1 ? Xbox : Keyboard;

        public static string Resolve(int idx, string gameBound)
        {
            int mi = GetModeIndex();
            string custom = Prefs.CustomLabel(mi, idx);
            if (!string.IsNullOrEmpty(custom)) return custom;
            if (mi == 0 && !string.IsNullOrEmpty(gameBound)) return gameBound;
            var arr = TableFor(mi);
            return (uint)idx < (uint)arr.Length ? arr[idx] : "#" + idx;
        }
    }

    // Best-effort read of the key the game itself shows on an action bar slot.
    // An earlier reflection sweep over generic Components could never match
    // because Il2Cpp interop hands back base-type wrappers, so the managed
    // GetType() never exposes a `text` property. The typed Text pass below is
    // the only one that produces results.
    internal static class HotkeyReader
    {
        public static string TryRead(AbilityBarIcon icon)
        {
            if (icon == null) return null;
            try
            {
                var texts = icon.GetComponentsInChildren<Text>(true);
                foreach (var t in texts)
                { var s = t?.text?.Trim(); if (IsHotkeyLike(s)) return s; }
            }
            catch { }
            return null;
        }

        static bool IsHotkeyLike(string s)
        {
            if (string.IsNullOrEmpty(s) || s.Length < 1 || s.Length > 6) return false;
            if (s.All(char.IsDigit)) return false;
            if (s.Contains("%") || s.Contains(":") || s.Contains(".")) return false;
            return true;
        }
    }
}
