using System.Linq;
using UnityEngine;
using static UnityEngine.GUI;
using MelonLoader;
using Mod.Cheats;
using Mod.Cheats.ESP;
using Mod.Cheats.Inventory;
using TrackerRuntime = Mod.Cheats.CooldownTracker.CooldownTracker;
using CooldownTrackerTheme = Mod.Cheats.CooldownTracker.Theme;
using FallenAutoEnabler = Mod.Cheats.FallenPlugins.AutoEnabler;
using FallenImprovedTooltips = Mod.Cheats.FallenPlugins.ImprovedTooltips;
using TtPrefs = Mod.Cheats.TerribleTooltips.Prefs;
using Mod.Game;
using Mod.Utils;

namespace Mod
{
	internal class Menu
	{
		private static bool guiVisible = false;
		private const float resizeGripSize = 20.0f;
		private static bool isResizing = false;
		public static bool npcDrawingsDropdown = false;
		public static bool npcClassificationsDropdown = false;
		public static bool itemDrawingsDropdown = false;
		public static bool antiIdleSubDropdown = false;
		public static bool dpsMeterSubDropdown = false;
		public static bool specialsSubDropdown = false; // Placeholder for future per-special options
		private static bool s_hasAppliedInputBlockState;
		private static bool s_lastAppliedInputBlockState;
		private static bool s_hasAppliedMouseBlockState;
		private static bool s_lastAppliedMouseBlockState;
		private static int s_lastMenuToggleFrame = -1;
		private static int s_selectedTab = 0;
		private static readonly GUIContent[] s_tabLabels = new GUIContent[]
		{
			new GUIContent("ESP"),
			new GUIContent("Automation"),
			new GUIContent("Inventory"),
			new GUIContent("Cooldowns"),
			new GUIContent("Gameplay"),
			new GUIContent("Risky / Debug"),
			new GUIContent("Auto Enabler"),
			new GUIContent("Improved Tooltips")
		};
		private static readonly Vector2[] s_tabScrollPositions = new Vector2[s_tabLabels.Length];
#if DEBUG
		public static bool debugToolsDropdown = false;
#endif

		public static void DrawModWindow(int windowID)
		{
			Drawing.DrawHudBackground(new Rect(2f, 28f, windowRect.width - 4f, windowRect.height - 30f));
			GUIStyle titleStyle = CooldownTrackerTheme.Label(20, FontStyle.Bold, TextAnchor.MiddleCenter);
			Drawing.OutlinedLabel(new Rect(0f, 1f, windowRect.width - 34f, 28f), "LEHUD", titleStyle);
			float titleWidth = titleStyle.CalcSize(new GUIContent("LEHUD")).x;
			float versionX = (windowRect.width - 34f) * 0.5f + titleWidth * 0.5f + 4f;
			Drawing.OutlinedLabel(new Rect(versionX, 5f, 80f, 24f), "v" + global::Mod.BuildInfo.Version, CooldownTrackerTheme.Label(11, FontStyle.Normal, TextAnchor.MiddleLeft));
			Drawing.DrawMoveIcon(new Rect(8f, 4f, 20f, 20f));
			GUILayout.BeginVertical();

			DrawTabBar();
			GUILayout.Space(4f);

			s_tabScrollPositions[s_selectedTab] = GUILayout.BeginScrollView(
				s_tabScrollPositions[s_selectedTab],
				GUILayout.ExpandHeight(true));

			switch (s_selectedTab)
			{
				case 0:
					DrawEspTab();
					break;
				case 1:
					DrawAutomationTab();
					break;
				case 2:
					DrawInventoryTab();
					break;
				case 3:
					DrawCooldownsTab();
					break;
				case 4:
					DrawGameplayTab();
					break;
				case 6:
					DrawAutoEnablerTab();
					break;
				case 7:
					DrawImprovedTooltipsTab();
					break;
				default:
					DrawRiskyAndDebugTab();
					break;
			}

			GUILayout.EndScrollView();

			GUILayout.EndVertical();

			Rect resizeGripRect = new Rect(
				windowRect.width - resizeGripSize, windowRect.height - resizeGripSize, resizeGripSize, resizeGripSize);
			Box(resizeGripRect, "");

			Rect closeRect = new Rect(windowRect.width - 30f, 1f, 24f, 22f);
			if (Drawing.OutlinedButton(closeRect, "X", CooldownTrackerTheme.Button(12, danger: true), FontStyle.Bold))
				ToggleMenu("Close button");

			DragWindow(new Rect(0, 0, windowRect.width - 34f, 24f));

			ProcessResizing(resizeGripRect, windowID);

			Event currentEvent = Event.current;
			if (currentEvent != null && currentEvent.isMouse &&
				new Rect(0f, 0f, windowRect.width, windowRect.height).Contains(currentEvent.mousePosition) &&
				currentEvent.type != EventType.Used)
				currentEvent.Use();
		}

