// ================================================================
//  Colors.cs — the WoW-retina colour language. FROZEN BRAND.
//
//  "the colors come from world of warcraft item colorization...
//   sense of comfort and familiarity and thats what i want to bring
//   to players who use this mod" — medick
//
//  Tier colours (T1 gray → T7+ Mythic Pink):
//    T1  #DADADA   T2  #E1E1E1   T3  #16FF0E (green)
//    T4  #77ACFF (blue)   T5  #A807FF (purple)
//    T6  #FA9E3D (legendary gold)   T7+ #FF44FF (MYTHIC — D4 Ancestral pink,
//    medick's signature: "I am a diablo 4 guy")
//
//  Grades:  F <50 · C <70 · B <80 · A <95 · S ≥95  (roll × 100)
//  Gray F is a feature: "telling the player ya that roll sucks bro"
//  greater-affix tint, ruled 2026-09-11, deliberately outside the palette:
//    #C990FF
//
//  These hex values are canonical — marketing copy drifts get corrected
//  TO this file, never the reverse. Thresholds inherited from KG.
//  (v1's unused RollColor() band function was removed in the v2 audit.)
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

public static class Colors
{
    public const string GreaterAffixTintDefault = "#C990FF";

    // ── Tier colour — keyed by crafting tier number ──────────────────
    public static string TierColor(int tier)
        => tier switch
        {
            1 => "#DADADA",  // T1
            2 => "#E1E1E1",  // T2
            3 => "#16FF0E",  // T3 green
            4 => "#77ACFF",  // T4 blue
            5 => "#A807FF",  // T5 purple / epic
            6 => "#FA9E3D",  // T6 legendary gold
            _ => "#FF44FF",  // T7+ MYTHIC
        };

    // ── Grade letter ─────────────────────────────────────────────────
    public static string GradeLetter(double roll)
        => roll switch
        {
            < 50 => "F",
            < 70 => "C",
            < 80 => "B",
            < 95 => "A",
            _    => "S",
        };

    // ── Grade letter colour ──────────────────────────────────────────
    public static string GradeLetterColor(double roll)
        => roll switch
        {
            < 50 => "#DADADA",  // F — gray
            < 70 => "#77ACFF",  // C — blue
            < 80 => "#A807FF",  // B — purple
            < 95 => "#FA9E3D",  // A — gold
            _    => "#FF44FF",  // S — MYTHIC
        };

}
