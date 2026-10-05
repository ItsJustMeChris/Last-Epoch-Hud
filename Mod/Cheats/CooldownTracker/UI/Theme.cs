#nullable disable
using System.Collections.Generic;
using UnityEngine;

namespace Mod.Cheats.CooldownTracker
{
    // "Terrible Cooldowns" design system — one dark Last-Epoch-native palette,
    // one gold accent, 9-sliced bordered surfaces, cached styles. Every pixel
    // the settings UI draws goes through here so the whole panel stays coherent.
    //
    // Must be initialised from inside OnGUI — GUI.skin is invalid elsewhere.
    internal static class Theme
    {
        // ── Palette ───────────────────────────────────────────────
        public static readonly Color Bg        = Hex(0x0C0E13, 0.97f);
        public static readonly Color Surface   = Hex(0x161A24);
        public static readonly Color SurfaceHi = Hex(0x202637);
        public static readonly Color Inset     = Hex(0x10131B);
        public static readonly Color Border    = Hex(0x2A3040);
        public static readonly Color BorderHi  = Hex(0x3C4456);
        public static readonly Color Accent    = Hex(0xC9A653);   // LE gold
        public static readonly Color AccentDim = Hex(0x8A7339);
        public static readonly Color TextHi    = Hex(0xEDE6D4);
        public static readonly Color Text      = Hex(0xC6C2B6);
        public static readonly Color TextMut   = Hex(0x807D8C);
        public static readonly Color TextDark  = Hex(0x14110A);
        public static readonly Color Ready       = Hex(0x59C97B);
        public static readonly Color ReadyBright = new(0.25f, 1f, 0.35f);   // in-world "skill ready" green
        public static readonly Color Cooling   = Hex(0xD98E3B);
        public static readonly Color Danger    = Hex(0xB5484D);
        public static readonly Color XboxGreen = Hex(0x3FA63F);
        public static readonly Color PsBlue    = Hex(0x4F8FE0);

        static Color Hex(int rgb, float a = 1f) => new(
            ((rgb >> 16) & 0xFF) / 255f,
            ((rgb >> 8)  & 0xFF) / 255f,
            ( rgb        & 0xFF) / 255f, a);

        // ── Textures & core styles ────────────────────────────────
        static bool _ready;
        public static GUIStyle Panel, Card, InsetBox, SwitchOn, SwitchOff;
        static GUIStyle _btn, _btnSelected, _btnDanger, _textField, _label, _iconLabel, _badgeLabel;
        static GUIStyle _sliderThumb;
        static GUIStyle _optionRow;
        static GUIStyle _optionRowSelected;
        static GUISkin _hudSkin;
        public static Texture2D Disc { get; private set; }
        static Font _serif;
        static readonly Dictionary<(int size, FontStyle fs, TextAnchor anchor, bool serif), GUIStyle> _labels = new();
        static readonly Dictionary<(int size, int kind), GUIStyle> _buttons = new();
        static readonly Dictionary<int, GUIStyle> _fields = new();

