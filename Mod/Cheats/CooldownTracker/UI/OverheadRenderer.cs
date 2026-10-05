#nullable disable
using System.Collections.Generic;
using UnityEngine;

namespace Mod.Cheats.CooldownTracker
{
    // Draws the floating cooldown icons above the player.
    //
    // Anchor rule: if there is no living player to anchor to, draw NOTHING.
    // A fallback screen position when the anchor is missing causes ghost icons
    // on the login screen, so no fallback is used.
    // council 2026-09-11 #5
    //
    // Move mode: while the settings panel is open and Move is engaged, every
    // enabled slot renders as a draggable preview cluster — drag it where you
    // want the icons to float and the Offset X/Y prefs (and sliders) follow.
    internal static class OverheadRenderer
    {
        static readonly List<SlotData> _buf = new();
        static bool    _dragging;
        static Vector2 _dragStart;
        static float   _startOX, _startOY;
        static int     _cameraFrame = -1;
        static Camera  _camera;

        public static void Draw()
        {
            var cam = CameraForCurrentFrame();
            if (cam == null) return;

            bool moveMode = UiState.MoveIcons && UiState.ShowSettings;
            if (!moveMode) _dragging = false;   // a drag aborted mid-move must not leak into the next session
            if (moveMode) SlotRegistry.SnapshotEnabled(_buf);
            else          SlotRegistry.SnapshotActive(_buf);
            if (_buf.Count == 0) { _dragging = false; return; }
            if (!TryGetPlayerAnchor(cam, out Vector2 anchor)) { _dragging = false; return; }

            float sz    = Mathf.Clamp(Prefs.Size.Value, 32f, 120f);
            float gap   = 8f;
            float alpha = Mathf.Clamp01(Prefs.Alpha.Value);
            if (moveMode) alpha = Mathf.Max(alpha, 0.9f);

            float totalW  = _buf.Count * (sz + gap) - gap;
            float startX  = anchor.x + Prefs.OffsetX.Value - totalW * 0.5f;
            float startY  = anchor.y + Prefs.OffsetY.Value - sz * 0.5f;
            var   cluster = new Rect(startX, startY, totalW, sz);

            if (moveMode) HandleMoveDrag(cluster);

            for (int i = 0; i < _buf.Count; i++)
                DrawIcon(new Rect(startX + i * (sz + gap), startY, sz, sz), _buf[i], alpha, moveMode);

            if (moveMode)
            {
                var hit = Theme.Pad(cluster, 8f);
                Theme.DrawBorder(hit, Theme.Accent, 2f);
                int hfs = Mathf.Max(12, Mathf.RoundToInt(sz * 0.22f));   // scale with icon size like the labels do
                Theme.Text9(new Rect(hit.x - hfs * 5f, hit.y - hfs * 2f, hit.width + hfs * 10f, hfs * 1.7f),
                    "drag to reposition — press Lock when done",
                    Theme.Accent, hfs, FontStyle.Bold, TextAnchor.MiddleCenter);
            }
        }

        // council 2026-09-11 #4
        static Camera CameraForCurrentFrame()
        {
            try
            {
                int frame = Time.frameCount;
                if (_cameraFrame != frame)
                {
                    _cameraFrame = frame;
                    _camera = null;
                    _camera = Camera.main;
                }
            }
            catch { _camera = null; }
            return _camera;
        }

        static void HandleMoveDrag(Rect cluster)
        {
            var ev = Event.current;
            if (ev == null) return;
            var hit = Theme.Pad(cluster, 8f);
            switch (ev.type)
            {
                case EventType.MouseDown when ev.button == 0
                                              && hit.Contains(ev.mousePosition)
                                              && !SettingsPanel.PanelRect.Contains(ev.mousePosition):
                    _dragging  = true;
                    _dragStart = ev.mousePosition;
                    _startOX   = Prefs.OffsetX.Value;
                    _startOY   = Prefs.OffsetY.Value;
                    ev.Use();
                    break;
                case EventType.MouseDrag when _dragging:
                    var d = ev.mousePosition - _dragStart;
                    Prefs.OffsetX.Value = Mathf.Clamp(_startOX + d.x, -500f, 500f);
                    Prefs.OffsetY.Value = Mathf.Clamp(_startOY + d.y, -600f, 200f);
                    ev.Use();
                    break;
                case EventType.MouseUp when _dragging:
                    _dragging = false;
                    Prefs.Save();
                    break;
            }
        }

        static bool TryGetPlayerAnchor(Camera cam, out Vector2 gui)
        {
            gui = default;
            foreach (var s in _buf)
            {
                try
                {
                    var src = s.Source;
                    if (src == null) continue;          // Unity-overloaded null check
                    var player = src.player;
                    if (player == null) continue;
                    var sp = cam.WorldToScreenPoint(player.transform.position);
                    if (sp.z <= 0f) continue;
                    gui = new Vector2(sp.x, Screen.height - sp.y);
                    return true;
                }
                catch { }                                // collected Il2Cpp reference
            }
            return false;
        }

