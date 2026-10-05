#nullable disable
using System;
using System.Collections.Generic;
using Il2CppTMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Mod.Cheats.Inventory
{
    // The Quick Teleport menu: an always-visible master tab at the panel's
    // left border, expanding a collapsible column of native-skinned buttons
    // grouped FACTIONS / DUNGEONS / HUBS.
    //
    // Geometry constants are Andrew's hand-tuned April values (flush with
    // the weapon slots, clear of the panel's decorative border) — the layout
    // was always right; only the rendering was flat. v2 keeps the numbers
    // and replaces every visual with clones of the game's own button.
    // The column lives in a VerticalLayoutGroup (collapse = SetActive);
    // v1's hand-rolled Reflow list is retired.
    internal static class TeleportMenu
    {
        const string GUARD = "medick_TpMaster";

        // ── Andrew's geometry (do not retune without in-game evidence) ──
        const float BTN_H = 48f;
        const float HDR_H = 30f;
        const float GAP   = 3f;
        const float COL_W = 118f;

        // Shared top edge for the tab AND the column — aligned with the
        // helmet/amulet equipment row so the tab mirrors the amulet slot.
        // Tuning log: -4 (too low) → 62 (overshot, pass #3) → 22.
        const float TOP_Y = 22f;

        // Master tab sits to the RIGHT of the panel's decorative border bar
        // (~52 canvas units wide) so the frame art can't cover its text.
        const float TAB_X = 76f;
        const float TAB_W = 168f;
        const float TAB_H = 70f;

        // The column does NOT follow the tab — frozen at the original anchor
        // (28 - GAP - COL_W); only its TOP rides TOP_Y with the tab.
        const float COL_X = -93f;

        // ── Destinations (scene names verified in-game; see ARCHAEOLOGY.md —
        //    Dun1Q10 = Temporal Sanctum, Dun2Q10 = Lightless Arbor; the v1.0
        //    swap is not to be re-introduced) ──
        static readonly (string line1, string line2, string scene, Color color)[][] Groups =
        {
            new[]   // FACTIONS
            {
                ("Circle of Fortune", "The Observatory", "Observatory", new Color(0.25f, 0.42f, 0.95f)),
                ("Merchant's Guild",  "The Bazaar",      "Bazaar",      new Color(0.90f, 0.25f, 0.25f)),
                ("Forgotten Knights", "Shattered Road",  "M_Knight",    new Color(0.62f, 0.30f, 0.90f)),
                ("The Woven",         "Haven of Silk",   "WeaversHub",  new Color(0.15f, 0.75f, 0.72f)),
            },
            new[]   // DUNGEONS
            {
                ("Lightless Arbor",   "DUNGEON",         "Dun2Q10",     new Color(0.45f, 0.40f, 0.65f)),
                ("Temporal Sanctum",  "DUNGEON",         "Dun1Q10",     new Color(0.55f, 0.35f, 0.95f)),
                ("Soulfire Bastion",  "DUNGEON",         "Dun3Q10",     new Color(0.95f, 0.42f, 0.18f)),
            },
            new[]   // HUBS
            {
                ("The End of Time",   "TOWN",            "EoT",         new Color(0.80f, 0.65f, 0.95f)),
                ("Champion's Gate",   "ARENA",           "ArenaLobby",  new Color(0.55f, 0.75f, 0.95f)),
                ("The Nexus",       "MONOLITHS",       "MonolithHub", new Color(0.80f, 0.30f, 0.30f)),
            },
        };

        static readonly string[] GroupNames = { "FACTIONS", "DUNGEONS", "HUBS" };

        static GameObject _masterTab;
        static GameObject _column;
        static TMP_Text   _masterLabel;
        static RectTransform _masterChevron;
        static readonly RectTransform[] _groupChevrons = new RectTransform[3];

        // HUD palette (matches the LEHud menu).
        static readonly Color Gold = new Color(0.788f, 0.651f, 0.325f);
        static readonly Color TextHi = new Color(0.929f, 0.902f, 0.831f);
        static bool       _columnOpen = true;
        static readonly bool[]       _groupOpen   = { true, true, true };
        static readonly TMP_Text[]   _groupLabels = new TMP_Text[3];
        static readonly List<(GameObject go, int group)> _items = new();

        public static void Inject(Transform panel, GameObject template)
        {
            try
            {
                if (panel.Find(GUARD) != null) { ApplyVisibility(); return; }   // this panel already built

                // Fresh panel instance: reset the item registry only.
                // _columnOpen/_groupOpen deliberately survive rebuilds — the
                // player's collapse choices carry across zone changes.
                _items.Clear();

                // ── Master tab (always visible at the panel border) ──
                _masterTab = NativeClone.Button(template, panel, GUARD, ToggleColumn);
                NativeClone.HideIcon(_masterTab);
                NativeClone.SetRect(_masterTab, new Vector2(TAB_X, TOP_Y), new Vector2(TAB_W, TAB_H));
                StripLayoutElement(_masterTab);
                NativeClone.SetRichLabel(_masterTab, MasterLabelText());
                _masterLabel = NativeClone.Label(_masterTab);
                _masterChevron = NativeClone.AddChevron(_masterTab, Gold, 16f, 16f);
                NativeClone.SetLabelInset(_masterTab, 26f);
                ApplyMasterChevron();

                // ── Column container (VerticalLayoutGroup owns the layout) ──
                _column = new GameObject("medick_TpColumn");
                _column.transform.SetParent(panel, false);
                var crt = _column.AddComponent<RectTransform>();
                crt.anchorMin = crt.anchorMax = crt.pivot = new Vector2(0f, 1f);
                crt.anchoredPosition = new Vector2(COL_X, TOP_Y);
                crt.sizeDelta        = new Vector2(COL_W, 0f);
                var vlg = _column.AddComponent<VerticalLayoutGroup>();
                vlg.spacing = GAP;
                vlg.childForceExpandWidth  = false;
                vlg.childForceExpandHeight = false;
                vlg.childControlWidth      = true;
                vlg.childControlHeight     = true;
                vlg.childAlignment         = TextAnchor.UpperLeft;
                var fitter = _column.AddComponent<ContentSizeFitter>();
                fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

                for (int g = 0; g < Groups.Length; g++)
                {
                    AddGroupHeader(template, g);
                    foreach (var d in Groups[g])
                        AddDestination(template, g, d);
                }

                ApplyGroupStates();
                ApplyVisibility();
                Dbg.Log("teleport menu injected");
            }
            catch (Exception e)
            {
                MelonLoader.MelonLogger.Error("teleport menu injection failed: " + e);
                // Destroyed in catch: the GUARD object is built first, so a
                // half-built column would otherwise latch forever and leave a
                // QUICK TELEPORT tab that toggles nothing.
                try
                {
                    if (_masterTab) UnityEngine.Object.DestroyImmediate(_masterTab);
                    if (_column)    UnityEngine.Object.DestroyImmediate(_column);
                }
                catch { }
                _masterTab = null;
                _column = null;
                _masterLabel = null;
                _masterChevron = null;
                System.Array.Clear(_groupChevrons, 0, _groupChevrons.Length);
                _items.Clear();
            }
        }

        // ── Builders ──────────────────────────────────────────────

        static void AddGroupHeader(GameObject template, int group)
        {
            int g = group;
            GameObject go = NativeClone.Button(template, _column.transform, $"medick_TpHdr{g}",
                () => ToggleGroup(g));
            NativeClone.HideIcon(go);
            NativeClone.SetLayoutSize(go, COL_W, HDR_H);
            NativeClone.SetRichLabel(go, HeaderText(g));
            _groupLabels[g] = NativeClone.Label(go);
            _groupChevrons[g] = NativeClone.AddChevron(go, Gold, 12f, 12f);
            NativeClone.SetLabelInset(go, 22f);
            ApplyGroupChevron(g);
        }

        static void AddDestination(GameObject template, int group,
            (string line1, string line2, string scene, Color color) d)
        {
            string scene = d.scene;
            GameObject go = NativeClone.Button(template, _column.transform, $"medick_Tp_{scene}",
                () => TravelService.RequestTravel(scene));
            NativeClone.HideIcon(go);
            NativeClone.SetLayoutSize(go, COL_W, BTN_H);

            string hex = ColorUtility.ToHtmlStringRGB(d.color);
            // Relative size tags + auto-sizing so "Circle of Fortune" and
            // "Forgotten Knights" shrink-to-fit instead of clipping silently.
            NativeClone.SetRichLabel(go,
                $"<color=#EDE6D4>{d.line1}</color>\n<size=78%><cspace=0.6><color=#{hex}>{d.line2}</color></cspace></size>",
                baseSize: 11.5f, autoMin: 8.5f, autoMax: 11.5f);
            NativeClone.AddAccentBar(go, d.color);   // faction identity, native art untouched

            _items.Add((go, group));
        }

        // ── State ─────────────────────────────────────────────────

        // Explicit size: the donor label's auto-size leaves a nondeterministic
        // fontSize behind (whatever its own localized string last computed).
        static string MasterLabelText() =>
            "<size=12><cspace=1><color=#C9A653>QUICK\nTELEPORT</color></cspace></size>";

        static string HeaderText(int g) =>
            $"<size=10.5><cspace=1.2><color=#C9A653>{GroupNames[g]}</color></cspace></size>";

        // Open: points left (collapse); closed: points right.
        static void ApplyMasterChevron()
        {
            if (_masterChevron != null)
                _masterChevron.localRotation = Quaternion.Euler(0f, 0f, _columnOpen ? 180f : 0f);
        }

        // Open: points down; closed: points right.
        static void ApplyGroupChevron(int g)
        {
            if (_groupChevrons[g] != null)
                _groupChevrons[g].localRotation = Quaternion.Euler(0f, 0f, _groupOpen[g] ? -90f : 0f);
        }
        static void ToggleColumn()
        {
            _columnOpen = !_columnOpen;
            if (_masterLabel != null) _masterLabel.text = MasterLabelText();
            ApplyMasterChevron();
            ApplyVisibility();
        }

        static void ToggleGroup(int g)
        {
            _groupOpen[g] = !_groupOpen[g];
            if (_groupLabels[g] != null) _groupLabels[g].text = HeaderText(g);
            ApplyGroupChevron(g);
            ApplyGroupStates();
        }

        static void ApplyGroupStates()
        {
            foreach (var (go, group) in _items)
            {
                try { go.SetActive(_groupOpen[group]); } catch { }
            }
        }

        // Master visibility: the ShowTeleport pref hides everything;
        // _columnOpen folds the column behind the master tab.
        public static void ApplyVisibility()
        {
            bool show = Prefs.ShowTeleport.Value;
            try { if (_masterTab != null && _masterTab.activeSelf != show) _masterTab.SetActive(show); } catch { }
            bool col = show && _columnOpen;
            try { if (_column != null && _column.activeSelf != col) _column.SetActive(col); } catch { }
        }

        static void StripLayoutElement(GameObject go)
        {
            try
            {
                var le = go.GetComponent<LayoutElement>();
                if (le != null) UnityEngine.Object.DestroyImmediate(le);
            }
            catch { }
        }
    }
}
