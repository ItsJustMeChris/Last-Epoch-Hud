#nullable disable
using System.Collections.Generic;
using Mod;
using UnityEngine;

namespace Mod.Cheats.CooldownTracker
{
    // The Home-key settings panel — Terrible Cooldowns design system.
    // Screen position persists across sessions (PanelX/PanelY prefs):
    // drag it once, it opens there forever.
    internal static class SettingsPanel
    {
        static Rect    _rect = new(Prefs.DefaultPanelX, Prefs.DefaultPanelY, 400, 30);
        static bool    _posLoaded;
        static bool    _dragging;
        static bool    _dragMoved;
        static Vector2 _dragOff;
        static Vector2 _scroll;
        static Rect    _closeRect;
        static Rect    _fieldBand;      // screen-space column where label fields live (prev frame)
        static float   _sc = 1f;        // layout scale, frozen while a control is hot (see Draw)
        static bool    _embedded;
        static Rect    _hostRect;
        static readonly List<SlotData> _rows = new();

        public static Rect LocalPanelRect => _rect;
        public static Rect PanelRect => Menu.IsCooldownsTabActive
            ? new Rect(_rect.x + Menu.windowRect.x + _hostRect.x,
                _rect.y + Menu.windowRect.y + _hostRect.y - Menu.CooldownTabScrollOffset,
                _rect.width, _rect.height)
            : _rect;

        public static float ContentHeight
        {
            get
            {
                float sc = Mathf.Clamp(Mathf.Min(Prefs.MenuScale.Value, (Menu.windowRect.width - 40f) / 400f), 0.7f, 2f);
                int count = SlotRegistry.Count;
                float rowStep = 46f * sc;
                float listHeight = count == 0 ? 40f * sc : Mathf.Min(count * rowStep, 7f * rowStep);
                float total = 30f * sc + 10f * sc + 20f * sc + 30f * sc
                    + (Prefs.InputMode.Value == 0 ? 44f * sc : 0f)
                    + 7f * 26f * sc + (UiState.MoveIcons ? 20f * sc : 0f)
                    + 2f * 26f * sc + 4f * 20f * sc + listHeight
                    + 8f * 3f * sc + 5f * sc + 22f * sc;
                return Mathf.Ceil(total + 16f * sc);
            }
        }

        static readonly string[] ModeLabels   = { "Auto", "Keyboard", "Xbox", "PS5" };
        static readonly string[] LayoutLabels = { "Auto", "Xbox", "PS5" };

        public static Color ModeDot(int effMI) =>
            effMI == 1 ? Theme.XboxGreen : effMI == 2 ? Theme.PsBlue : Theme.TextMut;

        public static void DrawInHudTab(Rect hostRect)
        {
            _embedded = true;
            _hostRect = hostRect;
            _rect.x = 8f;
            _rect.y = 8f;
            Draw();
            _embedded = false;
        }

