// ================================================================
//  UnitBorder.cs — runtime 9-slice outline for the clean-line unit.
//
//  The composer marks the PlainText Tier·Grade unit with a TMP link.
//  This feature measures that link once per text generation and keeps a
//  fresh, non-layout Image sibling immediately below the TMP glyphs.
// ================================================================

#nullable disable
using HarmonyLib;
using Il2Cpp;
using Il2CppItemFiltering;
using Il2CppTMPro;
using MelonLoader;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;
namespace Mod.Cheats.TerribleTooltips;

internal static class UnitBorder
{
    private const string OwnedBorderName = "TT_UnitBorder";
    private const string OwnedDividerName = "TT_UnitDivider";
    private const string LinkOpen = "<link=\"ttu\">";
    private const string DividerLinkOpen = "<link=\"ttd\">";
    private const string LinkClose = "</link>";

    private static readonly string Marker = new string((char)0x200B, 4);
    private static readonly Regex s_unitTierRegex = new(
        @"(?:Tier\s*(\d+)|\bT(\d+))",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex s_unitPrefixRegex = new(
        @"^(?:Sealed(?:\s*(?:\||·)\s*))?(?<unit>(?:(?:Tier\s*\d+|T\d+)(?:(?<divider>\s*(?:\||·)\s*)(?:[SABCDF](?:·[SABCDF])*))?|(?:[SABCDF](?:·[SABCDF])*)))",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Dictionary<int, TooltipState> s_tooltips = new();
    private static readonly List<int> s_deadTooltipKeys = new();

    private static readonly Texture2D[] s_textures = new Texture2D[5];
    private static readonly Texture2D[] s_debugTextures = new Texture2D[5];
    private static readonly Sprite[] s_sprites = new Sprite[5];
    private static readonly Sprite[] s_debugSprites = new Sprite[5];
    private static Texture2D s_dividerTexture;
    private static Sprite s_dividerSprite;
    private static int s_ownedBorderCount;
    private static bool s_runtimeActive;
    private static bool s_latchedOff;
    private static bool s_failureLogged;
    private static bool s_offLogged;
    private static bool s_linkFallbackLogged;
    private static bool s_wrappedLogged;
    private static bool s_canvasLogged;
    private static bool s_degenerateLogged;
    private static bool s_parentRectMissingLogged;

    private sealed class TooltipState
    {
        public UITooltipItem Tooltip;
        public readonly List<RowState> Rows = new();
        public int ScanToken;
        public int LoggedFingerprint;
        public bool HasLoggedFingerprint;
    }

    private sealed class RowState
    {
        public UITooltipItem Tooltip;
        public TextMeshProUGUI Tmp;
        public readonly List<UnitState> Units = new();
        public int TmpId;
        public int TextHash;
        public int SeenToken;
        public int ActiveUnitCount;
        public float RectWidth;
        public float RectHeight;
        public int LineCount;
        public int ResyncAfterFrame;
        public int PadX;
        public int PadY;
        public int Thickness;
        public DividerStyle DividerStyle;
        public bool HasLayoutCache;
    }

    private sealed class UnitState
    {
        public GameObject Border;
        public RectTransform BorderRect;
        public Image BorderImage;
        public GameObject Divider;
        public RectTransform DividerRect;
        public Image DividerImage;
        public bool OwnsBorder;
        public bool HasDivider;
        public float DividerX;
        public int Tier;
        public float Left;
        public float Right;
        public float Bottom;
        public float Top;
        public BorderColorMode ColorMode;
        public bool Debug;
    }

    private readonly struct MeasuredUnit
    {
        public MeasuredUnit(Bounds bounds, int tier, bool hasDivider, float dividerX)
        {
            Bounds = bounds;
            Tier = tier;
            HasDivider = hasDivider;
            DividerX = dividerX;
        }

        public Bounds Bounds { get; }
        public int Tier { get; }
        public bool HasDivider { get; }
        public float DividerX { get; }
    }

    [HarmonyPatch(typeof(UITooltipItem), "UpdateLayout")]
    internal static class Patch_UpdateLayout
    {
        private static void Postfix(UITooltipItem __instance, object[] __args)
        {
            try { Run(__instance); }
            catch (Exception ex) { Fail("UpdateLayout", ex); }
        }
    }

    internal static void OnLateUpdate()
    {
        try
        {
            if (!GateEnabled())
            {
                HandleGateOff();
                return;
            }

            if (s_latchedOff) return;
            s_runtimeActive = true;
            if (s_ownedBorderCount == 0) return;

            s_deadTooltipKeys.Clear();
            foreach (var pair in s_tooltips)
            {
                TooltipState tooltipState = pair.Value;
                if (tooltipState.Tooltip == null)
                {
                    DestroyRows(tooltipState);
                    s_deadTooltipKeys.Add(pair.Key);
                    continue;
                }

                for (int i = tooltipState.Rows.Count - 1; i >= 0; i--)
                {
                    RowState row = tooltipState.Rows[i];
                    if (row.Tmp == null)
                    {
                        DestroyRow(row);
                        tooltipState.Rows.RemoveAt(i);
                        continue;
                    }

                    string text = row.Tmp.text ?? "";
                    if (!row.Tmp.gameObject.activeInHierarchy || !IsCandidateText(text))
                    {
                        Hide(row);
                        continue;
                    }

                    bool pending = row.ResyncAfterFrame > 0 &&
                                   Time.frameCount >= row.ResyncAfterFrame;
                    if (pending || LayoutChanged(row))
                    {
                        row.ResyncAfterFrame = 0;
                        RefreshRow(row, text, i + 1, scheduleNextFrame: false);
                    }
                }
            }

            foreach (int key in s_deadTooltipKeys) s_tooltips.Remove(key);
            s_deadTooltipKeys.Clear();
        }
        catch (Exception ex) { Fail("LateUpdate", ex); }
    }

    private static void Run(UITooltipItem tooltip)
    {
        if (!GateEnabled())
        {
            HandleGateOff();
            return;
        }

        if (s_latchedOff) return;
        s_runtimeActive = true;
        if (tooltip == null) return;

        int tooltipId = tooltip.GetInstanceID();
        if (!s_tooltips.TryGetValue(tooltipId, out TooltipState tooltipState) ||
            tooltipState.Tooltip == null || tooltipState.Tooltip != tooltip)
        {
            if (tooltipState != null) DestroyRows(tooltipState);
            tooltipState = new TooltipState { Tooltip = tooltip };
            s_tooltips[tooltipId] = tooltipState;
        }

        tooltipState.ScanToken++;
        if (tooltipState.ScanToken == 0) tooltipState.ScanToken = 1;
        int scanToken = tooltipState.ScanToken;
        int fingerprint = 17;
        int candidates = 0;
        int placed = 0;

        TextMeshProUGUI[] tmps = tooltip.GetComponentsInChildren<TextMeshProUGUI>(false);
        if (tmps == null) return;

        foreach (TextMeshProUGUI tmp in tmps)
        {
            if (tmp == null) continue;
            string text = tmp.text ?? "";
            if (!IsCandidateText(text)) continue;

            candidates++;
            int tmpId = tmp.GetInstanceID();
            int textHash = TextHash(text);
            fingerprint = unchecked(fingerprint * 31 + tmpId);
            fingerprint = unchecked(fingerprint * 31 + textHash);

            RowState row = FindRow(tooltipState, tmpId);
            if (row != null) row.SeenToken = scanToken;

            if (row != null && row.Tmp != null && row.Units.Count > 0 &&
                row.TextHash == textHash)
            {
                row.Tooltip = tooltip;
                row.Tmp = tmp;
                placed += LayoutChanged(row)
                    ? RefreshRow(row, text, candidates, scheduleNextFrame: false)
                    : SyncRow(row, candidates);
                continue;
            }

            if (row == null)
            {
                row = new RowState();
                tooltipState.Rows.Add(row);
            }

            row.Tooltip = tooltip;
            row.Tmp = tmp;
            row.TmpId = tmpId;
            row.TextHash = textHash;
            row.SeenToken = scanToken;
            placed += RefreshRow(row, text, candidates, scheduleNextFrame: true);
        }

        foreach (RowState row in tooltipState.Rows)
            if (row.SeenToken != scanToken) Hide(row);

        if (candidates > 0 &&
            (!tooltipState.HasLoggedFingerprint || tooltipState.LoggedFingerprint != fingerprint))
        {
            tooltipState.HasLoggedFingerprint = true;
            tooltipState.LoggedFingerprint = fingerprint;
            MelonLogger.Msg(
                $"[UnitBorder] placed {placed} units across {candidates} rows path=runtime-9slice");
        }
    }

    private readonly struct Bounds
    {
        public Bounds(float left, float right, float bottom, float top)
        {
            Left = left;
            Right = right;
            Bottom = bottom;
            Top = top;
        }

        public float Left { get; }
        public float Right { get; }
        public float Bottom { get; }
        public float Top { get; }
    }

    private static bool TryMeasure(TextMeshProUGUI tmp, string richText,
                                   List<MeasuredUnit> measuredUnits)
    {
        measuredUnits.Clear();
        TMP_TextInfo info = tmp.textInfo;
        if (info == null || info.characterInfo == null || info.characterCount <= 0)
            return false;

        int characterCount = Math.Min(info.characterCount, info.characterInfo.Length);
        bool foundLink = false;
        if (info.linkInfo != null && info.linkCount > 0)
        {
            int linkCount = Math.Min(info.linkCount, info.linkInfo.Length);
            for (int i = 0; i < linkCount; i++)
            {
                TMP_LinkInfo link = info.linkInfo[i];
                if (link == null ||
                    !string.Equals(link.GetLinkID(), "ttu", StringComparison.Ordinal))
                    continue;

                foundLink = true;
                int start = link.linkTextfirstCharacterIndex;
                int end = start + link.linkTextLength;
                int dividerStart = -1;
                int dividerEnd = -1;
                if (i + 2 < linkCount)
                {
                    TMP_LinkInfo dividerLink = info.linkInfo[i + 1];
                    TMP_LinkInfo gradeLink = info.linkInfo[i + 2];
                    if (dividerLink != null && gradeLink != null &&
                        string.Equals(dividerLink.GetLinkID(), "ttd", StringComparison.Ordinal) &&
                        string.Equals(gradeLink.GetLinkID(), "ttu", StringComparison.Ordinal))
                    {
                        dividerStart = dividerLink.linkTextfirstCharacterIndex;
                        dividerEnd = dividerStart + dividerLink.linkTextLength;
                        end = gradeLink.linkTextfirstCharacterIndex + gradeLink.linkTextLength;
                        i += 2;
                    }
                }

                if (TryMeasureRange(info, characterCount, start, end,
                                    dividerStart, dividerEnd, out MeasuredUnit measured))
                    measuredUnits.Add(measured);
            }
        }

        if (foundLink) return measuredUnits.Count > 0;

        MeasurePrefixUnits(info, characterCount, CountUnitBoxes(richText), measuredUnits);
        if (!s_linkFallbackLogged)
        {
            s_linkFallbackLogged = true;
            MelonLogger.Msg("[UnitBorder] linkInfo empty; using per-line prefix count");
        }
        return measuredUnits.Count > 0;
    }

    private static bool TryMeasureRange(TMP_TextInfo info, int characterCount,
                                        int start, int end, int dividerStart, int dividerEnd,
                                        out MeasuredUnit measured)
    {
        measured = default;
        start = Math.Max(0, start);
        end = Math.Min(characterCount, end);
        while (end > start)
        {
            TMP_CharacterInfo trailingCharacter = info.characterInfo[end - 1];
            if (trailingCharacter == null || !char.IsWhiteSpace(trailingCharacter.character)) break;
            end--;
        }

        bool found = false;
        int unitLine = -1;
        float left = float.MaxValue;
        float right = float.MinValue;
        float bottom = float.MaxValue;
        float top = float.MinValue;
        var unitText = new StringBuilder();

        for (int i = start; i < end; i++)
        {
            TMP_CharacterInfo character = info.characterInfo[i];
            if (character == null || character.character == (char)0x200B) continue;
            if (unitLine < 0) unitLine = character.lineNumber;
            else if (character.lineNumber != unitLine)
            {
                LogWrappedOnce();
                return false;
            }

            unitText.Append(character.character);
            if (!character.isVisible) continue;
            found = true;
            left = Math.Min(left, character.bottomLeft.x);
            bottom = Math.Min(bottom, character.bottomLeft.y);
            right = Math.Max(right, character.topRight.x);
            top = Math.Max(top, character.topRight.y);
        }

        if (!found || unitLine < 0) return false;

        float padX = BorderPadX();
        float padY = BorderPadY();
        float paddedRight = right + padX;
        for (int i = end; i < characterCount; i++)
        {
            TMP_CharacterInfo sentenceCharacter = info.characterInfo[i];
            if (sentenceCharacter == null || sentenceCharacter.character == (char)0x200B)
                continue;
            if (sentenceCharacter.lineNumber != unitLine) break;
            if (!sentenceCharacter.isVisible) continue;
            paddedRight = Math.Min(paddedRight, sentenceCharacter.bottomLeft.x - 2f);
            break;
        }

        bool hasDivider = TryDividerCenter(
            info, characterCount, dividerStart, dividerEnd, unitLine, out float dividerX);
        var bounds = new Bounds(left - padX, paddedRight, bottom - padY, top + padY);
        measured = new MeasuredUnit(
            bounds, ParseTier(unitText.ToString()), hasDivider, dividerX);
        return true;
    }

    private static bool TryDividerCenter(TMP_TextInfo info, int characterCount,
                                         int start, int end, int unitLine, out float center)
    {
        center = 0f;
        if (start < 0 || end <= start) return false;
        start = Math.Max(0, start);
        end = Math.Min(characterCount, end);
        for (int i = start; i < end; i++)
        {
            TMP_CharacterInfo character = info.characterInfo[i];
            if (character == null || character.lineNumber != unitLine ||
                char.IsWhiteSpace(character.character) || character.character == (char)0x200B)
                continue;
            center = (character.bottomLeft.x + character.topRight.x) * 0.5f;
            return true;
        }
        return false;
    }

    private static void MeasurePrefixUnits(TMP_TextInfo info, int characterCount,
                                           int expectedCount, List<MeasuredUnit> measuredUnits)
    {
        if (expectedCount <= 0) return;

        int index = 0;
        while (index < characterCount && measuredUnits.Count < expectedCount)
        {
            TMP_CharacterInfo first = info.characterInfo[index];
            if (first == null)
            {
                index++;
                continue;
            }

            int lineNumber = first.lineNumber;
            int lineEnd = index + 1;
            while (lineEnd < characterCount)
            {
                TMP_CharacterInfo next = info.characterInfo[lineEnd];
                if (next != null && next.lineNumber != lineNumber) break;
                lineEnd++;
            }

            var lineText = new StringBuilder();
            var characterIndices = new List<int>();
            for (int i = index; i < lineEnd; i++)
            {
                TMP_CharacterInfo character = info.characterInfo[i];
                if (character == null || character.character == (char)0x200B ||
                    character.character == '\r' || character.character == '\n')
                    continue;
                lineText.Append(character.character);
                characterIndices.Add(i);
            }

            string plainLine = lineText.ToString();
            int leading = 0;
            while (leading < plainLine.Length && char.IsWhiteSpace(plainLine[leading])) leading++;
            Match match = s_unitPrefixRegex.Match(plainLine.Substring(leading));
            if (match.Success)
            {
                Group unit = match.Groups["unit"];
                int firstOffset = leading + unit.Index;
                int lastOffset = firstOffset + unit.Length - 1;
                if (firstOffset >= 0 && lastOffset < characterIndices.Count)
                {
                    Group divider = match.Groups["divider"];
                    int dividerStart = -1;
                    int dividerEnd = -1;
                    if (divider.Success)
                    {
                        int dividerFirstOffset = leading + divider.Index;
                        int dividerLastOffset = dividerFirstOffset + divider.Length - 1;
                        if (dividerFirstOffset >= 0 && dividerLastOffset < characterIndices.Count)
                        {
                            dividerStart = characterIndices[dividerFirstOffset];
                            dividerEnd = characterIndices[dividerLastOffset] + 1;
                        }
                    }

                    if (TryMeasureRange(info, characterCount, characterIndices[firstOffset],
                                        characterIndices[lastOffset] + 1, dividerStart, dividerEnd,
                                        out MeasuredUnit measured))
                        measuredUnits.Add(measured);
                }
            }

            index = lineEnd;
        }
    }

    private static UnitState CreateUnit(RowState row, Sprite sprite, bool debug)
    {
        GameObject border = null;
        GameObject divider = null;
        try
        {
            border = new GameObject(
                OwnedBorderName,
                Il2CppInterop.Runtime.Il2CppType.Of<RectTransform>(),
                Il2CppInterop.Runtime.Il2CppType.Of<Image>(),
                Il2CppInterop.Runtime.Il2CppType.Of<LayoutElement>());
            border.transform.SetParent(row.Tmp.transform.parent, false);
            border.layer = row.Tmp.gameObject.layer;

            if (border.GetComponent("CanvasRenderer") == null)
                throw new InvalidOperationException("Image did not create its required CanvasRenderer");

            RectTransform rect = border.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.localRotation = Quaternion.identity;
            rect.localScale = Vector3.one;

            Image image = border.GetComponent<Image>();
            image.sprite = sprite;
            image.type = Image.Type.Sliced;
            image.fillCenter = debug;
            image.pixelsPerUnitMultiplier = 1f;
            image.raycastTarget = false;
            image.material = null;
            image.enabled = false;
            image.enabled = true;
            image.SetAllDirty();

            LayoutElement layout = border.GetComponent<LayoutElement>();
            layout.ignoreLayout = true;

            divider = new GameObject(
                OwnedDividerName,
                Il2CppInterop.Runtime.Il2CppType.Of<RectTransform>(),
                Il2CppInterop.Runtime.Il2CppType.Of<Image>(),
                Il2CppInterop.Runtime.Il2CppType.Of<LayoutElement>());
            divider.transform.SetParent(row.Tmp.transform.parent, false);
            divider.layer = row.Tmp.gameObject.layer;

            if (divider.GetComponent("CanvasRenderer") == null)
                throw new InvalidOperationException("Divider Image did not create its required CanvasRenderer");

            RectTransform dividerRect = divider.GetComponent<RectTransform>();
            dividerRect.anchorMin = new Vector2(0f, 1f);
            dividerRect.anchorMax = new Vector2(0f, 1f);
            dividerRect.pivot = new Vector2(0.5f, 1f);
            dividerRect.localRotation = Quaternion.identity;
            dividerRect.localScale = Vector3.one;

            Image dividerImage = divider.GetComponent<Image>();
            dividerImage.sprite = EnsureDividerSprite();
            dividerImage.type = Image.Type.Simple;
            dividerImage.raycastTarget = false;
            dividerImage.material = null;
            dividerImage.enabled = true;

            LayoutElement dividerLayout = divider.GetComponent<LayoutElement>();
            dividerLayout.ignoreLayout = true;
            divider.SetActive(false);

            s_ownedBorderCount++;
            return new UnitState
            {
                Border = border,
                BorderRect = rect,
                BorderImage = image,
                Divider = divider,
                DividerRect = dividerRect,
                DividerImage = dividerImage,
                OwnsBorder = true,
                ColorMode = (BorderColorMode)(-1),
                Debug = !debug
            };
        }
        catch
        {
            try { if (divider != null) UnityEngine.Object.DestroyImmediate(divider); } catch { }
            try { if (border != null) UnityEngine.Object.DestroyImmediate(border); } catch { }
            throw;
        }
    }

    private static int RefreshRow(RowState row, string text, int rowNumber,
                                  bool scheduleNextFrame)
    {
        row.Tmp.ForceMeshUpdate();
        var measuredUnits = new List<MeasuredUnit>();
        if (!TryMeasure(row.Tmp, text, measuredUnits))
        {
            row.ActiveUnitCount = 0;
            Hide(row);
            CacheLayout(row);
            return 0;
        }

        if (!EnsureSprite(out Sprite sprite, out bool debug)) return 0;
        while (row.Units.Count < measuredUnits.Count)
            row.Units.Add(CreateUnit(row, sprite, debug));

        row.ActiveUnitCount = measuredUnits.Count;
        for (int i = 0; i < measuredUnits.Count; i++)
        {
            UnitState unit = row.Units[i];
            MeasuredUnit measured = measuredUnits[i];
            if (unit.Tier != measured.Tier) unit.ColorMode = (BorderColorMode)(-1);
            unit.Tier = measured.Tier;
            unit.Left = measured.Bounds.Left;
            unit.Right = measured.Bounds.Right;
            unit.Bottom = measured.Bounds.Bottom;
            unit.Top = measured.Bounds.Top;
            unit.HasDivider = measured.HasDivider;
            unit.DividerX = measured.DividerX;
        }
        for (int i = measuredUnits.Count; i < row.Units.Count; i++) Hide(row.Units[i]);

        CacheLayout(row);
        if (scheduleNextFrame) row.ResyncAfterFrame = Time.frameCount + 1;
        return SyncRow(row, rowNumber);
    }

    private static int SyncRow(RowState row, int rowNumber)
    {
        if (!EnsureSprite(out Sprite sprite, out bool debug)) return 0;

        int placed = 0;
        int count = Math.Min(row.ActiveUnitCount, row.Units.Count);
        for (int i = 0; i < count; i++)
        {
            UnitState unit = row.Units[i];
            ApplySprite(unit, sprite, debug);
            if (!unit.Border.activeSelf) unit.Border.SetActive(true);
            ApplyColor(unit);
            if (!SyncGeometry(row, unit, rowNumber)) continue;
            placed++;
            LogCanvasOnce(row, unit);
            LogDebugPlacement(row, unit, rowNumber);
        }
        return placed;
    }

    private static bool LayoutChanged(RowState row)
    {
        if (!row.HasLayoutCache || row.Tmp == null) return true;
        Rect rect = row.Tmp.rectTransform.rect;
        int lineCount = row.Tmp.textInfo != null ? row.Tmp.textInfo.lineCount : 0;
        DividerStyle dividerStyle = CurrentDividerStyle();
        return Math.Abs(rect.width - row.RectWidth) > 0.01f ||
               Math.Abs(rect.height - row.RectHeight) > 0.01f ||
               lineCount != row.LineCount ||
               BorderPadX() != row.PadX || BorderPadYBase() != row.PadY ||
               BorderThickness() != row.Thickness || dividerStyle != row.DividerStyle;
    }

    private static void CacheLayout(RowState row)
    {
        Rect rect = row.Tmp.rectTransform.rect;
        row.RectWidth = rect.width;
        row.RectHeight = rect.height;
        row.LineCount = row.Tmp.textInfo != null ? row.Tmp.textInfo.lineCount : 0;
        row.PadX = BorderPadX();
        row.PadY = BorderPadYBase();
        row.Thickness = BorderThickness();
        row.DividerStyle = CurrentDividerStyle();
        row.HasLayoutCache = true;
    }

    private static bool SyncGeometry(RowState row, UnitState unit, int rowNumber)
    {
        if (row.Tmp == null || unit.BorderRect == null || row.Tmp.transform.parent == null) return false;

        RectTransform tmpRect = row.Tmp.rectTransform;
        Transform parent = row.Tmp.transform.parent;
        RectTransform parentRect = parent.TryCast<RectTransform>() ?? parent.GetComponent<RectTransform>();
        if (parentRect == null)
        {
            Hide(row);
            if (!s_parentRectMissingLogged)
            {
                s_parentRectMissingLogged = true;
                MelonLogger.Msg("[UnitBorder] parent has no RectTransform; row skipped");
            }
            return false;
        }

        Vector3 topLeft = parent.InverseTransformPoint(
            tmpRect.TransformPoint(new Vector3(unit.Left, unit.Top, 0f)));
        Vector3 topRight = parent.InverseTransformPoint(
            tmpRect.TransformPoint(new Vector3(unit.Right, unit.Top, 0f)));
        Vector3 bottomLeft = parent.InverseTransformPoint(
            tmpRect.TransformPoint(new Vector3(unit.Left, unit.Bottom, 0f)));
        Vector3 bottomRight = parent.InverseTransformPoint(
            tmpRect.TransformPoint(new Vector3(unit.Right, unit.Bottom, 0f)));

        float left = Math.Min(Math.Min(topLeft.x, topRight.x), Math.Min(bottomLeft.x, bottomRight.x));
        float right = Math.Max(Math.Max(topLeft.x, topRight.x), Math.Max(bottomLeft.x, bottomRight.x));
        float bottom = Math.Min(Math.Min(topLeft.y, topRight.y), Math.Min(bottomLeft.y, bottomRight.y));
        float top = Math.Max(Math.Max(topLeft.y, topRight.y), Math.Max(bottomLeft.y, bottomRight.y));

        float width = right - left;
        float height = top - bottom;
        if (width < 4f || height < 4f)
        {
            Hide(unit);
            LogDegenerateOnce(rowNumber, width, height);
            return false;
        }

        unit.BorderRect.anchorMin = new Vector2(0f, 1f);
        unit.BorderRect.anchorMax = new Vector2(0f, 1f);
        unit.BorderRect.pivot = new Vector2(0f, 1f);
        unit.BorderRect.sizeDelta = new Vector2(right - left, top - bottom);
        unit.BorderRect.localPosition = new Vector3(left, top, 0f);
        unit.BorderRect.localRotation = Quaternion.identity;
        unit.BorderRect.localScale = Vector3.one;
        unit.BorderRect.SetSiblingIndex(row.Tmp.transform.GetSiblingIndex());

        bool showDivider = unit.HasDivider && unit.DividerRect != null &&
                           CurrentDividerStyle() == DividerStyle.Strip;
        if (showDivider)
        {
            Vector3 dividerCenter = parent.InverseTransformPoint(
                tmpRect.TransformPoint(new Vector3(
                    unit.DividerX, (unit.Top + unit.Bottom) * 0.5f, 0f)));
            unit.DividerRect.anchorMin = new Vector2(0f, 1f);
            unit.DividerRect.anchorMax = new Vector2(0f, 1f);
            unit.DividerRect.pivot = new Vector2(0.5f, 1f);
            unit.DividerRect.sizeDelta = new Vector2(BorderThickness(), top - bottom);
            unit.DividerRect.localPosition = new Vector3(dividerCenter.x, top, 0f);
            unit.DividerRect.localRotation = Quaternion.identity;
            unit.DividerRect.localScale = Vector3.one;
            if (!unit.Divider.activeSelf) unit.Divider.SetActive(true);
            unit.DividerRect.SetSiblingIndex(row.Tmp.transform.GetSiblingIndex());
        }
        else
        {
            HideDivider(unit);
        }
        return true;
    }

    private static void ApplyColor(UnitState unit)
    {
        BorderColorMode mode = Prefs.BorderColorMode.Value;
        bool debug = Prefs.BorderDebug != null && Prefs.BorderDebug.Value;
        if (unit.BorderImage == null || (unit.ColorMode == mode && unit.Debug == debug)) return;

        Color color = new(138f / 255f, 116f / 255f, 168f / 255f, 0.9f);
        if (debug)
        {
            color = new Color(1f, 0f, 1f, 1f);
        }
        else if (mode == BorderColorMode.TierColor && unit.Tier > 0 &&
            ColorUtility.TryParseHtmlString(Colors.TierColor(unit.Tier), out Color tierColor))
        {
            tierColor.a = 0.75f;
            color = tierColor;
        }

        unit.BorderImage.color = color;
        if (unit.DividerImage != null) unit.DividerImage.color = color;
        unit.ColorMode = mode;
        unit.Debug = debug;
    }

    private static void ApplySprite(UnitState unit, Sprite sprite, bool debug)
    {
        if (unit.BorderImage == null) return;
        unit.BorderImage.sprite = sprite;
        unit.BorderImage.fillCenter = debug;
        unit.BorderImage.pixelsPerUnitMultiplier = 1f;
    }

    private static bool EnsureSprite(out Sprite sprite, out bool debug)
    {
        int thickness = BorderThickness();
        debug = Prefs.BorderDebug != null && Prefs.BorderDebug.Value;
        Sprite[] sprites = debug ? s_debugSprites : s_sprites;
        Texture2D[] textures = debug ? s_debugTextures : s_textures;
        sprite = sprites[thickness];
        if (sprite != null) return true;

        try
        {
            Texture2D texture = new(32, 32, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;

            for (int y = 0; y < 32; y++)
            {
                for (int x = 0; x < 32; x++)
                {
                    bool edge = x < thickness || x >= 32 - thickness ||
                                y < thickness || y >= 32 - thickness;
                    bool corner = (x < 2 || x >= 30) && (y < 2 || y >= 30);
                    Color pixel = edge ? Color.white :
                        debug ? new Color(1f, 1f, 1f, 0.25f) : Color.clear;
                    texture.SetPixel(x, y, corner ? Color.clear : pixel);
                }
            }

            texture.Apply();
            sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, 32f, 32f),
                new Vector2(0.5f, 0.5f),
                100f,
                0u,
                SpriteMeshType.FullRect,
                new Vector4(6f, 6f, 6f, 6f));
            if (sprite == null) throw new InvalidOperationException("Sprite.Create returned null");
            sprite.name = debug
                ? $"TT_UnitBorder_t{thickness}_debug"
                : $"TT_UnitBorder_t{thickness}";
            textures[thickness] = texture;
            sprites[thickness] = sprite;
            return true;
        }
        catch (Exception ex)
        {
            Fail("sprite", ex);
            return false;
        }
    }

    private static Sprite EnsureDividerSprite()
    {
        if (s_dividerSprite != null) return s_dividerSprite;

        s_dividerTexture = new Texture2D(4, 4, TextureFormat.RGBA32, false);
        s_dividerTexture.filterMode = FilterMode.Bilinear;
        s_dividerTexture.wrapMode = TextureWrapMode.Clamp;
        for (int y = 0; y < 4; y++)
            for (int x = 0; x < 4; x++)
                s_dividerTexture.SetPixel(x, y, Color.white);
        s_dividerTexture.Apply();

        s_dividerSprite = Sprite.Create(
            s_dividerTexture,
            new Rect(0f, 0f, 4f, 4f),
            new Vector2(0.5f, 0.5f),
            100f,
            0u,
            SpriteMeshType.FullRect);
        if (s_dividerSprite == null)
            throw new InvalidOperationException("Divider Sprite.Create returned null");
        s_dividerSprite.name = "TT_UnitDivider_solid";
        return s_dividerSprite;
    }

    private static int BorderThickness()
    {
        int value = Prefs.BorderThickness != null ? Prefs.BorderThickness.Value : 2;
        int clamped = Math.Clamp(value, 1, 4);
        if (Prefs.BorderThickness != null && value != clamped)
            Prefs.BorderThickness.Value = clamped;
        return clamped;
    }

    private static int BorderPadX()
    {
        int value = Prefs.BorderPadX != null ? Prefs.BorderPadX.Value : 6;
        int clamped = Math.Clamp(value, 2, 12);
        if (Prefs.BorderPadX != null && value != clamped) Prefs.BorderPadX.Value = clamped;
        return clamped;
    }

    private static int BorderPadYBase()
    {
        int value = Prefs.BorderPadY != null ? Prefs.BorderPadY.Value : 2;
        int clamped = Math.Clamp(value, 0, 6);
        if (Prefs.BorderPadY != null && value != clamped) Prefs.BorderPadY.Value = clamped;
        return clamped;
    }

    private static float BorderPadY() => BorderPadYBase() + 0.5f;

    private static DividerStyle CurrentDividerStyle()
        => Prefs.DividerStyle != null ? Prefs.DividerStyle.Value : DividerStyle.Strip;

    private static void LogCanvasOnce(RowState row, UnitState unit)
    {
        if (s_canvasLogged) return;
        s_canvasLogged = true;

        Component canvas = null;
        try
        {
            Il2CppSystem.Type canvasType = Il2CppSystem.Type.GetType(
                "UnityEngine.Canvas, UnityEngine.UIModule");
            if (canvasType != null) canvas = row.Tmp.GetComponentInParent(canvasType);
        }
        catch { }

        Camera camera = RuntimeProperty(canvas, "worldCamera") as Camera;
        string canvasName = canvas != null ? canvas.name : "none";
        string mode = RuntimeProperty(canvas, "renderMode")?.ToString() ?? "-";
        string cameraName = camera != null ? camera.name : "none";
        string mask = camera != null ? camera.cullingMask.ToString() : "-";
        MelonLogger.Msg(
            $"[UnitBorder] canvas={canvasName} mode={mode} cam={cameraName} mask={mask} " +
            $"layer(tmp)={row.Tmp.gameObject.layer} layer(border)={unit.Border.layer}");
    }

    private static void LogDebugPlacement(RowState row, UnitState unit, int rowNumber)
    {
        if (Prefs.BorderDebug == null || !Prefs.BorderDebug.Value) return;

        Vector2 position = unit.BorderRect.anchoredPosition;
        Vector3 localPosition = unit.BorderRect.localPosition;
        Vector2 size = unit.BorderRect.sizeDelta;
        Transform parent = unit.BorderRect.parent;
        RectTransform parentRect = parent?.TryCast<RectTransform>() ?? parent?.GetComponent<RectTransform>();
        float anchorX = parentRect != null ? parentRect.rect.xMin : 0f;
        float anchorY = parentRect != null ? parentRect.rect.yMax : 0f;
        string parentRectName = parentRect != null ? parentRect.name : "null";
        int sibling = unit.Border.transform.GetSiblingIndex();
        int siblingCount = unit.Border.transform.parent.childCount;
        string spriteName = unit.BorderImage.sprite != null ? unit.BorderImage.sprite.name : "null";
        Component canvasRenderer = unit.Border.GetComponent("CanvasRenderer");
        string cull = RuntimeProperty(canvasRenderer, "cull")?.ToString() ?? "-";
        MelonLogger.Msg(
            $"[UnitBorder] dbg row={rowNumber} pos=({position.x:0.##},{position.y:0.##}) " +
            $"anchor=({anchorX:0.##},{anchorY:0.##}) " +
            $"local=({localPosition.x:0.##},{localPosition.y:0.##}) parentRect={parentRectName} " +
            $"size=({size.x:0.##},{size.y:0.##}) sib={sibling}/{siblingCount} " +
            $"layer={unit.Border.layer} enabled={unit.BorderImage.enabled} " +
            $"cull={cull} sprite={spriteName}");
    }

    private static void LogDegenerateOnce(int rowNumber, float width, float height)
    {
        if (s_degenerateLogged) return;
        s_degenerateLogged = true;
        MelonLogger.Msg($"[UnitBorder] degenerate rect on row {rowNumber} ({width:0.##}×{height:0.##})");
    }

    private static object RuntimeProperty(object instance, string propertyName)
    {
        if (instance == null) return null;
        try { return instance.GetType().GetProperty(propertyName)?.GetValue(instance); }
        catch { return null; }
    }

    private static RowState FindRow(TooltipState tooltipState, int tmpId)
    {
        foreach (RowState row in tooltipState.Rows)
            if (row.TmpId == tmpId) return row;
        return null;
    }

    private static int ParseTier(string unitText)
    {
        Match match = s_unitTierRegex.Match(unitText ?? "");
        if (!match.Success) return 0;
        string digits = match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value;
        return int.TryParse(digits, out int tier) ? tier : 0;
    }

    private static int CountUnitBoxes(string richText)
    {
        if (string.IsNullOrEmpty(richText)) return 0;
        int unitLinks = CountLinks(richText, LinkOpen);
        int dividerLinks = CountLinks(richText, DividerLinkOpen);
        return Math.Max(0, unitLinks - dividerLinks);
    }

    private static int CountLinks(string richText, string linkOpen)
    {
        int count = 0;
        int index = 0;
        while ((index = richText.IndexOf(linkOpen, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += linkOpen.Length;
        }
        return count;
    }

    private static bool IsCandidateText(string text)
        => text.EndsWith(Marker, StringComparison.Ordinal) &&
           text.Contains(LinkOpen, StringComparison.Ordinal);

    private static bool GateEnabled()
        => Prefs.UnitBorder != null && Prefs.UnitBorder.Value &&
           Prefs.Enabled &&
           Prefs.Style != null && Prefs.Style.Value == SignalStyle.PlainText;

    private static bool MasterAndPrefEnabled()
        => Prefs.UnitBorder != null && Prefs.UnitBorder.Value &&
           Prefs.Enabled;

    private static void HandleGateOff()
    {
        if (!MasterAndPrefEnabled())
        {
            if (s_runtimeActive || s_ownedBorderCount > 0)
            {
                CleanupAll();
                if (!s_offLogged)
                {
                    s_offLogged = true;
                    MelonLogger.Msg("[UnitBorder] off — borders removed");
                }
            }
            s_runtimeActive = false;
            return;
        }

        if (!s_runtimeActive) return;
        HideAll();
        s_runtimeActive = false;
    }

    private static void HideAll()
    {
        foreach (TooltipState tooltipState in s_tooltips.Values)
            foreach (RowState row in tooltipState.Rows)
                Hide(row);
    }

    private static void Hide(RowState row)
    {
        if (row == null) return;
        foreach (UnitState unit in row.Units) Hide(unit);
    }

    private static void Hide(UnitState unit)
    {
        if (unit?.Border != null && unit.Border.activeSelf) unit.Border.SetActive(false);
        HideDivider(unit);
    }

    private static void HideDivider(UnitState unit)
    {
        if (unit?.Divider != null && unit.Divider.activeSelf) unit.Divider.SetActive(false);
    }

    private static void DestroyRows(TooltipState tooltipState)
    {
        if (tooltipState == null) return;
        foreach (RowState row in tooltipState.Rows) DestroyRow(row);
        tooltipState.Rows.Clear();
    }

    private static void DestroyRow(RowState row)
    {
        if (row == null) return;
        foreach (UnitState unit in row.Units) DestroyUnit(unit);
        row.Units.Clear();
        row.ActiveUnitCount = 0;
    }

    private static void DestroyUnit(UnitState unit)
    {
        if (unit == null || !unit.OwnsBorder) return;
        try { if (unit.Divider != null) UnityEngine.Object.DestroyImmediate(unit.Divider); }
        catch { }
        try { if (unit.Border != null) UnityEngine.Object.DestroyImmediate(unit.Border); }
        catch { }
        unit.Border = null;
        unit.BorderRect = null;
        unit.BorderImage = null;
        unit.Divider = null;
        unit.DividerRect = null;
        unit.DividerImage = null;
        unit.OwnsBorder = false;
        if (s_ownedBorderCount > 0) s_ownedBorderCount--;
    }

    private static void CleanupAll()
    {
        foreach (TooltipState tooltipState in s_tooltips.Values) DestroyRows(tooltipState);
        s_tooltips.Clear();
        s_ownedBorderCount = 0;
    }

    private static void LogWrappedOnce()
    {
        if (s_wrappedLogged) return;
        s_wrappedLogged = true;
        MelonLogger.Msg("[UnitBorder] unit wrapped; row skipped");
    }

    private static void Fail(string stage, Exception ex)
    {
        if (!s_failureLogged)
        {
            s_failureLogged = true;
            MelonLogger.Warning(
                $"[UnitBorder] OFF ({stage}: {ex.GetType().Name}: {ex.Message}) — plain coloured text");
        }
        CleanupAll();
        s_latchedOff = true;
        s_runtimeActive = false;
    }

    private static int TextHash(string text) => StringComparer.Ordinal.GetHashCode(text ?? "");
}
