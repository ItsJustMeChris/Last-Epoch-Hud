using Il2Cpp;
using Mod.Game;
using UnityEngine;
using System;

namespace Mod.Cheats.ESP
{
	internal class Actors
	{
		//        Alignments: 
		//     
		//        Good: Seems to be players and player pets
		//        Evil: Seems to be enemies
		//        Barrel: Containers that are destructible
		//        HostileNeutral: Seems to be neutral enemies
		//        FriendlyNeutral: Seems to be neutral NPCs
		//        SummonedCorpse: Necromancer summons


		private static string GetActorName(ActorVisuals actor, ActorDisplayInformation? displayInformation)
		{
			if (actor.isPlayer && actor.UserIdentity != null)
			{
				return EspUtils.SanitizeLabel(actor.UserIdentity.Username);
			}
			else
			{
				if (displayInformation != null)
				{
					// Prefer the localized name when available; fall back to displayName
					string? localizedName = null;
					try
					{
						localizedName = displayInformation.GetLocalizedName();
					}
					catch (Exception)
					{
						// Some IL2CPP builds may throw; ignore and use fallbacks
					}

					if (!string.IsNullOrWhiteSpace(localizedName))
					{
						return EspUtils.SanitizeLabel(localizedName);
					}

					if (!string.IsNullOrWhiteSpace(displayInformation.displayName))
					{
						return EspUtils.SanitizeLabel(displayInformation.displayName);
					}
				}
			}

			return EspUtils.SanitizeLabel(actor.name);
		}

		private static Color GetRarityColor(ActorDisplayInformation? info, string alignmentName)
		{
			if (info != null)
			{
				if (info.actorClass == DisplayActorClass.Boss) return Color.red;
				if (info.actorClass == DisplayActorClass.Rare) return Color.yellow;
				if (info.actorClass == DisplayActorClass.Magic) return Drawing.MagicLightBlue;
				// Normal/other defaults to white
				return Color.white;
			}
			// Fallback to alignment color if no display info
			return Drawing.AlignmentToColor(alignmentName);
		}

		public static void GatherActors(GameObject localPlayer)
		{
			if (ActorManager.instance == null) return;
			var playerTransform = localPlayer.transform;
			var playerPosition = playerTransform.position;
			float maxDistance = Settings.drawDistance;
			float maxDistanceSquared = maxDistance * maxDistance;

			foreach (var visual in ActorManager.instance.visuals)
			{
				string alignmentName = visual.alignment?.name ?? string.Empty;
				foreach (var actor in visual.visuals._list)
				{
					if (actor == null || actor.gameObject == null || !actor.gameObject.activeInHierarchy) continue;
					var actorTransform = actor.transform;

					// Skip the local player's own actor visuals
					if (actorTransform.IsChildOf(playerTransform)) continue;

					// Cull distant/dead actors before doing the more expensive special-entity
					// component and hierarchy checks.
					var position = actorTransform.position;
					var delta = position - playerPosition;
					if (actor.dead || delta.sqrMagnitude >= maxDistanceSquared) continue;

					var actorDisplayInfo = actor.GetComponent<ActorDisplayInformation>();
					bool passesActorFilters = alignmentName.Length > 0
						&& alignmentName != "Barrel"
						&& Settings.ShouldDrawNPCAlignment(alignmentName)
						&& (actorDisplayInfo == null || Settings.ShouldDrawNPCClassification(actorDisplayInfo.actorClass));

					// Detect special entities (loot lizards, champions, etc.)
					var special = SpecialEntityEspHelper.DetectType(actor, actorDisplayInfo);
					bool bypassActorFilters = special is SpecialEntityType.LootLizard or SpecialEntityType.Omen;
					bool isAnySpecial = SpecialEntityEspHelper.IsSpecial(special);

					// Some special entities are atypical and should bypass normal actor filters.
					if (!bypassActorFilters && !passesActorFilters) continue;

					var name = GetActorName(actor, actorDisplayInfo);
					var labelPosition = position;
					labelPosition.y += 1.5f;

					// Per-special gating
					if (isAnySpecial && !SpecialEntityEspHelper.ShouldRender(special))
					{
						continue;
					}

					name = SpecialEntityEspHelper.BuildLabel(special, name);
					var textStyle = SpecialEntityEspHelper.ResolveTextStyle(special);
					var color = SpecialEntityEspHelper.ResolveColor(special, GetRarityColor(actorDisplayInfo, alignmentName));

					if (Settings.showESPLines) ESP.AddLine(playerPosition, position, color);
					//ESP.AddString(name + " (" + distance.ToString("F1") + ")  ", position, color);
					if (Settings.showESPLabels) ESP.AddString(name, labelPosition, color, textStyle);
				}
			}
		}
		
		public static void OnUpdate(GameObject player)
		{
			GatherActors(player);
		}
	}
}
