using MelonLoader;
using Mod.Cheats;
using Mod.Cheats.ESP;
using Mod.Cheats.Inventory;
using CooldownTrackerRuntime = Mod.Cheats.CooldownTracker.CooldownTracker;
using FallenAutoEnabler = Mod.Cheats.FallenPlugins.AutoEnabler;
using FallenImprovedTooltips = Mod.Cheats.FallenPlugins.ImprovedTooltips;
using TerribleTooltipsRuntime = Mod.Cheats.TerribleTooltips.TerribleTooltipsRuntime;
using Mod.Game;
using System.Reflection;
using HarmonyLib;
using Il2CppLidgren.Network;
using Il2CppSystem.Net;
using Mod.Utils;

[assembly: MelonInfo(typeof(Mod.Mod), "LEHud", "0.6.0", "Wolfis")]
[assembly: MelonGame("Eleventh Hour Games", "Last Epoch")]

namespace Mod;

	public static class BuildInfo
	{
		public const string Name = "LEHud"; // Name of the Mod.  (MUST BE SET)
		public const string Description = "Hud mod for Last Epoch"; // Description for the Mod.  (Set as null if none)
		public const string Author = "Wolfis"; // Author of the Mod.  (MUST BE SET)
		public const string Company = null; // Company that made the Mod.  (Set as null if none)
		public const string Version = "0.6.0"; // Version of the Mod.  (MUST BE SET)
		public const string DownloadLink = null; // Download Link for the Mod.  (Set as null if none)
	}

	public class Mod : MelonMod
	{
		private static bool isOnGUI = false;
		private static bool s_timeScaleWasApplied;
		private const string HarmonyId = "LEHud.Patches";
		private static HarmonyLib.Harmony? s_harmony;

		public override void OnInitializeMelon()
		{
			try
			{
				// Initialize preferences and load into Settings before applying patches
				SettingsConfig.Init();
				SettingsConfig.LoadIntoSettings();
				Prefs.Init();
				CooldownTrackerRuntime.Initialize();
				FallenAutoEnabler.Initialize();
				FallenImprovedTooltips.Initialize();
				TerribleTooltipsRuntime.Initialize();
				MapHack.InitializeSceneFallback();

				s_harmony = new HarmonyLib.Harmony(HarmonyId);
				PatchAllIsolatingTerribleTooltips(s_harmony);
				MelonLogger.Msg("[LEHud] Harmony patches applied");
				VerifyNetworkingTargets();
			}
			catch (System.Exception e)
			{
				MelonLogger.Error($"[LEHud] Harmony init failed: {e.Message}");
			}
		}

		// Terrible Tooltips patches are applied one class at a time so a game update that breaks one of
		// its signatures degrades that feature only, instead of aborting every LEHud patch.
		private static void PatchAllIsolatingTerribleTooltips(HarmonyLib.Harmony harmony)
		{
			foreach (Type type in HarmonyLib.AccessTools.GetTypesFromAssembly(typeof(Mod).Assembly))
			{
				if (type.Namespace != "Mod.Cheats.TerribleTooltips" && !(type.DeclaringType?.Namespace == "Mod.Cheats.TerribleTooltips"))
				{
					harmony.CreateClassProcessor(type).Patch();
					continue;
				}

				try
				{
					harmony.CreateClassProcessor(type).Patch();
				}
				catch (Exception e)
				{
					MelonLogger.Warning($"[LEHud] Terrible Tooltips feature '{type.Name}' disabled: {e.Message}");
				}
			}
		}

		public override void OnLateInitializeMelon() // Runs after OnApplicationStart.
		{
			//MelonLogger.Msg("OnApplicationLateStart");
			Drawing.Initialize();
		}

		public override void OnSceneWasLoaded(int buildindex, string sceneName) // Runs when a Scene has Loaded and is passed the Scene's Build Index and Name.
		{
			//MelonLogger.Msg("OnSceneWasLoaded: " + buildindex.ToString() + " | " + sceneName); // occurs before scene init
			AutoDisconnect.OnSceneChanged();
			DpsMeter.OnSceneChanged();
			InventoryUi.ResetStashAllGuard();
			TravelService.NotifySceneLoaded();
			GameMods.FogRemover();
		}

		public override void OnSceneWasInitialized(int buildindex, string sceneName) // Runs when a Scene has Initialized and is passed the Scene's Build Index and Name.
		{
			CooldownTrackerRuntime.OnSceneInitialized();
			//MelonLogger.Msg("OnSceneWasInitialized: " + buildindex.ToString() + " | " + sceneName);

			//foreach (MethodInfo mi in typeof(UnityEngine.Physics)
			//	.GetMethods(BindingFlags.Public | BindingFlags.Static))
			//{
			//	MelonLogger.Msg($"Method: {mi.Name} ({string.Join(", ", mi.GetParameters().Select(p => p.ParameterType.Name))})");
			//}

			try
			{
				ObjectManager.OnSceneLoaded();
				MapHack.OnSceneWasInitialized();
				GameMods.FogRemover();
				GameMods.playerLantern();

				// Inform AntiIdleSystem to suppress synthetic keepalive briefly after scene load
				AntiIdleSystem.OnSceneChanged();
				AutoDisconnect.OnSceneChanged();
				Shrines.OnSceneChanged();
				RunePrisons.OnSceneChanged();
				Chests.OnSceneChanged();
				Items.OnSceneChanged();
				SpecialEntityEspHelper.OnSceneChanged();
				MinimapEnemyCircles.OnSceneChanged();
				DamageNumberDiagnostics.OnSceneChanged();
				TravelService.EnsurePrimed();
				FallenAutoEnabler.OnSceneInitialized();
			}
			catch (System.Exception e)
			{
				MelonLogger.Error(e.ToString());
			}
		}

		public override void OnSceneWasUnloaded(int buildIndex, string sceneName) // Runs when a Scene has Unloaded and is passed the Scene's Build Index and Name.
		{
			//MelonLogger.Msg("OnSceneWasUnloaded: " + buildIndex.ToString() + " | " + sceneName);
		}

		public override void OnUpdate() // Runs once per frame.
		{
			bool hasPlayer;
			try
			{
				hasPlayer = ObjectManager.HasPlayer();
			}
			catch (Exception e)
			{
				hasPlayer = false;
				Log.ErrorThrottled(LogSource.LEHud, "update:player-check", $"Player-state check failed: {e}", TimeSpan.FromSeconds(5));
			}
			if (hasPlayer)
			{
				RunUpdateSafely("MapHack", hasPlayer, MapHack.OnUpdate);
				RunUpdateSafely("ESP", ESP.OnUpdate);
				RunUpdateSafely("AutoPotion", AutoPotion.OnUpdate);
				RunUpdateSafely("MinimapEnemyCircles", MinimapEnemyCircles.Update);
				RunUpdateSafely("AutoDisconnect", AutoDisconnect.OnUpdate);
			}
			else
			{
				RunUpdateSafely("MapHack", hasPlayer, MapHack.OnUpdate);
				// Keep overlays clean when no world/player context is available.
				RunUpdateSafely("ESP.Clear", ESP.Clear);
				RunUpdateSafely("MinimapEnemyCircles.Clear", MinimapEnemyCircles.ClearCircles);
			}

			RunUpdateSafely("Menu", Menu.OnUpdate);
			RunUpdateSafely("TravelService", TravelService.Update);
			RunUpdateSafely("AutoEnabler", FallenAutoEnabler.Update);
			RunUpdateSafely("CooldownTracker", CooldownTrackerRuntime.Update);
			RunUpdateSafely("DpsMeter", DpsMeter.OnUpdate);
			RunUpdateSafely("TerribleTooltips", TerribleTooltipsRuntime.OnUpdate);
			RunUpdateSafely("AntiIdleSystem", AntiIdleSystem.OnUpdate);

			try
			{
				if (Settings.timeScale != 1.0f)
				{
					UnityEngine.Time.timeScale = Settings.timeScale;
					s_timeScaleWasApplied = true;
				}
				else if (s_timeScaleWasApplied)
				{
					UnityEngine.Time.timeScale = 1.0f;
					s_timeScaleWasApplied = false;
				}
			}
			catch (Exception e)
			{
				Log.ErrorThrottled(LogSource.LEHud, "timescale-update", $"Time-scale update failed: {e.Message}", TimeSpan.FromSeconds(5));
			}
		}

		private static void RunUpdateSafely(string feature, Action update)
		{
			try
			{
				update();
			}
			catch (Exception e)
			{
				Log.ErrorThrottled(LogSource.LEHud, $"update:{feature}", $"{feature} update failed: {e}", TimeSpan.FromSeconds(5));
			}
		}

		private static void RunUpdateSafely<T>(string feature, T context, Action<T> update)
		{
			try
			{
				update(context);
			}
			catch (Exception e)
			{
				Log.ErrorThrottled(LogSource.LEHud, $"update:{feature}", $"{feature} update failed: {e}", TimeSpan.FromSeconds(5));
			}
		}

		public override void OnFixedUpdate() // Can run multiple times per frame. Mostly used for Physics.
		{
			//MelonLogger.Msg("OnFixedUpdate");
		}

		public override void OnLateUpdate() // Runs once per frame after OnUpdate and OnFixedUpdate have finished.
		{
			InventoryUi.KeepAlive();
			RunUpdateSafely("TerribleTooltips.Late", TerribleTooltipsRuntime.OnLateUpdate);
		}

		public override void OnGUI() // Can run multiple times per frame. Mostly used for Unity's IMGUI.
		{
			if (isOnGUI) return;
			isOnGUI = true;

			try
			{
				Drawing.SetupGuiStyle();
				CooldownTrackerRuntime.DrawOverlay();
				Menu.OnGUI();
				ESP.OnGUI();
				DpsMeter.OnGUI();
#if DEBUG
				DebugDiagnostics.OnGUI();
#endif
			}
			catch (System.Exception e)
			{
				MelonLogger.Error(e.ToString());
			}

			isOnGUI = false;
		}

		// Shutdown does only managed saves. Destroying Unity objects and unpatching Harmony
		// detours while the game is tearing down native/Il2Cpp state crashes the process;
		// the OS reclaims everything on exit anyway.
		public override void OnApplicationQuit()
		{
			Quit("CooldownTracker", CooldownTrackerRuntime.Save);
			Quit("AutoEnabler", FallenAutoEnabler.Save);
			Quit("ImprovedTooltips", FallenImprovedTooltips.Save);
			Quit("TerribleTooltips", TerribleTooltipsRuntime.Save);
			Quit("Settings", () =>
			{
				SettingsConfig.ApplyToPreferencesFromSettings();
				SettingsConfig.Save();
				Prefs.Save();
			});
			s_timeScaleWasApplied = false;
			MelonLogger.Msg("[LEHud] Shutdown saves complete");
		}

		static void Quit(string what, Action action)
		{
			try { action(); }
			catch (Exception e) { MelonLogger.Warning($"[LEHud] Quit save '{what}' failed: {e.Message}"); }
		}
		public override void OnPreferencesSaved() // Runs when Melon Preferences get saved.
		{
			//MelonLogger.Msg("OnPreferencesSaved");
		}

		public override void OnPreferencesLoaded() // Runs when Melon Preferences get loaded.
		{
			try
			{
				if (!SettingsConfig.IsInitialized)
					return;
				SettingsConfig.LoadIntoSettings();
				MelonLogger.Msg("[LEHud] Preferences loaded into Settings");
			}
			catch (Exception e)
			{
				MelonLogger.Error($"[LEHud] Preferences load error: {e.Message}");
			}
		}

		private static void VerifyNetworkingTargets()
		{
			try
			{
				var nmcType = typeof(NetMultiClient);
				var npType = typeof(NetPeer);
				MelonLogger.Msg($"[LEHud] Verify: NetMultiClient type = {nmcType.Name}, NetPeer type = {npType.Name}");

				var connect = AccessTools.Method(nmcType, "Connect", new[] { typeof(IPEndPoint), typeof(NetOutgoingMessage) });
				MelonLogger.Msg($"[LEHud] Verify: NetMultiClient.Connect found = {connect != null}");

				var disconnect = AccessTools.Method(nmcType, "Disconnect", new[] { typeof(string) });
				MelonLogger.Msg($"[LEHud] Verify: NetMultiClient.Disconnect found = {disconnect != null}");

				var sendMessage = AccessTools.Method(nmcType, "SendMessage", new[] { typeof(NetOutgoingMessage), typeof(NetDeliveryMethod), typeof(int) });
				MelonLogger.Msg($"[LEHud] Verify: NetMultiClient.SendMessage found = {sendMessage != null}");

				var getConnStatus = AccessTools.Method(nmcType, "get_ConnectionStatus");
				MelonLogger.Msg($"[LEHud] Verify: NetMultiClient.get_ConnectionStatus found = {getConnStatus != null}");

				var heartbeatField = AccessTools.Field(npType, "m_lastHeartbeat");
				MelonLogger.Msg($"[LEHud] Verify: NetPeer.m_lastHeartbeat field found = {heartbeatField != null}");

				// NetTime verification
				try
				{
					var netTimeType = TypeLookup.FindType("Il2CppLidgren.Network.NetTime", "Lidgren.Network.NetTime");
					if (netTimeType != null)
					{
						var nowProp = netTimeType.GetProperty("Now", BindingFlags.Public | BindingFlags.Static) ?? netTimeType.GetProperty("get_Now", BindingFlags.Public | BindingFlags.Static);
						var nowMethod = nowProp == null ? netTimeType.GetMethod("get_Now", BindingFlags.Public | BindingFlags.Static) : null;
						bool found = nowProp != null || nowMethod != null;
						MelonLogger.Msg($"[LEHud] Verify: NetTime.Now found = {found} ({netTimeType.FullName})");
					}
					else
					{
						MelonLogger.Msg("[LEHud] Verify: NetTime type not found");
					}
				}
				catch (System.Exception ex)
				{
					MelonLogger.Error($"[LEHud] Verify: NetTime check error: {ex.Message}");
				}
			}
			catch (System.Exception e)
			{
				MelonLogger.Error($"[LEHud] VerifyNetworkingTargets error: {e.Message}");
			}
		}
	}
