using System.Collections.Generic;
using Il2Cpp;
using Mod.Game;
using UnityEngine;
using UnityEngine.UI;

namespace Mod.Cheats
{
    public static class MinimapEnemyCircles
    {
        // Canvas reference for UI
        private static Canvas? minimapCanvas;
        private static bool isInitialized = false;
        private static bool isMinimapOpen = false;
        private static bool wasTabPressed = false;
        private static float nextInitializeAttemptAt = 0f;
        private static float initializeRetryDelaySeconds = InitialInitializeRetrySeconds;
        private const float InitialInitializeRetrySeconds = 0.25f;
        private const float MaxInitializeRetrySeconds = 3.0f;
        private const int MaxRenderedEnemies = 100;
        private const float RotationEpsilon = 0.0001f;
        private const string HostileAlignmentName = "Evil";
        private const string HostileNeutralAlignmentName = "HostileNeutral";
        private const float MapModeLocalPositionEpsilon = 0.01f;
        private const string FullscreenMapPath = "GUI/Canvas (animated)/Minimap Holder/Minimap(Clone)/DMMap Canvas/Map (1)";
        private const string MinimapMapPath = "GUI/Canvas (animated)/Minimap Holder/Minimap(Clone)/Minimap/SquareMinimap/DMMap Canvas/Map (1)";
        private const string MinimapBgPath = "GUI/Canvas (animated)/Minimap Holder/Minimap(Clone)/Minimap/SquareMinimap/minimapBG";
        private const string LegacyMinimapBgPath = "GUI/Canvas (animated)/Minimap Holder/Minimap(Clone)/SquareMinimap/minimapBG";
        
        // Debug info for GUI display
        public static string lastDebugInfo = "";
        public static int lastEnemyCount = 0;
        public static int lastCircleCount = 0;
        public static string lastCreateAttempt = "";
        
        private sealed class CircleVisual
        {
            public CircleVisual(GameObject gameObject, RectTransform rectTransform, Image image)
            {
                GameObject = gameObject;
                RectTransform = rectTransform;
                Image = image;
            }

            public GameObject GameObject { get; }
            public RectTransform RectTransform { get; }
            public Image Image { get; }
        }

        // Store and reuse our UI circles to avoid per-frame Instantiate/Destroy churn
        private static readonly List<CircleVisual> enemyCircles = new List<CircleVisual>(MaxRenderedEnemies);
        private static int activeCircleCount = 0;
        private static int debugUpdateCounter = 0; // Add counter to reduce debug frequency
        
        // Target containers discovered from the scene hierarchy (screenshot)
        private static RectTransform? iconsContainer; // ".../DMMap Canvas/Icons"
        private static RectTransform? mapContainer;   // ".../DMMap Canvas/Map" (optional for later)
        private static RectTransform? fullscreenIconsContainer;
        private static RectTransform? activeIconsContainer;
        private static RectTransform? activeMapContainer;
        private static string activeMapMode = "Closed";
        private static GameObject? minimapMap;
        private static GameObject? fullscreenMap;
        private static GameObject? smallMinimapBg;
        
        // Prebuilt sprites cache to avoid per-frame texture allocations
        private static Sprite? spriteWhite;

