using Il2Cpp;
using Il2CppItemFiltering;
using Mod.Game;
using System;
using System.Reflection;
using UnityEngine;

namespace Mod.Cheats.ESP
{
    internal class Items
    {
        private const int ItemInfoRefreshIntervalFrames = 300;
        private const int ItemInfoPruneIntervalFrames = 300;
        private const int MaxCachedItemInfos = 4096;

        private sealed class ItemDisplayInfo
        {
            public string? Rarity;
            public string Name = string.Empty;
            public int ResolvedAtFrame;
            public int LastSeenFrame;
        }

        private static readonly Dictionary<int, ItemDisplayInfo> s_itemInfoByInstanceId = new();
        private static readonly List<int> s_itemInfoPruneBuffer = new();
        private static int s_nextItemInfoPruneFrame;

        private static readonly string[] s_supportedRarities =
        {
            "Magic",
            "Common",
            "Unique",
            "Legendary",
            "Rare",
            "Set",
            "Exalted"
        };

        public static void GatherItems(GameObject player)
        {
            if (GroundItemVisuals.all == null) return;

            var playerPos = player.transform.position;
            float maxDistSq = Settings.drawDistance * Settings.drawDistance;
            int frame = Time.frameCount;
            if (frame >= s_nextItemInfoPruneFrame)
            {
                PruneItemInfoCache(frame);
                s_nextItemInfoPruneFrame = frame + ItemInfoPruneIntervalFrames;
            }

            foreach (var item in GroundItemVisuals.all._list)
            {
                if (item?.gameObject == null || !item.gameObject.activeInHierarchy) continue;

                var itemPos = item.transform.position;
                var delta = itemPos - playerPos;
                if (delta.sqrMagnitude > maxDistSq) continue;

                if (Settings.useLootFilter)
                {
                    Rule.RuleOutcome filter = ItemFiltering.Match(item.itemData, null, null, 0);
                    if (filter == Rule.RuleOutcome.HIDE) continue;
                }

                var itemInfo = GetItemDisplayInfo(item, frame);
                var rarity = itemInfo.Rarity;

                if (string.IsNullOrEmpty(rarity) || !Settings.ShouldDrawItemRarity(rarity))
                {
                    continue;
                }

                var color = Drawing.ItemRarityToColor(rarity);

                ESP.AddLine(playerPos, itemPos, color);
                ESP.AddString(itemInfo.Name, itemPos, color);
            }
        }

        public static void OnSceneChanged()
        {
            s_itemInfoByInstanceId.Clear();
            s_itemInfoPruneBuffer.Clear();
            s_nextItemInfoPruneFrame = 0;
        }

        private static ItemDisplayInfo GetItemDisplayInfo(GroundItemVisuals item, int frame)
        {
            int instanceId = item.GetInstanceID();
            if (!s_itemInfoByInstanceId.TryGetValue(instanceId, out var info)
                || frame - info.ResolvedAtFrame >= ItemInfoRefreshIntervalFrames)
            {
                info = new ItemDisplayInfo
                {
                    Rarity = ResolveItemRarity(item),
                    Name = item.itemData?.FullName ?? item.name,
                    ResolvedAtFrame = frame,
                    LastSeenFrame = frame
                };

                if (s_itemInfoByInstanceId.Count >= MaxCachedItemInfos)
                    s_itemInfoByInstanceId.Clear();
                s_itemInfoByInstanceId[instanceId] = info;
            }
            else
            {
                info.LastSeenFrame = frame;
            }

            return info;
        }

        private static void PruneItemInfoCache(int frame)
        {
            if (s_itemInfoByInstanceId.Count == 0)
                return;

            s_itemInfoPruneBuffer.Clear();
            foreach (var entry in s_itemInfoByInstanceId)
            {
                if (frame - entry.Value.LastSeenFrame >= ItemInfoPruneIntervalFrames)
                    s_itemInfoPruneBuffer.Add(entry.Key);
            }

            for (int i = 0; i < s_itemInfoPruneBuffer.Count; i++)
                s_itemInfoByInstanceId.Remove(s_itemInfoPruneBuffer[i]);
        }