        public static void Ensure()
        {
            if (_ready) return;
            _ready = true;

            try { _serif = Font.CreateDynamicFontFromOSFont("Georgia", 14); }
            catch { _serif = null; }

            Panel     = BoxStyle(Bg,      Border);
            Card      = BoxStyle(Surface, Border);
            InsetBox  = BoxStyle(Inset,   Border);
            _optionRow = BoxStyle(Surface, Border);
            _optionRowSelected = BoxStyle(SurfaceHi, AccentDim);
            SwitchOn  = BoxStyle(Accent,  AccentDim);
            SwitchOff = BoxStyle(Inset,   Border);

            _btn = BoxStyle(Surface, Border);
            _btn.alignment = TextAnchor.MiddleCenter;
            _btn.normal.textColor = Text;
            _btn.hover.background  = Bordered(SurfaceHi, BorderHi);
            _btn.hover.textColor   = TextHi;
            _btn.active.background = Bordered(Inset, AccentDim);
            _btn.active.textColor  = TextHi;

            _btnSelected = BoxStyle(Accent, AccentDim);
            _btnSelected.alignment = TextAnchor.MiddleCenter;
            _btnSelected.normal.textColor = TextDark;
            _btnSelected.onNormal.background = Bordered(Accent, AccentDim);
            _btnSelected.onNormal.textColor = TextDark;
            _btnSelected.hover.background = Bordered(Accent, Accent);
            _btnSelected.hover.textColor  = TextDark;
            _btnSelected.onHover.background = Bordered(Accent, Accent);
            _btnSelected.onHover.textColor = TextDark;
            _btnSelected.active.background = Bordered(AccentDim, AccentDim);
            _btnSelected.active.textColor  = TextDark;
            _btnSelected.onActive.background = Bordered(AccentDim, AccentDim);
            _btnSelected.onActive.textColor = TextDark;

            _btnDanger = BoxStyle(Surface, Border);
            _btnDanger.alignment = TextAnchor.MiddleCenter;
            _btnDanger.normal.textColor = TextMut;
            _btnDanger.hover.background = Bordered(Danger, Danger);
            _btnDanger.hover.textColor  = TextHi;
            _btnDanger.active.background = Bordered(Danger, BorderHi);
            _btnDanger.active.textColor  = TextHi;

            _textField = new GUIStyle(GUI.skin.textField)
            {
                alignment = TextAnchor.MiddleLeft,
                padding   = new RectOffset(6, 6, 2, 2),
                border    = new RectOffset(1, 1, 1, 1),
            };
            _textField.normal.background  = Bordered(Inset, Border);
            _textField.normal.textColor   = TextHi;
            _textField.hover.background   = Bordered(Inset, BorderHi);
            _textField.hover.textColor    = TextHi;
            _textField.focused.background = Bordered(Inset, Accent);   // gold ring on focus
            _textField.focused.textColor  = TextHi;

            _label = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleLeft,
                padding   = new RectOffset(0, 0, 0, 0),
            };
            _label.normal.textColor = Color.white;   // tinted via GUI.color at draw time

            _iconLabel  = new GUIStyle(_label) { alignment = TextAnchor.MiddleCenter, wordWrap = false };
            _badgeLabel = new GUIStyle(_iconLabel) { fontStyle = FontStyle.Bold };

            Disc = MakeDisc(64);

            _sliderThumb = new GUIStyle();
            _sliderThumb.normal.background = Bordered(Accent, AccentDim);
            _sliderThumb.hover.background  = Bordered(Accent, Accent);
            _sliderThumb.border = new RectOffset(1, 1, 1, 1);
        }

