using UnityEngine;
using MelonLoader;
using Il2Cpp;
// using Il2CppDMM;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppLE.UI;
using Il2CppLE.Telemetry;
using Il2CppItemFiltering;
using Il2CppLidgren.Network;
using Il2CppSystem.Net;
using Il2CppSteamworks;
using HarmonyLib;
using System.Linq;
using Mod.Cheats;

using HarmonyPatch = HarmonyLib.HarmonyPatch;
using static MelonLoader.LoaderConfig;
using static Il2Cpp.GroundItemManager;
using Mod.Utils;
using Mod.Game;


namespace Mod.Cheats.Patches
{
    public partial class MapIconPatch
    {
        internal partial class HarmonyPatches
        {
            #region user action monitoring patches
            // TODO: These patches need correct type names
            /*
			[HarmonyPatch(typeof(Il2CppLE.UI.InventoryPanel), "Open")]
			public class InventoryPanel_Open
			{
				private static void Postfix(Il2CppLE.UI.InventoryPanel __instance)
				{
					try
					{
						MelonLogger.Msg("[AntiIdle] Inventory panel opened - this might trigger anti-idle reset");
					}
					catch (Exception e)
					{
						MelonLogger.Error($"[Mod] InventoryPanel.Open Postfix error: {e.Message}");
					}
				}
			}

			[HarmonyPatch(typeof(Il2CppLE.UI.InventoryPanel), "Close")]
			public class InventoryPanel_Close
			{
				private static void Postfix(Il2CppLE.UI.InventoryPanel __instance)
				{
					try
					{
						MelonLogger.Msg("[AntiIdle] Inventory panel closed - this might trigger anti-idle reset");
					}
					catch (Exception e)
					{
						MelonLogger.Error($"[Mod] InventoryPanel.Close Postfix error: {e.Message}");
					}
				}
			}

			[HarmonyPatch(typeof(Il2CppLE.UI.CharacterPanel), "Open")]
			public class CharacterPanel_Open
			{
				private static void Postfix(Il2CppLE.UI.CharacterPanel __instance)
				{
					try
					{
						MelonLogger.Msg("[AntiIdle] Character panel opened - this might trigger anti-idle reset");
					}
					catch (Exception e)
					{
						MelonLogger.Error($"[Mod] CharacterPanel.Open Postfix error: {e.Message}");
					}
				}
			}

			[HarmonyPatch(typeof(Il2CppLE.UI.CharacterPanel), "Close")]
			public class CharacterPanel_Close
			{
				private static void Postfix(Il2CppLE.UI.CharacterPanel __instance)
				{
					try
					{
						MelonLogger.Msg("[AntiIdle] Character panel closed - this might trigger anti-idle reset");
					}
					catch (Exception e)
					{
						MelonLogger.Error($"[Mod] CharacterPanel.Close Postfix error: {e.Message}");
					}
				}
			}

			// Monitor player movement/input
			[HarmonyPatch(typeof(Il2CppLE.Player.LocalPlayer), "Update")]
			public class LocalPlayer_Update
			{
				private static void Postfix(Il2CppLE.Player.LocalPlayer __instance)
				{
					try
					{
						// Only log occasionally to avoid spam
						if (Time.frameCount % 300 == 0) // Every 300 frames (about 5 seconds at 60fps)
						{
							MelonLogger.Msg("[AntiIdle] Player update tick - checking for movement/input");
						}
					}
					catch (Exception e)
					{
						MelonLogger.Error($"[Mod] LocalPlayer.Update Postfix error: {e.Message}");
					}
				}
			}
			*/
            #endregion
        }
    }
}
