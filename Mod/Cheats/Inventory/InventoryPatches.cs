using HarmonyLib;
using Il2Cpp;

namespace Mod.Cheats.Inventory;

[HarmonyPatch(typeof(EnableWovenEchoesTabIfRelevant), nameof(EnableWovenEchoesTabIfRelevant.Awake))]
internal static class InventoryPanelAwakePatch
{
	[HarmonyPostfix]
	private static void Postfix(EnableWovenEchoesTabIfRelevant __instance)
	{
		InventoryUi.Inject(__instance);
	}
}
