#nullable disable
using HarmonyLib;
using Il2Cpp;
using Il2CppItemFiltering;
using Il2CppTMPro;
using MelonLoader;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;
namespace Mod.Cheats.TerribleTooltips;

// Enum member names and order are FROZEN — the cfg round-trips them by name.
public enum GroundLabelStyle
{
    None,         // disabled — brackets never shown
    TierAndRank,  // [5A 3C 7S]   ← default
    TierOnly,     // [5 3 7]
    RankOnly      // [A C S]
}

public enum FilterRuleDisplay
{
    Off,           // nothing shown
    NumberOnly,    // "Rule#69"
    NumberAndName  // "Rule #69: Maxroll told me to pick this up blah blah"
}

public enum RuleNumberPosition
{
    EHGDefault,  // leave wherever EHG writes it (between name and our brackets)
    Start,       // "(69) PLATED BELT [5A 1F 4C]"
    End          // "PLATED BELT [5A 1F 4C] (69)"
}

// v3 clean-line layouts — the ship gate: several must work at release.
public enum TooltipLayout
{
    BadgeLeft,    // "Tier 5·A  58% increased Lightning Damage"  ← default (Andrew's pick)
    SignalRight,  // "58% increased Lightning Damage        Tier 5 A"
    Trailing      // "58% increased Lightning Damage — Tier 5 A"
}

public enum AffixNameColorMode
{
    TierColor,   // affix text wears its tier color (the WoW retina read)
    GameDefault, // the game's own text color; only the Tier·Grade signal is colored
    GreaterAffix // only Tier 6/7 affix text wears the ruled greater-affix tint ← default
}

public enum SignalStyle
{
    Badge,      // colored chip behind "Tier 7" / grades — the label look
    PlainText   // colored text only, no chips ← default
}

public enum TierWordStyle
{
    Spelled,
    Compact
}

public enum UnitSeparatorStyle
{
    Bar,
    Dot
}

public enum DividerStyle
{
    Strip,
    Glyph
}

public enum BorderColorMode
{
    Neutral,
    TierColor
}

// All persisted settings. Category name, entry names and the cfg path are
// FROZEN — the in-place upgrade path for v1.x users.
internal static class Prefs
{
    public static MelonPreferences_Category Category;

    // Tooltip
    public static MelonPreferences_Entry<bool> EnableTooltips;
    public static MelonPreferences_Entry<bool> TooltipTierColors;
    public static MelonPreferences_Entry<bool> TooltipRankColors;

    // v3 clean line
    public static MelonPreferences_Entry<TooltipLayout>       Layout;
    public static MelonPreferences_Entry<SignalStyle>         Style;
    public static MelonPreferences_Entry<AffixNameColorMode>  NameColorMode;
    public static MelonPreferences_Entry<bool>                ShowGradeLetters;
    public static MelonPreferences_Entry<bool>                AlwaysShowRanges;
    public static MelonPreferences_Entry<bool>                AlwaysShowTierDetails;

    // Ground labels
    public static MelonPreferences_Entry<GroundLabelStyle> LabelStyle;
    public static MelonPreferences_Entry<bool>             LabelFilterOnly;
    public static MelonPreferences_Entry<bool>             LabelAltKey;

    // Filter rule
    public static MelonPreferences_Entry<FilterRuleDisplay>  ShowFilterRuleNumber;
    public static MelonPreferences_Entry<RuleNumberPosition> LabelRulePosition;

    // New in v2
    public static MelonPreferences_Entry<bool> DebugLog;
    public static MelonPreferences_Entry<string> GreaterAffixTint;
    public static MelonPreferences_Entry<bool> UnitBorder;
    public static MelonPreferences_Entry<TierWordStyle> TierWord;
    public static MelonPreferences_Entry<UnitSeparatorStyle> UnitSeparator;
    public static MelonPreferences_Entry<DividerStyle> DividerStyle;
    public static MelonPreferences_Entry<BorderColorMode> BorderColorMode;
    public static MelonPreferences_Entry<int> BorderThickness;
    public static MelonPreferences_Entry<int> BorderPadX;
    public static MelonPreferences_Entry<int> BorderPadY;
    public static MelonPreferences_Entry<bool> BorderDebug;