        private static Color ReadMarkerColor(string hex, Color fallback)
        {
            if (!string.IsNullOrWhiteSpace(hex) && ColorUtility.TryParseHtmlString("#" + hex.TrimStart('#'), out Color parsed))
                return parsed;
            return fallback;
        }

        
        public static void Update()
        {
            // Only update debug info every 30 frames (about twice per second at 60fps)
            debugUpdateCounter++;
            bool shouldUpdateDebug = debugUpdateCounter >= 30;
            if (shouldUpdateDebug) 
            {
                debugUpdateCounter = 0;
            }
            
            // Always update debug info, even if feature is disabled
            if (!Settings.showMinimapEnemyCircles) 
            {
                ClearCircles();
                if (shouldUpdateDebug) lastDebugInfo = "Feature DISABLED in settings";
                return;
            }

            var playerActor = ObjectManager.GetLocalPlayer();
            if (playerActor == null)
            {
                ClearCircles();
                if (shouldUpdateDebug) lastDebugInfo = "No local player found";
                return;
            }
            
            if (!Initialize(shouldUpdateDebug)) return;
            
            bool fullscreenMapOpen = IsFullscreenMapOpen();
            if (fullscreenMapOpen && fullscreenIconsContainer == null)
                ResolveFullscreenIconsContainer();

            bool fullscreenIconsVisible = fullscreenIconsContainer != null
                && fullscreenIconsContainer.gameObject.activeInHierarchy;
            bool minimapIconsVisible = iconsContainer != null
                && iconsContainer.gameObject.activeInHierarchy;
            bool minimapVisible = smallMinimapBg != null
                ? smallMinimapBg.activeInHierarchy
                : minimapIconsVisible;

            fullscreenMapOpen = fullscreenMapOpen && fullscreenIconsVisible;
            activeIconsContainer = fullscreenMapOpen
                ? fullscreenIconsContainer
                : minimapVisible ? iconsContainer : null;
            activeMapMode = activeIconsContainer == null
                ? "Closed"
                : fullscreenMapOpen ? "Overlay" : "Small";
            activeMapContainer = fullscreenMapOpen
                ? fullscreenMap != null ? fullscreenMap.GetComponent<RectTransform>() : null
                : mapContainer;

            if (activeIconsContainer == null)
            {
                ClearCircles();
                if (shouldUpdateDebug)
                    lastDebugInfo = fullscreenMapOpen
                        ? "Fullscreen map open, but its Icons container was not found"
                        : "Minimap Icons container not found";
                return;
            }

            isMinimapOpen = activeIconsContainer != null;
            if (!isMinimapOpen)
            {
                ClearCircles();
                if (shouldUpdateDebug) lastDebugInfo = "Both minimap and overlay are closed";
                return;
            }
            
            UpdateEnemyCircles(playerActor, shouldUpdateDebug);
            if (shouldUpdateDebug)
                MelonLoader.MelonLogger.Msg($"[LEHud] [Radar] {lastDebugInfo}");
        }
        
        private static void CheckMinimapToggle(bool updateDebug = true)
        {
            // Drive open state from the Icons container visibility if available
            // Fallback: previous tab toggle (kept as a last resort)
            bool isTabPressed = Input.GetKey(KeyCode.Tab);
            if (isTabPressed && !wasTabPressed)
            {
                isMinimapOpen = !isMinimapOpen;
            }
            wasTabPressed = isTabPressed;
        }

        private static void ResolveFullscreenIconsContainer()
        {
            fullscreenIconsContainer = null;
            if (fullscreenMap == null)
                fullscreenMap = GameObject.Find(FullscreenMapPath);

            if (fullscreenMap == null || fullscreenMap.transform.parent == null)
                return;

            Transform mapCanvas = fullscreenMap.transform.parent;
            Transform iconsTransform = mapCanvas.Find("Icons");
            if (iconsTransform == null)
                iconsTransform = mapCanvas.Find("icons");

            if (iconsTransform != null)
                fullscreenIconsContainer = iconsTransform.GetComponent<RectTransform>();
        }
        
        private static bool IsFullscreenMapOpen()
        {
            if (fullscreenMap == null)
            {
                fullscreenMap = GameObject.Find(FullscreenMapPath);
            }

            if (fullscreenMap != null)
            {
                return fullscreenMap.activeInHierarchy
                    && IsNearZeroLocalPosition(fullscreenMap.transform.localPosition);
            }

            if (minimapMap == null)
                minimapMap = GameObject.Find(MinimapMapPath);
            return false;
        }

        private static bool IsNearZeroLocalPosition(Vector3 localPosition)
        {
            return Mathf.Abs(localPosition.x) <= MapModeLocalPositionEpsilon &&
                   Mathf.Abs(localPosition.y) <= MapModeLocalPositionEpsilon &&
                   Mathf.Abs(localPosition.z) <= MapModeLocalPositionEpsilon;
        }
        
