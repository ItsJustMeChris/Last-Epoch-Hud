using Il2CppLE.Networking;
using MelonLoader;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Mod.Game
{
    internal class ObjectManager
    {
        private static GameObject? localPlayer;

        public static void AttemptToFindPlayer()
        {
            localPlayer = GameObject.Find("MainPlayer(Clone)");
        }

        public static void OnSceneLoaded()
        {
            AttemptToFindPlayer();
        }

        public static bool HasPlayer()
        {
            if (localPlayer == null)
            {
                AttemptToFindPlayer();
            }

            return localPlayer != null;
        }

        public static GameObject? GetLocalPlayer()
        {
            if (localPlayer == null)
            {
                AttemptToFindPlayer();
            }

            return localPlayer;
        }
    }
}