    // Master switch AND the LEHud tooltip-provider selection (Fallen vs Terrible).
    public static bool Enabled => global::Mod.Cheats.FallenPlugins.ImprovedTooltips.UseTerribleTooltips && EnableTooltips != null && EnableTooltips.Value;

    public static void Init()
    {
        Category = MelonPreferences.CreateCategory("medick_Terrible_Tooltips");

        EnableTooltips    = Category.CreateEntry("EnableTooltips",    true,
            "Terrible Tooltips", "Master on/off for WoW-style tier/grade tooltip colours");
        TooltipTierColors = Category.CreateEntry("TooltipTierColors", true,
            "Tooltip Tier Colors", "Colour affix names by their crafting tier (T1 gray → T7 mythic)");
        TooltipRankColors = Category.CreateEntry("TooltipRankColors", true,
            "Tooltip Rank Colors", "Colour grade letters by roll quality (F gray → S mythic)");

        // v3 — THE CLEAN LINE (one line per affix; the essay dies)
        Layout = Category.CreateEntry("TooltipLayout", TooltipLayout.BadgeLeft,
            "Tooltip Layout", "Where the Tier·Grade signal sits on each affix line (BadgeLeft / SignalRight / Trailing)");
        Style = Category.CreateEntry("SignalStyle", SignalStyle.PlainText,
            "Signal Style", "PlainText = colored text only (default); Badge = Tier/Grade as colored chips");
        NameColorMode = Category.CreateEntry("AffixNameColor", AffixNameColorMode.GreaterAffix,
            "Affix Name Color", "GreaterAffix = only Tier 6/7 text wears the greater-affix tint (default); TierColor = text wears its tier color; GameDefault = game's own text color");
        ShowGradeLetters = Category.CreateEntry("ShowGradeLetters", true,
            "Show Grade Letters", "The S/A/B/C/F roll grade on each affix line");
        AlwaysShowRanges = Category.CreateEntry("AlwaysShowRanges", false,
            "Always Show Ranges", "Pin EHG's 'Range: X to Y' lines permanently (default: hidden, hold Alt to peek)");
        AlwaysShowTierDetails = Category.CreateEntry("AlwaysShowTierDetails", false,
            "Always Show Tier Details", "Pin EHG's full 'Tier: N (max craftable)' line (default: folded into the clean line, hold Alt to peek)");

        LabelStyle      = Category.CreateEntry("GroundLabelStyle", GroundLabelStyle.TierAndRank,
            "Ground Label Style", "What to show on items on the ground (None / TierAndRank / TierOnly / RankOnly)");
        LabelFilterOnly = Category.CreateEntry("GroundLabelFilterOnly", false,
            "Ground Labels: Filter Only", "Only show ground labels on loot-filter highlighted items");
        LabelAltKey     = Category.CreateEntry("GroundLabelAltKey", false,
            "Ground Labels: Hold Alt to Show", "Hide ground brackets until you hold Alt (KG-style)");

        // Default changed Off → NumberOnly in v2.0.0 — the April decision,
        // finally shipped ("make a note to set... number only setting on by default").
        ShowFilterRuleNumber = Category.CreateEntry("ShowFilterRuleNumber", FilterRuleDisplay.NumberOnly,
            "Tooltip: Show Filter Rule #", "Show the matched loot filter rule number inside the item tooltip on hover");
        LabelRulePosition = Category.CreateEntry("LabelRulePosition", RuleNumberPosition.EHGDefault,
            "Ground Label: Rule # Position", "Where to place EHG's filter rule number on the ground label (Start / End / EHGDefault)");

        DebugLog = Category.CreateEntry("DebugLog", false, "Verbose log output");
        GreaterAffixTint = Category.CreateEntry("GreaterAffixTint", Colors.GreaterAffixTintDefault,
            "GREATER-AFFIX TINT for Tier 6/7 affix sentences (ruled 2026-09-11). Consumed by the 3.1.0 composer ticket; the probe only logs it.");
        UnitBorder = Category.CreateEntry("UnitBorder", true,
            "Draw a thin border around the Tier·Grade unit under the text (PlainText style only). Dev build default on.");
        TierWord = Category.CreateEntry("TierWord", TierWordStyle.Spelled,
            "Tier 7 (Spelled) or T7 (Compact) in the tooltip signal. Ground labels unaffected.");
        UnitSeparator = Category.CreateEntry("UnitSeparator", UnitSeparatorStyle.Bar,
            "Bar = Tier 7 | A (default); Dot = Tier 7·A. Ground labels unaffected.");
        DividerStyle = Category.CreateEntry("DividerStyle", global::Mod.Cheats.TerribleTooltips.DividerStyle.Strip,
            "Strip = full-height divider image (default); Glyph = show the selected | or · glyph.");
        BorderColorMode = Category.CreateEntry("BorderColorMode", global::Mod.Cheats.TerribleTooltips.BorderColorMode.Neutral,
            "Neutral = the muted #5A4670 outline; TierColor = the tier colour at 60% alpha.");
        BorderThickness = Category.CreateEntry("BorderThickness", 2,
            "Border thickness in texels (clamped to 1–4).");
        BorderPadX = Category.CreateEntry("BorderPadX", 6,
            "Horizontal unit-border padding (clamped to 2–12).");
        BorderPadY = Category.CreateEntry("BorderPadY", 2,
            "Vertical unit-border padding base (clamped to 0–6, plus 0.5 units).");
        BorderDebug = Category.CreateEntry("BorderDebug", false,
            "Draw the unit border in diagnostic magenta with a translucent centre and log every placement.");

        Category.SetFilePath("UserData/medick_Terrible_Tooltips.cfg", autoload: true);
        WarnOrphanedKeys();
    }