        public static void ApplyHudSkin()
        {
            Ensure();
            if (_hudSkin == null)
            {
                _hudSkin = new GUISkin();
                _hudSkin.window = new GUIStyle(GUI.skin.window);
                _hudSkin.button = new GUIStyle(GUI.skin.button);
                _hudSkin.box = new GUIStyle(GUI.skin.box);
                _hudSkin.label = new GUIStyle(GUI.skin.label);
                _hudSkin.toggle = new GUIStyle(GUI.skin.toggle);
                _hudSkin.textField = new GUIStyle(GUI.skin.textField);
                _hudSkin.horizontalSlider = new GUIStyle(GUI.skin.horizontalSlider);
                _hudSkin.horizontalSliderThumb = new GUIStyle(GUI.skin.horizontalSliderThumb);
                _hudSkin.scrollView = new GUIStyle(GUI.skin.scrollView);
                _hudSkin.verticalScrollbar = new GUIStyle(GUI.skin.verticalScrollbar);
                _hudSkin.verticalScrollbarThumb = new GUIStyle(GUI.skin.verticalScrollbarThumb);
                _hudSkin.horizontalScrollbar = new GUIStyle(GUI.skin.horizontalScrollbar);
                _hudSkin.horizontalScrollbarThumb = new GUIStyle(GUI.skin.horizontalScrollbarThumb);
                _hudSkin.textArea = new GUIStyle(GUI.skin.textArea);

                var window = _hudSkin.window;
                window.normal.background = Bordered(Bg, Border);
                window.onNormal.background = Bordered(Bg, Border);
                window.border = new RectOffset(2, 2, 2, 2);
                window.padding = new RectOffset(10, 10, 30, 10);
                window.alignment = TextAnchor.UpperCenter;
                window.fontSize = 11;
                window.normal.textColor = TextHi;
                window.onNormal.textColor = TextHi;
                if (_serif != null) window.font = _serif;

                _hudSkin.button = new GUIStyle(_btn) { fontSize = 10 };
                _hudSkin.box = new GUIStyle(Card);
                _hudSkin.label = new GUIStyle(_label);
                _hudSkin.label.normal.textColor = Text;
                _hudSkin.toggle = new GUIStyle(_hudSkin.toggle);
                _hudSkin.toggle.normal.background = Bordered(Surface, Border);
                _hudSkin.toggle.hover.background = Bordered(SurfaceHi, BorderHi);
                _hudSkin.toggle.onNormal.background = Bordered(Surface, AccentDim);
                _hudSkin.toggle.onHover.background = Bordered(SurfaceHi, Accent);
                _hudSkin.toggle.normal.textColor = Text;
                _hudSkin.toggle.onNormal.textColor = TextHi;
                _hudSkin.toggle.hover.textColor = TextHi;
                _hudSkin.toggle.onHover.textColor = TextHi;
                _hudSkin.textField = new GUIStyle(_textField);

                var slider = _hudSkin.horizontalSlider;
                slider.normal.background = Bordered(Inset, Border);
                slider.hover.background = Bordered(Inset, BorderHi);
                slider.active.background = Bordered(Inset, AccentDim);
                var thumb = _hudSkin.horizontalSliderThumb;
                thumb.normal.background = Bordered(Accent, AccentDim);
                thumb.hover.background = Bordered(Accent, Accent);
                thumb.active.background = Bordered(AccentDim, Accent);
                thumb.border = new RectOffset(1, 1, 1, 1);
            }

            GUI.skin = _hudSkin;
        }

        // Anti-aliased white disc, tinted at draw time — the basis for the
        // console button badges (no Microsoft/Sony assets, just math).
        static Texture2D MakeDisc(int size)
        {
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false)
            { hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            float c = (size - 1) * 0.5f;
            float radius = size * 0.5f - 1.5f;
            for (int yy = 0; yy < size; yy++)
                for (int xx = 0; xx < size; xx++)
                {
                    float dx = xx - c, dy = yy - c;
                    float a = Mathf.Clamp01(radius - Mathf.Sqrt(dx * dx + dy * dy) + 0.5f);
                    t.SetPixel(xx, yy, new Color(1f, 1f, 1f, a));
                }
            t.Apply();
            return t;
        }