		private static void DrawTabBar()
		{
			const int tabsPerRow = 4;
			for (int row = 0; row < (s_tabLabels.Length + tabsPerRow - 1) / tabsPerRow; row++)
			{
				GUILayout.BeginHorizontal();
				for (int col = 0; col < tabsPerRow; col++)
				{
					int i = row * tabsPerRow + col;
					if (i >= s_tabLabels.Length) break;
					bool isSelected = s_selectedTab == i;
					Color prevColor = GUI.color;
					GUI.color = Color.white;

					GUIStyle tabStyle = CooldownTrackerTheme.Button(13, selected: isSelected);
					bool pressed = GUILayout.Toggle(isSelected, GUIContent.none, tabStyle,
						GUILayout.Height(28f), GUILayout.ExpandWidth(true));
					Drawing.DrawButtonCaption(GUILayoutUtility.GetLastRect(), s_tabLabels[i].text, tabStyle, FontStyle.Bold);
					GUI.color = prevColor;

					if (pressed && !isSelected)
						s_selectedTab = i;
				}
				GUILayout.EndHorizontal();
			}
		}

		private static void DrawEspTab()
		{
			GUI.enabled = true;

			specialsSubDropdown = DrawSubmenuToggle(specialsSubDropdown, "Specials");
			if (specialsSubDropdown)
			{
					Settings.espShowLootLizards = DrawOptionToggle(Settings.espShowLootLizards, "Show Loot Lizards");
					Settings.espShowChampions = DrawOptionToggle(Settings.espShowChampions, "Show Champions");
					Settings.espShowOmens = DrawOptionToggle(Settings.espShowOmens, "Show Omens");
					Settings.espShowChests = DrawOptionToggle(Settings.espShowChests, "Show Chests");
					Settings.espShowShrines = DrawOptionToggle(Settings.espShowShrines, "Show Shrines");
					Settings.espShowRunePrisons = DrawOptionToggle(Settings.espShowRunePrisons, "Show Rune Prisons");

				GUILayout.Space(6f);
					Settings.showESPLines = DrawOptionToggle(Settings.showESPLines, "Show ESP Lines");
					Settings.showESPLabels = DrawOptionToggle(Settings.showESPLabels, "Show ESP Labels");

				Drawing.OutlinedLabel("Chest ESP Vertical Cull (m): " + Settings.espVerticalCullMeters.ToString("F0"));
				Settings.espVerticalCullMeters = GUILayout.HorizontalSlider(Settings.espVerticalCullMeters, 0f, 200f);
			}

			npcDrawingsDropdown = DrawSubmenuToggle(npcDrawingsDropdown, "NPC Alignment");
			if (npcDrawingsDropdown)
			{
				foreach (KeyValuePair<string, bool> entry in Settings.npcDrawings)
				{
					bool result = DrawOptionToggle(entry.Value, entry.Key);
					if (result != entry.Value)
					{
						Settings.npcDrawings[entry.Key] = result;
					}
				}
			}

			npcClassificationsDropdown = DrawSubmenuToggle(npcClassificationsDropdown, "NPC Rarity");
			if (npcClassificationsDropdown)
			{
				foreach (KeyValuePair<string, bool> entry in Settings.npcClassifications)
				{
					bool result = DrawOptionToggle(entry.Value, entry.Key);
					if (result != entry.Value)
					{
						Settings.npcClassifications[entry.Key] = result;
					}
				}
			}

			itemDrawingsDropdown = DrawSubmenuToggle(itemDrawingsDropdown, "Item Filters");
			if (itemDrawingsDropdown)
			{
				bool lootFilterEnabled = Settings.useLootFilter;
				Settings.useLootFilter = DrawOptionToggle(Settings.useLootFilter, "Use Loot Filter");
				if (lootFilterEnabled)
				{
					GUI.enabled = false;
				}

				foreach (KeyValuePair<string, bool> entry in Settings.itemDrawings)
				{
					if (!lootFilterEnabled)
					{
						bool result = DrawOptionToggle(entry.Value, entry.Key);
						if (result != entry.Value)
						{
							Settings.itemDrawings[entry.Key] = result;
						}
					}
				}

				GUI.enabled = true;
			}

			GUILayout.Space(6f);
			Drawing.OutlinedLabel("Draw Distance: " + Settings.drawDistance.ToString("F1"));
			Settings.drawDistance = GUILayout.HorizontalSlider(Settings.drawDistance, 0.0f, 300.0f);
		}