        public static void Draw()
        {
            if (!_embedded && !_posLoaded)
            {
                _posLoaded = true;
                _rect.x = Prefs.PanelX.Value;
                _rect.y = Prefs.PanelY.Value;
            }

            if (!_embedded) HandleDrag();

            // Runtime IMGUI never releases keyboard focus on its own: without
            // this, one click into a label field leaves TextFieldActive stuck
            // true (Home dead, input blocked) until the panel is ✕-closed.
            var evt = Event.current;
            if (evt != null && evt.type == EventType.MouseDown && !_fieldBand.Contains(evt.mousePosition))
                GUIUtility.keyboardControl = 0;

            // Layout scale is frozen while any IMGUI control is hot: the Menu
            // scale slider's own rect derives from this value, so applying it
            // mid-drag remaps the cursor against a moving rect — a divergent
            // feedback loop that strobes the panel between min and max scale.
            // The new scale takes effect on mouse-up.
            if (GUIUtility.hotControl == 0)
            {
                float maxScale = _embedded ? (Menu.windowRect.width - 40f) / 400f : 2f;
                _sc = Mathf.Clamp(Mathf.Min(Prefs.MenuScale.Value, maxScale), 0.7f, 2.0f);
            }
            float sc = _sc;
            float w  = 400f * sc;
            int   effMI = ButtonLabels.GetModeIndex();

            // ── Height budget ─────────────────────────────────────
            float titleH  = 30f * sc;
            float pad     = 10f * sc;
            float hdrH    = 20f * sc;                       // SectionHeader advance
            float segH    = 24f * sc + 6f * sc;
            float autoH   = Prefs.InputMode.Value == 0
                ? 20f * sc + 24f * sc                       // status row + layout override row
                : 0f;
            float sliders = 5 * 26f * sc + 2 * 26f * sc        // 5 sliders + Move row + badges row
                + (UiState.MoveIcons ? 20f * sc : 0f);         // hint while moving
            float behave  = 2 * 26f * sc;
            float gapSect = 8f * sc;
            int   slotCount = SlotRegistry.Count;
            float rowH    = 42f * sc, rowGap = 4f * sc;
            float listH   = slotCount == 0
                ? 40f * sc
                : Mathf.Min(slotCount * (rowH + rowGap), 7 * (rowH + rowGap));
            float footH   = 22f * sc;

            float total = titleH + pad
                + hdrH + segH + autoH + gapSect
                + hdrH + sliders + gapSect
                + hdrH + behave + gapSect
                + hdrH + listH
                + pad * 0.5f + footH;

            _rect.width  = _embedded ? Mathf.Min(w, Menu.windowRect.width - 32f) : w;
            _rect.height = total;
            if (!_embedded)
            {
                _rect.x = Mathf.Clamp(_rect.x, 0, Mathf.Max(0, Screen.width  - w));
                _rect.y = Mathf.Clamp(_rect.y, 0, Mathf.Max(0, Screen.height - total));
            }

            // ── Chrome ────────────────────────────────────────────
            GUI.color = Color.white;
            Theme.Box(_rect, Theme.Panel);

            DrawTitleBar(sc, w, titleH);

            float x = _rect.x + pad;
            float y = _rect.y + titleH + pad * 0.7f;
            float cw = w - pad * 2f;

            // ── INPUT ─────────────────────────────────────────────
            Widgets.SectionHeader(x, ref y, cw, sc, "INPUT");
            int newMode = Widgets.Segmented(new Rect(x, y, cw, 24f * sc),
                Prefs.InputMode.Value, ModeLabels, sc);
            if (newMode != Prefs.InputMode.Value)
            { Prefs.InputMode.Value = newMode; UiState.PickerSlot = -1; }
            y += 24f * sc + 6f * sc;

            if (Prefs.InputMode.Value == 0)
            {
                if (InputTracker.IsControllerActive)
                    Widgets.StatusRow(x, ref y, cw, sc,
                        $"Controller detected ({(InputTracker.DetectedLayout == CtrlLayout.PlayStation ? "PS5" : "Xbox")})",
                        InputTracker.DetectedLayout == CtrlLayout.PlayStation ? Theme.PsBlue : Theme.XboxGreen);
                else
                    Widgets.StatusRow(x, ref y, cw, sc, "Keyboard / Mouse", Theme.Ready);

                float labW = 110f * sc;
                Theme.Text9(new Rect(x, y, labW, 20f * sc), "Layout override",
                    Theme.TextMut, Mathf.RoundToInt(9 * sc));
                int newLay = Widgets.Segmented(new Rect(x + labW, y, cw - labW, 20f * sc),
                    Prefs.CtrlLayout.Value, LayoutLabels, sc);
                if (newLay != Prefs.CtrlLayout.Value)
                { Prefs.CtrlLayout.Value = newLay; UiState.PickerSlot = -1; }
                y += 24f * sc;
            }
            y += gapSect;

            // ── DISPLAY ───────────────────────────────────────────
            Widgets.SectionHeader(x, ref y, cw, sc, "DISPLAY");
            Widgets.SliderRow(x, ref y, cw, sc, "Icon opacity",      Prefs.Alpha,     0.05f,   1f, "F2");
            Widgets.SliderRow(x, ref y, cw, sc, "Icon size",         Prefs.Size,      32f,   120f, "F0");
            Widgets.SliderRow(x, ref y, cw, sc, "Horizontal offset", Prefs.OffsetX, -500f,  500f, "F0");
            Widgets.SliderRow(x, ref y, cw, sc, "Vertical offset",   Prefs.OffsetY, -600f,  200f, "F0");

            // Move mode: drag the live icon cluster instead of doing slider math.
            if (Widgets.ButtonRow(x, ref y, cw, sc, "Icon position",
                    UiState.MoveIcons ? "Lock" : "Move", selected: UiState.MoveIcons))
            {
                UiState.MoveIcons = !UiState.MoveIcons;
                if (!UiState.MoveIcons) Prefs.Save();   // just locked in a position
            }
            if (UiState.MoveIcons)
                Widgets.StatusRow(x, ref y, cw, sc,
                    "drag the icon cluster in the game view — sliders follow", Theme.Accent);

            Widgets.SwitchRow(x, ref y, cw, sc, "Console button badges", Prefs.ButtonBadges);
            Widgets.SliderRow(x, ref y, cw, sc, "Menu scale",        Prefs.MenuScale, 0.7f,  2.0f, "F1");
            y += gapSect;

            // ── BEHAVIOR ──────────────────────────────────────────
            Widgets.SectionHeader(x, ref y, cw, sc, "BEHAVIOR");
            Widgets.SwitchRow(x, ref y, cw, sc, "Block movement while menu is open", Prefs.LockInput);
            if (Widgets.ButtonRow(x, ref y, cw, sc, "Panel position", "Reset"))
            {
                _rect.x = Prefs.DefaultPanelX;
                _rect.y = Prefs.DefaultPanelY;
                SavePanelPos();
            }
            y += gapSect;

            // ── SKILLS ────────────────────────────────────────────
            Widgets.SectionHeader(x, ref y, cw, sc, "SKILLS",
                $"editing: {Prefs.ModeDispName[effMI]}", ModeDot(effMI));

            SlotRegistry.SnapshotAll(_rows);
            if (_rows.Count == 0)
            {
                var er = new Rect(x, y, cw, 36f * sc);
                GUI.color = Color.white;
                Theme.Box(er, Theme.Card);
                Theme.Text9(er, "Waiting for your skill bar — load into a zone.",
                    Theme.TextMut, Mathf.RoundToInt(10 * sc), FontStyle.Normal, TextAnchor.MiddleCenter);
                UiState.TextFieldActive = false;
                DrawFooter(sc, w, footH);
                GUI.color = Color.white;
                return;
            }

            DrawSlotList(x, y, cw, sc, effMI, rowH, rowGap, listH);

            // Suppress game hotkeys while a label field has keyboard focus.
            UiState.TextFieldActive = UiState.ShowSettings && GUIUtility.keyboardControl != 0;

            DrawFooter(sc, w, footH);
            GUI.color = Color.white;
        }

