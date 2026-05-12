using System.Runtime.CompilerServices;
using MegaCrit.Sts2.Core.Models;

namespace SlayTheSpire2Mod.CardEditor;

internal static class CardEditorPowerSourceMap
{
	private sealed class SourceInfo
	{
		public required CardModel SourceCard { get; init; }
	}

	private static readonly ConditionalWeakTable<PowerModel, SourceInfo> _sources = new ConditionalWeakTable<PowerModel, SourceInfo>();

	public static void Register(PowerModel power, CardModel sourceCard)
	{
		if (power == null || sourceCard == null)
		{
			return;
		}

		_sources.Remove(power);
		_sources.Add(power, new SourceInfo { SourceCard = sourceCard });
	}

	public static void RegisterIfMissing(PowerModel power, CardModel sourceCard)
	{
		if (power == null || sourceCard == null)
		{
			return;
		}
		if (_sources.TryGetValue(power, out _))
		{
			return;
		}

		_sources.Add(power, new SourceInfo { SourceCard = sourceCard });
	}

	public static CardModel? TryGetSourceCard(PowerModel power)
	{
		if (power == null)
		{
			return null;
		}

		return _sources.TryGetValue(power, out SourceInfo? info) ? info.SourceCard : null;
	}
}