		private static void DrawAutomationTab()
		{
			GUI.enabled = true;

			Settings.useAutoPot = DrawOptionToggle(Settings.useAutoPot, "Auto HP Pot");
			if (Settings.useAutoPot)
			{
				Drawing.OutlinedLabel("Auto HP Pot Threshold %: " + Settings.autoHealthPotion.ToString("F1"));
				Settings.autoHealthPotion = GUILayout.HorizontalSlider(Settings.autoHealthPotion, 0.0f, 100.0f);

				Drawing.OutlinedLabel("Auto HP Pot Cooldown: " + Settings.autoPotionCooldown.ToString("F1") + "s");
				Settings.autoPotionCooldown = GUILayout.HorizontalSlider(Settings.autoPotionCooldown, 0.1f, 5.0f);
			}

			Settings.useAutoDisconnect = DrawOptionToggle(Settings.useAutoDisconnect, "Auto Disconnect on Low HP");
			if (Settings.useAutoDisconnect)
			{
				Drawing.OutlinedLabel("Auto Disconnect Threshold %: " + Settings.autoDisconnectHealthPercent.ToString("F1"));
				Settings.autoDisconnectHealthPercent = GUILayout.HorizontalSlider(Settings.autoDisconnectHealthPercent, 0.0f, 100.0f);

				Drawing.OutlinedLabel("Auto Disconnect Cooldown: " + Settings.autoDisconnectCooldownSeconds.ToString("F0") + "s");
				Settings.autoDisconnectCooldownSeconds = GUILayout.HorizontalSlider(Settings.autoDisconnectCooldownSeconds, 1f, 60f);

				Settings.autoDisconnectOnlyWhenNoPotions = DrawOptionToggle(Settings.autoDisconnectOnlyWhenNoPotions, "Only Disconnect When Out of Potions");
			}

			dpsMeterSubDropdown = DrawSubmenuToggle(dpsMeterSubDropdown, "DPS Meter");
			if (dpsMeterSubDropdown)
			{
				bool wasEnabled = Settings.enableDpsMeter;
				Settings.enableDpsMeter = DrawOptionToggle(Settings.enableDpsMeter, "Enable DPS Meter Overlay");
				Settings.enableDpsMeterOnlineRaw = DrawOptionToggle(
					Settings.enableDpsMeterOnlineRaw,
					"Allow Online Raw Source");
				Settings.dpsMeterPanelLocked = DrawOptionToggle(
					Settings.dpsMeterPanelLocked,
					"Lock DPS Panel Position/Size");
				Settings.enableDamageNumberDiagnostics = DrawOptionToggle(
					Settings.enableDamageNumberDiagnostics,
					"Enable DamageNumber Diagnostics (Verbose Logs)");
				if (wasEnabled && !Settings.enableDpsMeter)
				{
					DpsMeter.Reset();
				}

				if (Settings.enableDpsMeter)
				{
					Drawing.OutlinedLabel("DPS Window (s): " + Settings.dpsMeterWindowSeconds.ToString("F1"));
					Settings.dpsMeterWindowSeconds = GUILayout.HorizontalSlider(Settings.dpsMeterWindowSeconds, 1f, 20f);

					Settings.dpsMeterAutoReset = DrawOptionToggle(Settings.dpsMeterAutoReset, "Auto Reset After Inactivity");
					if (Settings.dpsMeterAutoReset)
					{
						Drawing.OutlinedLabel("Inactivity Reset (s): " + Settings.dpsMeterInactivityResetSeconds.ToString("F1"));
						Settings.dpsMeterInactivityResetSeconds = GUILayout.HorizontalSlider(Settings.dpsMeterInactivityResetSeconds, 2f, 60f);
					}

					if (LayoutButton("Reset DPS Stats"))
					{
						DpsMeter.Reset();
					}
					if (LayoutButton("Reset DPS Panel Layout"))
					{
						DpsMeter.ResetPanelLayout();
					}

					if (!ObjectManager.IsOfflineMode() && Settings.enableDpsMeterOnlineRaw)
					{
						GUILayout.Space(4f);
						Drawing.OutlinedLabel("Online Ownership Filter");
						if (LayoutButton("Filter Mode: " + DescribeDpsFilterMode(Settings.dpsMeterOnlineFilterMode)))
						{
							Settings.dpsMeterOnlineFilterMode = (Settings.dpsMeterOnlineFilterMode + 1) % 3;
						}

						Drawing.OutlinedLabel("Near Radius (incoming bias): " + Settings.dpsMeterNearPlayerMeters.ToString("F1") + "m");
						Settings.dpsMeterNearPlayerMeters = GUILayout.HorizontalSlider(Settings.dpsMeterNearPlayerMeters, 0.5f, 6f);

						float minFar = Mathf.Max(Settings.dpsMeterNearPlayerMeters + 0.2f, 0.7f);
						Drawing.OutlinedLabel("Far Radius (outgoing bias): " + Settings.dpsMeterFarPlayerMeters.ToString("F1") + "m");
						Settings.dpsMeterFarPlayerMeters = GUILayout.HorizontalSlider(Settings.dpsMeterFarPlayerMeters, minFar, 12f);

						Drawing.OutlinedLabel("HP Drop Correlation Window: " + Settings.dpsMeterHpDropCorrelationMs.ToString("F0") + "ms");
						Settings.dpsMeterHpDropCorrelationMs = GUILayout.HorizontalSlider(Settings.dpsMeterHpDropCorrelationMs, 50f, 1000f);
					}
				}

				if (!Settings.dpsMeterPanelLocked)
				{
					Drawing.OutlinedLabel("DPS panel unlocked: drag title to move, bottom-right grip to resize.");
				}
				if (!ObjectManager.IsOfflineMode() && !Settings.enableDpsMeterOnlineRaw)
				{
					Drawing.OutlinedLabel("Online meter disabled. Enable 'Online Raw Source' to collect from damage-number text.");
				}
				if (!ObjectManager.IsOfflineMode() && Settings.enableDpsMeterOnlineRaw)
				{
					Drawing.OutlinedLabel("Online Raw can be filtered by proximity + local HP-drop correlation.");
				}
				if (Settings.enableDamageNumberDiagnostics)
				{
					Drawing.OutlinedLabel("DamageNumber diagnostics are active. Check Melon logs for renderer summaries.");
				}
			}
		}

