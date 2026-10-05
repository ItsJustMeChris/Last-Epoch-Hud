#nullable disable
using HarmonyLib;
using Il2Cpp;

namespace Mod.Cheats.CooldownTracker
{
    // Harmony patches live nested inside the MelonMod class — MelonLoader's
    // auto-discovery reliably finds them there (top-level patch classes have
    // silently failed to register in other LE mods).
    //
    // AbilityBarIcon is the game's real action-bar slot component
    // (fields: icon, cooldownBar, cooldownBarActive, abilityNumber, player).
    // Note: no patch on Start — it was removed from the game in Season 4 and
    // patching it breaks loading. Awake and OnPlayerChanged cover registration
    // when the local-player owner is assigned before or after construction.
    internal static partial class CooldownTracker
    {
        [HarmonyPatch(typeof(AbilityBarIcon), "Awake")]
        private static class Patch_AbilityBarAwake
        {
            [HarmonyPostfix]
            public static void Postfix(AbilityBarIcon __instance) =>
                SlotRegistry.Register(__instance);
        }

        [HarmonyPatch(typeof(AbilityBarIcon), "OnPlayerChanged")]
        private static class Patch_AbilityBarPlayerChanged
        {
            [HarmonyPostfix]
            public static void Postfix(AbilityBarIcon __instance) =>
                SlotRegistry.Register(__instance);
        }

        [HarmonyPatch(typeof(AbilityBarIcon), "activateCooldownBar")]
        private static class Patch_CooldownActivate
        {
            [HarmonyPostfix]
            public static void Postfix(AbilityBarIcon __instance) =>
                SlotRegistry.SetCooldown(__instance, true);
        }

        [HarmonyPatch(typeof(AbilityBarIcon), "deactivateCooldownBar")]
        private static class Patch_CooldownDeactivate
        {
            [HarmonyPostfix]
            public static void Postfix(AbilityBarIcon __instance) =>
                SlotRegistry.SetCooldown(__instance, false);
        }
    }
}
