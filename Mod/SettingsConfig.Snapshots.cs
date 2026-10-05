using MelonLoader;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System;

namespace Mod
{
	internal static partial class SettingsConfig
	{
		// Snapshot DTO for JSON IO
		private sealed class SettingsSnapshot
		{
			public bool mapHack { get; set; }
			public float drawDistance { get; set; }
			public float autoHealthPotion { get; set; }
			public float autoPotionCooldown { get; set; }
			public float timeScale { get; set; }
			public bool useAutoPot { get; set; }
			public bool useLootFilter { get; set; }
			public bool enableNetworkDiagnostics { get; set; }
			public bool enableDpsMeter { get; set; }
			public float dpsMeterWindowSeconds { get; set; }
			public float dpsMeterInactivityResetSeconds { get; set; }
			public bool dpsMeterAutoReset { get; set; }
			public bool enableDpsMeterOnlineRaw { get; set; }
			public int dpsMeterOnlineFilterMode { get; set; }
			public float dpsMeterNearPlayerMeters { get; set; }
			public float dpsMeterFarPlayerMeters { get; set; }
			public float dpsMeterHpDropCorrelationMs { get; set; }
			public bool dpsMeterPanelLocked { get; set; }
			public float dpsMeterPanelX { get; set; }
			public float dpsMeterPanelY { get; set; }
			public float dpsMeterPanelWidth { get; set; }
			public float dpsMeterPanelHeight { get; set; }
			public bool enableDamageNumberDiagnostics { get; set; }
			public bool removeFog { get; set; }
			public bool cameraZoomUnlock { get; set; }
			public bool minimapZoomUnlock { get; set; }
			public bool playerLantern { get; set; }
			public bool useAnyWaypoint { get; set; }
			public bool blockMenuInputWhenOpen { get; set; }
			public bool useAntiIdle { get; set; }
			public float antiIdleInterval { get; set; }
			public bool suppressKeepAliveOnActivity { get; set; }
			public float activitySuppressionSeconds { get; set; }
			public float sceneChangeSuppressionSeconds { get; set; }
			public float networkActivitySuppressionSeconds { get; set; }
			public bool forceIsIdleFalseFallback { get; set; }
			public bool useAutoDisconnect { get; set; }
			public float autoDisconnectHealthPercent { get; set; }
			public float autoDisconnectCooldownSeconds { get; set; }
			public bool autoDisconnectOnlyWhenNoPotions { get; set; }
			public bool showMinimapEnemyCircles { get; set; }
			public float minimapCircleSize { get; set; }
			public float minimapScale { get; set; }
			public bool autoScaleMinimap { get; set; }
			public float minimapScaleFactor { get; set; }
			public float minimapWorldRadiusMeters { get; set; }
			public bool minimapFlipX { get; set; }
			public bool minimapFlipY { get; set; }
			public float minimapBasisRotationDegrees { get; set; }
			public bool showMagicMonsters { get; set; }
			public bool showRareMonsters { get; set; }
			public bool showWhiteMonsters { get; set; }
			public bool showBossMonsters { get; set; }
			public bool showUniqueMonsters { get; set; } = true;
			public float minimapCircleOpacity { get; set; } = 0.9f;
			public float minimapNormalCircleSize { get; set; } = 6f;
			public float minimapMagicCircleSize { get; set; } = 8f;
			public float minimapRareCircleSize { get; set; } = 10f;
			public float minimapUniqueCircleSize { get; set; } = 12f;
			public float minimapBossCircleSize { get; set; } = 16f;
			public string minimapNormalCircleColor { get; set; } = "E53935";
			public string minimapMagicCircleColor { get; set; } = "3588FF";
			public string minimapRareCircleColor { get; set; } = "F5D328";
			public string minimapUniqueCircleColor { get; set; } = "F28C28";
			public string minimapBossCircleColor { get; set; } = "F28C28";
			public float minimapOffsetX { get; set; }
			public float minimapOffsetY { get; set; }
			public float minimapFullscreenScaleCorrection { get; set; } = 0.1422f;
			public float minimapFullscreenOffsetX { get; set; }
			public float minimapFullscreenOffsetY { get; set; }
			public Dictionary<string, bool> npcClassifications { get; set; } = new();
			public Dictionary<string, bool> npcDrawings { get; set; } = new();
			public Dictionary<string, bool> itemDrawings { get; set; } = new();
			public bool useSimpleAntiIdle { get; set; }
			public float simpleAntiIdleInterval { get; set; }
			public bool showESPLines { get; set; }
			public bool showESPLabels { get; set; }
			public float espVerticalCullMeters { get; set; }
			public bool espShowChests { get; set; }
			public bool espShowShrines { get; set; }
			public bool espShowRunePrisons { get; set; }
			public bool espShowChampions { get; set; }
			public bool espShowLootLizards { get; set; }
			public bool espShowOmens { get; set; }
#if DEBUG
			public bool debugEnableDiagnostics { get; set; }
			public bool debugShowLocalPlayerPanel { get; set; }
			public bool debugShowLocalPlayerWorldLabel { get; set; }
			public bool debugDrawAllManagerActors { get; set; }
			public bool debugDrawAllGroundItems { get; set; }
			public bool debugDrawAllGroundGold { get; set; }
			public bool debugDrawManagerLines { get; set; }
			public bool debugIgnoreDistanceCulling { get; set; }
			public int debugMaxEntriesPerSystem { get; set; }
#endif
		}

