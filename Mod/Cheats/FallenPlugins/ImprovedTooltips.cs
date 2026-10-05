#nullable disable
#nullable enable
using System.Collections;
using HarmonyLib;
using Il2Cpp;
using Il2CppItemFiltering;
using Il2CppTMPro;
using MelonLoader;

namespace Mod.Cheats.FallenPlugins;

internal static class ImprovedTooltips
{
    private const string TooltipMarker = "\u200B\u200B\u200B";
    private static MelonPreferences_Category? s_category;
    public static MelonPreferences_Entry<bool>? ShowFullItemName { get; private set; }
    public static MelonPreferences_Entry<bool>? ShowLegendaryPotential { get; private set; }
    public static MelonPreferences_Entry<bool>? CompareStashItems { get; private set; }
    public static MelonPreferences_Entry<bool>? UseTerribleTooltipsEntry { get; private set; }

    // false = Fallen Star's Improved Tooltips, true = MedicK's Terrible Tooltips.
    public static bool UseTerribleTooltips => UseTerribleTooltipsEntry?.Value ?? false;

    private static bool s_kgImprovementsLoaded;
    public static bool KgImprovementsLoaded => s_kgImprovementsLoaded;

    public static void Initialize()
    {
        if (s_category != null) return;

        s_kgImprovementsLoaded = MelonMod.RegisteredMelons.Any(m =>
            string.Equals(m.Info.Name, "kg_LastEpoch_Improvements", StringComparison.Ordinal));
        s_category = MelonPreferences.CreateCategory("ImprovedTooltips", "Improved Tooltips");
        s_category.SetFilePath("UserData/FallenImprovedTooltips.cfg");
        ShowFullItemName = s_category.CreateEntry("ShowFullItemName", true, "Use the complete item name on ground labels");
        ShowLegendaryPotential = s_category.CreateEntry("ShowLPOnGroundLabels", true, "Show LP or Weaver's Will on ground labels");
        CompareStashItems = s_category.CreateEntry("ShowLPComparison", true, "Compare unique items against stash copies");
        UseTerribleTooltipsEntry = s_category.CreateEntry("UseTerribleTooltips", false, "Use MedicK's Terrible Tooltips instead of Fallen's Improved Tooltips");
    }

    public static void Save()
    {
        try { s_category?.SaveToFile(false); } catch { }
    }

    [HarmonyPatch(typeof(TooltipItemManager), "CreateTooltipContent")]
    private static class TooltipContentPatch
    {
        [HarmonyPrefix]
        private static void Prefix(TooltipItemManager __instance)
        {
            if (UseTerribleTooltips) return;
            try
            {
                var item = __instance?.ActiveItem;
                if (item != null) AppendTooltipDetails(item);
            }
            catch (Exception e)
            {
                MelonLogger.Warning($"[LEHud] Improved Tooltips skipped an item: {e.Message}");
            }
        }
    }

    [HarmonyPatch(typeof(GroundItemLabel), "SetGroundTooltipText")]
    private static class GroundLabelPatch
    {
        [HarmonyPostfix]
        private static void Postfix(GroundItemLabel __instance)
        {
            if (UseTerribleTooltips) return;
            try { MelonCoroutines.Start(UpdateGroundLabel(__instance)); }
            catch (Exception e) { MelonLogger.Warning($"[LEHud] Ground-label enhancement failed to start: {e.Message}"); }
        }
    }

    private static void AppendTooltipDetails(ItemDataUnpacked item)
    {
        item.LoreText ??= string.Empty;
        int markerIndex = item.LoreText.IndexOf(TooltipMarker, StringComparison.Ordinal);
        if (markerIndex >= 0)
            item.LoreText = item.LoreText.Substring(0, markerIndex).TrimEnd('\n', '\r', ' ');

        string additions = string.Empty;
        bool hadLore = !string.IsNullOrEmpty(item.LoreText);
        void Append(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            additions += (additions.Length == 0 ? (hadLore ? "\n\n" : string.Empty) : "\n\n") + text;
        }

        Rule? matchedRule = FindMatchingRule(item);
        if (matchedRule != null && (ItemList.isEquipment(item.itemType) || ItemList.isIdol(item.itemType)))
        {
            string description = matchedRule.GetRuleDescription();
            if (!string.IsNullOrWhiteSpace(description))
                Append($"<color=#E0E0E0>Filter Rule:</color> {description}");
        }

        if (item.isUniqueSetOrLegendary())
            Append(BuildOwnershipText(item));

        if (additions.Length > 0)
            item.LoreText += TooltipMarker + additions;
    }

