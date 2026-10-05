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
    public partial class MapIconPatch : MonoBehaviour
    {
        // private bool isInitialized = false;
        private static readonly string Icon_Base64 = SpriteBases.npcMapIcon;

        [HarmonyPatch(typeof(ActorSync), nameof(ActorSync.ReceiveInitDisplayInformation))]
        private static class ActorSync_MessageSyncRarit
        {
            private static void Postfix(ActorSync __instance, byte rarity)
            {
                try
                {

                }
                catch (Exception)
                {

                }
            }
        }

        [HarmonyPatch]
        internal partial class HarmonyPatches
        {
            //todo: verify that the patches are working
            //todo: verify there arent any new methods that should be patched
            //todo: verify that we patched out the unreal crash handler

        }
    }
}