        public static bool Initialize(bool updateDebug = true)
        {
            if (isInitialized && (iconsContainer != null))
            {
                EnsureSpriteCache();
                return true;
            }
            if (Time.unscaledTime < nextInitializeAttemptAt) return false;
            
            try
            {
                int previousIconsContainerId = iconsContainer != null ? iconsContainer.GetInstanceID() : 0;

                // Try exact paths first (from screenshot)
                GameObject iconsGO = GameObject.Find("BWF/GameManager/GeneralGameManager/Minimap Folder/DMMap/DMMap Canvas/Icons");
                GameObject mapGO = GameObject.Find("BWF/GameManager/GeneralGameManager/Minimap Folder/DMMap/DMMap Canvas/Map");
                
                if (iconsGO == null)
                {
                    // Fallback: find any GameObject named "Icons" under a parent named "DMMap Canvas"
                    foreach (var rt in UnityEngine.Object.FindObjectsOfType<RectTransform>())
                    {
                        if (rt == null) continue;
                        if (rt.name != "Icons") continue;
                        Transform parent = rt.transform.parent;
                        while (parent != null)
                        {
                            if (parent.name == "DMMap Canvas")
                            {
                                iconsGO = rt.gameObject;
                                break;
                            }
                            parent = parent.parent;
                        }
                        if (iconsGO != null) break;
                    }
                }
                
                if (iconsGO != null)
                {
                    iconsContainer = iconsGO.GetComponent<RectTransform>();
                    minimapCanvas = iconsGO.GetComponentInParent<Canvas>();
                }
                
                if (mapGO != null)
                {
                    mapContainer = mapGO.GetComponent<RectTransform>();
                }
                
                // Bind minimap/fullscreen sentinels for visibility gating.
                minimapMap = GameObject.Find(MinimapMapPath);
                fullscreenMap = GameObject.Find(FullscreenMapPath);
                if (mapContainer == null && minimapMap != null)
                    mapContainer = minimapMap.GetComponent<RectTransform>();
                ResolveFullscreenIconsContainer();
                smallMinimapBg = GameObject.Find(MinimapBgPath);
                if (smallMinimapBg == null)
                {
                    smallMinimapBg = GameObject.Find(LegacyMinimapBgPath);
                }
                
                // Build sprite cache once
                EnsureSpriteCache();
                
                if (iconsContainer != null)
                {
                    if (previousIconsContainerId != iconsContainer.GetInstanceID())
                    {
                        DestroyAllCircles();
                    }

                    isInitialized = true;
                    initializeRetryDelaySeconds = InitialInitializeRetrySeconds;
                    nextInitializeAttemptAt = 0f;
                    if (updateDebug) lastDebugInfo = $"Bound Icons container: {iconsContainer.name}";
                    return true;
                }
                
                nextInitializeAttemptAt = Time.unscaledTime + initializeRetryDelaySeconds;
                initializeRetryDelaySeconds = Mathf.Min(MaxInitializeRetrySeconds, initializeRetryDelaySeconds * 2f);
                if (updateDebug) lastDebugInfo = "Icons container not found";
                return false;
            }
            catch (System.Exception e)
            {
                nextInitializeAttemptAt = Time.unscaledTime + initializeRetryDelaySeconds;
                initializeRetryDelaySeconds = Mathf.Min(MaxInitializeRetrySeconds, initializeRetryDelaySeconds * 2f);
                if (updateDebug) lastDebugInfo = $"Initialize error: {e.Message}";
                return false;
            }
        }

        public static void OnSceneChanged()
        {
            DestroyAllCircles();
            minimapCanvas = null;
            iconsContainer = null;
            mapContainer = null;
            minimapMap = null;
            fullscreenMap = null;
            fullscreenIconsContainer = null;
            activeIconsContainer = null;
            activeMapContainer = null;
            activeMapMode = "Closed";
            smallMinimapBg = null;
            isInitialized = false;
            isMinimapOpen = false;
            wasTabPressed = false;
            nextInitializeAttemptAt = 0f;
            initializeRetryDelaySeconds = InitialInitializeRetrySeconds;
            debugUpdateCounter = 0;
        }
        
