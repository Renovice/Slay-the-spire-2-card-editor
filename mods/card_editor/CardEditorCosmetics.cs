using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Vfx;

namespace SlayTheSpire2Mod.CardEditor;

public enum CardEditorCosmeticVfxPreset
{
	None = 0,
	StarryImpact = 1,
	Hyperbeam = 2,
	Haze = 3,
	AttackSlash = 4,
	AttackBlunt = 5,
	AttackLightning = 6,
	DaggerThrow = 7,
	DaggerSpray = 8,
	GiantHorizontalSlash = 9,
	Scratch = 10,
	Thrash = 11,
	Bite = 12,
	Chain = 13,
	Heal = 14,
	Block = 15,
	Scream = 16,
	SpookyScream = 17,
	HeavyBlunt = 18,
	FlyingSlash = 19,
	BloodyImpact = 20,
	RockShatter = 21,
	SandyImpact = 22,
	DramaticStab = 23,
	SlimeImpact = 24,
	Gaze = 25,
	CoinExplosionSmall = 26,
	CoinExplosionRegular = 27,
	CoinExplosionJumbo = 28,
	Adrenaline = 29,
	HellraiserSword = 30
}

public enum CardEditorCosmeticAnimationPreset
{
	None = 0,
	MatchCardType = 1,
	Attack = 2,
	Cast = 3,
	Shiv = 4,
	Poke = 5,
	HyperbeamCast = 6,
	DiveBombCast = 7
}

public enum CardEditorCosmeticStylePreset
{
	None = 0,
	RegentStrike = 1,
	RegentCast = 2,
	SilentShiv = 3,
	Hyperbeam = 4,
	DiveBomb = 5,
	PoisonCloud = 6
}

public enum CardEditorCosmeticAttach
{
	Self = 0,
	Target = 1,
	AllEnemies = 2,
	RandomEnemy = 3
}

internal static class CardEditorCosmetics
{
	public static string VfxPresetLabel(CardEditorCosmeticVfxPreset preset)
	{
		string fallback = preset switch
		{
			CardEditorCosmeticVfxPreset.None => "None",
			CardEditorCosmeticVfxPreset.StarryImpact => "Starry Impact (Astral Pulse)",
			CardEditorCosmeticVfxPreset.Hyperbeam => "Hyperbeam",
			CardEditorCosmeticVfxPreset.Haze => "Haze (Smoke / Poison Cloud)",
			CardEditorCosmeticVfxPreset.AttackSlash => "Slash",
			CardEditorCosmeticVfxPreset.AttackBlunt => "Blunt",
			CardEditorCosmeticVfxPreset.AttackLightning => "Lightning",
			CardEditorCosmeticVfxPreset.DaggerThrow => "Dagger Throw",
			CardEditorCosmeticVfxPreset.DaggerSpray => "Dagger Spray",
			CardEditorCosmeticVfxPreset.GiantHorizontalSlash => "Giant Horizontal Slash",
			CardEditorCosmeticVfxPreset.Scratch => "Scratch",
			CardEditorCosmeticVfxPreset.Thrash => "Thrash",
			CardEditorCosmeticVfxPreset.Bite => "Bite",
			CardEditorCosmeticVfxPreset.Chain => "Chain",
			CardEditorCosmeticVfxPreset.Heal => "Heal",
			CardEditorCosmeticVfxPreset.Block => "Block",
			CardEditorCosmeticVfxPreset.Scream => "Scream",
			CardEditorCosmeticVfxPreset.SpookyScream => "Spooky Scream",
			CardEditorCosmeticVfxPreset.HeavyBlunt => "Heavy Blunt",
			CardEditorCosmeticVfxPreset.FlyingSlash => "Flying Slash",
			CardEditorCosmeticVfxPreset.BloodyImpact => "Bloody Impact",
			CardEditorCosmeticVfxPreset.RockShatter => "Rock Shatter",
			CardEditorCosmeticVfxPreset.SandyImpact => "Sandy Impact",
			CardEditorCosmeticVfxPreset.DramaticStab => "Dramatic Stab",
			CardEditorCosmeticVfxPreset.SlimeImpact => "Slime Impact",
			CardEditorCosmeticVfxPreset.Gaze => "Gaze",
			CardEditorCosmeticVfxPreset.CoinExplosionSmall => "Coin Explosion (Small)",
			CardEditorCosmeticVfxPreset.CoinExplosionRegular => "Coin Explosion (Regular)",
			CardEditorCosmeticVfxPreset.CoinExplosionJumbo => "Coin Explosion (Jumbo)",
			CardEditorCosmeticVfxPreset.Adrenaline => "Adrenaline",
			CardEditorCosmeticVfxPreset.HellraiserSword => "Hellraiser Sword",
			_ => preset.ToString()
		};

		return CardEditorLoc.Enum("cosmeticVfx", preset, fallback);
	}

