using MegaCrit.Sts2.Core.Models;

namespace SlayTheSpire2Mod.CardEditor;

internal static class CardEditorGeneratedCardSourceResolver
{
	public static CardModel? ResolveSourceCard()
	{
		CardModel? explicitSource = CardEditorEffectSourceContext.Current;
		if (explicitSource != null)
		{
			return explicitSource;
		}

		if (CardEditorHookModelContext.Current is PowerModel power)
		{
			CardModel? powerSource = CardEditorPowerSourceMap.TryGetSourceCard(power);
			if (powerSource != null)
			{
				return powerSource;
			}
		}

		return CardEditorCardPlayContext.Current?.Card;
	}
}
