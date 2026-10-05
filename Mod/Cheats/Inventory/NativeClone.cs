#nullable disable
using System;
using System.Collections.Generic;
using Il2Cpp;
using Il2CppTMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Mod.Cheats.Inventory
{
    // The EHG-proud factory: every visual element this mod shows is a CLONE
    // of the game's own Sort button — native 9-slice sprite, native font and
    // material, native hover/press transitions. Nothing hand-painted.
    // (v1 drew flat-color rectangles with a scavenged font; see SPEC.md.)
    internal static class NativeClone
    {
        // Il2Cpp delegates passed to AddListener must stay referenced from
        // managed code or the GC collects them and clicks go dead.
        static readonly Dictionary<UnityEngine.UI.Button, Delegate> _keepAlive = new();

        static void Retain(UnityEngine.UI.Button control, Delegate listener)
        {
            // Council A5: key delegates by live control; rebinds replace and destroyed controls are pruned.
            foreach (var retained in new List<UnityEngine.UI.Button>(_keepAlive.Keys))
                if (retained == null) _keepAlive.Remove(retained);
            _keepAlive[control] = listener;
        }

        public static GameObject Button(GameObject template, Transform parent, string name, Action onClick)
        {
            GameObject go = UnityEngine.Object.Instantiate(template, parent);
            try
            {
                // Strip the donor's behaviour so it can't fight our wiring.
                var sort = go.GetComponent<SortInventoryButton>();
                if (sort != null) UnityEngine.Object.DestroyImmediate(sort);

                // Kill localization bindings or the game rewrites our labels.
                foreach (var loc in go.GetComponentsInChildren<UnityEngine.Localization.Components.LocalizeStringEvent>(true))
                    UnityEngine.Object.DestroyImmediate(loc);

                var btn = go.GetComponent<Button>();
                if (btn != null)
                {
                    // Fresh event object: clears runtime AND serialized
                    // persistent listeners (RemoveAllListeners only clears
                    // runtime ones — a game update wiring the donor via
                    // persistent calls would survive onto our clones).
                    btn.onClick = new Button.ButtonClickedEvent();
                    var action = new Action(onClick);
                    Retain(btn, action);
                    btn.onClick.AddListener(action);
                }

                // Native-look diagnostics for the in-game validation pass:
                // the 9-slice assumption lives or dies on this sprite.
                if (Prefs.DebugLog != null && Prefs.DebugLog.Value)
                {
                    try
                    {
                        var img = go.transform.childCount > 0
                            ? go.transform.GetChild(0).GetComponent<Image>()
                            : go.GetComponent<Image>();
                        if (img != null)
                            Dbg.Log($"clone '{name}': sprite={(img.sprite != null ? img.sprite.name : "none")} " +
                                    $"type={img.type} border={(img.sprite != null ? img.sprite.border.ToString() : "?")}");
                    }
                    catch { }
                }

                go.name = name;   // name LAST — a half-built clone is never findable as ours
                return go;
            }
            catch
            {
                // Destroyed in catch — never leak a live donor-named clone
                // into the footer/column (it would duplicate every rebuild).
                try { if (go) UnityEngine.Object.DestroyImmediate(go); } catch { }
                throw;            // callers own the single-log degradation
            }
        }

        // The Sort button's child(1) is its dots glyph — it overlaps custom
        // label text ("the S and the symbol with the dots are overlapping").
        public static void HideIcon(GameObject btn)
        {
            try
            {
                Transform icon = null;
                Button button = btn.GetComponent<Button>();
                for (int i = 0; i < btn.transform.childCount; i++)
                {
                    Transform child = btn.transform.GetChild(i);
                    Image image = child.GetComponent<Image>();
                    if (image == null) continue;
                    string name = (child.name ?? "").ToLowerInvariant();
                    bool namedIcon = name.Contains("icon") || name.Contains("dot") || name.Contains("sort");
                    bool componentIcon = (button == null || image != button.targetGraphic) &&
                                         child.GetComponentInChildren<TMP_Text>(true) == null;
                    if (namedIcon || componentIcon) { icon = child; break; }
                }

                // Council A6: prefer icon component/name discovery, retaining the known donor index as fallback.
                if (icon != null) icon.gameObject.SetActive(false);
                else if (btn.transform.childCount > 1)
                    btn.transform.GetChild(1).gameObject.SetActive(false);
            }
            catch { }
        }

        public static TMP_Text Label(GameObject btn)
        {
            try { return btn.GetComponentInChildren<TMP_Text>(true); }
            catch { return null; }
        }

        // Single-line auto-sizing label (footer buttons). Pinned single-line:
        // the donor's wrap setting must not break "STASH ALL" onto two lines.
        public static void SetLabel(GameObject btn, string text, float minSize, float maxSize)
        {
            var tmp = Label(btn);
            if (tmp == null) return;
            tmp.text = text;
            tmp.enableAutoSizing = true;
            tmp.fontSizeMin = minSize;
            tmp.fontSizeMax = maxSize;
            tmp.enableWordWrapping = false;
            tmp.overflowMode = TextOverflowModes.Truncate;
        }

        // Rich-text label stretched over the whole button (teleport column).
        // baseSize > 0 pins a deterministic font size (the donor's auto-size
        // leaves whatever it last computed for its own string); autoMin/Max
        // > 0 enables shrink-to-fit for long destination names.
        public static void SetRichLabel(GameObject btn, string richText,
            float baseSize = 0f, float autoMin = 0f, float autoMax = 0f)
        {
            var tmp = Label(btn);
            if (tmp == null) return;
            tmp.text = richText;
            if (baseSize > 0f) tmp.fontSize = baseSize;
            if (autoMin > 0f && autoMax > 0f)
            {
                tmp.enableAutoSizing = true;
                tmp.fontSizeMin = autoMin;
                tmp.fontSizeMax = autoMax;
            }
            else
            {
                tmp.enableAutoSizing = false;
            }
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.enableWordWrapping = false;
            tmp.overflowMode = TextOverflowModes.Truncate;
            try
            {
                var rt = tmp.GetComponent<RectTransform>();
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = new Vector2(6f, 2f);
                rt.offsetMax = new Vector2(-4f, -2f);
            }
            catch { }
        }

        // Slim faction-identity accent bar on the button's left edge —
        // colour coding without painting over the native art.
        public static void AddAccentBar(GameObject btn, Color color)
        {
            var bar = new GameObject("medick_Accent");
            bar.transform.SetParent(btn.transform, false);
            var rt = bar.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot     = new Vector2(0f, 0.5f);
            rt.offsetMin = new Vector2(3f, 4f);
            rt.offsetMax = new Vector2(7f, -4f);   // 4px wide strip, inset from the frame
            var img = bar.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
        }

        static Sprite _chevronSprite;

        // Anti-aliased right-pointing triangle, rotated per state. Replaces ASCII arrows,
        // which the game's TMP font has no good glyphs for.
        static Sprite ChevronSprite()
        {
            if (_chevronSprite != null) return _chevronSprite;
            const int size = 32;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            Vector2 a = new Vector2(9f, 6f), b = new Vector2(9f, 26f), c = new Vector2(25f, 16f);
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int hits = 0;
                    for (int sy = 0; sy < 4; sy++)
                        for (int sx = 0; sx < 4; sx++)
                            if (InTriangle(new Vector2(x + (sx + 0.5f) / 4f, y + (sy + 0.5f) / 4f), a, b, c)) hits++;
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(hits * 255 / 16));
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            _chevronSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            _chevronSprite.hideFlags = HideFlags.HideAndDontSave;
            return _chevronSprite;
        }

        static bool InTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float d1 = Sign(p, a, b), d2 = Sign(p, b, c), d3 = Sign(p, c, a);
            bool neg = d1 < 0 || d2 < 0 || d3 < 0, pos = d1 > 0 || d2 > 0 || d3 > 0;
            return !(neg && pos);
        }

        static float Sign(Vector2 p1, Vector2 p2, Vector2 p3)
            => (p1.x - p3.x) * (p2.y - p3.y) - (p2.x - p3.x) * (p1.y - p3.y);

        // Tinted triangle at the button's left edge; returns its RectTransform so callers can rotate it.
        public static RectTransform AddChevron(GameObject btn, Color color, float x, float size)
        {
            var go = new GameObject("medick_Chevron");
            go.transform.SetParent(btn.transform, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(x, 0f);
            rt.sizeDelta = new Vector2(size, size);
            var img = go.AddComponent<Image>();
            img.sprite = ChevronSprite();
            img.color = color;
            img.raycastTarget = false;
            return rt;
        }

        // Shifts the label's left edge so text clears a left-hand chevron.
        public static void SetLabelInset(GameObject btn, float left)
        {
            try
            {
                var tmp = Label(btn);
                if (tmp == null) return;
                var rt = tmp.GetComponent<RectTransform>();
                rt.offsetMin = new Vector2(left, rt.offsetMin.y);
            }
            catch { }
        }
        // Sized for free-form parents (master tab) — no layout group there.
        public static void SetRect(GameObject go, Vector2 anchoredPos, Vector2 size)
        {
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta        = size;
        }

        // Sized for VerticalLayoutGroup parents (the teleport column).
        public static void SetLayoutSize(GameObject go, float width, float height)
        {
            var le = go.GetComponent<LayoutElement>();
            if (le == null) le = go.AddComponent<LayoutElement>();
            le.preferredWidth  = width;
            le.preferredHeight = height;
            le.flexibleWidth   = 0f;
            le.flexibleHeight  = 0f;
        }
    }
}
