#nullable disable
namespace Mod.Cheats.CooldownTracker
{
    // Shared GUI state between the mod loop, settings panel, picker and renderer.
    internal static class UiState
    {
        public static bool ShowSettings;
        public static int  PickerSlot = -1;     // -1 = picker closed
        public static bool TextFieldActive;     // a label field has keyboard focus
        public static bool MoveIcons;           // drag-to-position mode for the icon cluster
    }
}