        private static void EnsureSpriteCache()
        {
            // Choose a fixed reasonable texture size for cached sprites; scale via RectTransform
            const int baseSize = 16;
            if (spriteWhite == null) spriteWhite = BuildCircleSprite(baseSize, Color.white);
        }
        
        private static Sprite BuildCircleSprite(int size, Color color)
        {
            var tex = CreateCircleTexture(size, color);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }
        
        public static void UpdateEnemyCircles(GameObject playerActor, bool updateDebug = true)
        {
            try
            {
                if (ActorManager.instance == null) 
                {
                    if (updateDebug) lastDebugInfo = "ActorManager.instance is null";
                    return;
                }

                CompactCirclePool();

                Transform playerTransform = playerActor.transform;
                Vector3 playerPosition = playerTransform.position;
                float drawDistance = Settings.drawDistance;
                float drawDistanceSqr = drawDistance * drawDistance;
                int totalVisuals = 0;
                int alignmentFiltered = 0;
                int candidateEnemyCount = 0;
                int successfulCircles = 0;

                // Precompute mapping parameters once per update
                float pixelsPerMeter = GetPixelsPerMeter();
                Vector2 maxBounds = GetIconRectHalfSize();
                float mapRotationRad = GetMapRotationRadians();
                float basisRad = GetBasisRotationRadians();

                bool applyBasisRotation = Mathf.Abs(basisRad) > RotationEpsilon;
                bool applyMapRotation = Mathf.Abs(mapRotationRad) > RotationEpsilon;
                float cosBasis = 1f;
                float sinBasis = 0f;
                float cosMap = 1f;
                float sinMap = 0f;

                if (applyBasisRotation)
                {
                    cosBasis = Mathf.Cos(basisRad);
                    sinBasis = Mathf.Sin(basisRad);
                }

                if (applyMapRotation)
                {
                    cosMap = Mathf.Cos(mapRotationRad);
                    sinMap = Mathf.Sin(mapRotationRad);
                }

                bool flipX = Settings.minimapFlipX;
                bool flipY = Settings.minimapFlipY;
                float offsetX = activeMapMode == "Overlay"
                    ? Settings.minimapFullscreenOffsetX
                    : Settings.minimapOffsetX;
                float offsetY = activeMapMode == "Overlay"
                    ? Settings.minimapFullscreenOffsetY
                    : Settings.minimapOffsetY;

                foreach (var visual in ActorManager.instance.visuals)
                {
                    totalVisuals++;
                    
                    // Skip if no alignment or not hostile
                    if (visual.alignment == null) continue;
                    string alignmentName = visual.alignment.name;
                    if (alignmentName != HostileAlignmentName && alignmentName != HostileNeutralAlignmentName) 
                    {
                        alignmentFiltered++;
                        continue;
                    }
                    
                    // Check each visual actor
                    if (visual.visuals != null && visual.visuals._list != null)
                    {
                        foreach (var actor in visual.visuals._list)
                        {
                            if (actor == null || actor.gameObject == null) continue;
                            if (!actor.gameObject.activeInHierarchy) continue;
                            
                            // Skip dead enemies
                            if (actor.dead) continue;

                            Vector3 actorPosition = actor.transform.position;
                            Vector3 delta = actorPosition - playerPosition;
                            if (delta.sqrMagnitude >= drawDistanceSqr) continue;

                            candidateEnemyCount++;
                            if (successfulCircles >= MaxRenderedEnemies) continue;

                            if (!TryGetEnemyStyle(actor, out Color markerColor, out float markerSize))
                                continue;

                            Vector2 minimapPos = WorldDeltaToMinimapPosition(
                                delta.x,
                                delta.z,
                                pixelsPerMeter,
                                offsetX,
                                offsetY,
                                flipX,
                                flipY,
                                applyBasisRotation,
                                cosBasis,
                                sinBasis,
                                applyMapRotation,
                                cosMap,
                                sinMap);

                            if (Mathf.Abs(minimapPos.x) > maxBounds.x || Mathf.Abs(minimapPos.y) > maxBounds.y)
                                continue;

                            if (spriteWhite == null) continue;
                            int circleSize = Mathf.Max(2, Mathf.RoundToInt(markerSize));
                            UpsertMinimapCircle(successfulCircles, spriteWhite, minimapPos,
                                new Vector2(circleSize, circleSize), markerColor);
                            successfulCircles++;
                        }
                    }
                }

                lastEnemyCount = candidateEnemyCount;
                SetActiveCircleCount(successfulCircles);
                lastCircleCount = successfulCircles;
                lastCreateAttempt = $"Updated {successfulCircles} circles for {candidateEnemyCount} enemies";
                
                // Set final debug info with complete status
                if (updateDebug)
                {
                    lastDebugInfo = $"ACTIVE | Map: {activeMapMode}, Scale: {pixelsPerMeter:F3}px/m, MapRatio: {GetActiveMapScaleRatio():F3}, OverlayCorrection: {Settings.minimapFullscreenScaleCorrection:F3}, Visuals: {totalVisuals}, Filtered: {alignmentFiltered}, InRange: {lastEnemyCount}, Rendered: {lastCircleCount}, Pool: {enemyCircles.Count}";
                }
            }
            catch (System.Exception e)
            {
                if (updateDebug) lastDebugInfo = $"Error updating circles: {e.Message}";
            }
        }

