// ================================================================
//  GroundLabels.cs — appends tier+grade brackets to dropped item
//  name labels:   PLATED BELT  [5A 7C 4C 1S]
//
//  Ported from v1 nearly verbatim — the most battle-tested subsystem
//  in the mod. Credits: logic adapted from KG / war3i4i.
//
//  Laws (see ARCHAEOLOGY.md):
//  • One-frame coroutine: EHG writes the base text the same frame.
//  • REUSE EHG's written text as the base (never rebuild from FullName)
//    — preserves the native "(53)" rule number (MedianAura's report).
//  • Three-zero-width-space Marker: double-process guard AND the strip
//    token for the MedianAura fix. Load-bearing twice.
//  • SetText saves/restores TMP faceColor (LeHud truce — .text writes
//    wipe LeHud's custom rarity colors via mesh rebuild).
//  • isUniqueSetOrLegendary() ground items are Fallen Star's territory.
//    Skipped, entirely, forever.
//  • Never ToUpper assembled rich text (breaks <color> tags).
// ================================================================

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

public static class GroundLabels
{
    // Zero-width space marker — prevents double-processing a label.
    // Escapes, not literal chars: load-bearing bytes must be visible.
    internal const string Marker = "\u200B\u200B\u200B";

    // EHG's trailing rule number: "Ruby Ring (53)" — parenthesized
    // (the bare-number regex was v1.4's silent failure)
    private static readonly Regex s_ruleNumRegex = new(@"^(.*?)\s+\((\d+)\)\s*$",
        RegexOptions.Compiled | RegexOptions.Singleline);

    // ── Alt-key cache ─────────────────────────────────────────────────
    private static readonly List<(GroundItemLabel label, string plain, string bracketed)>
        s_altCache = new();

    private static bool s_altWasHeld = false;

    // Called from TerribleTooltipsMod.OnUpdate()
    public static void OnUpdate()
    {
        if (!Prefs.LabelAltKey.Value) return;
        if (s_altCache.Count == 0) return;

        s_altCache.RemoveAll(e =>
            e.label == null || !e.label.gameObject.activeInHierarchy);

        bool altHeld = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
        if (altHeld == s_altWasHeld) return;   // no change — skip costly text updates
        s_altWasHeld = altHeld;

        foreach (var (label, plain, bracketed) in s_altCache)
        {
            try
            {
                if (label.itemText == null) continue;
                string target = altHeld ? bracketed : plain;
                // No ToUpper here (Law 4): the cached strings were assembled
                // from EHG's text, which is already uppercased for emphasized
                // labels — re-uppercasing would mangle the <color> tags.
                SetText(label.itemText, target + Marker);
                label.sceneFollower?.calculateDimensions();
            }
            catch { }
        }
    }

    // ── Patch ─────────────────────────────────────────────────────────
    [HarmonyPatch(typeof(GroundItemLabel),
        nameof(GroundItemLabel.SetGroundTooltipText))]
    internal static class Patch_GroundLabel
    {
        private static void Postfix(GroundItemLabel __instance)
        {
            if (!Prefs.Enabled || Prefs.LabelStyle.Value == GroundLabelStyle.None) return;
            MelonCoroutines.Start(DelayRoutine(__instance));
        }
    }