		private static void DrawInventoryTab()
		{
			GUI.enabled = true;
			Drawing.OutlinedLabel("Inventory buttons are added to the game's inventory panel.");
			Drawing.OutlinedLabel("Stash/vendor-from-anywhere features may conflict with online play; use at your own risk.");
			GUILayout.Space(6f);

			DrawInventoryToggle("Show STASH button", Prefs.ShowStash);
			DrawInventoryToggle("Show STASH ALL button", Prefs.ShowStashAll);
			DrawInventoryToggle("Show VENDOR button (off by default)", Prefs.ShowVendor);
			DrawInventoryToggle("Show Quick Teleport menu", Prefs.ShowTeleport);
			DrawInventoryToggle("Inventory debug logging", Prefs.DebugLog);
		}

		private static void DrawInventoryToggle(string label, MelonPreferences_Entry<bool> entry)
		{
			bool value = DrawOptionToggle(entry.Value, label);
			if (value == entry.Value)
				return;

			entry.Value = value;
			InventoryUi.ApplyVisibility();
			Prefs.Save();
		}

		private static bool LayoutButton(string label)
		{
			GUIStyle style = CooldownTrackerTheme.Button(11);
			Rect rect = GUILayoutUtility.GetRect(GUIContent.none, style, GUILayout.Height(24f), GUILayout.ExpandWidth(true));
			return Drawing.OutlinedButton(rect, label, style);
		}

		private static bool DrawSubmenuToggle(bool selected, string label)
		{
			GUIStyle style = CooldownTrackerTheme.Button(12, selected: selected);
			bool result = GUILayout.Toggle(selected, GUIContent.none, style, GUILayout.Height(26f));
			Drawing.DrawButtonCaption(GUILayoutUtility.GetLastRect(), label, style, FontStyle.Bold);
			return result;
		}

		private static bool DrawOptionToggle(bool selected, string label)
		{
			GUIStyle rowStyle = CooldownTrackerTheme.OptionRow(selected);
			Rect row = GUILayoutUtility.GetRect(GUIContent.none, rowStyle,
				GUILayout.ExpandWidth(true), GUILayout.Height(25f));
			if (GUI.Button(row, GUIContent.none, rowStyle))
				selected = !selected;

			CooldownTrackerTheme.Text9(new Rect(row.x + 9f, row.y, row.width - 68f, row.height),
				label, selected ? CooldownTrackerTheme.TextHi : CooldownTrackerTheme.Text, 11);
			Rect badge = new Rect(row.xMax - 51f, row.y + 3f, 42f, row.height - 6f);
			Drawing.OutlinedButton(badge, selected ? "ON" : "OFF", CooldownTrackerTheme.Button(9, selected: selected), FontStyle.Bold);
			return selected;
		}

		private static void DrawCooldownsTab()
		{
			GUI.enabled = true;
			float height = TrackerRuntime.SettingsContentHeight;
			Rect hostRect = GUILayoutUtility.GetRect(
				Mathf.Max(320f, windowRect.width - 28f), height, GUILayout.ExpandWidth(true));
			TrackerRuntime.DrawSettingsTab(hostRect);
		}

		private static void DrawAutoEnablerTab()
		{
			GUI.enabled = true;
			bool previousShowRings = FallenAutoEnabler.ShowRings.Value;
			FallenAutoEnabler.ShowRings.Value = DrawOptionToggle(previousShowRings, "Show proximity rings");

			float previousDistance = FallenAutoEnabler.Distance.Value;
			Drawing.OutlinedLabel($"Activation radius: {previousDistance:F1} m");
			FallenAutoEnabler.Distance.Value = GUILayout.HorizontalSlider(previousDistance, 1f, 10f);

			GUILayout.Space(6f);
			Drawing.OutlinedLabel("Proximity ring color");
			Color previousColor = FallenAutoEnabler.RingColor.Value;
			Color color = previousColor;
			color.r = DrawColorChannel("R", color.r);
			color.g = DrawColorChannel("G", color.g);
			color.b = DrawColorChannel("B", color.b);
			color.a = DrawColorChannel("Opacity", color.a);
			FallenAutoEnabler.RingColor.Value = color;

			GUILayout.Space(6f);
			Rect filterHeader = GUILayoutUtility.GetRect(0f, 20f, GUILayout.ExpandWidth(true));
			CooldownTrackerTheme.Text9(filterHeader, "AUTO-ACTIVATION FILTERS", CooldownTrackerTheme.Accent, 10, FontStyle.Bold);
			foreach (var entry in FallenAutoEnabler.TypeOptions)
				entry.Value.Value = DrawOptionToggle(entry.Value.Value, $"Auto-activate {entry.Key}");
			FallenAutoEnabler.DebugLog.Value = DrawOptionToggle(FallenAutoEnabler.DebugLog.Value, "Auto Enabler debug logging");
			Drawing.OutlinedLabel($"Tracked interactables: {FallenAutoEnabler.TrackedCount}");
			Drawing.OutlinedLabel(FallenAutoEnabler.LastEvent);

			if (previousShowRings != FallenAutoEnabler.ShowRings.Value ||
				Mathf.Abs(previousDistance - FallenAutoEnabler.Distance.Value) > 0.001f || color != previousColor)
				FallenAutoEnabler.ApplySettings();
		}

