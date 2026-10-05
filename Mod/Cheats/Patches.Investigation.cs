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
            #region investigation hooks
            //[HarmonyPatch(typeof(DMMapIcon), "UpdateIcons")]
            //public class DMMapIconHooks
            //{
            //	//private static bool isFriendlyDotFound = false;
            //	//private static Image? friendlyDotImage = null;
            //	//private static Sprite? friendlyDotSprite = null;
            //
            //	//public static Image? FriendlyDotImage => friendlyDotImage;
            //	//public static Sprite? FriendlyDotSprite => friendlyDotSprite;
            //	private static void Postfix(DMMapIcon __instance)
            //	{
            //		//if (isFriendlyDotFound) return;
            //
            //		if (__instance != null)
            //		{
            //			//MelonLogger.Msg($"[Mod] DMMapIcon instance: {__instance.name}");
            //			//GameObject? gObject = __instance.gameObject;
            //			//Image? imageComponent = gObject.GetComponent<Image>();
            //			//Sprite? spriteComponent = gObject.GetComponent<Sprite>();
            //			//if (spriteComponent == null && imageComponent != null)
            //			//{
            //			//	MelonLogger.Msg("[Mod] DMMapIcon sprite is null. Trying to find in children.");
            //			//	spriteComponent = imageComponent.GetComponent<Sprite>();
            //			//}
            //			//if (imageComponent != null && spriteComponent != null && spriteComponent.name == "friendly-dot")
            //			//{
            //			//	friendlyDotImage = imageComponent;
            //			//	friendlyDotSprite = spriteComponent;
            //			//	isFriendlyDotFound = true;
            //
            //			//	MelonLogger.Msg($"[Mod] Found 'friendly-dot' with Image component. Storing reference.");
            //			//}
            //		}
            //		else
            //		{
            //			MelonLogger.Msg("[Mod] DMMapIcon.UpdateIcons instance is null.");
            //		}
            //	}
            //}

            //[HarmonyPatch(typeof(DMMapWorldIcon), "SetIcon")]
            //public class DMMapWorldIconHooks
            //{
            //	//private static void Prefix(DMMapWorldIcon __instance)
            //	//{
            //	//	if (__instance != null)
            //	//	{
            //	//		//MelonLogger.Msg($"[Mod] DMMapIconManager.SetIcon Prefix instance: {__instance.name}");
            //	//		MelonLogger.Msg($"[Mod] DMMapWorldIcon.SetIcon Prefix currentIcon: {__instance.currentIcon}");
            //	//		MelonLogger.Msg($"[Mod] DMMapWorldIcon.SetIcon Prefix IconType: {__instance.icon}");
            //	//	}
            //	//}
            //	private static void Postfix(DMMapWorldIcon __instance)
            //	{
            //		if (__instance != null)
            //		{
            //			//MelonLogger.Msg($"[Mod] DMMapIconManager.SetIcon Postfix instance: {__instance.name}");
            //
            //			//MelonLogger.Msg($"[Mod] DMMapWorldIcon.SetIcon Postfix currentIcon: {__instance.currentIcon}");
            //			//MelonLogger.Msg($"[Mod] DMMapWorldIcon.SetIcon Postfix IconType: {__instance.icon}");
            //		}
            //	}
            //}

            //[HarmonyPatch(typeof(DMMapIconManager), "Start")]
            //public class DMMapIconManagerHooks
            //{
            //	// the flow seems to be start from DMMapIconManager.Start -> BaseDMMapIcon.initialise to create minion icons on map
            //	private static void Prefix(DMMapIconManager __instance)
            //	{
            //		if (__instance != null)
            //		{
            //			//MelonLogger.Msg($"[Mod] DMMapIconManager.Start Prefix instance: {__instance.name}");
            //
            //			//MelonLogger.Msg($"[Mod] DMMapIconManager.Start Prefix currentIcon: {__instance.currentIcon}");
            //			//MelonLogger.Msg($"[Mod] DMMapIconManager.Start Prefix IconType: {__instance.icon}");
            //		}
            //	}
            //	private static void Postfix(DMMapIconManager __instance)
            //	{
            //		if (__instance != null)
            //		{
            //			//MelonLogger.Msg($"[Mod] DMMapIconManager.Start Postfix instance: {__instance.name}");
            //
            //			//MelonLogger.Msg($"[Mod] DMMapWorldIcon.Start Postfix currentIcon: {__instance.currentIcon}");
            //			//MelonLogger.Msg($"[Mod] DMMapWorldIcon.Start Postfix IconType: {__instance.icon}");
            //		}
            //	}
            //}

            //[HarmonyPatch(typeof(BaseDMMapIcon), nameof(BaseDMMapIcon.initialise))]
            //[HarmonyPostfix]
            //private static void initialisePostfix(BaseDMMapIcon __instance)
            //{
            //	if (__instance == null) return;
            //
            //	//MelonLogger.Msg($"[Mod] BaseDMMapIcon.initialise Postfix: {__instance.name}");
            //}

            //[HarmonyPatch(typeof(BaseDMMapIcon), "initialise")]
            //public class BaseDMMapIconInitHooks
            //{
            //	private static void Prefix(BaseDMMapIcon __instance)
            //	{
            //		if (__instance != null)
            //		{
            //			MelonLogger.Msg($"[Mod] BaseDMMapIcon.initialise Prefix instance: {__instance.name}");
            //
            //			//MelonLogger.Msg($"[Mod] DMMapIconManager.initialise Prefix currentIcon: {__instance.currentIcon}");
            //			//MelonLogger.Msg($"[Mod] DMMapIconManager.initialise Prefix IconType: {__instance.icon}");
            //		}
            //	}
            //	private static void Postfix(BaseDMMapIcon __instance)
            //	{
            //		if (__instance != null)
            //		{
            //			MelonLogger.Msg($"[Mod] BaseDMMapIcon.initialise Postfix instance: {__instance.name}");
            //
            //			//MelonLogger.Msg($"[Mod] DMMapWorldIcon.initialise Postfix currentIcon: {__instance.currentIcon}");
            //			//MelonLogger.Msg($"[Mod] DMMapWorldIcon.initialise Postfix IconType: {__instance.icon}");
            //		}
            //	}
            //}

            // this one fires every frame, we should avoid hooking into it unless necessary
            //[HarmonyPatch(typeof(BaseDMMapIcon), nameof(BaseDMMapIcon.UpdateIconSprite))]
            //[HarmonyPostfix]
            //private static void UpdateIconSpritePostfix(BaseDMMapIcon __instance)
            //{
            //	//if (__instance == null) return;
            //
            //	//MelonLogger.Msg($"[Mod] BaseDMMapIcon.UpdateIconSprite: {__instance}");
            //}

            // this one has no names we can grab, even from the GO. unsure how useful it will be
            //[HarmonyPatch(typeof(BaseDMMapIcon), nameof(BaseDMMapIcon.UpdateIcons))]
            //[HarmonyPostfix]
            //private static void UpdateIconsPostfix(BaseDMMapIcon __instance)
            //{
            //	//if (__instance == null) return;
            //
            //	//MelonLogger.Msg($"[Mod] BaseDMMapIcon.UpdateIcons: {__instance.name}");
            //}
            #endregion
        }
    }
}
