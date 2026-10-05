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

// Single source of truth for identity/version — v1 shipped with disagreeing
// version strings (Core header said 1.2.0 while the assembly said 1.5.0).
//
// ⚠ Name is PUBLIC ABI: Fallen Star's Improved Tooltips detects this mod by
// scanning RegisteredMelons for the exact string "Terrible Tooltips".
// Changing it breaks the ecosystem treaty. (See ARCHAEOLOGY.md.)
internal static class BuildInfo
{
    public const string Name         = "Terrible Tooltips";   // MelonInfo name — FROZEN (Fallen Star ABI)
    public const string OfficialName = "MedicK's Terrible Tooltips";
    public const string Version      = "3.1.0";
    public const string Author       = "medick";
}