		private static float DrawColorChannel(string label, float value)
		{
			GUILayout.BeginHorizontal();
			Drawing.OutlinedLabel(label, GUILayout.Width(64f));
			float next = GUILayout.HorizontalSlider(value, 0f, 1f);
			Drawing.OutlinedLabel(next.ToString("F2"), GUILayout.Width(36f));
			GUILayout.EndHorizontal();
			return next;
		}

		private static void DrawImprovedTooltipsTab()
		{
			GUI.enabled = true;
			Drawing.DrawHeading("Tooltip Provider");
			bool useTerrible = FallenImprovedTooltips.UseTerribleTooltips;
			bool fallenClicked = DrawOptionToggle(!useTerrible, "Fallen's Improved Tooltips") != !useTerrible;
			bool terribleClicked = DrawOptionToggle(useTerrible, "MedicK's Terrible Tooltips") != useTerrible;
			if (fallenClicked)
				useTerrible = false;
			else if (terribleClicked)
				useTerrible = true;
			if (useTerrible != FallenImprovedTooltips.UseTerribleTooltips)
			{
				FallenImprovedTooltips.UseTerribleTooltipsEntry!.Value = useTerrible;
				FallenImprovedTooltips.Save();
			}			GUILayout.Space(8f);

			if (useTerrible)
				DrawTerribleTooltipsOptions();
			else
				DrawFallenTooltipsOptions();
		}

		private static void DrawFallenTooltipsOptions()
		{
			Drawing.DrawHeading("Fallen's Improved Tooltips");
			if (FallenImprovedTooltips.KgImprovementsLoaded)
				Drawing.OutlinedLabel("Ground-label name and LP additions are disabled while kg_LastEpoch_Improvements is loaded.");

			GUI.enabled = !FallenImprovedTooltips.KgImprovementsLoaded;
			FallenImprovedTooltips.ShowFullItemName!.Value = DrawOptionToggle(
				FallenImprovedTooltips.ShowFullItemName.Value, "Use full item names on ground labels");
			FallenImprovedTooltips.ShowLegendaryPotential!.Value = DrawOptionToggle(
				FallenImprovedTooltips.ShowLegendaryPotential.Value, "Show LP / Weaver's Will on ground labels");
			GUI.enabled = true;
			FallenImprovedTooltips.CompareStashItems!.Value = DrawOptionToggle(
				FallenImprovedTooltips.CompareStashItems.Value, "Compare LP / Weaver's Will with stash copies");
		}

		private static void DrawEnumCycle<T>(string title, MelonPreferences_Entry<T> entry) where T : struct, System.Enum
		{
			if (LayoutButton($"{title}: {entry.Value}"))
			{
				var values = (T[])System.Enum.GetValues(typeof(T));
				int index = System.Array.IndexOf(values, entry.Value);
				entry.Value = values[(index + 1) % values.Length];
			}
		}

