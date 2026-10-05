// ================================================================
//  AffixInjector.cs — injects tier+grade brackets into affix tooltip
//  strings so TooltipRecolor can read them and apply WoW-style colours.
//
//  Standalone replacement for the equivalent patches in
//  kg_LastEpoch_Improvements (credit: KG / war3i4i) — Terrible Tooltips
//  does NOT require KG to be installed. Bracket format is intentionally
//  identical to KG's Letter_Style output:
//
//    Tiered affix:   [<color=#A807FF>5</color><color=#FA9E3D>A</color>] text
//    Untiered affix: [<color=#FA9E3D>A</color>] text
//
//  Three hooks cover every stat line:
//    • AffixFormatter          — normal craftable affixes (prefix/suffix)
//    • UniqueBasicModFormatter — unique / legendary fixed mods
//    • ImplicitFormatter       — implicit stats on the item base
//
//  v2.0.0 — THE LEGENDARY GRADING FIX (zoundb's Nexus report):
//  v1 (and KG before it) RECONSTRUCTED the unique-mod roll from display
//  values: (modifierValue - min) / (max - min). On legendary-converted
//  uniques the LP roll multiplier and display semantics break that
//  arithmetic — every stat graded C, even max rolls. The game stores the
//  actual roll byte in ItemDataUnpacked.uniqueRolls, indexed by
//  UniqueItemMod.rollID (a field neither KG nor v1 ever read; discovered
//  via research/ApiProbe). v2 reads the stored truth.
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

public static class AffixInjector
{

    private static void Trace(string hook, string result)
    {
        try
        {
            if (!Prefs.DebugLog.Value) return;
            string text = (result ?? "")
                .Replace("\r\n", " | ")
                .Replace("\n", " | ")
                .Replace("\r", " | ");
            if (text.Length > 90) text = text.Substring(0, 90);
            MelonLogger.Msg($"[trace] {hook}: {text}");
        }
        catch { }
    }

    private static string InjectBracket(string affixStr, float rollFloat, int tier)
    {
        double roll        = Math.Round(rollFloat * 100.0, 1);
        string gradeColor  = Colors.GradeLetterColor(roll);
        string gradeLetter = Colors.GradeLetter(roll);

        string bracket = tier > 0
            ? $"[<color={Colors.TierColor(tier)}>{tier}</color><color={gradeColor}>{gradeLetter}</color>] "
            : $"[<color={gradeColor}>{gradeLetter}</color>] ";

        // Prepending keeps the bracket on the first line of multi-line
        // strings. (v1 had a dead Insert(lastNewLine, "") branch here —
        // a no-op since forever; the fleet finally retired it.)
        return bracket + affixStr;
    }

    // ── Patch: normal craftable affixes (prefix / suffix) ────────────
    // affix.getRollFloat() is the game's own 0–1 roll — correct since v1.

    [HarmonyPatch(typeof(TooltipItemManager), nameof(TooltipItemManager.AffixFormatter))]
    internal static class Patch_AffixFormatter
    {
        // Codex trace finding 2026-09-10: multi-stat calls pass affix=null, so resolve by SP.
        private static void Postfix(ItemDataUnpacked item, ItemAffix affix,
                                    SP modProperty, int implicitIndex, int uniqueModIndex,
                                    ref string __result)
        {
            try
            {
                if (!Prefs.Enabled) return;
                if (item == null)
                {
                    LogItemNullOnce();
                    return;
                }

                ItemAffix resolved = affix;
                if (resolved == null && implicitIndex < 0 && uniqueModIndex < 0)
                    resolved = ResolveByProperty(item, modProperty);
                if (resolved == null) return;

                __result = InjectBracket(__result, resolved.getRollFloat(), resolved.DisplayTier);
                TooltipRecolor.MarkDirty();
            }
            catch (Exception ex) { Dbg.Log("multi-stat affix lookup failed: " + ex.Message); }
            finally
            {
                Trace($"AffixFormatter item={(item == null ? "null" : "set")} " +
                      $"affix={(affix == null ? "null" : "set")}", __result);
            }
        }

