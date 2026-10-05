#nullable disable
using MelonLoader;
using Mod;
using UnityEngine;

namespace Mod.Cheats.CooldownTracker;

internal static partial class CooldownTracker
{
    static float _tick;
    static float _iconsVisibleAfter = float.PositiveInfinity;
    const float TickRate = 0.05f;

    public static bool ShouldBlockGameInput =>
        Menu.IsCooldownsTabActive &&
        (Prefs.LockInput.Value || UiState.TextFieldActive || UiState.MoveIcons);
    public static float SettingsContentHeight => SettingsPanel.ContentHeight;

    public static void OnSceneInitialized()
    {
        _iconsVisibleAfter = Time.time + 2f;
        SlotRegistry.OnSceneInitialized();
    }

    public static void Initialize()
    {
        Prefs.Init();
        MelonLogger.Msg($"{BuildInfo.OfficialName} v{BuildInfo.Version} integrated into LEHud");
    }

    public static void Update()
    {
        InputTracker.Update();
        UiState.ShowSettings = Menu.IsCooldownsTabActive;
        if (!UiState.ShowSettings) UiState.TextFieldActive = false;

        _tick += Time.deltaTime;
        if (_tick < TickRate) return;
        _tick = 0f;
        SlotRegistry.Tick(Time.time);
    }

    public static void DrawOverlay()
    {
        Theme.Ensure();
        if (Time.time < _iconsVisibleAfter) return;
        OverheadRenderer.Draw();
    }

    public static void DrawSettingsTab(Rect hostRect)
    {
        UiState.ShowSettings = true;
        GUI.BeginGroup(hostRect);
        ButtonPicker.PreInput();
        SettingsPanel.DrawInHudTab(new Rect(0f, 0f, hostRect.width, hostRect.height));
        if (UiState.PickerSlot >= 0)
        {
            int mode = ButtonLabels.GetModeIndex();
            if (mode == 0) UiState.PickerSlot = -1;
            else ButtonPicker.Draw(mode);
        }
        GUI.EndGroup();
    }

    public static void Save()
    {
        UiState.ShowSettings = false;
        UiState.TextFieldActive = false;
        UiState.MoveIcons = false;
        UiState.PickerSlot = -1;
        Prefs.Save();
    }
}