		private static void DrawTerribleTooltipsOptions()
		{
			Drawing.DrawHeading("MedicK's Terrible Tooltips");
			Drawing.OutlinedLabel("Tier / grade colouring on tooltips and ground labels. Hold Alt while hovering to see full affix detail.");
			TtPrefs.EnableTooltips.Value = DrawOptionToggle(TtPrefs.EnableTooltips.Value, "Tier / grade tooltip colours");
			TtPrefs.TooltipTierColors.Value = DrawOptionToggle(TtPrefs.TooltipTierColors.Value, "Colour affixes by tier");
			TtPrefs.TooltipRankColors.Value = DrawOptionToggle(TtPrefs.TooltipRankColors.Value, "Colour grade letters by roll quality");
			TtPrefs.ShowGradeLetters.Value = DrawOptionToggle(TtPrefs.ShowGradeLetters.Value, "Show grade letters (S/A/B/C/F)");
			TtPrefs.AlwaysShowRanges.Value = DrawOptionToggle(TtPrefs.AlwaysShowRanges.Value, "Always show affix ranges");
			TtPrefs.AlwaysShowTierDetails.Value = DrawOptionToggle(TtPrefs.AlwaysShowTierDetails.Value, "Always show tier details");
			TtPrefs.UnitBorder.Value = DrawOptionToggle(TtPrefs.UnitBorder.Value, "Border around the tier / grade unit");
			DrawEnumCycle("Layout", TtPrefs.Layout);
			DrawEnumCycle("Signal style", TtPrefs.Style);
			DrawEnumCycle("Affix name colour", TtPrefs.NameColorMode);
			DrawEnumCycle("Tier word", TtPrefs.TierWord);
			DrawEnumCycle("Unit separator", TtPrefs.UnitSeparator);

			GUILayout.Space(6f);
			Drawing.DrawHeading("Ground Labels");
			DrawEnumCycle("Label style", TtPrefs.LabelStyle);
			TtPrefs.LabelFilterOnly.Value = DrawOptionToggle(TtPrefs.LabelFilterOnly.Value, "Only on loot-filter highlighted items");
			TtPrefs.LabelAltKey.Value = DrawOptionToggle(TtPrefs.LabelAltKey.Value, "Hold Alt to show brackets");
			DrawEnumCycle("Filter rule # on tooltip", TtPrefs.ShowFilterRuleNumber);
			DrawEnumCycle("Rule # position on label", TtPrefs.LabelRulePosition);
		}
		private static void DrawGameplayTab()
		{
			GUI.enabled = true;

			bool previousRemoveFog = Settings.removeFog;
			Settings.removeFog = DrawOptionToggle(Settings.removeFog, "Remove Fog");
			if (Settings.removeFog != previousRemoveFog)
			{
				GameMods.FogRemover();
			}

			Settings.cameraZoomUnlock = DrawOptionToggle(Settings.cameraZoomUnlock, "Camera Zoom Unlock");
			Settings.minimapZoomUnlock = DrawOptionToggle(Settings.minimapZoomUnlock, "Minimap Zoom Unlock");
			Settings.mapHack = DrawOptionToggle(Settings.mapHack, "Map Hack (Boost RevealRadius 14,000%)");

			bool previousPlayerLantern = Settings.playerLantern;
			Settings.playerLantern = DrawOptionToggle(Settings.playerLantern, "Player Lantern");
			if (Settings.playerLantern != previousPlayerLantern)
			{
				GameMods.playerLantern();
			}

			Settings.blockMenuInputWhenOpen = DrawOptionToggle(
				Settings.blockMenuInputWhenOpen,
				"Block Game Input While Menu Open (Keyboard + Mouse)");

			GUILayout.Space(10f);

			Drawing.DrawHeading("Radar Monster Type Filters");
			Settings.showWhiteMonsters = DrawOptionToggle(Settings.showWhiteMonsters, "Show White Monsters");
			Settings.showMagicMonsters = DrawOptionToggle(Settings.showMagicMonsters, "Show Magic Monsters");
			Settings.showRareMonsters = DrawOptionToggle(Settings.showRareMonsters, "Show Rare Monsters");
			Settings.showUniqueMonsters = DrawOptionToggle(Settings.showUniqueMonsters, "Show Unique Monsters");
			Settings.showBossMonsters = DrawOptionToggle(Settings.showBossMonsters, "Show Boss Monsters");

			GUILayout.Space(6f);
			Drawing.OutlinedLabel("Marker size and color (#RRGGBB)");
			DrawRarityMarkerStyle("Normal", ref Settings.minimapNormalCircleSize, ref Settings.minimapNormalCircleColor);
			DrawRarityMarkerStyle("Magic", ref Settings.minimapMagicCircleSize, ref Settings.minimapMagicCircleColor);
			DrawRarityMarkerStyle("Rare", ref Settings.minimapRareCircleSize, ref Settings.minimapRareCircleColor);
			DrawRarityMarkerStyle("Unique", ref Settings.minimapUniqueCircleSize, ref Settings.minimapUniqueCircleColor);
			DrawRarityMarkerStyle("Boss", ref Settings.minimapBossCircleSize, ref Settings.minimapBossCircleColor);
			Drawing.OutlinedLabel("Marker opacity: " + Settings.minimapCircleOpacity.ToString("F2"));
			Settings.minimapCircleOpacity = GUILayout.HorizontalSlider(Settings.minimapCircleOpacity, 0.1f, 1f);

			GUILayout.Space(6f);
			Drawing.OutlinedLabel("Radar status: " + MinimapEnemyCircles.lastDebugInfo);
			Drawing.OutlinedLabel("Fullscreen scale correction: " + Settings.minimapFullscreenScaleCorrection.ToString("F3"));
			Settings.minimapFullscreenScaleCorrection = GUILayout.HorizontalSlider(
				Settings.minimapFullscreenScaleCorrection, 0.01f, 1f);
			Drawing.OutlinedLabel("Fullscreen radar offset X: " + Settings.minimapFullscreenOffsetX.ToString("F0"));
			Settings.minimapFullscreenOffsetX = GUILayout.HorizontalSlider(
				Settings.minimapFullscreenOffsetX, -500f, 500f);
			Drawing.OutlinedLabel("Fullscreen radar offset Y: " + Settings.minimapFullscreenOffsetY.ToString("F0"));
			Settings.minimapFullscreenOffsetY = GUILayout.HorizontalSlider(
				Settings.minimapFullscreenOffsetY, -500f, 500f);
		}

		private static void DrawRarityMarkerStyle(string label, ref float size, ref string colorHex)
		{
			GUILayout.BeginHorizontal();
			Drawing.OutlinedLabel(label, GUILayout.Width(54f));
			Drawing.OutlinedLabel(size.ToString("F0"), GUILayout.Width(22f));
			size = GUILayout.HorizontalSlider(size, 2f, 20f, GUILayout.Width(96f));

			Color swatch = Color.white;
			if (ColorUtility.TryParseHtmlString("#" + colorHex, out Color parsed)) swatch = parsed;
			Color previous = GUI.color;
			GUI.color = swatch;
			GUILayout.Box(GUIContent.none, GUILayout.Width(18f), GUILayout.Height(18f));
			GUI.color = previous;
			Drawing.OutlinedLabel("#", GUILayout.Width(10f));
			string edited = GUILayout.TextField(colorHex, 6, GUILayout.Width(58f));
			colorHex = FilterHexColor(edited);
			GUILayout.EndHorizontal();
		}