	public static string AnimationPresetLabel(CardEditorCosmeticAnimationPreset preset)
	{
		string fallback = preset switch
		{
			CardEditorCosmeticAnimationPreset.None => "None",
			CardEditorCosmeticAnimationPreset.MatchCardType => "Owner Attack/Cast",
			CardEditorCosmeticAnimationPreset.Attack => "Attack",
			CardEditorCosmeticAnimationPreset.Cast => "Cast",
			CardEditorCosmeticAnimationPreset.Shiv => "Shiv",
			CardEditorCosmeticAnimationPreset.Poke => "Poke",
			CardEditorCosmeticAnimationPreset.HyperbeamCast => "Hyperbeam Cast",
			CardEditorCosmeticAnimationPreset.DiveBombCast => "Dive Bomb Cast",
			_ => preset.ToString()
		};

		return CardEditorLoc.Enum("cosmeticAnimation", preset, fallback);
	}

	public static string StylePresetLabel(CardEditorCosmeticStylePreset preset)
	{
		string fallback = preset switch
		{
			CardEditorCosmeticStylePreset.None => "None",
			CardEditorCosmeticStylePreset.RegentStrike => "Regent Strike Style",
			CardEditorCosmeticStylePreset.RegentCast => "Regent Cast Style",
			CardEditorCosmeticStylePreset.SilentShiv => "Silent Shiv Style",
			CardEditorCosmeticStylePreset.Hyperbeam => "Hyperbeam Style",
			CardEditorCosmeticStylePreset.DiveBomb => "Dive Bomb Style",
			CardEditorCosmeticStylePreset.PoisonCloud => "Poison Cloud Style",
			_ => preset.ToString()
		};

		return CardEditorLoc.Enum("cosmeticStyle", preset, fallback);
	}

	public static string AttachLabel(CardEditorCosmeticAttach attach)
	{
		string fallback = attach switch
		{
			CardEditorCosmeticAttach.Self => "Self",
			CardEditorCosmeticAttach.Target => "Target",
			CardEditorCosmeticAttach.AllEnemies => "All Enemies",
			CardEditorCosmeticAttach.RandomEnemy => "Random Enemy",
			_ => attach.ToString()
		};

		return CardEditorLoc.Enum("cosmeticAttach", attach, fallback);
	}

	public static async Task RunBeforeCardPlayed(CombatState combatState, CardPlay cardPlay)
	{
		CardModel? card = cardPlay?.Card;
		Player? owner = card?.Owner;
		Creature? ownerCreature = owner?.Creature;
		if (combatState == null || card == null || ownerCreature == null)
		{
			return;
		}

		CardOverride? overrideData = null;
		if (CardEditorUiState.TryGetDraftOverride(card.Id, out CardOverride draftOverride))
		{
			overrideData = draftOverride;
		}
		else if (CardEditorOverrides.TryGet(card.Id, out CardOverride storedOverride))
		{
			overrideData = storedOverride;
		}

		if (overrideData == null)
		{
			return;
		}

		CardEditorCosmeticAnimationPreset animationPreset =
			overrideData.CosmeticAnimationPreset
			?? ((overrideData.CosmeticPlayAttackerAnim ?? false)
				? CardEditorCosmeticAnimationPreset.MatchCardType
				: CardEditorCosmeticAnimationPreset.None);
		CardEditorCosmeticVfxPreset preset = overrideData.CosmeticVfxPreset ?? CardEditorCosmeticVfxPreset.None;
		CardEditorCosmeticAttach attach = overrideData.CosmeticVfxAttach ?? CardEditorCosmeticAttach.Target;
		if (overrideData.CosmeticStylePreset is CardEditorCosmeticStylePreset stylePreset
			&& stylePreset != CardEditorCosmeticStylePreset.None)
		{
			ResolveStylePreset(stylePreset, ref animationPreset, ref preset, ref attach, overrideData);
		}

		if (animationPreset == CardEditorCosmeticAnimationPreset.None && preset == CardEditorCosmeticVfxPreset.None)
		{
			return;
		}

		if (animationPreset != CardEditorCosmeticAnimationPreset.None)
		{
			await TryPlayAnimationPreset(card, owner, ownerCreature, animationPreset, cardPlay);
		}

		if (preset != CardEditorCosmeticVfxPreset.None)
		{
			await TryPlayVfxPreset(combatState, cardPlay, owner, ownerCreature, preset, attach);
		}
	}