        private static string? ResolveItemRarity(GroundItemVisuals item)
        {
            if (item == null) return null;

            string? itemDataRarity = ResolveItemDataRarity(item.itemData);
            if (!string.IsNullOrEmpty(itemDataRarity))
            {
                return itemDataRarity;
            }

            // New LE versions populate the V2 rarity visuals; keep legacy as a reflection fallback
            // because the legacy member is no longer present in current generated assemblies.
            object? legacyVisuals = GetLegacyRarityVisuals(item);
            string? v2Name = NormalizeRarity(item.groundItemRarityVisualsV2?.name);
            if (!string.IsNullOrEmpty(v2Name))
            {
                return v2Name;
            }

            string? legacyName = NormalizeRarity(GetVisualMember<string>(legacyVisuals, "name"));
            if (!string.IsNullOrEmpty(legacyName))
            {
                return legacyName;
            }

            Transform? rarityRoot = item.groundItemRarityVisualsV2?.transform
                ?? GetVisualMember<Transform>(legacyVisuals, "transform");
            if (rarityRoot == null)
            {
                return null;
            }

            int childCount = rarityRoot.childCount;
            for (int i = 0; i < childCount; i++)
            {
                Transform child = rarityRoot.GetChild(i);
                if (child == null || child.gameObject == null || !child.gameObject.activeSelf)
                {
                    continue;
                }

                string? childRarity = NormalizeRarity(child.name);
                if (!string.IsNullOrEmpty(childRarity))
                {
                    return childRarity;
                }
            }

            return null;
        }

        public static string? GetRarityVisualName(GroundItemVisuals item)
        {
            if (item == null)
            {
                return null;
            }

            string? v2Name = item.groundItemRarityVisualsV2?.name;
            if (!string.IsNullOrWhiteSpace(v2Name))
            {
                return v2Name;
            }

            return GetVisualMember<string>(GetLegacyRarityVisuals(item), "name");
        }

        private static object? GetLegacyRarityVisuals(GroundItemVisuals item)
        {
            return GetVisualMember<object>(item, "groundItemRarityVisuals");
        }

        private static T? GetVisualMember<T>(object? instance, string memberName) where T : class
        {
            if (instance == null)
            {
                return null;
            }

            try
            {
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
                for (Type? type = instance.GetType(); type != null; type = type.BaseType)
                {
                    PropertyInfo? property = type.GetProperty(memberName, flags);
                    if (property != null)
                    {
                        return property.GetValue(instance) as T;
                    }

                    FieldInfo? field = type.GetField(memberName, flags);
                    if (field != null)
                    {
                        return field.GetValue(instance) as T;
                    }
                }
            }
            catch (Exception)
            {
                // Keep version-compatibility reflection non-fatal.
            }

            return null;
        }

        private static string? ResolveItemDataRarity(ItemDataUnpacked? itemData)
        {
            if (itemData == null)
            {
                return null;
            }

            // Preferred source in current LE builds.
            try
            {
                string? methodRarity = NormalizeRarity(itemData.GetDefaultRarityVisualRarity().ToString());
                if (!string.IsNullOrEmpty(methodRarity))
                {
                    return methodRarity;
                }
            }
            catch (Exception)
            {
                // IL2CPP binding can throw in some edge cases; continue with byte fallback.
            }

            return ResolveItemDataRarityFromByte(itemData.rarity);
        }

        private static string? ResolveItemDataRarityFromByte(byte rarityByte)
        {
            // Known values observed in-game after the LE update.
            return rarityByte switch
            {
                3 => "Rare",
                7 => "Unique",
                _ => null
            };
        }

        private static string? NormalizeRarity(string? rawName)
        {
            if (string.IsNullOrWhiteSpace(rawName))
            {
                return null;
            }

            for (int i = 0; i < s_supportedRarities.Length; i++)
            {
                string rarity = s_supportedRarities[i];
                if (rawName.IndexOf(rarity, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return rarity;
                }
            }

            return null;
        }

        public static void OnUpdate(GameObject player)
        {
            GatherItems(player);
        }
    }
}
