using Il2Cpp;
using Il2CppLE.Networking;
using Il2CppLE.Services.Models;
using MelonLoader;
using Mod.Game;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Il2CppRewired.Demos.GamepadTemplateUI.GamepadTemplateUI;

namespace Mod.Cheats
{
    internal class AutoPotion
    {
        // Debounce the potion usage
        private static DateTime lastUse = DateTime.MinValue;

        public static void UseHealthPotion()
        {
            if (DateTime.Now - lastUse < TimeSpan.FromSeconds(1)) return;

            lastUse = DateTime.Now;

            var localPlayer = ObjectManager.GetLocalPlayer();

            if (localPlayer == null) return;

            localPlayer.GetComponent<HealthPotion>().UsePotion();
        }

        public static void OnUpdate()
        {
            if (!ObjectManager.HasPlayer()) return;

            var localPlayer = ObjectManager.GetLocalPlayer();

            if (localPlayer.GetComponent<PlayerHealth>().getHealthPercent() * 100 <= Settings.autoHealthPotion)
            {
                UseHealthPotion();
            }
        }
    }
}
