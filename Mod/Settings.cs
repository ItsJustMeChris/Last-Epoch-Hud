using Il2Cpp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Mod
{
    internal class Settings
    {
        public static bool mapHack = true;
        public static float drawDistance = 100.0f;
        public static float autoHealthPotion = 50.0f;
        public static bool useLootFilter = true;

        public static Dictionary<string, bool> npcClassifications = new Dictionary<string, bool>
        {
            { "Normal", true },
            { "Magic", true },
            { "Rare", true },
            { "Boss", true }
        };

        public static Dictionary<string, bool> npcDrawings = new Dictionary<string, bool>
        {
            { "Good", true },
            { "Evil", true },
            { "Barrel", true },
            { "HostileNeutral", true },
            { "FriendlyNeutral", true },
            { "SummonedCorpse", true }
        };

        public static Dictionary<string, bool> itemDrawings = new Dictionary<string, bool>
        {
            { "Magic", true },
            { "Common", true },
            { "Unique", true },
            { "Rare", true },
            { "Set", true },
            { "Exalted", true },
            { "Gold Piles", true }
        };

        public static bool DrawGoldPiles()
        {
            return itemDrawings["Gold Piles"];
        }

        public static bool ShouldDrawItemRarity(string rarity)
        {
            foreach (KeyValuePair<string, bool> entry in itemDrawings)
            {
                if (rarity.Contains(entry.Key))
                {
                    return entry.Value;
                }
            }

            return false;
        }

        public static bool ShouldDrawNPCAlignment(string alignment)
        {
            return npcDrawings[alignment];
        }

        public static bool ShouldDrawNPCClassification(DisplayActorClass actorClass)
        {
            string classificationKey = "Normal";

            switch (actorClass)
            {
                case DisplayActorClass.Magic:
                    classificationKey = "Magic";
                    break;
                case DisplayActorClass.Rare:
                    classificationKey = "Rare";
                    break;
                case DisplayActorClass.Boss:
                    classificationKey = "Boss";
                    break;
            }

            return npcClassifications[classificationKey];
        }
    }
}