	private static void ResolveStylePreset(
		CardEditorCosmeticStylePreset stylePreset,
		ref CardEditorCosmeticAnimationPreset animationPreset,
		ref CardEditorCosmeticVfxPreset preset,
		ref CardEditorCosmeticAttach attach,
		CardOverride overrideData)
	{
		if (!TryGetStyleDefaults(stylePreset, out CardEditorCosmeticAnimationPreset styleAnimation, out CardEditorCosmeticVfxPreset styleVfx, out CardEditorCosmeticAttach styleAttach))
		{
			return;
		}

		if (overrideData.CosmeticAnimationPreset == null && styleAnimation != CardEditorCosmeticAnimationPreset.None)
		{
			animationPreset = styleAnimation;
		}

		if (overrideData.CosmeticVfxPreset == null && styleVfx != CardEditorCosmeticVfxPreset.None)
		{
			preset = styleVfx;
		}

		if (overrideData.CosmeticVfxAttach == null)
		{
			attach = styleAttach;
		}
	}

	public static bool TryGetStyleDefaults(
		CardEditorCosmeticStylePreset stylePreset,
		out CardEditorCosmeticAnimationPreset animationPreset,
		out CardEditorCosmeticVfxPreset vfxPreset,
		out CardEditorCosmeticAttach attach)
	{
		animationPreset = CardEditorCosmeticAnimationPreset.None;
		vfxPreset = CardEditorCosmeticVfxPreset.None;
		attach = CardEditorCosmeticAttach.Target;

		switch (stylePreset)
		{
			case CardEditorCosmeticStylePreset.RegentStrike:
				animationPreset = CardEditorCosmeticAnimationPreset.Attack;
				vfxPreset = CardEditorCosmeticVfxPreset.StarryImpact;
				return true;
			case CardEditorCosmeticStylePreset.RegentCast:
				animationPreset = CardEditorCosmeticAnimationPreset.Cast;
				vfxPreset = CardEditorCosmeticVfxPreset.StarryImpact;
				return true;
			case CardEditorCosmeticStylePreset.SilentShiv:
				animationPreset = CardEditorCosmeticAnimationPreset.Shiv;
				vfxPreset = CardEditorCosmeticVfxPreset.DaggerThrow;
				return true;
			case CardEditorCosmeticStylePreset.Hyperbeam:
				animationPreset = CardEditorCosmeticAnimationPreset.HyperbeamCast;
				vfxPreset = CardEditorCosmeticVfxPreset.Hyperbeam;
				attach = CardEditorCosmeticAttach.AllEnemies;
				return true;
			case CardEditorCosmeticStylePreset.DiveBomb:
				animationPreset = CardEditorCosmeticAnimationPreset.DiveBombCast;
				return true;
			case CardEditorCosmeticStylePreset.PoisonCloud:
				animationPreset = CardEditorCosmeticAnimationPreset.Cast;
				vfxPreset = CardEditorCosmeticVfxPreset.Haze;
				attach = CardEditorCosmeticAttach.AllEnemies;
				return true;
			default:
				return false;
		}
	}

