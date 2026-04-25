using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using PowerCmd = SlayTheSpire2Mod.CardEditor.CardEditorPowerCmdCompat;

namespace SlayTheSpire2Mod.CardEditor;

internal abstract class CardEditorTempStatTrackerPower<TUnderlying> : PowerModel where TUnderlying : PowerModel
{
	public override PowerType Type => PowerType.Buff;

	public override PowerStackType StackType => PowerStackType.Counter;

	public override bool AllowNegative => true;

	protected override bool IsVisibleInternal => false;

	public override async Task AfterTurnEnd(PlayerChoiceContext choiceContext, CombatSide side)
	{
		Creature? owner = Owner;
		if (owner == null)
		{
			return;
		}
		if (side != owner.Side)
		{
			return;
		}

		int delta = Amount;
		await PowerCmd.Remove(this);
		if (delta != 0)
		{
			await PowerCmd.Apply<TUnderlying>(owner, -delta, owner, null);
		}
	}
}

internal sealed class CardEditorTempStrengthTrackerPower : CardEditorTempStatTrackerPower<StrengthPower>
{
}

internal sealed class CardEditorTempDexterityTrackerPower : CardEditorTempStatTrackerPower<DexterityPower>
{
}

internal sealed class CardEditorTempFocusTrackerPower : CardEditorTempStatTrackerPower<FocusPower>
{
}

internal sealed class CardEditorTempWeakTrackerPower : CardEditorTempStatTrackerPower<WeakPower>
{
}

internal sealed class CardEditorTempFrailTrackerPower : CardEditorTempStatTrackerPower<FrailPower>
{
}

internal sealed class CardEditorTempVulnerableTrackerPower : CardEditorTempStatTrackerPower<VulnerablePower>
{
}

internal sealed class CardEditorTempPoisonTrackerPower : CardEditorTempStatTrackerPower<PoisonPower>
{
}

internal sealed class CardEditorTempDoomTrackerPower : CardEditorTempStatTrackerPower<DoomPower>
{
}

internal sealed class CardEditorTempArtifactTrackerPower : CardEditorTempStatTrackerPower<ArtifactPower>
{
}

internal sealed class CardEditorTempThornsTrackerPower : CardEditorTempStatTrackerPower<ThornsPower>
{
}

internal sealed class CardEditorTempRegenTrackerPower : CardEditorTempStatTrackerPower<RegenPower>
{
}

internal sealed class CardEditorTempPlatingTrackerPower : CardEditorTempStatTrackerPower<PlatingPower>
{
}

internal sealed class CardEditorTempIntangibleTrackerPower : CardEditorTempStatTrackerPower<IntangiblePower>
{
}

internal sealed class CardEditorTempBufferTrackerPower : CardEditorTempStatTrackerPower<BufferPower>
{
}

internal sealed class CardEditorTempVigorTrackerPower : CardEditorTempStatTrackerPower<VigorPower>
{
}

internal sealed class CardEditorTempBlurTrackerPower : CardEditorTempStatTrackerPower<BlurPower>
{
}

internal sealed class CardEditorTempRitualTrackerPower : CardEditorTempStatTrackerPower<RitualPower>
{
}

internal sealed class CardEditorTempConstrictTrackerPower : CardEditorTempStatTrackerPower<ConstrictPower>
{
}