		private static string FilterHexColor(string value)
		{
			var result = new System.Text.StringBuilder(6);
			foreach (char c in value)
			{
				if ((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F'))
					result.Append(char.ToUpperInvariant(c));
				if (result.Length == 6) break;
			}
			return result.ToString();
		}

		private static void DrawRiskyAndDebugTab()
		{
			GUI.enabled = true;

			Drawing.OutlinedLabel("These options are provided at your own risk.");
			GUILayout.Space(6f);

			Drawing.OutlinedLabel("TimeScale: " + Settings.timeScale.ToString("F1"));
			Settings.timeScale = GUILayout.HorizontalSlider(Settings.timeScale, 0.1f, 6.0f);

			GUILayout.Space(6f);
			if (!ObjectManager.IsOfflineMode())
			{
				Drawing.OutlinedLabel("Allow Any Waypoint: unavailable in online mode");
			}
			else
			{
				Settings.useAnyWaypoint = DrawOptionToggle(Settings.useAnyWaypoint, "Allow Any Waypoint");
			}

			GUILayout.Space(10f);
			antiIdleSubDropdown = DrawSubmenuToggle(antiIdleSubDropdown, "Anti-Idle");
			if (antiIdleSubDropdown)
			{
				Settings.useSimpleAntiIdle = DrawOptionToggle(Settings.useSimpleAntiIdle, "Enable Anti-Idle");
				if (Settings.useSimpleAntiIdle)
				{
					Drawing.OutlinedLabel("Pulse Interval (s): " + Settings.simpleAntiIdleInterval.ToString("F0"));
					Settings.simpleAntiIdleInterval = GUILayout.HorizontalSlider(Settings.simpleAntiIdleInterval, 60f, 900f);
					Settings.forceIsIdleFalseFallback = DrawOptionToggle(
						Settings.forceIsIdleFalseFallback,
						"Force IsIdle FALSE Fallback (high risk)");

					Settings.suppressKeepAliveOnActivity = DrawOptionToggle(Settings.suppressKeepAliveOnActivity, "Suppress When Actively Playing");
					if (Settings.suppressKeepAliveOnActivity)
					{
						Drawing.OutlinedLabel("Activity Suppression (s): " + Settings.activitySuppressionSeconds.ToString("F0"));
						Settings.activitySuppressionSeconds = GUILayout.HorizontalSlider(Settings.activitySuppressionSeconds, 5f, 300f);

						Drawing.OutlinedLabel("Scene Change Suppression (s): " + (Settings.sceneChangeSuppressionSeconds <= 0f ? "Disabled" : Settings.sceneChangeSuppressionSeconds.ToString("F0")));
						Settings.sceneChangeSuppressionSeconds = GUILayout.HorizontalSlider(Settings.sceneChangeSuppressionSeconds, 0f, 300f);
					}
				}
			}

#if DEBUG
			GUILayout.Space(10f);
			DrawDebugToolsSection();
#endif
		}

#if DEBUG
		private static void DrawDebugToolsSection()
		{
			debugToolsDropdown = DrawSubmenuToggle(debugToolsDropdown, "DEBUG Tools");
			if (!debugToolsDropdown)
			{
				return;
			}

			Settings.enableNetworkDiagnostics = DrawOptionToggle(Settings.enableNetworkDiagnostics, "Enable Network Diagnostics (Verbose)");
			if (Settings.enableNetworkDiagnostics)
			{
				Drawing.OutlinedLabel("Captures deep ClientNetworkService breadcrumbs during connect/load troubleshooting.");
			}

			Settings.debugEnableDiagnostics = DrawOptionToggle(Settings.debugEnableDiagnostics, "Enable Diagnostics");
			if (!Settings.debugEnableDiagnostics)
			{
				return;
			}

			Settings.debugShowLocalPlayerPanel = DrawOptionToggle(Settings.debugShowLocalPlayerPanel, "Show Local Player Panel");
			Settings.debugShowLocalPlayerWorldLabel = DrawOptionToggle(Settings.debugShowLocalPlayerWorldLabel, "Show Local Player World Label");
			Settings.debugDrawAllManagerActors = DrawOptionToggle(Settings.debugDrawAllManagerActors, "Draw All ActorManager Actors (No Sorting)");
			Settings.debugDrawAllGroundItems = DrawOptionToggle(Settings.debugDrawAllGroundItems, "Draw All GroundItemVisuals");
			Settings.debugDrawAllGroundGold = DrawOptionToggle(Settings.debugDrawAllGroundGold, "Draw All GroundGoldVisuals");
			Settings.debugDrawManagerLines = DrawOptionToggle(Settings.debugDrawManagerLines, "Draw Debug Lines To Targets");
			Settings.debugIgnoreDistanceCulling = DrawOptionToggle(Settings.debugIgnoreDistanceCulling, "Ignore Draw Distance Culling");

			Drawing.OutlinedLabel("Debug Max Entries/System: " + Settings.debugMaxEntriesPerSystem.ToString());
			var debugMax = GUILayout.HorizontalSlider(Settings.debugMaxEntriesPerSystem, 10f, 500f);
			Settings.debugMaxEntriesPerSystem = Mathf.RoundToInt(debugMax);
		}
#endif

		private static void ProcessResizing(Rect resizeGripRect, int windowID)
		{
			Event currentEvent = Event.current;
			switch (currentEvent.type)
			{
				case EventType.MouseDown:
					// Check if the mouse is within the resize grip area
					if (resizeGripRect.Contains(currentEvent.mousePosition))
					{
						currentEvent.Use(); // Mark the event as used
						isResizing = true; // Set a flag indicating that we're resizing
					}
					break;

				case EventType.MouseUp:
					isResizing = false; // Clear the resizing flag on mouse up
					break;

				case EventType.MouseDrag:
					if (isResizing)
					{
						// Directly adjust windowRect for resizing
						windowRect.width += currentEvent.delta.x;
						windowRect.height += currentEvent.delta.y;
						// Enforce minimum size constraints
						windowRect.width = Mathf.Max(windowRect.width, 320);
						windowRect.height = Mathf.Max(windowRect.height, 200);
						currentEvent.Use();
					}
					break;
			}
		}

		private static string DescribeDpsFilterMode(int mode)
		{
			return mode switch
			{
				1 => "Likely Outgoing",
				2 => "Likely Incoming",
				_ => "All Visible"
			};
		}

		public static Rect windowRect = new Rect(20, 20, 500, 700);
		public static bool IsCooldownsTabActive => guiVisible && s_selectedTab == 3;
		public static float CooldownTabScrollOffset => s_tabScrollPositions[3].y;

		public static void OnGUI()
		{
			var currentEvent = Event.current;
			if (currentEvent.type == EventType.KeyDown
				&& (currentEvent.keyCode == KeyCode.Insert || currentEvent.keyCode == KeyCode.F10))
			{
				var key = currentEvent.keyCode;
				currentEvent.Use();
				ToggleMenu(key.ToString());
			}

			if (guiVisible)
			{
				GUISkin previousSkin = GUI.skin;
				try
				{
					CooldownTrackerTheme.ApplyHudSkin();
					windowRect = GUI.Window(0, windowRect, (WindowFunction)DrawModWindow, string.Empty);
				}
				finally
				{
					GUI.skin = previousSkin;
				}
			}
		}

		public static void OnUpdate()
		{
			if (Input.GetKeyDown(KeyCode.Insert))
			{
				ToggleMenu("Insert");
			}
		else if (Input.GetKeyDown(KeyCode.F10))
			{
				ToggleMenu("F10");
			}

			// Optional input-blocking: blocks gameplay keyboard + mouse while menu is visible.
			bool shouldBlockGameInput = (Settings.blockMenuInputWhenOpen && guiVisible) ||
				TrackerRuntime.ShouldBlockGameInput;
			if (!s_hasAppliedInputBlockState || s_lastAppliedInputBlockState != shouldBlockGameInput)
			{
				EpochInputManagerBridge.TrySetMenuInputBlocked(shouldBlockGameInput);
				s_lastAppliedInputBlockState = shouldBlockGameInput;
				s_hasAppliedInputBlockState = true;
			}

			bool pointerOverHud = IsPointerOverHud();
			bool shouldBlockMouse = shouldBlockGameInput || pointerOverHud;
			if (pointerOverHud || !s_hasAppliedMouseBlockState || s_lastAppliedMouseBlockState != shouldBlockMouse)
			{
				EpochInputManagerBridge.TrySetMouseInputBlocked(shouldBlockMouse, force: pointerOverHud);
				s_lastAppliedMouseBlockState = shouldBlockMouse;
				s_hasAppliedMouseBlockState = true;
			}

			// Debug key for auto-potion system (F12)
			if (Input.GetKeyDown(KeyCode.F12))
			{
				AutoPotion.LogDebugInfo();
			}

#if DEBUG
			// Debug key for actor/local-player correlation diagnostics (F11)
			if (Input.GetKeyDown(KeyCode.F11))
			{
				DebugDiagnostics.LogCorrelationSnapshot();
			}
#endif
		}

		private static bool IsPointerOverHud()
		{
			if (DpsMeter.IsPointerOverPanel()) return true;
			if (!guiVisible) return false;
			Vector3 mouse = Input.mousePosition;
			Vector2 guiMouse = new Vector2(mouse.x, Screen.height - mouse.y);
			return windowRect.Contains(guiMouse);
		}

		private static void ToggleMenu(string inputSource)
		{
			if (s_lastMenuToggleFrame == Time.frameCount)
				return;

			s_lastMenuToggleFrame = Time.frameCount;
			guiVisible = !guiVisible;
			MelonLogger.Msg($"[LEHud] Menu {(guiVisible ? "opened" : "closed")} via {inputSource}.");
			if (guiVisible)
			{
				AntiIdleSystem.OnMenuOpened();
				return;
			}

			SettingsConfig.ApplyToPreferencesFromSettings();
			SettingsConfig.Save();
			TrackerRuntime.Save();
			FallenAutoEnabler.Save();
			FallenImprovedTooltips.Save();
			global::Mod.Cheats.TerribleTooltips.TerribleTooltipsRuntime.Save();
			MelonLogger.Msg("[LEHud] Preferences Saved!");
			AntiIdleSystem.OnMenuClosed();
		}
	}
}