    private static Rule? FindMatchingRule(ItemDataUnpacked item)
    {
        try
        {
            var manager = ItemFilterManager.Instance;
            var rules = manager?.Filter?.rules;
            if (rules == null) return null;

            int level = 0;
            try { level = PlayerFinder.getExperienceTracker()?.CurrentLevel ?? 0; } catch { }
            for (int i = rules.Count - 1; i >= 0; i--)
            {
                Rule rule = rules[i];
                if (rule != null && rule.isEnabled && rule.type.ToString() != "HIDE" && rule.Match(item, level))
                    return rule;
            }
        }
        catch { }
        return null;
    }

    private static string BuildOwnershipText(ItemDataUnpacked item)
    {
        bool isSet = item.isSet();
        bool isWeaver = item.weaversWill > 0;
        ItemDataUnpacked? stashMatch = FindMatchingStashItem(item, isWeaver);
        if (stashMatch == null)
            return "<i><color=#FFD700>[NEW - NOT IN STASH]</color></i>";
        if (isSet)
            return "<color=#00FF00>[OWNED - IN STASH]</color>";

        int currentValue = isWeaver ? item.weaversWill : item.getLegendaryPotentialTier();
        int stashValue = isWeaver ? stashMatch.weaversWill : stashMatch.legendaryPotential;
        string stat = isWeaver ? "WW" : "LP";
        string tint = isWeaver ? "#8D6EE8" : "#FF5555";
        string comparison = stashValue > currentValue
            ? $"<color=#FF5555>↓</color> (Stash has <color={tint}>{stat}:{stashValue}</color>)"
            : stashValue < currentValue
                ? $"<color=#53D769>↑</color> (Stash has <color={tint}>{stat}:{stashValue}</color>)"
                : $"<color=#68A7FF>=</color> (Stash has <color={tint}>{stat}:{stashValue}</color>)";
        return $"<color=#53D769>[OWNED]</color> - <color={tint}>[{stat}:{currentValue}]</color> {comparison}";
    }

    private static ItemDataUnpacked? FindMatchingStashItem(ItemDataUnpacked item, bool preferWeaver)
    {
        try
        {
            var containers = ItemContainersManager.Instance?.stash?.CurrentContainer?.containers;
            if (containers == null) return null;

            ItemDataUnpacked? best = null;
            int bestValue = -1;
            foreach (ItemContainer container in containers)
            {
                if (container?.content == null) continue;
                foreach (ItemContainerEntry entry in container.content)
                {
                    var data = entry?.data;
                    if (data == null || !data.isUniqueSetOrLegendary() || data.uniqueID != item.uniqueID) continue;
                    int value = preferWeaver ? data.weaversWill : data.legendaryPotential;
                    if (best == null || value > bestValue)
                    {
                        best = data.getAsUnpacked();
                        bestValue = value;
                    }
                }
            }
            return best;
        }
        catch { return null; }
    }

    private static IEnumerator UpdateGroundLabel(GroundItemLabel label)
    {
        yield return null;
        if (label == null) yield break;

        ItemDataUnpacked? item;
        try
        {
            item = label.getItemData();
            if (item == null || !item.isUniqueSetOrLegendary()) yield break;
        }
        catch { yield break; }

        TextMeshProUGUI text;
        try { text = label.itemText; }
        catch { yield break; }
        if (text == null) yield break;

        const string labelMarker = "\u200B\u200B\u200B";
        string current = text.text ?? string.Empty;
        if (current.Contains(labelMarker, StringComparison.Ordinal)) yield break;

        string baseName = current;
        string stats = string.Empty;
        string suffix;
        bool isSet = item.isSet();
        if (!s_kgImprovementsLoaded)
        {
            if (ShowFullItemName?.Value == true) baseName = item.FullName;
            if (!isSet && ShowLegendaryPotential?.Value == true)
                stats = item.weaversWill > 0 ? $" <color=#8D6EE8>[WW:{item.weaversWill}]</color>"
                    : $" <color=#FF5555>[LP:{item.legendaryPotential}]</color>";
        }

        ItemDataUnpacked? stashMatch = FindMatchingStashItem(item, item.weaversWill > 0);
        if (stashMatch == null) suffix = " <i><color=#FFD700>NEW</color></i>";
        else if (isSet) suffix = " <color=#53D769>[OWNED]</color>";
        else if (CompareStashItems?.Value == true)
        {
            int currentValue = item.weaversWill > 0 ? item.weaversWill : item.legendaryPotential;
            int stashValue = item.weaversWill > 0 ? stashMatch.weaversWill : stashMatch.legendaryPotential;
            suffix = stashValue > currentValue ? " <color=#FF5555>↓</color>"
                : stashValue < currentValue ? " <color=#53D769>↑</color>" : " <color=#68A7FF>=</color>";
        }
        else suffix = string.Empty;

        text.text = $"{baseName}{stats}{suffix}{labelMarker}";
        try { if (label.emphasized) text.text = text.text.ToUpperInvariant(); } catch { }
        text.enableWordWrapping = false;

        // The game sizes the highlight box once for the original name, and may re-layout it later,
        // so keep re-fitting for a short while.
        for (int frame = 0; frame < 12 && label != null && text != null; frame++)
        {
            FitLabelBox(label, text, frame == 0);
            if (frame == 3) ApplyContrastOutline(text);
            yield return null;
        }
    }