        // ── Chrome pieces ─────────────────────────────────────────
        static void DrawTitleBar(float sc, float w, float titleH)
        {
            var bar = new Rect(_rect.x, _rect.y, w, titleH);
            Theme.Fill(new Rect(bar.x + 1, bar.y + 1, bar.width - 2, bar.height - 1), Theme.Surface);
            Theme.Fill(new Rect(bar.x + 1, bar.yMax - 2f, bar.width - 2, 2f), Theme.AccentDim);

            Theme.Text9(new Rect(bar.x + 10f * sc, bar.y, w * 0.6f, titleH),
                BuildInfo.DisplayName, Theme.TextHi,
                Mathf.RoundToInt(13 * sc), FontStyle.Bold, TextAnchor.MiddleLeft, serif: true);

            var titleStyle = Theme.Label(Mathf.RoundToInt(13 * sc), FontStyle.Bold, TextAnchor.MiddleLeft, true);
            float tw = titleStyle.CalcSize(new GUIContent(BuildInfo.DisplayName)).x;
            Theme.Text9(new Rect(bar.x + 10f * sc + tw + 8f * sc, bar.y + 1f * sc, 80f * sc, titleH),
                "v" + BuildInfo.Version, Theme.TextMut, Mathf.RoundToInt(8 * sc));

            if (!_embedded)
            {
                float cs = 18f * sc;
                _closeRect = new Rect(bar.xMax - cs - 6f * sc, bar.y + (titleH - cs) * 0.5f, cs, cs);
                GUI.color = Color.white;
                if (Drawing.OutlinedButton(_closeRect, "✕", Theme.Button(Mathf.RoundToInt(10 * sc), danger: true)))
                    Close();
            }
        }