        private static ItemAffix ResolveByProperty(ItemDataUnpacked item, SP modProperty)
        {
            ItemAffix match = null;
            int matches = 0;
            foreach (ItemAffix ia in item.affixes)
            {
                if (ia == null) continue;
                AffixList.Affix def = AffixList.get().GetAffix(ia.affixId);
                if (def != null && def.HasProperty(modProperty))
                {
                    match = ia;
                    matches++;
                    if (matches > 1) break;
                }
            }

            if (matches == 1) return match;
            if (matches == 0)
            {
                if (!s_noMatchLogged)
                {
                    s_noMatchLogged = true;
                    Dbg.Log($"multi-stat affix: no match for property {modProperty}");
                }
            }
            else if (!s_ambiguousMatchLogged)
            {
                s_ambiguousMatchLogged = true;
                Dbg.Log($"multi-stat affix: ambiguous match for property {modProperty}");
            }
            return null;
        }

        private static void LogItemNullOnce()
        {
            if (s_itemNullLogged) return;
            s_itemNullLogged = true;
            Dbg.Log("AffixFormatter: item=null");
        }

        private static bool s_itemNullLogged;
        private static bool s_noMatchLogged;
        private static bool s_ambiguousMatchLogged;
    }

    // ── Patch: unique / legendary fixed mods ─────────────────────────
    // THE FIX lives here. Roll source priority:
    //   1. Fixed stat (can't roll / min == max)      → 1.0  (perfect by definition)
    //   2. item.getUniqueRoll(mod.rollID) / 255      → the stored truth,
    //      exact on uniques AND legendaries (LP-multiplier-immune)
    //   3. KG's display-value reconstruction          → logged fallback only

    [HarmonyPatch(typeof(TooltipItemManager), nameof(TooltipItemManager.UniqueBasicModFormatter))]
    internal static class Patch_UniqueFormatter
    {
        private static void Postfix(ItemDataUnpacked item, ref string __result,
                                    int uniqueModIndex, float modifierValue)
        {
            try
            {
                if (!Prefs.Enabled) return;
                if (item == null) return;
                if (item.uniqueID >= UniqueList.instance.uniques.Count) return;   // v1 had > (off-by-one)
                if (uniqueModIndex < 0) return;
                if (UniqueList.instance.uniques[item.uniqueID] is not { } uniqueEntry) return;
                if (uniqueModIndex >= uniqueEntry.mods.Count) return;

                UniqueItemMod uniqueMod = uniqueEntry.mods[uniqueModIndex];

                float roll;
                if (!uniqueMod.canRoll || uniqueMod.value == uniqueMod.maxValue)
                {
                    roll = 1f;   // fixed stat — always shown as perfect (KG semantics, kept)
                }
                else
                {
                    // uniqueRolls fetched only for the bounds check; the read
                    // goes through the game's own accessor — the exact path
                    // the ApiProbe research validated.
                    var rolls = item.uniqueRolls;
                    byte rollId = uniqueMod.rollID;
                    if (rolls != null && rollId < rolls.Length)
                    {
                        roll = item.getUniqueRoll(rollId) / 255f;
                    }
                    else
                    {
                        // Fallback: KG's reconstruction. Known-wrong on
                        // legendaries — log once so a regression is visible.
                        float min = uniqueMod.value;
                        float max = uniqueMod.maxValue;
                        roll = (min == max || modifierValue > max)
                            ? 1f
                            : (modifierValue - min) / (max - min);
                        WarnFallbackOnce();
                    }
                }

                __result = InjectBracket(__result, roll, tier: 0);
                TooltipRecolor.MarkDirty();
            }
            catch { }
            finally { Trace("UniqueBasicModFormatter", __result); }
        }

        private static bool s_fallbackWarned;
        private static void WarnFallbackOnce()
        {
            if (s_fallbackWarned) return;
            s_fallbackWarned = true;
            MelonLogger.Warning(
                "unique-roll byte unavailable — using legacy reconstruction (grades may be wrong on legendaries)");
        }
    }

    // ── Patch: implicit stats ─────────────────────────────────────────
    // getImplictRollFloat [sic — the game's own typo] — correct since v1.

    [HarmonyPatch(typeof(TooltipItemManager), nameof(TooltipItemManager.ImplicitFormatter))]
    internal static class Patch_ImplicitFormatter
    {
        private static void Postfix(ItemDataUnpacked item, int implicitNumber,
                                    ref string __result)
        {
            try
            {
                if (!Prefs.Enabled) return;
                if (item == null) return;
                float roll = item.getImplictRollFloat((byte)implicitNumber);
                __result = InjectBracket(__result, roll, tier: 0);
                TooltipRecolor.MarkDirty();
            }
            catch { }
            finally { Trace("ImplicitFormatter", __result); }
        }
    }
}