        static void DrawIcon(Rect r, SlotData s, float alpha, bool preview)
        {
            // Backplate
            GUI.color = new Color(0f, 0f, 0f, alpha * 0.55f);
            GUI.DrawTexture(Theme.Pad(r, 3), Texture2D.whiteTexture);

            // Skill sprite (or dim placeholder)
            GUI.color = new Color(1f, 1f, 1f, alpha);
            bool drewSprite = false;
            try
            {
                if (s.Icon != null && s.Icon.texture != null)
                { Theme.DrawSprite(r, s.Icon); drewSprite = true; }
            }
            catch { }
            if (!drewSprite)
            {
                GUI.color = new Color(0.30f, 0.30f, 0.35f, alpha);
                GUI.DrawTexture(r, Texture2D.whiteTexture);
            }

            // Cooldown sweep (top-down fill) — not in preview, icons there are "ready"
            bool cooling = !preview && s.OnCooldown && s.Fill > 0.005f;
            if (cooling)
            {
                GUI.color = new Color(0f, 0f, 0f, alpha * 0.70f);
                GUI.DrawTexture(new Rect(r.x, r.y, r.width, r.height * s.Fill), Texture2D.whiteTexture);
            }

            // Border: orange while cooling, green when nearly ready
            bool nearReady = !cooling || s.Fill < 0.10f;
            Theme.DrawBorder(r, nearReady
                ? Theme.WithAlpha(Theme.ReadyBright, alpha)
                : new Color(1f, 0.55f, 0.1f, alpha * 0.85f), 2);

            // Label: console button badge for short button-like labels (when
            // enabled), classic text strip for spell names / long customs.
            string lbl = s.RawLabel ?? "";
            bool badge = Prefs.ButtonBadges.Value && !s.TwoLine
                && lbl.Length > 0 && lbl.Length <= 5;
            if (badge) DrawBadge(r, s, lbl, alpha);
            else       DrawLabelStrip(r, s, alpha, nearReady);
            GUI.color = Color.white;
        }

        // Round face button (red B, gold Y, blue ✕…) or a capsule keycap
        // (RB, LT, L3, Q…), centred on the icon's bottom edge — drawn from a
        // runtime-generated anti-aliased disc, no console assets involved.
        static void DrawBadge(Rect r, SlotData s, string lbl, float alpha)
        {
            float d  = Mathf.Clamp(r.height * 0.40f, 16f, 44f);
            float cx = r.x + r.width * 0.5f;
            float cy = r.yMax;
            float ring  = Mathf.Max(1.5f, d * 0.09f);
            var   body  = new Color(0.07f, 0.08f, 0.11f, alpha * 0.95f);

            // GUI.color must be white for the glyph labels: the style already
            // carries the colour+alpha, and IMGUI multiplies GUI.color in —
            // tinting twice squares the alpha and darkens the face colours.
            if (s.BadgeIsFace)
            {
                var br = new Rect(cx - d * 0.5f, cy - d * 0.5f, d, d);
                Theme.DrawDisc(Theme.Pad(br, d * 0.10f), new Color(0f, 0f, 0f, alpha * 0.50f));
                var rc = s.BadgeColor; rc.a = alpha;
                Theme.DrawDisc(br, rc);                          // coloured ring
                Theme.DrawDisc(Theme.Pad(br, -ring), body);      // dark face
                int fs = Mathf.Max(9, Mathf.RoundToInt(d * 0.52f));
                GUI.color = Color.white;
                GUI.Label(br, lbl, Theme.BadgeLabel(fs, rc));
            }
            else
            {
                int   fs    = Mathf.Max(8, Mathf.RoundToInt(d * 0.46f));
                float textW = lbl.Length * fs * 0.62f;
                float w     = Mathf.Max(d, textW + d * 0.55f);
                var   br    = new Rect(cx - w * 0.5f, cy - d * 0.5f, w, d);
                var   ringC = new Color(0.66f, 0.66f, 0.72f, alpha);
                Theme.DrawCapsule(Theme.Pad(br, d * 0.10f), new Color(0f, 0f, 0f, alpha * 0.50f));
                Theme.DrawCapsule(br, ringC);
                Theme.DrawCapsule(Theme.Pad(br, -ring), body);
                var txtC = new Color(0.93f, 0.90f, 0.84f, alpha);
                GUI.color = Color.white;
                GUI.Label(br, lbl, Theme.BadgeLabel(fs, txtC));
            }
        }

        static void DrawLabelStrip(Rect r, SlotData s, float alpha, bool nearReady)
        {
            float lh = s.TwoLine
                ? Mathf.Max(28f, r.height * 0.42f)
                : Mathf.Max(15f, r.height * 0.23f);
            Rect lr = new(r.x, r.yMax - lh, r.width, lh);
            GUI.color = new Color(0f, 0f, 0f, alpha * 0.80f);
            GUI.DrawTexture(lr, Texture2D.whiteTexture);

            Color lblCol = nearReady
                ? Theme.WithAlpha(Theme.ReadyBright, alpha)
                : new Color(1f, 0.92f, 0.55f, alpha);
            int fontSize = Mathf.Max(8, Mathf.RoundToInt(r.height * (s.TwoLine ? 0.13f : 0.20f)));
            GUI.color = Color.white;   // colour lives in the style; double-tinting squares the alpha
            GUI.Label(lr, s.DisplayLabel, Theme.IconLabel(fontSize, lblCol));
        }
    }
}
