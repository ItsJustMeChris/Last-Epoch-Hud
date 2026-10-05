#nullable disable
using MelonLoader;

namespace Mod.Cheats.Inventory
{
    // Persisted settings — NEW in v2 (v1 had no preferences at all).
    // Delivers the April 2026 dropped wish: "allow the users to select what
    // they want showing".
    internal static class Prefs
    {
        public static MelonPreferences_Category Category;
        public static MelonPreferences_Entry<bool> ShowStash;
        public static MelonPreferences_Entry<bool> ShowStashAll;
        public static MelonPreferences_Entry<bool> ShowVendor;
        public static MelonPreferences_Entry<bool> ShowTeleport;
        public static MelonPreferences_Entry<bool> DebugLog;

        public static void Init()
        {
            Category     = MelonPreferences.CreateCategory("medick_Terrible_Inventory", "Terrible Inventory");
            ShowStash    = Category.CreateEntry("ShowStash",    true,  "Show the STASH button");
            ShowStashAll = Category.CreateEntry("ShowStashAll", true,  "Show the STASH ALL button");
            ShowVendor   = Category.CreateEntry("ShowVendor",   false, "Show the VENDOR button");
            ShowTeleport = Category.CreateEntry("ShowTeleport", true,  "Show the Quick Teleport menu");
            DebugLog     = Category.CreateEntry("DebugLog",     false, "Verbose log output");
        }

        public static void Save()
        {
            try { Category.SaveToFile(false); Dbg.Log("prefs saved"); } catch { }
        }
    }

    internal static class Dbg
    {
        public static void Log(string msg)
        {
            if (Prefs.DebugLog != null && Prefs.DebugLog.Value)
                MelonLogger.Msg("[debug] " + msg);
        }
    }
}
