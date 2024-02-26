using Il2Cpp;
using Mod.Game;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Color = UnityEngine.Color;

namespace Mod.Cheats.ESP
{
    internal class GoldPiles
    {
        public static void GatherGoldPiles()
        {
            if (!Settings.DrawGoldPiles()) return;
            if (GroundGoldVisuals.all == null) return;

            foreach (var item in GroundGoldVisuals.all)
            {
                if (!item.gameObject.activeInHierarchy) return;
                if (Vector3.Distance(ObjectManager.GetLocalPlayer().transform.position, item.transform.position) > Settings.drawDistance) continue;

                ESP.AddLine(ObjectManager.GetLocalPlayer().transform.position, item.transform.position, Color.white);
                ESP.AddString(item.goldValue.ToString() + " Gold", item.transform.position, Color.white);
            }
        }

        public static void OnUpdate()
        {
            if (ObjectManager.HasPlayer())
            {
                GatherGoldPiles();
            }
        }
    }
}
