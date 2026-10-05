using MelonLoader;

namespace Mod.Cheats.TerribleTooltips;

// Lifecycle hooks for the integrated MedicK's Terrible Tooltips code.
internal static class TerribleTooltipsRuntime
{
    public const string OfficialName = "MedicK's Terrible Tooltips";

    public static void Initialize()
    {
        Prefs.Init();
    }

    public static void OnUpdate()
    {
        if (!Prefs.Enabled) return;
        GroundLabels.OnUpdate();
        FilterRuleTooltip.MonitorUpdate();
    }

    public static void OnLateUpdate()
    {
        if (!Prefs.Enabled) return;
        TooltipRecolor.OnLateUpdate();
        UnitBorder.OnLateUpdate();
    }

    public static void Save()
    {
        Prefs.Save();
    }
}