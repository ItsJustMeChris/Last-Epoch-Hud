using Il2Cpp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Mod.Cheats
{
    internal class MapHack
    {
        public static void OnSceneWasLoaded()
        {
            var dmmapCanvas = GameObject.Find("DMMap Canvas");
            if (dmmapCanvas == null) return;

            var minimapFogOfWar = dmmapCanvas.GetComponent<MinimapFogOfWar>();
            if (minimapFogOfWar == null) return;

            if (Settings.mapHack)
            {
                if (minimapFogOfWar.discoveryDistance != 5000.0f)
                {
                    minimapFogOfWar.discoveryDistance = 5000.0f;
                }
            }
            else
            {
                minimapFogOfWar.discoveryDistance = 20.0f;
            }
        }
    }
}