        static void DrawFooter(float sc, float w, float footH)
        {
            var f = new Rect(_rect.x + 1, _rect.yMax - footH, w - 2, footH - 1);
            Theme.Fill(new Rect(f.x + 9f * sc, f.y, w - 20f * sc, 1f), Theme.Border);
            Theme.Text9(new Rect(f.x + 9f * sc, f.y, w * 0.75f, footH),
                _embedded ? BuildInfo.OfficialName : $"{BuildInfo.OfficialName} — {BuildInfo.Tagline}",
                Theme.TextMut, Mathf.RoundToInt(8 * sc));
            Theme.Text9(new Rect(f.x, f.y, f.width - 8f * sc, footH),
                _embedded ? "F10 / Insert closes HUD" : "Home closes", Theme.TextMut, Mathf.RoundToInt(8 * sc),
                FontStyle.Normal, TextAnchor.MiddleRight);
        }

        internal static void Close()
        {
            UiState.ShowSettings    = false;
            UiState.PickerSlot      = -1;
            UiState.TextFieldActive = false;
            UiState.MoveIcons       = false;
            _dragging               = false;  // a missed MouseUp must not leak into the next open
            GUIUtility.keyboardControl = 0;   // stale focus must not survive a reopen
            Prefs.Save();
        }

        // ── Skill rows ────────────────────────────────────────────
        static void DrawSlotList(float x, float y, float cw, float sc, int effMI,
            float rowH, float rowGap, float listH)
        {
            float contentH = _rows.Count * (rowH + rowGap);
            bool  scrolls  = contentH > listH + 1f;
            var   outer    = new Rect(x, y, cw, listH);
            var   inner    = new Rect(0, 0, cw - (scrolls ? 8f * sc : 0f), contentH);

            // Screen-space column occupied by the label text fields — clicks
            // outside it release IMGUI keyboard focus (see Draw).
            {
                float iSz  = rowH - 12f * sc;
                float tfX  = 6f * sc + iSz + 8f * sc + 44f * sc + 6f * sc;
                float pkW  = effMI >= 1 ? 24f * sc : 0f;
                float tfW  = inner.width - tfX - pkW - 34f * sc - 20f * sc;
                _fieldBand = new Rect(x + tfX, y, tfW, listH);
            }

            _scroll = GUI.BeginScrollView(outer, _scroll, inner, GUIStyle.none, GUIStyle.none);
            float ry = 0f;
            foreach (var s in _rows)
            {
                DrawSlotRow(new Rect(0, ry, inner.width, rowH), s, sc, effMI);
                ry += rowH + rowGap;
            }
            GUI.EndScrollView();

            if (scrolls)   // slim scroll indicator, wheel does the work
            {
                float track = listH;
                float thumbH = Mathf.Max(16f, track * (listH / contentH));
                float tY = y + (track - thumbH) * Mathf.Clamp01(_scroll.y / (contentH - listH));
                Theme.Fill(new Rect(x + cw - 3f, tY, 3f, thumbH), Theme.AccentDim);
            }
        }

