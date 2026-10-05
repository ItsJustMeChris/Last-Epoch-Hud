using Il2Cpp;
using Mod.Game;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mod.Cheats.ESP
{
	internal static class Shrines
	{
		private static GameObject? s_shrineManager;
		private static readonly List<Transform> s_shrineTransforms = new List<Transform>(16);
		private static readonly List<string> s_shrineNames = new List<string>(16);
		private static readonly List<Transform> s_scanTransforms = new List<Transform>(16);
		private static readonly Color ShrineColor = Drawing.BloodOrange;
		private const int ManagerSearchIntervalFrames = 180;
		private static int s_nextManagerSearchFrame;


		public static void OnSceneChanged()
		{
			// Clear cached references on scene transitions
			s_shrineManager = null;
			s_shrineTransforms.Clear();
			s_shrineNames.Clear();
			s_scanTransforms.Clear();
			s_nextManagerSearchFrame = 0;
		}

		private static readonly string[] ManagerNameCandidates =
		{
			"Shrine Placement Manager",
			"ShrinePlacementManager",
			"Shrine Manager",
			"ShrineManager"
		};

		private static void TryFindManager()
		{
			if (s_shrineManager != null) return;
			if (Time.frameCount < s_nextManagerSearchFrame) return;
			s_nextManagerSearchFrame = Time.frameCount + ManagerSearchIntervalFrames;

			for (int i = 0; i < ManagerNameCandidates.Length; i++)
			{
				s_shrineManager = GameObject.Find(ManagerNameCandidates[i]);
				if (s_shrineManager != null) return;
			}
		}

		private static string GetDisplayName(GameObject shrineGo)
		{
			try
			{
				var info = shrineGo.GetComponent<DisplayInformation>();
				if (info != null)
				{
					string? localized = null;
					try { localized = info.GetLocalizedName(); } catch (Exception) { }
					if (!string.IsNullOrWhiteSpace(localized)) return EspUtils.SanitizeLabel(localized);
					if (!string.IsNullOrWhiteSpace(info.displayName)) return EspUtils.SanitizeLabel(info.displayName);
				}
			}
			catch (Exception) { /* Some builds may lack DisplayInformation; fall back to name */ }

			return EspUtils.SanitizeLabel(shrineGo.name);
		}

		private static bool LooksLikeShrine(GameObject go)
		{
			if (go == null) return false;
			var name = go.name ?? string.Empty;
			// Exclude placeholder/empty nodes used only for placement
			// if (name.Equals("Shrine Spot", StringComparison.OrdinalIgnoreCase)) return false;
			
			// Also exclude common variants like "Shrine Spot (Clone)" or numbered spots
			if (name.StartsWith("Shrine Spot", StringComparison.OrdinalIgnoreCase)) return false;
			// Names often: "Maelstrom Shrine(Clone)", "Loot Lizard Shrine - Low Level(Clone)"
			if (name.IndexOf("Shrine", StringComparison.OrdinalIgnoreCase) >= 0) return true;

			// Component-based hints seen in inspector
			if (go.GetComponent<UseAbilityInteraction>() != null) return true;
			if (go.GetComponent<ActivateGameObjectInteraction>() != null) return true;
			if (go.GetComponent<WorldObjectClickListener>() != null) return true;

			return false;
		}


		private static bool IsShrineInteractable(GameObject go)
		{
			// If object is inactive, it is not interactable
			if (!go.activeInHierarchy) return false;

			bool anyFound = false;
			bool anyEnabled = false;

			// var uai = go.GetComponent<UseAbilityInteraction>();
			// if (uai != null) { anyFound = true; anyEnabled |= IsComponentEnabled(uai); }
			// var agi = go.GetComponent<ActivateGameObjectInteraction>();
			// if (agi != null) { anyFound = true; anyEnabled |= IsComponentEnabled(agi); }
			// var wocl = go.GetComponent<WorldObjectClickListener>();
			// if (wocl != null) { anyFound = true; anyEnabled |= IsComponentEnabled(wocl); }
			// var collider = go.GetComponent<BoxCollider>();
			// if (collider != null) { anyFound = true; anyEnabled |= collider.enabled; }
			var outline = go.GetComponent<OutlineOnMouseOver>();
			if (outline != null) { anyFound = true; anyEnabled |= EspUtils.IsComponentEnabled(outline); }

			// If no known components are present, treat as not interactable to avoid placeholders
			return anyFound && anyEnabled;
		}

		private static void RebuildShrineCacheIfNeeded()
		{
			TryFindManager();
			if (s_shrineManager == null) return;

			var t = s_shrineManager.transform;
			if (t == null) return;

			// Collect valid transforms once. Comparing identity as well as count avoids
			// keeping stale references when a shrine is replaced by another one.
			s_scanTransforms.Clear();
			for (int i = 0; i < t.childCount; i++)
			{
				var child = t.GetChild(i);
				if (child != null && child.gameObject != null && child.gameObject.activeInHierarchy && LooksLikeShrine(child.gameObject) && IsShrineInteractable(child.gameObject))
				{
					s_scanTransforms.Add(child);
				}
			}

			bool cacheMatches = s_scanTransforms.Count == s_shrineTransforms.Count;
			if (cacheMatches)
			{
				for (int i = 0; i < s_scanTransforms.Count; i++)
				{
					if (s_scanTransforms[i] != s_shrineTransforms[i])
					{
						cacheMatches = false;
						break;
					}
				}
			}
			if (cacheMatches) return;

			s_shrineTransforms.Clear();
			s_shrineNames.Clear();
			for (int i = 0; i < s_scanTransforms.Count; i++)
			{
				var child = s_scanTransforms[i];
				var go = child.gameObject;
				s_shrineTransforms.Add(child);
				s_shrineNames.Add(GetDisplayName(go));
			}
		}

		public static void OnUpdate(GameObject player)
		{
			if (!Settings.espShowShrines) return;

			RebuildShrineCacheIfNeeded();
			if (s_shrineTransforms.Count == 0) return;

			var playerPos = player.transform.position;
			float maxDistSq = Settings.drawDistance * Settings.drawDistance;

			for (int i = 0; i < s_shrineTransforms.Count; i++)
			{
				var tr = s_shrineTransforms[i];
				if (tr == null) continue;
				var go = tr.gameObject;
				if (go == null || !go.activeInHierarchy) continue;

				var pos = tr.position;
				var delta = pos - playerPos;
				if (delta.sqrMagnitude > maxDistSq) continue;

				var labelPos = pos; labelPos.y += 0.5f;
				var name = (i < s_shrineNames.Count) ? s_shrineNames[i] : EspUtils.SanitizeLabel(go.name);

				if (Settings.showESPLines) ESP.AddLine(playerPos, pos, ShrineColor);
				if (Settings.showESPLabels) ESP.AddString(name, labelPos, ShrineColor);
			}
		}
	}
}