		private static SettingsSnapshot CreateSnapshot()
		{
			return new SettingsSnapshot
			{
				mapHack = Settings.mapHack,
				drawDistance = Settings.drawDistance,
				autoHealthPotion = Settings.autoHealthPotion,
				autoPotionCooldown = Settings.autoPotionCooldown,
				timeScale = Settings.timeScale,
				useAutoPot = Settings.useAutoPot,
				useLootFilter = Settings.useLootFilter,
				enableNetworkDiagnostics = Settings.enableNetworkDiagnostics,
				enableDpsMeter = Settings.enableDpsMeter,
				dpsMeterWindowSeconds = Settings.dpsMeterWindowSeconds,
				dpsMeterInactivityResetSeconds = Settings.dpsMeterInactivityResetSeconds,
				dpsMeterAutoReset = Settings.dpsMeterAutoReset,
				enableDpsMeterOnlineRaw = Settings.enableDpsMeterOnlineRaw,
				dpsMeterOnlineFilterMode = Settings.dpsMeterOnlineFilterMode,
				dpsMeterNearPlayerMeters = Settings.dpsMeterNearPlayerMeters,
				dpsMeterFarPlayerMeters = Settings.dpsMeterFarPlayerMeters,
				dpsMeterHpDropCorrelationMs = Settings.dpsMeterHpDropCorrelationMs,
				dpsMeterPanelLocked = Settings.dpsMeterPanelLocked,
				dpsMeterPanelX = Settings.dpsMeterPanelX,
				dpsMeterPanelY = Settings.dpsMeterPanelY,
				dpsMeterPanelWidth = Settings.dpsMeterPanelWidth,
				dpsMeterPanelHeight = Settings.dpsMeterPanelHeight,
				enableDamageNumberDiagnostics = Settings.enableDamageNumberDiagnostics,
				removeFog = Settings.removeFog,
				cameraZoomUnlock = Settings.cameraZoomUnlock,
				minimapZoomUnlock = Settings.minimapZoomUnlock,
				playerLantern = Settings.playerLantern,
				useAnyWaypoint = Settings.useAnyWaypoint,
				blockMenuInputWhenOpen = Settings.blockMenuInputWhenOpen,
				useAntiIdle = Settings.useAntiIdle,
				antiIdleInterval = Settings.antiIdleInterval,
				forceIsIdleFalseFallback = Settings.forceIsIdleFalseFallback,
				suppressKeepAliveOnActivity = Settings.suppressKeepAliveOnActivity,
				activitySuppressionSeconds = Settings.activitySuppressionSeconds,
				sceneChangeSuppressionSeconds = Settings.sceneChangeSuppressionSeconds,
				// networkActivitySuppressionSeconds = Settings.networkActivitySuppressionSeconds,
				useAutoDisconnect = Settings.useAutoDisconnect,
				autoDisconnectHealthPercent = Settings.autoDisconnectHealthPercent,
				autoDisconnectCooldownSeconds = Settings.autoDisconnectCooldownSeconds,
				autoDisconnectOnlyWhenNoPotions = Settings.autoDisconnectOnlyWhenNoPotions,
				showMinimapEnemyCircles = Settings.showMinimapEnemyCircles,
				minimapCircleSize = Settings.minimapCircleSize,
				minimapScale = Settings.minimapScale,
				autoScaleMinimap = Settings.autoScaleMinimap,
				minimapScaleFactor = Settings.minimapScaleFactor,
				minimapWorldRadiusMeters = Settings.minimapWorldRadiusMeters,
				minimapFlipX = Settings.minimapFlipX,
				minimapFlipY = Settings.minimapFlipY,
				minimapBasisRotationDegrees = Settings.minimapBasisRotationDegrees,
				showMagicMonsters = Settings.showMagicMonsters,
				showRareMonsters = Settings.showRareMonsters,
				showWhiteMonsters = Settings.showWhiteMonsters,
				showBossMonsters = Settings.showBossMonsters,
				showUniqueMonsters = Settings.showUniqueMonsters,
				minimapCircleOpacity = Settings.minimapCircleOpacity,
				minimapNormalCircleSize = Settings.minimapNormalCircleSize,
				minimapMagicCircleSize = Settings.minimapMagicCircleSize,
				minimapRareCircleSize = Settings.minimapRareCircleSize,
				minimapUniqueCircleSize = Settings.minimapUniqueCircleSize,
				minimapBossCircleSize = Settings.minimapBossCircleSize,
				minimapNormalCircleColor = Settings.minimapNormalCircleColor,
				minimapMagicCircleColor = Settings.minimapMagicCircleColor,
				minimapRareCircleColor = Settings.minimapRareCircleColor,
				minimapUniqueCircleColor = Settings.minimapUniqueCircleColor,
				minimapBossCircleColor = Settings.minimapBossCircleColor,
				minimapOffsetX = Settings.minimapOffsetX,
				minimapOffsetY = Settings.minimapOffsetY,
				minimapFullscreenScaleCorrection = Settings.minimapFullscreenScaleCorrection,
				minimapFullscreenOffsetX = Settings.minimapFullscreenOffsetX,
				minimapFullscreenOffsetY = Settings.minimapFullscreenOffsetY,
				npcClassifications = new Dictionary<string, bool>(Settings.npcClassifications),
				npcDrawings = new Dictionary<string, bool>(Settings.npcDrawings),
				itemDrawings = new Dictionary<string, bool>(Settings.itemDrawings),
				useSimpleAntiIdle = Settings.useSimpleAntiIdle,
				simpleAntiIdleInterval = Settings.simpleAntiIdleInterval,
				showESPLines = Settings.showESPLines,
				showESPLabels = Settings.showESPLabels,
				espVerticalCullMeters = Settings.espVerticalCullMeters,
				espShowChests = Settings.espShowChests,
				espShowShrines = Settings.espShowShrines,
				espShowRunePrisons = Settings.espShowRunePrisons,
				espShowChampions = Settings.espShowChampions,
				espShowLootLizards = Settings.espShowLootLizards,
				espShowOmens = Settings.espShowOmens,
#if DEBUG
				debugEnableDiagnostics = Settings.debugEnableDiagnostics,
				debugShowLocalPlayerPanel = Settings.debugShowLocalPlayerPanel,
				debugShowLocalPlayerWorldLabel = Settings.debugShowLocalPlayerWorldLabel,
				debugDrawAllManagerActors = Settings.debugDrawAllManagerActors,
				debugDrawAllGroundItems = Settings.debugDrawAllGroundItems,
				debugDrawAllGroundGold = Settings.debugDrawAllGroundGold,
				debugDrawManagerLines = Settings.debugDrawManagerLines,
				debugIgnoreDistanceCulling = Settings.debugIgnoreDistanceCulling,
				debugMaxEntriesPerSystem = Settings.debugMaxEntriesPerSystem
#endif
			};
		}

