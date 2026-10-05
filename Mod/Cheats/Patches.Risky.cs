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
            #region risky game patches
            [HarmonyPatch(typeof(GroundItemManager), "dropItemForPlayer", new Type[] { typeof(Actor), typeof(ItemData), typeof(Vector3), typeof(bool) })]
            public class GroundItemManager_vacuumNearbyStackableItems
            {
	public static void Postfix(ref GroundItemManager __instance, ref int __state, ref Actor player, ref ItemData itemData, ref Vector3 location, ref bool playDropSound)
	{
                    _ = __state;
                    Log.InfoThrottled(LogSource.Hooks, "GroundItemManager.dropItemForPlayer", "GroundItemManager.dropItemForPlayer hooked", TimeSpan.FromSeconds(15));
		if (ItemList.isCraftingItem(itemData.itemType) && Settings.pickupCrafting)
		{
			__instance.TryGetGroundItemList(player, out GroundItemList groundItemList);
			__instance.vacuumNearbyStackableItems(player, groundItemList, location, StackableItemFlags.AllCrafting);
		}
	}
            }
            #endregion
        }
    }
}