    // ── Label builder ─────────────────────────────────────────────────
    private static IEnumerator DelayRoutine(GroundItemLabel item)
    {
        // One-frame yield — let EHG finish writing the base label text first
        yield return null;

        if (item == null || !item) yield break;

        ItemDataUnpacked itemData;
        try
        {
            itemData = item.getItemData();
            if (itemData == null) yield break;
        }
        catch { yield break; }

        TextMeshProUGUI tmp = item.itemText;
        if (!tmp) yield break;

        // Already processed — skip
        if (tmp.text.Contains(Marker)) yield break;

        // Defer unique/set/legendary items to Fallen_LE_Mods which shows
        // LP, Weaver's Will, NEW, OWNED and stash comparison — much better.
        if (itemData.isUniqueSetOrLegendary()) yield break;

        // Filter check
        if (Prefs.LabelFilterOnly.Value)
            if (!TerribleTooltipsAPI.CheckFilter(itemData, out _, true)) yield break;

        // Read whatever EHG already wrote (includes its rule number) —
        // preserved so the native rule display is never lost (MedianAura).
        string ehgText = tmp.text.Replace(Marker, "").Trim();
        string ehgBase = !string.IsNullOrEmpty(ehgText)
            ? ehgText
            : (item.emphasized ? itemData.FullName.ToUpper() : itemData.FullName);

        Match  ruleMatch  = s_ruleNumRegex.Match(ehgBase);
        bool   hasRuleNum = ruleMatch.Success;
        string cleanName  = hasRuleNum ? ruleMatch.Groups[1].Value : ehgBase;
        string ruleNum    = hasRuleNum ? ruleMatch.Groups[2].Value : "";

        string bracket   = BuildBracket(itemData);
        string plain     = Assemble(cleanName, ruleNum, hasRuleNum, "");
        string bracketed = Assemble(cleanName, ruleNum, hasRuleNum, bracket);

        // ── Alt-key mode ──────────────────────────────────────────────
        if (Prefs.LabelAltKey.Value)
        {
            // Respect the CURRENT Alt state: an item dropped while Alt is
            // already held must show its brackets immediately — the OnUpdate
            // swap only fires on a state CHANGE, and that change already
            // happened before this label existed.
            bool altNow = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
            SetText(tmp, (altNow ? bracketed : plain) + Marker);
            item.sceneFollower?.calculateDimensions();

            s_altCache.RemoveAll(e => e.label == item);
            s_altCache.Add((item, plain, bracketed));
            yield break;
        }

        // ── Always-visible mode ───────────────────────────────────────
        SetText(tmp, bracketed + Marker);
        item.sceneFollower?.calculateDimensions();
    }

    // ── Bracket builder ───────────────────────────────────────────────
    private static string BuildBracket(ItemDataUnpacked itemData)
    {
        if (itemData.affixes.Count == 0) return "";

        var style = Prefs.LabelStyle.Value;
        var sb    = new StringBuilder("[");

        bool first = true;
        foreach (ItemAffix affix in itemData.affixes)
        {
            if (!first) sb.Append(' ');
            first = false;

            double roll        = Math.Round(affix.getRollFloat() * 100.0, 1);
            int    tier        = affix.DisplayTier;
            string tierColor   = Colors.TierColor(tier);
            string letterColor = Colors.GradeLetterColor(roll);
            string letter      = Colors.GradeLetter(roll);

            switch (style)
            {
                case GroundLabelStyle.TierAndRank:
                    if (tier > 0)
                        sb.Append($"<color={tierColor}>{tier}</color>");
                    sb.Append($"<color={letterColor}>{letter}</color>");
                    break;

                case GroundLabelStyle.TierOnly:
                    if (tier > 0)
                        sb.Append($"<color={tierColor}>{tier}</color>");
                    else
                        sb.Append("-");
                    break;

                case GroundLabelStyle.RankOnly:
                    sb.Append($"<color={letterColor}>{letter}</color>");
                    break;
            }
        }

        sb.Append(']');
        return sb.ToString();
    }

    // ── Safe TMP text writer (the LeHud truce) ────────────────────────
    // Setting .text triggers a TMP mesh rebuild that wipes faceColor
    // changes applied by other mods (LeHud's custom rarity colors).
    // Snapshot before, restore after; no intermediate empty-string write.
    private static void SetText(TextMeshProUGUI tmp, string value)
    {
        var savedColor = tmp.faceColor;
        tmp.text = value;
        tmp.faceColor = savedColor;
    }

    // ── Static label assembler — avoids IL2CPP closure issues ─────────
    private static string Assemble(string cleanName, string ruleNum, bool hasRuleNum, string bracket)
    {
        string br = !string.IsNullOrEmpty(bracket) ? " " + bracket : "";

        if (!hasRuleNum)
            return cleanName + br;

        string num = "(" + ruleNum + ")";
        return Prefs.LabelRulePosition.Value switch
        {
            RuleNumberPosition.Start => num + " " + cleanName + br,
            RuleNumberPosition.End   => cleanName + br + " " + num,
            _                        => cleanName + " " + num + br
        };
    }
}