    // Light base text gets a dark outline and dark text a light one (same rule as the HUD). Rich-text
    // colours (LP, NEW, arrows) share the one outline, so it follows the label's base colour.
    private const float OutlineWidth = 0.1f;

    private static void ApplyContrastOutline(TextMeshProUGUI text)
    {
        try
        {
            UnityEngine.Color c = text.color;
            float luminance = 0.299f * c.r + 0.587f * c.g + 0.114f * c.b;
            UnityEngine.Color outline = luminance > 0.5f ? new UnityEngine.Color(0f, 0f, 0f, 1f) : new UnityEngine.Color(1f, 1f, 1f, 1f);
            text.outlineColor = (UnityEngine.Color32)outline;
            text.outlineWidth = OutlineWidth;
        }
        catch (Exception e) { MelonLogger.Warning($"[LEHud] Ground-label outline failed: {e.Message}"); }
    }

    private static float MeasureTextWidth(TextMeshProUGUI text)
    {
        try
        {
            text.ForceMeshUpdate();
            return text.GetPreferredValues(text.text, 10000f, 0f).x;
        }
        catch { return 0f; }
    }

    // Grows the text rect and every non-stretched ancestor up to the label root by the same overflow,
    // which resizes the background box wherever it sits in the hierarchy.
    private static void FitLabelBox(GroundItemLabel label, TextMeshProUGUI text, bool dump)
    {
        try
        {
            UnityEngine.RectTransform textRt = text.rectTransform;
            float needed = MeasureTextWidth(text) + 4f;
            float delta = needed - textRt.rect.width;

            if (dump)
            {
                var sb = new System.Text.StringBuilder();
                for (UnityEngine.Transform? t = text.transform; t != null && t != label.transform.parent; t = t.parent)
                {
                    var r = t as UnityEngine.RectTransform;
                    sb.Append($"{t.name}[w={(r != null ? r.rect.width : 0):0.#},a={(r != null ? r.anchorMin.x : 0):0.#}-{(r != null ? r.anchorMax.x : 0):0.#}] < ");
                }
                foreach (var rt in label.GetComponentsInChildren<UnityEngine.RectTransform>(true))
                    sb.Append($"\n   {rt.name} w={rt.rect.width:0.#} h={rt.rect.height:0.#} a={rt.anchorMin.x:0.##}-{rt.anchorMax.x:0.##} sd={rt.sizeDelta.x:0.#} pv={rt.pivot.x:0.#} comps={string.Join(",", System.Linq.Enumerable.Select(rt.GetComponents<UnityEngine.Component>(), c => c?.GetType().Name))}");
                MelonLogger.Msg($"[LEHud] Ground label layout: needed={needed:0.#} delta={delta:0.#} chain={sb}");
            }

            if (delta <= 0.5f) return;

            for (UnityEngine.Transform? t = text.transform; t != null && t != label.transform.parent; t = t.parent)
            {
                if (t is UnityEngine.RectTransform rt && Math.Abs(rt.anchorMin.x - rt.anchorMax.x) < 0.001f)
                    rt.SetSizeWithCurrentAnchors(UnityEngine.RectTransform.Axis.Horizontal, rt.rect.width + delta);
            }

            UnityEngine.RectTransform? bg = label.buttonBackground?.transform as UnityEngine.RectTransform;
            if (bg != null && !text.transform.IsChildOf(bg) && Math.Abs(bg.anchorMin.x - bg.anchorMax.x) < 0.001f)
                bg.SetSizeWithCurrentAnchors(UnityEngine.RectTransform.Axis.Horizontal, bg.rect.width + delta);
        }
        catch (Exception e) { MelonLogger.Warning($"[LEHud] Ground-label resize failed: {e.Message}"); }
    }}