		private static void ApplySnapshot(SettingsSnapshot s)
		{
			Settings.mapHack = s.mapHack;
			Settings.drawDistance = Clamp(s.drawDistance, 0f, 1000f);
			Settings.autoHealthPotion = Clamp(s.autoHealthPotion, 0f, 100f);
			Settings.autoPotionCooldown = Clamp(s.autoPotionCooldown, 0.1f, 30f);
			Settings.timeScale = Clamp(s.timeScale, 0.1f, 10f);
			Settings.useAutoPot = s.useAutoPot;
			Settings.useLootFilter = s.useLootFilter;
			Settings.enableNetworkDiagnostics = s.enableNetworkDiagnostics;
			Settings.enableDpsMeter = s.enableDpsMeter;
			Settings.dpsMeterWindowSeconds = Clamp(s.dpsMeterWindowSeconds, 0.5f, 30f);
			Settings.dpsMeterInactivityResetSeconds = Clamp(s.dpsMeterInactivityResetSeconds, 2f, 300f);
			Settings.dpsMeterAutoReset = s.dpsMeterAutoReset;
			Settings.enableDpsMeterOnlineRaw = s.enableDpsMeterOnlineRaw;
			Settings.dpsMeterOnlineFilterMode = Math.Clamp(s.dpsMeterOnlineFilterMode, 0, 2);
			Settings.dpsMeterNearPlayerMeters = Clamp(s.dpsMeterNearPlayerMeters, 0.5f, 10f);
			Settings.dpsMeterFarPlayerMeters = Clamp(s.dpsMeterFarPlayerMeters, 0.6f, 20f);
			Settings.dpsMeterHpDropCorrelationMs = Clamp(s.dpsMeterHpDropCorrelationMs, 50f, 1000f);
			if (Settings.dpsMeterFarPlayerMeters <= Settings.dpsMeterNearPlayerMeters)
				Settings.dpsMeterFarPlayerMeters = Settings.dpsMeterNearPlayerMeters + 0.2f;
			Settings.dpsMeterPanelLocked = s.dpsMeterPanelLocked;
			Settings.dpsMeterPanelX = Clamp(s.dpsMeterPanelX, -1f, 10000f);
			Settings.dpsMeterPanelY = Clamp(s.dpsMeterPanelY, -1f, 10000f);
			Settings.dpsMeterPanelWidth = Clamp(s.dpsMeterPanelWidth, 280f, 1400f);
			Settings.dpsMeterPanelHeight = Clamp(s.dpsMeterPanelHeight, 220f, 1400f);
			Settings.enableDamageNumberDiagnostics = s.enableDamageNumberDiagnostics;
			Settings.removeFog = s.removeFog;
			Settings.cameraZoomUnlock = s.cameraZoomUnlock;
			Settings.minimapZoomUnlock = s.minimapZoomUnlock;
			Settings.playerLantern = s.playerLantern;
			Settings.useAnyWaypoint = s.useAnyWaypoint;
			Settings.blockMenuInputWhenOpen = s.blockMenuInputWhenOpen;
			Settings.useAntiIdle = s.useAntiIdle;
			Settings.antiIdleInterval = Clamp(s.antiIdleInterval, 10f, 600f);
			Settings.forceIsIdleFalseFallback = s.forceIsIdleFalseFallback;
			Settings.suppressKeepAliveOnActivity = s.suppressKeepAliveOnActivity;
			Settings.activitySuppressionSeconds = Clamp(s.activitySuppressionSeconds, 0f, 600f);
			Settings.sceneChangeSuppressionSeconds = Clamp(s.sceneChangeSuppressionSeconds, 0f, 600f);
			// Settings.networkActivitySuppressionSeconds = Clamp(s.networkActivitySuppressionSeconds, 0f, 600f);
			Settings.useAutoDisconnect = s.useAutoDisconnect;
			Settings.autoDisconnectHealthPercent = Clamp(s.autoDisconnectHealthPercent, 0f, 100f);
			Settings.autoDisconnectCooldownSeconds = Clamp(s.autoDisconnectCooldownSeconds, 1f, 300f);
			Settings.autoDisconnectOnlyWhenNoPotions = s.autoDisconnectOnlyWhenNoPotions;
			Settings.showMinimapEnemyCircles = s.showMinimapEnemyCircles;
			Settings.minimapCircleSize = Clamp(s.minimapCircleSize, 1f, 64f);
			Settings.minimapScale = Clamp(s.minimapScale, 0.1f, 100f);
			Settings.autoScaleMinimap = s.autoScaleMinimap;
			Settings.minimapScaleFactor = Clamp(s.minimapScaleFactor, 0.1f, 20f);
			Settings.minimapWorldRadiusMeters = Clamp(s.minimapWorldRadiusMeters, 10f, 10000f);
			Settings.minimapFlipX = s.minimapFlipX;
			Settings.minimapFlipY = s.minimapFlipY;
			Settings.minimapBasisRotationDegrees = Clamp(s.minimapBasisRotationDegrees, -360f, 360f);
			Settings.showMagicMonsters = s.showMagicMonsters;
			Settings.showRareMonsters = s.showRareMonsters;
			Settings.showWhiteMonsters = s.showWhiteMonsters;
			Settings.showBossMonsters = s.showBossMonsters;
			Settings.showUniqueMonsters = s.showUniqueMonsters;
			Settings.minimapCircleOpacity = Clamp(s.minimapCircleOpacity, 0.05f, 1f);
			Settings.minimapNormalCircleColor = s.minimapNormalCircleColor ?? "E53935";
			Settings.minimapMagicCircleColor = s.minimapMagicCircleColor ?? "3588FF";
			Settings.minimapRareCircleColor = s.minimapRareCircleColor ?? "F5D328";
			Settings.minimapUniqueCircleColor = s.minimapUniqueCircleColor ?? "F28C28";
			Settings.minimapBossCircleColor = s.minimapBossCircleColor ?? "F28C28";
			bool previousMarkerDefaults =
				s.minimapNormalCircleSize == 6f && s.minimapMagicCircleSize == 6f &&
				s.minimapRareCircleSize == 6f && s.minimapUniqueCircleSize == 6f &&
				s.minimapBossCircleSize == 6f && s.minimapCircleOpacity == 0.9f &&
				Settings.minimapNormalCircleColor == "E53935" && Settings.minimapMagicCircleColor == "3588FF" &&
				Settings.minimapRareCircleColor == "F5D328" && Settings.minimapUniqueCircleColor == "F28C28" &&
				Settings.minimapBossCircleColor == "F28C28";
			Settings.minimapNormalCircleSize = previousMarkerDefaults ? 6f : Clamp(s.minimapNormalCircleSize, 2f, 24f);
			Settings.minimapMagicCircleSize = previousMarkerDefaults ? 8f : Clamp(s.minimapMagicCircleSize, 2f, 24f);
			Settings.minimapRareCircleSize = previousMarkerDefaults ? 10f : Clamp(s.minimapRareCircleSize, 2f, 24f);
			Settings.minimapUniqueCircleSize = previousMarkerDefaults ? 12f : Clamp(s.minimapUniqueCircleSize, 2f, 24f);
			Settings.minimapBossCircleSize = previousMarkerDefaults ? 16f : Clamp(s.minimapBossCircleSize, 2f, 24f);
			Settings.minimapOffsetX = Clamp(s.minimapOffsetX, -1000f, 1000f);
			Settings.minimapOffsetY = Clamp(s.minimapOffsetY, -1000f, 1000f);
			Settings.minimapFullscreenScaleCorrection = Clamp(s.minimapFullscreenScaleCorrection, 0.01f, 2f);
			Settings.minimapFullscreenOffsetX = Clamp(s.minimapFullscreenOffsetX, -1000f, 1000f);
			Settings.minimapFullscreenOffsetY = Clamp(s.minimapFullscreenOffsetY, -1000f, 1000f);
			Settings.useSimpleAntiIdle = s.useSimpleAntiIdle;
			Settings.simpleAntiIdleInterval = Clamp(s.simpleAntiIdleInterval, 60f, 1800f);
			Settings.showESPLines = s.showESPLines;
			Settings.showESPLabels = s.showESPLabels;
			Settings.espVerticalCullMeters = Clamp(s.espVerticalCullMeters, 0f, 200f);
			Settings.espShowChests = s.espShowChests;
			Settings.espShowShrines = s.espShowShrines;
			Settings.espShowRunePrisons = s.espShowRunePrisons;
			Settings.espShowChampions = s.espShowChampions;
			Settings.espShowLootLizards = s.espShowLootLizards;
			Settings.espShowOmens = s.espShowOmens;
#if DEBUG
			Settings.debugEnableDiagnostics = s.debugEnableDiagnostics;
			Settings.debugShowLocalPlayerPanel = s.debugShowLocalPlayerPanel;
			Settings.debugShowLocalPlayerWorldLabel = s.debugShowLocalPlayerWorldLabel;
			Settings.debugDrawAllManagerActors = s.debugDrawAllManagerActors;
			Settings.debugDrawAllGroundItems = s.debugDrawAllGroundItems;
			Settings.debugDrawAllGroundGold = s.debugDrawAllGroundGold;
			Settings.debugDrawManagerLines = s.debugDrawManagerLines;
			Settings.debugIgnoreDistanceCulling = s.debugIgnoreDistanceCulling;
			Settings.debugMaxEntriesPerSystem = Math.Clamp(s.debugMaxEntriesPerSystem, 10, 500);
#endif

			ApplyDictionarySafely(Settings.npcClassifications, s.npcClassifications);
			ApplyDictionarySafely(Settings.npcDrawings, s.npcDrawings);
			ApplyDictionarySafely(Settings.itemDrawings, s.itemDrawings);
		}

		private static void ApplyDictionarySafely(Dictionary<string, bool> target, Dictionary<string, bool> source)
		{
			foreach (var kv in source)
			{
				target[kv.Key] = kv.Value;
			}
		}

	}
}
