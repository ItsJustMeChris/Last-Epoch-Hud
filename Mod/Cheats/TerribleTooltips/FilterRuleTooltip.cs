// ================================================================
//  FilterRuleTooltip.cs — injects the matched loot filter rule number
//  into the item tooltip.
//
//  Two-phase approach (the LeHud-war survivor — see ARCHAEOLOGY.md):
//    1. Harmony postfixes on UITooltipItem.SetAsItemTooltip /
//       SetAsGroundTooltip capture the ItemDataUnpacked. These are
//       verifiably DIFFERENT native functions from anything LeHud
//       patches. We NEVER patch TooltipItemManager.OpenItemTooltip —
//       it shares a native function with LeHud's OpenTooltip patch and
//       two Harmony patches on one native function = circular IL2CPP
//       trampoline = instant stack overflow on hover.
//    2. MonitorUpdate (from OnUpdate) injects the gold rule tag into
//       the 'requires' TMP after the tooltip is fully rendered.
//       (loreText is invisible on non-uniques; PrefixHeader gets
//       overwritten by the game — both were dead ends.)
//
//  Rule # stays GOLD #FA9E3D — Fallen Star owns filter color
//  coordination; the gold is a promise, not a limitation.
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

public static class FilterRuleTooltip
{
    // Two zero-width spaces (vs GroundLabels' three) — escapes, not
    // literal chars: load-bearing bytes must be visible in review.
    private const string Marker = "\u200B\u200B";
    private const string Gold   = "#FA9E3D";

    private static ItemDataUnpacked s_pendingItem  = null;
    private static bool             s_injected     = false;

    // ── Harmony: inventory / stash / equipment hover ──────────────────
    [HarmonyPatch(typeof(UITooltipItem), "SetAsItemTooltip")]
    internal static class Patch_SetAsItemTooltip
    {
        private static void Postfix(UITooltipItem __instance, ItemDataUnpacked item)
        {
            if (item != null)
            {
                s_pendingItem = item;
                TooltipRecolor.MarkDirty();   // new tooltip content — the composer must look
                s_injected    = false;
            }
        }
    }

    // ── Harmony: ground-label hover ───────────────────────────────────
    // Parameter is named _item in the game binary — HarmonyX matches by name.
    [HarmonyPatch(typeof(UITooltipItem), "SetAsGroundTooltip")]
    internal static class Patch_SetAsGroundTooltip
    {
        private static void Postfix(UITooltipItem __instance, ItemDataUnpacked _item)
        {
            if (_item != null)
            {
                s_pendingItem = _item;
                TooltipRecolor.MarkDirty();
                s_injected    = false;
            }
        }
    }

    // ── Called from OnUpdate — injects after tooltip is rendered ──────
    public static void MonitorUpdate()
    {
        // Master toggle promises "turn this off to restore default item
        // tooltips" — that includes the gold Rule #, which defaults ON in v2.
        if (!Prefs.Enabled) return;
        var mode = Prefs.ShowFilterRuleNumber.Value;
        if (mode == FilterRuleDisplay.Off) return;

        try
        {
            var tooltipUI = UITooltipItem.instance;
            if (tooltipUI == null) return;

            bool active = false;
            try { active = tooltipUI.tooltipActive; } catch { }

            if (!active)
            {
                s_pendingItem = null;
                s_injected    = false;
                return;
            }

            if (s_pendingItem == null || s_injected) return;

            // Marker already present → re-hover of same item, injection
            // persisted. Scan ONLY the 'requires' TMP (the sole element we
            // inject into) — a whole-tooltip scan could false-positive on
            // another mod's zero-width characters and silently suppress us.
            try
            {
                foreach (var tmp in tooltipUI.GetComponentsInChildren<TextMeshProUGUI>())
                {
                    if (tmp == null || tmp.gameObject.name != "requires") continue;
                    if ((tmp.text ?? "").Contains(Marker))
                    {
                        s_injected = true;
                        Dbg.Log("marker already in 'requires' — injection persisted");
                        return;
                    }
                }
            }
            catch { }

            if (!TryGetMatchedRule(s_pendingItem, out int displayNum, out Rule rule) || rule == null)
            {
                s_injected = true;  // don't retry
                return;
            }

            string ruleTag;
            if (mode == FilterRuleDisplay.NumberOnly)
                ruleTag = $"<size=120%><b><color={Gold}>Rule#{displayNum}</color></b></size>";
            else
                ruleTag = $"<color={Gold}>Rule #{displayNum}: {GetRuleName(rule)}</color>";

            // Inject into the 'requires' element (the proven-visible target)
            try
            {
                foreach (var tmp in tooltipUI.GetComponentsInChildren<TextMeshProUGUI>())
                {
                    if (tmp == null || tmp.gameObject.name != "requires") continue;
                    if (!tmp.gameObject.activeInHierarchy) continue;

                    string orig = tmp.text ?? "";
                    tmp.text = ruleTag + Marker + "\n" + orig;
                    s_injected = true;
                    Dbg.Log($"rule #{displayNum} injected");
                    return;
                }
            }
            catch { }
        }
        catch { }
    }

    // ── Rule finder ───────────────────────────────────────────────────
    private static bool TryGetMatchedRule(ItemDataUnpacked item,
                                          out int displayNum, out Rule matched)
    {
        displayNum = 0;
        matched    = null;

        var filter = ItemFilterManager.Instance?.Filter;
        if (filter == null) return false;
        var rules = filter.rules;
        if (rules == null || rules.Count == 0) return false;

        try
        {
            var outcome = filter.Match(item, out _, out _,
                                       out int matchingRuleNum,
                                       out _, out _, out _, out _, out _);
            if (outcome != Rule.RuleOutcome.HIDE && matchingRuleNum > 0)
            {
                // matchingRuleNum IS the display number the game uses (same
                // as ground labels); the rules array is in reverse UI order.
                int idx = rules.Count - matchingRuleNum;
                if (idx >= 0 && idx < rules.Count && rules[idx] != null)
                {
                    displayNum = matchingRuleNum;
                    matched    = rules[idx];
                    return true;
                }
            }
        }
        catch { }

        // Fallback: manual scan
        try
        {
            for (int i = rules.Count - 1; i >= 0; i--)
            {
                Rule r = rules[i];
                if (r == null || !r.isEnabled) continue;
                try
                {
                    if (!r.Match(item, 0)) continue;
                    displayNum = rules.Count - i;
                    matched    = r;
                    return true;
                }
                catch { }
            }
        }
        catch { }

        return false;
    }

    private static string GetRuleName(Rule rule)
    {
        try { if (!string.IsNullOrWhiteSpace(rule.nameOverride)) return rule.nameOverride; } catch { }
        try { var d = rule.GetRuleDescription(); if (!string.IsNullOrWhiteSpace(d)) return d; } catch { }
        return "Unnamed Rule";
    }
}