    public static void Save()
    {
        // printmsg: false — settings clicks must not spam the console;
        // "one startup line" is the family logging contract.
        try { Category.SaveToFile(false); Dbg.Log("prefs saved"); }
        catch (Exception ex)
        {
            // GLM/Kimi 2026-09-10: persistence failure must not be silent.
            if (s_saveWarningLogged) return;
            s_saveWarningLogged = true;
            MelonLogger.Warning("prefs save failed: " + ex.Message);
        }
    }

    // GLM/Kimi 2026-09-10: warn about cfg keys that have no registered preference.
    private static void WarnOrphanedKeys()
    {
        if (s_orphanWarningChecked) return;
        s_orphanWarningChecked = true;

        try
        {
            const string path = "UserData/medick_Terrible_Tooltips.cfg";
            if (!System.IO.File.Exists(path)) return;

            var registered = new HashSet<string>(StringComparer.Ordinal)
            {
                "EnableTooltips", "TooltipTierColors", "TooltipRankColors",
                "TooltipLayout", "SignalStyle", "AffixNameColor", "ShowGradeLetters",
                "AlwaysShowRanges", "AlwaysShowTierDetails", "GroundLabelStyle",
                "GroundLabelFilterOnly", "GroundLabelAltKey", "ShowFilterRuleNumber",
                "LabelRulePosition", "DebugLog",
                "GreaterAffixTint", "UnitBorder", "TierWord", "UnitSeparator", "DividerStyle",
                "BorderColorMode", "BorderThickness", "BorderPadX", "BorderPadY", "BorderDebug"
            };
            var orphans = new HashSet<string>(StringComparer.Ordinal);

            foreach (string raw in System.IO.File.ReadAllLines(path))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line[0] == '#' || line[0] == '[') continue;
                int equals = line.IndexOf('=');
                if (equals <= 0) continue;
                string key = line.Substring(0, equals).Trim();
                if (key.Length > 0 && !registered.Contains(key)) orphans.Add(key);
            }

            if (orphans.Count > 0)
                MelonLogger.Warning("unregistered preference keys in cfg: " +
                                    string.Join(", ", orphans.OrderBy(k => k)));
        }
        catch (Exception ex) { Dbg.Log("orphan preference scan failed: " + ex.Message); }
    }

    private static bool s_saveWarningLogged;
    private static bool s_orphanWarningChecked;
}

internal static class Dbg
{
    public static void Log(string msg)
    {
        if (Prefs.DebugLog != null && Prefs.DebugLog.Value)
            MelonLogger.Msg("[debug] " + msg);
    }
}