        private static bool TryGetEnemyStyle(ActorVisuals enemy, out Color color, out float size)
        {
            var displayInfo = enemy.GetComponent<ActorDisplayInformation>();
            if (displayInfo == null)
            {
                color = ReadMarkerColor(Settings.minimapNormalCircleColor, Color.red);
                size = Settings.minimapNormalCircleSize;
                return Settings.showWhiteMonsters;
            }

            string className = displayInfo.actorClass.ToString();
            if (className.IndexOf("Unique", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                color = ReadMarkerColor(Settings.minimapUniqueCircleColor, new Color(0.95f, 0.55f, 0.12f));
                size = Settings.minimapUniqueCircleSize;
                return Settings.showUniqueMonsters;
            }

            switch (displayInfo.actorClass)
            {
                case DisplayActorClass.Boss:
                    color = ReadMarkerColor(Settings.minimapBossCircleColor, new Color(0.95f, 0.55f, 0.12f));
                    size = Settings.minimapBossCircleSize;
                    return Settings.showBossMonsters;
                case DisplayActorClass.Rare:
                    color = ReadMarkerColor(Settings.minimapRareCircleColor, new Color(0.96f, 0.83f, 0.16f));
                    size = Settings.minimapRareCircleSize;
                    return Settings.showRareMonsters;
                case DisplayActorClass.Magic:
                    color = ReadMarkerColor(Settings.minimapMagicCircleColor, new Color(0.20f, 0.53f, 1f));
                    size = Settings.minimapMagicCircleSize;
                    return Settings.showMagicMonsters;
                case DisplayActorClass.Normal:
                    color = ReadMarkerColor(Settings.minimapNormalCircleColor, Color.red);
                    size = Settings.minimapNormalCircleSize;
                    return Settings.showWhiteMonsters;
                default:
                    // Match previous behavior: unknown classes with display info were shown by default.
                    color = ReadMarkerColor(Settings.minimapNormalCircleColor, Color.red);
                    size = Settings.minimapNormalCircleSize;
                    return true;
            }
        }

        private static void UpsertMinimapCircle(int index, Sprite sprite, Vector2 position, Vector2 sizeDelta, Color color)
        {
            CircleVisual circle = GetOrCreateCircle(index);
            if (activeIconsContainer != null && circle.RectTransform.parent != activeIconsContainer.transform)
            {
                circle.RectTransform.SetParent(activeIconsContainer.transform, false);
            }

            circle.RectTransform.anchoredPosition = position;
            if (circle.RectTransform.sizeDelta != sizeDelta)
            {
                circle.RectTransform.sizeDelta = sizeDelta;
            }

            if (circle.Image.sprite != sprite)
            {
                circle.Image.sprite = sprite;
            }

            color.a *= Settings.minimapCircleOpacity;
            circle.Image.color = color;

            if (!circle.GameObject.activeSelf)
            {
                circle.GameObject.SetActive(true);
            }
        }

        private static CircleVisual GetOrCreateCircle(int index)
        {
            if (index < enemyCircles.Count)
            {
                CircleVisual existing = enemyCircles[index];
                if (existing.GameObject != null && existing.RectTransform != null && existing.Image != null)
                {
                    return existing;
                }

                CircleVisual replacement = CreateCircleVisual();
                enemyCircles[index] = replacement;
                return replacement;
            }

            CircleVisual created = CreateCircleVisual();
            enemyCircles.Add(created);
            return created;
        }

        private static CircleVisual CreateCircleVisual()
        {
            var circleObj = new GameObject("EnemyCircle");
            if (activeIconsContainer != null)
            {
                circleObj.transform.SetParent(activeIconsContainer.transform, false);
            }

            var rectTransform = circleObj.AddComponent<RectTransform>();
            var image = circleObj.AddComponent<Image>();
            image.raycastTarget = false;
            circleObj.SetActive(false);
            return new CircleVisual(circleObj, rectTransform, image);
        }

        private static void CompactCirclePool()
        {
            for (int i = enemyCircles.Count - 1; i >= 0; i--)
            {
                CircleVisual circle = enemyCircles[i];
                if (circle.GameObject == null || circle.RectTransform == null || circle.Image == null)
                {
                    enemyCircles.RemoveAt(i);
                }
            }

            if (activeCircleCount > enemyCircles.Count)
            {
                activeCircleCount = enemyCircles.Count;
            }
        }

        private static void SetActiveCircleCount(int count)
        {
            activeCircleCount = Mathf.Clamp(count, 0, enemyCircles.Count);

            for (int i = activeCircleCount; i < enemyCircles.Count; i++)
            {
                GameObject circleObject = enemyCircles[i].GameObject;
                if (circleObject != null && circleObject.activeSelf)
                {
                    circleObject.SetActive(false);
                }
            }
        }

        private static void DestroyAllCircles()
        {
            foreach (CircleVisual circle in enemyCircles)
            {
                if (circle.GameObject != null)
                {
                    UnityEngine.Object.Destroy(circle.GameObject);
                }
            }

            enemyCircles.Clear();
            activeCircleCount = 0;
            lastCircleCount = 0;
        }
        
        private static float GetPixelsPerMeter()
        {
            float mapScaleRatio = GetActiveMapScaleRatio();
            if (activeIconsContainer == null)
            {
                return Settings.minimapScale * Settings.minimapScaleFactor * mapScaleRatio;
            }
            if (!Settings.autoScaleMinimap)
            {
                return Settings.minimapScale * Settings.minimapScaleFactor * mapScaleRatio * GetModeScaleCorrection();
            }

            // The fullscreen Icons rect is much larger than its world-space map coverage.
            // Use the calibrated small-map viewport as the base scale, then apply only
            // the actual map transform zoom ratio for the selected mode.
            RectTransform scaleReference = activeMapMode == "Overlay" && iconsContainer != null
                ? iconsContainer
                : activeIconsContainer;
            var rect = scaleReference.rect;
            float radiusPixels = Mathf.Min(rect.width, rect.height) * 0.5f;
            float radiusMeters = Mathf.Max(1f, Settings.minimapWorldRadiusMeters);
            return (radiusPixels / radiusMeters) * Settings.minimapScaleFactor * mapScaleRatio * GetModeScaleCorrection();
        }

        private static float GetModeScaleCorrection()
        {
            return activeMapMode == "Overlay" ? Settings.minimapFullscreenScaleCorrection : 1f;
        }

        private static float GetActiveMapScaleRatio()
        {
            if (activeMapMode != "Overlay" || activeMapContainer == null || mapContainer == null)
                return 1f;

            float smallScale = Mathf.Abs(mapContainer.lossyScale.x);
            float overlayScale = Mathf.Abs(activeMapContainer.lossyScale.x);
            if (smallScale <= 0.0001f || overlayScale <= 0.0001f)
                return 1f;

            return Mathf.Clamp(overlayScale / smallScale, 0.01f, 100f);
        }
        
        private static float GetMapRotationRadians()
        {
            if (activeMapContainer == null)
                return 0f;
            // Map often rotates around Z; rotate our relative vector by -map rotation to stay aligned
            float zDeg = activeMapContainer.localEulerAngles.z;
            return -zDeg * Mathf.Deg2Rad;
        }
        
        private static Vector2 GetIconRectHalfSize()
        {
            if (activeIconsContainer == null)
                return new Vector2(300f, 300f);
            var rect = activeIconsContainer.rect;
            return new Vector2(rect.width * 0.5f, rect.height * 0.5f);
        }
        
        private static float GetBasisRotationRadians()
        {
            return Settings.minimapBasisRotationDegrees * Mathf.Deg2Rad;
        }
        
        private static Vector2 WorldDeltaToMinimapPosition(
            float worldDeltaX,
            float worldDeltaZ,
            float pixelsPerMeter,
            float offsetX,
            float offsetY,
            bool flipX,
            bool flipY,
            bool applyBasisRotation,
            float cosBasis,
            float sinBasis,
            bool applyMapRotation,
            float cosMap,
            float sinMap)
        {
            float relX = worldDeltaX;
            float relY = worldDeltaZ;
            
            // Apply basis rotation to align world axes to DMap axes
            if (applyBasisRotation)
            {
                float bx = relX * cosBasis - relY * sinBasis;
                float by = relX * sinBasis + relY * cosBasis;
                relX = bx;
                relY = by;
            }
            
            // Rotate to match map rotation
            if (applyMapRotation)
            {
                float rx = relX * cosMap - relY * sinMap;
                float ry = relX * sinMap + relY * cosMap;
                relX = rx;
                relY = ry;
            }
            
            // Optional axis flips to match DMap handedness
            if (flipX) relX = -relX;
            if (flipY) relY = -relY;
            
            // Convert to minimap space (Unity UI is typically +Y up). Map convention: x->right, z->forward
            float minimapX = relX * pixelsPerMeter + offsetX;
            float minimapY = relY * pixelsPerMeter + offsetY;
            
            return new Vector2(minimapX, minimapY);
        }
        
        private static Texture2D CreateCircleTexture(int size, Color color)
        {
            size = Mathf.Max(2, size);
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Point;
            var center = new Vector2(size / 2f, size / 2f);
            var radius = size / 2f;
            
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x, y), center);
                    
                    if (distance <= radius)
                    {
                        float alpha = distance <= radius - 1 ? 1.0f : 1.0f - (distance - (radius - 1));
                        texture.SetPixel(x, y, new Color(color.r, color.g, color.b, alpha));
                    }
                    else
                    {
                        texture.SetPixel(x, y, Color.clear);
                    }
                }
            }
            
            texture.Apply(false, false);
            return texture;
        }
        
        public static void ClearCircles()
        {
            SetActiveCircleCount(0);
            lastCircleCount = 0;
        }

        public static void Cleanup()
        {
            DestroyAllCircles();
            DestroySpriteAndTexture(spriteWhite);
            spriteWhite = null;
            minimapCanvas = null;
            iconsContainer = null;
            mapContainer = null;
            fullscreenIconsContainer = null;
            activeIconsContainer = null;
            activeMapContainer = null;
            minimapMap = null;
            fullscreenMap = null;
            smallMinimapBg = null;
            isInitialized = false;
        }

        private static void DestroySpriteAndTexture(Sprite? sprite)
        {
            if (sprite == null) return;
            Texture2D? texture = sprite.texture;
            UnityEngine.Object.Destroy(sprite);
            if (texture != null)
                UnityEngine.Object.Destroy(texture);
        }
    }
}