	private static async Task TryPlayAnimationPreset(
		CardModel card,
		Player owner,
		Creature ownerCreature,
		CardEditorCosmeticAnimationPreset preset,
		CardPlay cardPlay)
	{
		try
		{
			string? anim = null;
			float delay = 0f;

			switch (preset)
			{
				case CardEditorCosmeticAnimationPreset.MatchCardType:
					anim = card.Type == CardType.Attack ? "Attack" : "Cast";
					delay = card.Type == CardType.Attack ? owner.Character.AttackAnimDelay : owner.Character.CastAnimDelay;
					break;
				case CardEditorCosmeticAnimationPreset.Attack:
					anim = "Attack";
					delay = owner.Character.AttackAnimDelay;
					break;
				case CardEditorCosmeticAnimationPreset.Cast:
					anim = "Cast";
					delay = owner.Character.CastAnimDelay;
					break;
				case CardEditorCosmeticAnimationPreset.Shiv:
					anim = "Shiv";
					delay = 0.2f;
					break;
				case CardEditorCosmeticAnimationPreset.Poke:
					anim = "attack_poke";
					delay = 0.3f;
					break;
				case CardEditorCosmeticAnimationPreset.HyperbeamCast:
					anim = "Cast";
					delay = 0.5f;
					break;
				case CardEditorCosmeticAnimationPreset.DiveBombCast:
					anim = "Cast";
					delay = 1f;
					break;
			}

			if (string.IsNullOrWhiteSpace(anim))
			{
				return;
			}

			await CreatureCmd.TriggerAnim(ownerCreature, anim, delay);

			if (preset == CardEditorCosmeticAnimationPreset.DiveBombCast)
			{
				TryPlayDiveBombVfx(cardPlay, ownerCreature);
			}
		}
		catch
		{
			// ignored
		}
	}