        static Texture2D Bordered(Color fill, Color border)
        {
            var t = new Texture2D(3, 3, TextureFormat.RGBA32, false)
            { hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            for (int x = 0; x < 3; x++)
                for (int y = 0; y < 3; y++)
                    t.SetPixel(x, y, x == 1 && y == 1 ? fill : border);
            t.Apply();
            return t;
        }

        static GUIStyle BoxStyle(Color fill, Color border)
        {
            var st = new GUIStyle { border = new RectOffset(1, 1, 1, 1) };
            st.normal.background = Bordered(fill, border);
            return st;
        }

        // ── Cached accessors ──────────────────────────────────────
        // Text colour is applied with GUI.color at draw time (multiplies the
        // style's white), so one style serves every palette role.
        public static GUIStyle Label(int fontSize, FontStyle fs = FontStyle.Normal,
            TextAnchor anchor = TextAnchor.MiddleLeft, bool serif = false)
        {
            var key = (fontSize, fs, anchor, serif && _serif != null);
            if (_labels.TryGetValue(key, out var st)) return st;
            st = new GUIStyle(_label) { fontSize = fontSize, fontStyle = fs, alignment = anchor };
            if (serif && _serif != null) st.font = _serif;
            _labels[key] = st;
            return st;
        }

        public static GUIStyle Button(int fontSize, bool selected = false, bool danger = false)
        {
            int kind = danger ? 2 : selected ? 1 : 0;
            var key = (fontSize, kind);
            if (_buttons.TryGetValue(key, out var st)) return st;
            st = new GUIStyle(danger ? _btnDanger : selected ? _btnSelected : _btn) { fontSize = fontSize };
            _buttons[key] = st;
            return st;
        }

        public static GUIStyle TextField(int fontSize)
        {
            if (_fields.TryGetValue(fontSize, out var st)) return st;
            st = new GUIStyle(_textField) { fontSize = fontSize };
            _fields[fontSize] = st;
            return st;
        }

        public static GUIStyle OptionRow(bool selected) => selected ? _optionRowSelected : _optionRow;

        public static GUIStyle SliderThumb(float sc)
        {
            _sliderThumb.fixedWidth  = Mathf.Round(10f * sc);
            _sliderThumb.fixedHeight = Mathf.Round(14f * sc);
            return _sliderThumb;
        }

        // Single mutable style reused for every overhead icon label.
        public static GUIStyle IconLabel(int fontSize, Color color)
        {
            _iconLabel.fontSize = fontSize;
            _iconLabel.normal.textColor = color;
            return _iconLabel;
        }

        // Bold centered glyph style for button badges.
        public static GUIStyle BadgeLabel(int fontSize, Color color)
        {
            _badgeLabel.fontSize = fontSize;
            _badgeLabel.normal.textColor = color;
            return _badgeLabel;
        }

        public static void DrawDisc(Rect r, Color c)
        {
            GUI.color = c;
            GUI.DrawTexture(r, Disc);
            GUI.color = Color.white;
        }

        // Pill shape composed from two discs + a centre rect (seamless when
        // the rect is drawn last, covering the discs' inner AA edges).
        public static void DrawCapsule(Rect r, Color c)
        {
            GUI.color = c;
            float h = r.height;
            GUI.DrawTexture(new Rect(r.x, r.y, h, h), Disc);
            GUI.DrawTexture(new Rect(r.xMax - h, r.y, h, h), Disc);
            if (r.width > h)
                GUI.DrawTexture(new Rect(r.x + h * 0.5f, r.y, r.width - h, h), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        // ── Primitive draw helpers ────────────────────────────────
        public static void Box(Rect r, GUIStyle style) => GUI.Label(r, GUIContent.none, style);

        public static void Fill(Rect r, Color c)
        {
            GUI.color = c;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        public static void Text9(Rect r, string text, Color c, int size,
            FontStyle fs = FontStyle.Normal, TextAnchor anchor = TextAnchor.MiddleLeft, bool serif = false)
        {
            GUI.color = c;
            Drawing.OutlinedLabel(r, text, Label(size, fs, anchor, serif));
            GUI.color = Color.white;
        }

        public static void DrawSprite(Rect r, Sprite s)
        {
            var tex = s.texture;
            // Atlas-packed sprites: rect is in ORIGINAL texture space, but the
            // pixels live in the packed atlas at textureRect. Sampling with
            // rect reads a random neighbouring region of the atlas — that is
            // why earlier releases drew emblem/portrait art instead of the
            // real skill icons. textureRect throws for tight-packed sprites;
            // fall back.
            // council 2026-09-11 #5
            Rect sr;
            try { sr = s.packed ? s.textureRect : s.rect; }
            catch { sr = s.rect; }
            GUI.DrawTextureWithTexCoords(r, tex,
                new Rect(sr.x / tex.width, sr.y / tex.height, sr.width / tex.width, sr.height / tex.height));
        }

        public static void DrawBorder(Rect r, Color c, float bw)
        {
            GUI.color = c;
            GUI.DrawTexture(new Rect(r.x,         r.y,         r.width, bw),       Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.x,         r.yMax - bw, r.width, bw),       Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.x,         r.y,         bw,      r.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.xMax - bw, r.y,         bw,      r.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        public static Rect Pad(Rect r, float d) => new(r.x - d, r.y - d, r.width + d * 2, r.height + d * 2);

        public static Color WithAlpha(Color c, float a) => new(c.r, c.g, c.b, a);
    }
}
