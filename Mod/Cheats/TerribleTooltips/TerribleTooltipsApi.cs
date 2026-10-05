// ================================================================
//  TerribleTooltipsAPI — public surface for other mods. FROZEN ABI.
//
//  Fallen_LE_Mods detection:
//    MelonMod.RegisteredMelons.Any(m => m.Info.Name == "Terrible Tooltips")
//  (add alongside the existing kg check: || m.Info.Name == "Terrible Tooltips")
//
//  The signature deliberately mirrors kg_LastEpoch_Improvements.CheckFilter
//  so this mod can serve as a drop-in dependency replacement for KG.
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

public static class TerribleTooltipsAPI
{
    // Returns true when itemData passes the loot filter and the matched
    // rule is emphasized (starred ★). bypass=true skips that requirement.
    public static bool CheckFilter(ItemDataUnpacked itemData, out Rule rule, bool bypass = false)
    {
        rule = null;
        try
        {
            if (itemData == null) return false;
            if (itemData.rarity == 9) return true;

            ItemFilter filter = ItemFilterManager.Instance?.Filter;
            if (filter == null) return false;

            if (filter.Match(itemData, out _, out _,
                             out int matchingRuleNumber,
                             out _, out _, out _, out _, out _) == Rule.RuleOutcome.HIDE)
                return false;

            if (matchingRuleNumber <= 0) return false;
            int orderedIndex = filter.rules.Count - matchingRuleNumber;
            // Kimi 2026-09-10: mirror the internal resolver's lower-bound guard.
            if (orderedIndex < 0 || orderedIndex >= filter.rules.Count) return false;

            rule = filter.rules[orderedIndex];
            if (rule == null) return false;

            return bypass || rule.emphasized;
        }
        catch { return false; }
    }
}