	private static async Task TryPlayVfxPreset(
		CombatState combatState,
		CardPlay cardPlay,
		Player owner,
		Creature ownerCreature,
		CardEditorCosmeticVfxPreset preset,
		CardEditorCosmeticAttach attach)
	{
		try
		{
			switch (preset)
			{
				case CardEditorCosmeticVfxPreset.Hyperbeam:
					await PlayHyperbeamVfx(combatState, ownerCreature);
					return;
				case CardEditorCosmeticVfxPreset.Haze:
					PlayHazeVfx(combatState);
					return;
				case CardEditorCosmeticVfxPreset.StarryImpact:
					PlaySimpleVfx(combatState, cardPlay, owner, ownerCreature, attach, "vfx/vfx_starry_impact", perEnemy: true);
					return;
				case CardEditorCosmeticVfxPreset.AttackSlash:
					PlaySimpleVfx(combatState, cardPlay, owner, ownerCreature, attach, VfxCmd.slashPath, perEnemy: false);
					return;
				case CardEditorCosmeticVfxPreset.AttackBlunt:
					PlaySimpleVfx(combatState, cardPlay, owner, ownerCreature, attach, VfxCmd.bluntPath, perEnemy: false);
					return;
				case CardEditorCosmeticVfxPreset.AttackLightning:
					PlaySimpleVfx(combatState, cardPlay, owner, ownerCreature, attach, VfxCmd.lightningPath, perEnemy: false);
					return;
				case CardEditorCosmeticVfxPreset.DaggerThrow:
					PlaySimpleVfx(combatState, cardPlay, owner, ownerCreature, attach, VfxCmd.daggerThrowPath, perEnemy: false);
					return;
				case CardEditorCosmeticVfxPreset.DaggerSpray:
					PlaySimpleVfx(combatState, cardPlay, owner, ownerCreature, attach, VfxCmd.daggerSprayPath, perEnemy: true);
					return;
				case CardEditorCosmeticVfxPreset.GiantHorizontalSlash:
					PlaySimpleVfx(combatState, cardPlay, owner, ownerCreature, attach, VfxCmd.giantHorizontalSlashPath, perEnemy: false);
					return;
				case CardEditorCosmeticVfxPreset.Scratch:
					PlaySimpleVfx(combatState, cardPlay, owner, ownerCreature, attach, VfxCmd.scratchPath, perEnemy: false);
					return;
				case CardEditorCosmeticVfxPreset.Thrash:
					PlaySimpleVfx(combatState, cardPlay, owner, ownerCreature, attach, VfxCmd.thrashPath, perEnemy: false);
					return;
				case CardEditorCosmeticVfxPreset.Bite:
					PlaySimpleVfx(combatState, cardPlay, owner, ownerCreature, attach, VfxCmd.bitePath, perEnemy: false);
					return;
				case CardEditorCosmeticVfxPreset.Chain:
					PlaySimpleVfx(combatState, cardPlay, owner, ownerCreature, attach, VfxCmd.chainPath, perEnemy: false);
					return;
				case CardEditorCosmeticVfxPreset.Heal:
					PlaySimpleVfx(combatState, cardPlay, owner, ownerCreature, attach, VfxCmd.healPath, perEnemy: false);
					return;
				case CardEditorCosmeticVfxPreset.Block:
					PlaySimpleVfx(combatState, cardPlay, owner, ownerCreature, attach, VfxCmd.blockPath, perEnemy: false);
					return;
				case CardEditorCosmeticVfxPreset.Scream:
					PlaySimpleVfx(combatState, cardPlay, owner, ownerCreature, attach, VfxCmd.screamVfx, perEnemy: false);
					return;
				case CardEditorCosmeticVfxPreset.SpookyScream:
					PlaySimpleVfx(combatState, cardPlay, owner, ownerCreature, attach, VfxCmd.spookyScreamVfx, perEnemy: false);
					return;
				case CardEditorCosmeticVfxPreset.HeavyBlunt:
					PlaySimpleVfx(combatState, cardPlay, owner, ownerCreature, attach, VfxCmd.heavyBluntPath, perEnemy: false);
					return;
				case CardEditorCosmeticVfxPreset.FlyingSlash:
					PlaySimpleVfx(combatState, cardPlay, owner, ownerCreature, attach, VfxCmd.flyingSlashPath, perEnemy: false);
					return;
				case CardEditorCosmeticVfxPreset.BloodyImpact:
					PlaySimpleVfx(combatState, cardPlay, owner, ownerCreature, attach, "vfx/vfx_bloody_impact", perEnemy: false);
					return;
				case CardEditorCosmeticVfxPreset.RockShatter:
					PlaySimpleVfx(combatState, cardPlay, owner, ownerCreature, attach, "vfx/vfx_rock_shatter", perEnemy: false);
					return;
				case CardEditorCosmeticVfxPreset.SandyImpact:
					PlaySimpleVfx(combatState, cardPlay, owner, ownerCreature, attach, "vfx/vfx_sandy_impact", perEnemy: false);
					return;
				case CardEditorCosmeticVfxPreset.DramaticStab:
					PlaySimpleVfx(combatState, cardPlay, owner, ownerCreature, attach, "vfx/vfx_dramatic_stab", perEnemy: false);
					return;
				case CardEditorCosmeticVfxPreset.SlimeImpact:
					PlaySimpleVfx(combatState, cardPlay, owner, ownerCreature, attach, "vfx/vfx_slime_impact", perEnemy: false);
					return;
				case CardEditorCosmeticVfxPreset.Gaze:
					PlaySimpleVfx(combatState, cardPlay, owner, ownerCreature, attach, "vfx/vfx_gaze", perEnemy: false);
					return;
				case CardEditorCosmeticVfxPreset.CoinExplosionSmall:
					PlaySimpleVfx(combatState, cardPlay, owner, ownerCreature, attach, "vfx/vfx_coin_explosion_small", perEnemy: false);
					return;
				case CardEditorCosmeticVfxPreset.CoinExplosionRegular:
					PlaySimpleVfx(combatState, cardPlay, owner, ownerCreature, attach, "vfx/vfx_coin_explosion_regular", perEnemy: false);
					return;
				case CardEditorCosmeticVfxPreset.CoinExplosionJumbo:
					PlaySimpleVfx(combatState, cardPlay, owner, ownerCreature, attach, "vfx/vfx_coin_explosion_jumbo", perEnemy: false);
					return;
				case CardEditorCosmeticVfxPreset.Adrenaline:
					PlaySimpleVfx(combatState, cardPlay, owner, ownerCreature, attach, "vfx/vfx_adrenaline", perEnemy: false);
					return;
				case CardEditorCosmeticVfxPreset.HellraiserSword:
					PlaySimpleVfx(combatState, cardPlay, owner, ownerCreature, attach, VfxCmd.hellraiserSwordVfxPath, perEnemy: false);
					return;
			}
		}
		catch
		{
			// ignored
		}
	}