        static void DrawSlotRow(Rect r, SlotData s, float sc, int effMI)
        {
            GUI.color = Color.white;
            Theme.Box(r, Theme.Card);

            // Icon + cooldown progress underline
            float iSz = r.height - 12f * sc;
            var ir = new Rect(r.x + 6f * sc, r.y + 5f * sc, iSz, iSz);
            bool drew = false;
            try
            {
                if (s.Icon != null)
                { Theme.DrawSprite(ir, s.Icon); drew = true; }
            }
            catch { }
            if (!drew) Theme.Fill(ir, Theme.Inset);
            Theme.DrawBorder(ir, Theme.Border, 1f);
            if (s.OnCooldown && s.Fill > 0f)
                Theme.Fill(new Rect(ir.x, ir.yMax + 1f, ir.width * (1f - s.Fill), 2f * sc), Theme.Cooling);

            // Key label + sub-hint
            float lbX = ir.xMax + 8f * sc;
            float lbW = 44f * sc;
            Theme.Text9(new Rect(lbX, r.y + 4f * sc, lbW, r.height * 0.5f),
                s.RawLabel ?? "", s.OnCooldown ? Theme.Cooling : Theme.TextHi,
                Mathf.RoundToInt(11 * sc), FontStyle.Bold);

            string hint = s.OnCooldown ? $"{(int)(s.Fill * 100)}%"
                       : s.SlotIndex == 6 ? "evade"
                       : (!string.IsNullOrEmpty(s.GameBoundKey) && effMI == 0) ? $"↑{s.GameBoundKey}"
                       : null;
            if (hint != null)
                Theme.Text9(new Rect(lbX, r.y + r.height * 0.5f, lbW, r.height * 0.5f - 4f * sc),
                    hint, s.OnCooldown ? Theme.Cooling : Theme.TextMut, Mathf.RoundToInt(8 * sc));

            // Custom label field
            bool  hasPicker = effMI >= 1;
            float swW = 34f * sc, swH = 16f * sc;
            float pkW = hasPicker ? 24f * sc : 0f;
            float tfX = lbX + lbW + 6f * sc;
            float tfW = r.xMax - tfX - pkW - swW - 20f * sc;
            float tfH = 22f * sc;
            float tfY = r.y + (r.height - tfH) * 0.5f;
            string cur = Prefs.CustomLabel(effMI, s.SlotIndex);
            bool hasCustom = !string.IsNullOrEmpty(cur);

            // The clear ✕ sits OUTSIDE the field rect — an overlapping button
            // never receives the MouseDown (the TextField consumes it first).
            float clearW = hasCustom ? 18f * sc : 0f;
            GUI.color = Color.white;
            string next = GUI.TextField(new Rect(tfX, tfY, tfW - clearW, tfH), cur, 20,
                Theme.TextField(Mathf.RoundToInt(10 * sc)));
            if (next != cur) Prefs.SetCustomLabel(effMI, s.SlotIndex, next);

            if (!hasCustom)
                Theme.Text9(new Rect(tfX + 7f * sc, tfY, tfW - 10f * sc, tfH), "auto",
                    Theme.TextMut, Mathf.RoundToInt(9 * sc));
            else
            {
                float xs = 15f * sc;
                GUI.color = Color.white;
                if (Drawing.OutlinedButton(new Rect(tfX + tfW - xs - 1f * sc, tfY + (tfH - xs) * 0.5f, xs, xs),
                        "✕", Theme.Button(Mathf.RoundToInt(8 * sc), danger: true)))
                {
                    Prefs.SetCustomLabel(effMI, s.SlotIndex, "");
                    if (UiState.PickerSlot == s.SlotIndex) UiState.PickerSlot = -1;
                }
            }

            // [▼] glyph picker (controller modes only)
            if (hasPicker)
            {
                bool open = UiState.PickerSlot == s.SlotIndex;
                GUI.color = Color.white;
                if (Drawing.OutlinedButton(new Rect(tfX + tfW + 4f * sc, tfY, 20f * sc, tfH), "▼",
                        Theme.Button(Mathf.RoundToInt(9 * sc), selected: open)))
                    UiState.PickerSlot = open ? -1 : s.SlotIndex;
            }

            // Per-slot enable switch (persists)
            bool en  = Prefs.IsSlotEnabled(s.SlotIndex);
            bool nen = Widgets.Switch(
                new Rect(r.xMax - swW - 8f * sc, r.y + (r.height - swH) * 0.5f, swW, swH), en, sc);
            if (nen != en) Prefs.SetSlotEnabled(s.SlotIndex, nen);
        }

        // ── Plumbing ──────────────────────────────────────────────
        static void HandleDrag()
        {
            var ev = Event.current;
            if (ev == null) return;
            var hR = new Rect(_rect.x, _rect.y, _rect.width, 30f * _sc);
            switch (ev.type)
            {
                case EventType.MouseDown when ev.button == 0
                                              && hR.Contains(ev.mousePosition)
                                              && !_closeRect.Contains(ev.mousePosition):
                    _dragging  = true;
                    _dragMoved = false;
                    _dragOff   = ev.mousePosition - new Vector2(_rect.x, _rect.y);
                    ev.Use();
                    break;
                case EventType.MouseDrag when _dragging:
                    _rect.x = ev.mousePosition.x - _dragOff.x;
                    _rect.y = ev.mousePosition.y - _dragOff.y;
                    _dragMoved = true;
                    ev.Use();
                    break;
                case EventType.MouseUp:
                    if (_dragging && _dragMoved) SavePanelPos();
                    _dragging = false;
                    break;
            }
        }

        static void SavePanelPos()
        {
            Prefs.PanelX.Value = _rect.x;
            Prefs.PanelY.Value = _rect.y;
            Prefs.Save();
        }
    }
}