	private static void PlaySimpleVfx(
		CombatState combatState,
		CardPlay cardPlay,
		Player owner,
		Creature ownerCreature,
		CardEditorCosmeticAttach attach,
		string vfxPath,
		bool perEnemy)
	{
		List<Creature> enemies = GetHittableEnemies(combatState);

		if (attach == CardEditorCosmeticAttach.AllEnemies || perEnemy)
		{
			foreach (Creature enemy in enemies)
			{
				VfxCmd.PlayOnCreatureCenter(enemy, vfxPath);
			}
			return;
		}

		if (attach == CardEditorCosmeticAttach.Self)
		{
			VfxCmd.PlayOnCreatureCenter(ownerCreature, vfxPath);
			return;
		}

		Creature? resolved = ResolveSingleEnemyTarget(owner, cardPlay, enemies, attach);
		if (resolved != null)
		{
			VfxCmd.PlayOnCreatureCenter(resolved, vfxPath);
		}
	}

	private static async Task PlayHyperbeamVfx(CombatState combatState, Creature ownerCreature)
	{
		Node? node = NCombatRoom.Instance?.CombatVfxContainer;
		if (node == null)
		{
			return;
		}

		List<Creature> enemies = combatState.Enemies.Where(e => e != null && e.IsAlive).ToList();
		if (enemies.Count == 0)
		{
			return;
		}

		NHyperbeamVfx? beam = NHyperbeamVfx.Create(ownerCreature, enemies.Last());
		if (beam != null)
		{
			node.AddChildSafely(beam);
			await Cmd.Wait(0.5f);
		}

		foreach (Creature enemy in enemies)
		{
			NHyperbeamImpactVfx? impact = NHyperbeamImpactVfx.Create(ownerCreature, enemy);
			if (impact != null)
			{
				node.AddChildSafely(impact);
			}
		}
	}

	private static void PlayHazeVfx(CombatState combatState)
	{
		Node? node = NCombatRoom.Instance?.CombatVfxContainer;
		if (node == null)
		{
			return;
		}

		node.AddChildSafely(NSmokyVignetteVfx.Create(new Color(0.8f, 0.8f, 0.3f, 0.66f), new Color(0f, 4f, 0f, 0.33f)));
		foreach (Creature enemy in GetHittableEnemies(combatState))
		{
			node.AddChildSafely(NSmokePuffVfx.Create(enemy, NSmokePuffVfx.SmokePuffColor.Green));
		}
	}

	private static void TryPlayDiveBombVfx(CardPlay cardPlay, Creature ownerCreature)
	{
		try
		{
			if (cardPlay?.Target == null || cardPlay.Target.IsDead)
			{
				return;
			}

			Node? node = NCombatRoom.Instance?.CombatVfxContainer;
			if (node == null)
			{
				return;
			}

			NMinionDiveBombVfx? vfx = NMinionDiveBombVfx.Create(ownerCreature, cardPlay.Target);
			if (vfx != null)
			{
				node.AddChildSafely(vfx);
			}
		}
		catch
		{
		}
	}

	private static List<Creature> GetHittableEnemies(CombatState combatState)
	{
		try
		{
			return combatState.HittableEnemies.Where(e => e != null).ToList();
		}
		catch
		{
			return new List<Creature>();
		}
	}

	private static Creature? ResolveSingleEnemyTarget(Player owner, CardPlay cardPlay, List<Creature> candidates, CardEditorCosmeticAttach attach)
	{
		if (attach == CardEditorCosmeticAttach.Target)
		{
			try
			{
				if (cardPlay.Target != null && cardPlay.Target.IsHittable && !cardPlay.Target.IsDead)
				{
					return cardPlay.Target;
				}
			}
			catch
			{
				// ignored
			}
		}

		if (attach == CardEditorCosmeticAttach.RandomEnemy || attach == CardEditorCosmeticAttach.Target)
		{
			if (candidates.Count == 0)
			{
				return null;
			}

			try
			{
				Creature? picked = owner.RunState?.Rng?.CombatTargets.NextItem(candidates);
				return picked ?? candidates[0];
			}
			catch
			{
				return candidates[0];
			}
		}

		return null;
	}
}
