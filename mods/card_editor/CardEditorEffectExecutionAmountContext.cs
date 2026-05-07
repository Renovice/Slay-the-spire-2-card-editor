using System;
using System.Collections.Generic;
using System.Threading;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Models;

namespace SlayTheSpire2Mod.CardEditor;

internal static class CardEditorEffectExecutionAmountContext
{
	private sealed class Session
	{
		public Dictionary<string, int> ConfiguredAmountsByEffectId { get; } = new(StringComparer.Ordinal);
		public Dictionary<string, int> ConfiguredCountsByEffectId { get; } = new(StringComparer.Ordinal);
		public Dictionary<string, int> AppliedAmountsByEffectId { get; } = new(StringComparer.Ordinal);
		public Dictionary<string, int> TotalDamageAmountsByEffectId { get; } = new(StringComparer.Ordinal);
		public Dictionary<string, int> BlockedDamageAmountsByEffectId { get; } = new(StringComparer.Ordinal);
		public Dictionary<string, int> OverkillDamageAmountsByEffectId { get; } = new(StringComparer.Ordinal);
		public Dictionary<string, int> KillCountsByEffectId { get; } = new(StringComparer.Ordinal);
		public Dictionary<string, int> AppliedCountsByEffectId { get; } = new(StringComparer.Ordinal);
		public Dictionary<string, List<CardModel>> SelectedCardsByEffectId { get; } = new(StringComparer.Ordinal);
		public HashSet<DamageResult> CurrentPlayDamageResults { get; } = new();
		public Stack<EffectFrame> Frames { get; } = new();
	}

	private sealed class EffectFrame
	{
		public required CardExtraEffect Effect { get; init; }
		public required int FallbackAppliedAmount { get; init; }
		public required int FallbackAppliedCount { get; init; }
		public int ReportedAppliedAmount { get; set; }
		public bool HasReportedAppliedAmount { get; set; }
		public int ReportedTotalDamageAmount { get; set; }
		public bool HasReportedTotalDamageAmount { get; set; }
		public int ReportedBlockedDamageAmount { get; set; }
		public bool HasReportedBlockedDamageAmount { get; set; }
		public int ReportedOverkillDamageAmount { get; set; }
		public bool HasReportedOverkillDamageAmount { get; set; }
		public int ReportedKillCount { get; set; }
		public bool HasReportedKillCount { get; set; }
		public int ReportedAppliedCount { get; set; }
		public bool HasReportedAppliedCount { get; set; }
		public List<CardModel> SelectedCards { get; } = new();
	}

	private sealed class RootSessionScope : IDisposable
	{
		private bool _disposed;

		public void Dispose()
		{
			if (_disposed)
			{
				return;
			}
			_disposed = true;
			_currentSession.Value = null;
		}
	}

	private sealed class EffectScope : IDisposable
	{
		private readonly Session? _session;
		private readonly EffectFrame? _frame;
		private bool _disposed;

		public EffectScope(Session? session, EffectFrame? frame)
		{
			_session = session;
			_frame = frame;
		}

		public void Dispose()
		{
			if (_disposed)
			{
				return;
			}
			_disposed = true;

			if (_session == null || _frame == null)
			{
				return;
			}

			RemoveFrame(_session, _frame);

			string? effectId = string.IsNullOrWhiteSpace(_frame.Effect.EffectId)
				? null
				: _frame.Effect.EffectId.Trim();
			if (effectId == null)
			{
				return;
			}

			int appliedAmount = _frame.HasReportedAppliedAmount
				? _frame.ReportedAppliedAmount
				: _frame.FallbackAppliedAmount;
			int totalDamageAmount = _frame.HasReportedTotalDamageAmount
				? _frame.ReportedTotalDamageAmount
				: appliedAmount;
			int blockedDamageAmount = _frame.HasReportedBlockedDamageAmount
				? _frame.ReportedBlockedDamageAmount
				: 0;
			int overkillDamageAmount = _frame.HasReportedOverkillDamageAmount
				? _frame.ReportedOverkillDamageAmount
				: 0;
			int killCount = _frame.HasReportedKillCount
				? _frame.ReportedKillCount
				: 0;
			int appliedCount = _frame.HasReportedAppliedCount
				? _frame.ReportedAppliedCount
				: _frame.FallbackAppliedCount;
			int configuredAmount = _frame.FallbackAppliedAmount;
			int configuredCount = _frame.FallbackAppliedCount;
			AccumulateIntoParentRunEffectSource(_session, appliedAmount, totalDamageAmount, blockedDamageAmount, overkillDamageAmount, killCount, appliedCount);
			AccumulateSelectedCardsIntoParentRunEffectSource(_session, _frame.SelectedCards);
			_session.ConfiguredAmountsByEffectId[effectId] = ClampNonNegative(configuredAmount);
			_session.ConfiguredCountsByEffectId[effectId] = ClampNonNegative(configuredCount);
			_session.AppliedAmountsByEffectId[effectId] = ClampNonNegative(appliedAmount);
			_session.TotalDamageAmountsByEffectId[effectId] = ClampNonNegative(totalDamageAmount);
			_session.BlockedDamageAmountsByEffectId[effectId] = ClampNonNegative(blockedDamageAmount);
			_session.OverkillDamageAmountsByEffectId[effectId] = ClampNonNegative(overkillDamageAmount);
			_session.KillCountsByEffectId[effectId] = ClampNonNegative(killCount);
			_session.AppliedCountsByEffectId[effectId] = ClampNonNegative(appliedCount);
			_session.SelectedCardsByEffectId[effectId] = CopySelectedCards(_frame.SelectedCards);
		}
	}

	private sealed class NoopScope : IDisposable
	{
		public static readonly NoopScope Instance = new();

		public void Dispose()
		{
		}
	}

	private sealed class SelectedCardsScope : IDisposable
	{
		private readonly Session? _session;
		private readonly Dictionary<string, List<CardModel>> _previous = new(StringComparer.Ordinal);
		private readonly HashSet<string> _missing = new(StringComparer.Ordinal);
		private bool _disposed;

		public SelectedCardsScope(Session? session, IReadOnlyDictionary<string, List<CardModel>> selectedCardsByEffectId)
		{
			_session = session;
			if (_session == null || selectedCardsByEffectId == null || selectedCardsByEffectId.Count == 0)
			{
				return;
			}

			foreach ((string rawKey, List<CardModel> cards) in selectedCardsByEffectId)
			{
				string key = rawKey?.Trim() ?? string.Empty;
				if (key.Length == 0)
				{
					continue;
				}

				if (_session.SelectedCardsByEffectId.TryGetValue(key, out List<CardModel>? previousCards))
				{
					_previous[key] = CopySelectedCards(previousCards);
				}
				else
				{
					_missing.Add(key);
				}

				_session.SelectedCardsByEffectId[key] = CopySelectedCards(cards);
			}
		}

		public void Dispose()
		{
			if (_disposed)
			{
				return;
			}
			_disposed = true;

			if (_session == null)
			{
				return;
			}

			foreach (string key in _missing)
			{
				_session.SelectedCardsByEffectId.Remove(key);
			}

			foreach ((string key, List<CardModel> cards) in _previous)
			{
				_session.SelectedCardsByEffectId[key] = CopySelectedCards(cards);
			}
		}
	}

	private static readonly AsyncLocal<Session?> _currentSession = new();

	public static IDisposable PushSessionScoped()
	{
		if (_currentSession.Value != null)
		{
			return NoopScope.Instance;
		}

		_currentSession.Value = new Session();
		return new RootSessionScope();
	}

	public static IDisposable PushEffectScoped(CardExtraEffect effect, int fallbackAppliedAmount, int fallbackAppliedCount = 0)
	{
		Session? session = _currentSession.Value;
		if (session == null || effect == null)
		{
			return NoopScope.Instance;
		}

		EffectFrame frame = new EffectFrame
		{
			Effect = effect,
			FallbackAppliedAmount = ClampNonNegative(fallbackAppliedAmount),
			FallbackAppliedCount = ClampNonNegative(fallbackAppliedCount)
		};
		if (ShouldDefaultActualResultToZero(effect.Kind))
		{
			frame.HasReportedAppliedAmount = true;
			frame.HasReportedAppliedCount = true;
		}
		session.Frames.Push(frame);
		return new EffectScope(session, frame);
	}

	public static Dictionary<string, List<CardModel>> CaptureCurrentSelectedCards(bool cloneCards)
	{
		Session? session = _currentSession.Value;
		if (session == null || session.SelectedCardsByEffectId.Count == 0)
		{
			return new Dictionary<string, List<CardModel>>(StringComparer.Ordinal);
		}

		return CloneSelectedCardsSnapshot(session.SelectedCardsByEffectId, cloneCards);
	}

	public static Dictionary<string, List<CardModel>> CloneSelectedCardsSnapshot(IReadOnlyDictionary<string, List<CardModel>>? selectedCardsByEffectId, bool cloneCards)
	{
		Dictionary<string, List<CardModel>> snapshot = new(StringComparer.Ordinal);
		if (selectedCardsByEffectId == null || selectedCardsByEffectId.Count == 0)
		{
			return snapshot;
		}

		foreach ((string rawKey, List<CardModel> cards) in selectedCardsByEffectId)
		{
			string key = rawKey?.Trim() ?? string.Empty;
			if (key.Length == 0)
			{
				continue;
			}

			List<CardModel> copied = CopySelectedCards(cards, cloneCards);
			if (copied.Count > 0)
			{
				snapshot[key] = copied;
			}
		}
		return snapshot;
	}

	public static IDisposable PushSelectedCardsScoped(IReadOnlyDictionary<string, List<CardModel>>? selectedCardsByEffectId)
	{
		Session? session = _currentSession.Value;
		if (session == null || selectedCardsByEffectId == null || selectedCardsByEffectId.Count == 0)
		{
			return NoopScope.Instance;
		}

		return new SelectedCardsScope(session, selectedCardsByEffectId);
	}

	private static bool ShouldDefaultActualResultToZero(CardExtraEffectKind kind)
	{
		return kind is CardExtraEffectKind.DrawCards
			or CardExtraEffectKind.DrawCardsThatCostLess
			or CardExtraEffectKind.AddCopyOfThisCard
			or CardExtraEffectKind.AddExactCopyOfThisCardToDeck
			or CardExtraEffectKind.AddRandomCardToHand
			or CardExtraEffectKind.ChooseOneOfThreeCardsToHand
			or CardExtraEffectKind.PlayRandomGeneratedCard
			or CardExtraEffectKind.AddSpecificCardToHand
			or CardExtraEffectKind.FetchSpecificCardToHand
			or CardExtraEffectKind.PlayCardFromPile
			or CardExtraEffectKind.MoveCardsBetweenPiles
			or CardExtraEffectKind.UpgradeCardsInPile
			or CardExtraEffectKind.DiscardCards
			or CardExtraEffectKind.ExhaustCards
			or CardExtraEffectKind.TransformCards
			or CardExtraEffectKind.GrantKeywordToPile
			or CardExtraEffectKind.UpgradeDeckCards
			or CardExtraEffectKind.CopyCardsFromPileToDeck
			or CardExtraEffectKind.CopyExactCardsFromPileToDeck
			or CardExtraEffectKind.SelectCardsFromPile
			or CardExtraEffectKind.RemoveCardsFromDeck
			or CardExtraEffectKind.DelayedPileAction
			or CardExtraEffectKind.ConsumeCardValue
			or CardExtraEffectKind.AutoPlaySelfFromPile
			or CardExtraEffectKind.AutoDrawSelfFromPile
			or CardExtraEffectKind.ConditionalAutoPlayFromPile
			or CardExtraEffectKind.ConditionalAutoDrawFromPile
			or CardExtraEffectKind.ConditionalAutoRunEffects;
	}

	public static bool TryGetConfiguredAmount(string? effectId, out int amount)
	{
		amount = 0;
		if (string.IsNullOrWhiteSpace(effectId))
		{
			return false;
		}

		Session? session = _currentSession.Value;
		if (session == null)
		{
			return false;
		}

		return session.ConfiguredAmountsByEffectId.TryGetValue(effectId.Trim(), out amount);
	}

	public static bool TryGetConfiguredCount(string? effectId, out int count)
	{
		count = 0;
		if (string.IsNullOrWhiteSpace(effectId))
		{
			return false;
		}

		Session? session = _currentSession.Value;
		if (session == null)
		{
			return false;
		}

		return session.ConfiguredCountsByEffectId.TryGetValue(effectId.Trim(), out count);
	}

	public static bool TryGetAppliedAmount(string? effectId, out int amount)
	{
		amount = 0;
		if (string.IsNullOrWhiteSpace(effectId))
		{
			return false;
		}

		Session? session = _currentSession.Value;
		if (session == null)
		{
			return false;
		}

		return session.AppliedAmountsByEffectId.TryGetValue(effectId.Trim(), out amount);
	}

	public static bool TryGetTotalDamageAmount(string? effectId, out int amount)
	{
		amount = 0;
		if (string.IsNullOrWhiteSpace(effectId))
		{
			return false;
		}

		Session? session = _currentSession.Value;
		if (session == null)
		{
			return false;
		}

		return session.TotalDamageAmountsByEffectId.TryGetValue(effectId.Trim(), out amount);
	}

	public static bool TryGetBlockedDamageAmount(string? effectId, out int amount)
	{
		amount = 0;
		if (string.IsNullOrWhiteSpace(effectId))
		{
			return false;
		}

		Session? session = _currentSession.Value;
		if (session == null)
		{
			return false;
		}

		return session.BlockedDamageAmountsByEffectId.TryGetValue(effectId.Trim(), out amount);
	}

	public static bool TryGetOverkillDamageAmount(string? effectId, out int amount)
	{
		amount = 0;
		if (string.IsNullOrWhiteSpace(effectId))
		{
			return false;
		}

		Session? session = _currentSession.Value;
		if (session == null)
		{
			return false;
		}

		return session.OverkillDamageAmountsByEffectId.TryGetValue(effectId.Trim(), out amount);
	}

	public static bool TryGetKillCount(string? effectId, out int count)
	{
		count = 0;
		if (string.IsNullOrWhiteSpace(effectId))
		{
			return false;
		}

		Session? session = _currentSession.Value;
		if (session == null)
		{
			return false;
		}

		return session.KillCountsByEffectId.TryGetValue(effectId.Trim(), out count);
	}

	public static bool TryGetAppliedCount(string? effectId, out int count)
	{
		count = 0;
		if (string.IsNullOrWhiteSpace(effectId))
		{
			return false;
		}

		Session? session = _currentSession.Value;
		if (session == null)
		{
			return false;
		}

		return session.AppliedCountsByEffectId.TryGetValue(effectId.Trim(), out count);
	}

	public static bool TryGetSelectedCards(string? effectId, out List<CardModel> cards)
	{
		cards = new List<CardModel>();
		if (string.IsNullOrWhiteSpace(effectId))
		{
			return false;
		}

		Session? session = _currentSession.Value;
		if (session == null)
		{
			return false;
		}

		if (!session.SelectedCardsByEffectId.TryGetValue(effectId.Trim(), out List<CardModel>? storedCards))
		{
			return false;
		}

		foreach (CardModel? card in storedCards)
		{
			if (card != null)
			{
				AddCardReference(cards, card);
			}
		}
		return true;
	}

	public static void ReportCurrentDamageApplied(int amount)
	{
		ReportIfCurrentKindMatches(amount, CardExtraEffectKind.DealDamage, CardExtraEffectKind.LoseHp);
	}

	public static void ReportCurrentDamageTotals(int totalDamageAmount, int damageInstanceCount, int blockedDamageAmount = 0, int overkillDamageAmount = 0)
	{
		if (totalDamageAmount <= 0 && damageInstanceCount <= 0 && blockedDamageAmount <= 0 && overkillDamageAmount <= 0)
		{
			return;
		}

		EffectFrame? frame = GetCurrentFrame();
		if (frame?.Effect == null)
		{
			return;
		}

		if (frame.Effect.Kind != CardExtraEffectKind.DealDamage
			&& frame.Effect.Kind != CardExtraEffectKind.RunEffectSourceCard)
		{
			return;
		}

		if (totalDamageAmount > 0)
		{
			frame.ReportedTotalDamageAmount = ClampNonNegative(frame.ReportedTotalDamageAmount + totalDamageAmount);
			frame.HasReportedTotalDamageAmount = true;
		}

		if (blockedDamageAmount > 0)
		{
			frame.ReportedBlockedDamageAmount = ClampNonNegative(frame.ReportedBlockedDamageAmount + blockedDamageAmount);
			frame.HasReportedBlockedDamageAmount = true;
		}

		if (overkillDamageAmount > 0)
		{
			frame.ReportedOverkillDamageAmount = ClampNonNegative(frame.ReportedOverkillDamageAmount + overkillDamageAmount);
			frame.HasReportedOverkillDamageAmount = true;
		}

		if (damageInstanceCount > 0)
		{
			frame.ReportedAppliedCount = frame.Effect.Kind == CardExtraEffectKind.DealDamage
				? Math.Max(ClampNonNegative(frame.ReportedAppliedCount), ClampNonNegative(damageInstanceCount))
				: ClampNonNegative(frame.ReportedAppliedCount + damageInstanceCount);
			frame.HasReportedAppliedCount = true;
		}
	}

	public static void ReportCurrentDamageResults(IEnumerable<DamageResult>? results)
	{
		if (results == null)
		{
			return;
		}

		Session? session = _currentSession.Value;
		if (session == null)
		{
			return;
		}

		foreach (DamageResult? result in results)
		{
			if (result == null || !session.CurrentPlayDamageResults.Add(result))
			{
				continue;
			}

			if (result.WasTargetKilled)
			{
				ReportCurrentKillCount(1);
			}
		}
	}

	public static void ReportCurrentKillCount(int count)
	{
		if (count <= 0)
		{
			return;
		}

		EffectFrame? frame = GetCurrentFrame();
		if (frame?.Effect == null)
		{
			return;
		}

		if (frame.Effect.Kind != CardExtraEffectKind.DealDamage
			&& frame.Effect.Kind != CardExtraEffectKind.RunEffectSourceCard)
		{
			return;
		}

		frame.ReportedKillCount = ClampNonNegative(frame.ReportedKillCount + count);
		frame.HasReportedKillCount = true;
	}

	public static bool ContainsCurrentPlayDamageResult(DamageResult? result)
	{
		if (result == null)
		{
			return false;
		}

		Session? session = _currentSession.Value;
		return session != null && session.CurrentPlayDamageResults.Contains(result);
	}

	public static void ReportCurrentBlockApplied(int amount)
	{
		ReportIfCurrentKindMatches(amount, CardExtraEffectKind.GainBlock);
	}

	public static void ReportCurrentBlockRemoved(int amount)
	{
		ReportIfCurrentKindMatches(amount, CardExtraEffectKind.RemoveBlock);
	}

	public static void ReportCurrentHealingApplied(int amount)
	{
		ReportIfCurrentKindMatches(amount, CardExtraEffectKind.Heal);
	}

	public static void ReportCurrentEnergyApplied(int amount, bool gain)
	{
		ReportIfCurrentKindMatches(amount, gain ? CardExtraEffectKind.GainEnergy : CardExtraEffectKind.LoseEnergy);
	}

	public static void ReportCurrentStarsApplied(int amount, bool gain)
	{
		ReportIfCurrentKindMatches(amount, gain ? CardExtraEffectKind.GainStars : CardExtraEffectKind.LoseStars);
	}

	public static void ReportCurrentSummonApplied(int amount)
	{
		ReportIfCurrentKindMatches(amount, CardExtraEffectKind.Summon);
	}

	public static void ReportCurrentAppliedResult(int amount, int count)
	{
		if (amount < 0 && count < 0)
		{
			return;
		}

		EffectFrame? frame = GetCurrentFrame();
		if (frame?.Effect == null)
		{
			return;
		}

		if (amount >= 0)
		{
			frame.ReportedAppliedAmount = ClampNonNegative(frame.ReportedAppliedAmount + amount);
			frame.HasReportedAppliedAmount = true;
		}

		if (count >= 0)
		{
			frame.ReportedAppliedCount = ClampNonNegative(frame.ReportedAppliedCount + count);
			frame.HasReportedAppliedCount = true;
		}
	}

	public static void ReportCurrentAppliedCount(int count)
	{
		if (count < 0)
		{
			return;
		}

		ReportCurrentAppliedResult(count, count);
	}

	public static void ReportCurrentSelectedCards(IEnumerable<CardModel>? cards)
	{
		if (cards == null)
		{
			return;
		}

		EffectFrame? frame = GetCurrentFrame();
		if (frame?.Effect == null)
		{
			return;
		}

		foreach (CardModel? card in cards)
		{
			if (card != null)
			{
				AddCardReference(frame.SelectedCards, card);
			}
		}
	}

	public static void ReportCurrentPowerAmountChanged(PowerModel power, int amount)
	{
		if (power == null || amount <= 0)
		{
			return;
		}

		EffectFrame? frame = GetCurrentFrame();
		if (frame?.Effect == null)
		{
			return;
		}

		if (frame.Effect.Kind == CardExtraEffectKind.RunEffectSourceCard)
		{
			frame.ReportedAppliedAmount = ClampNonNegative(frame.ReportedAppliedAmount + amount);
			frame.HasReportedAppliedAmount = true;
			frame.ReportedAppliedCount = ClampNonNegative(frame.ReportedAppliedCount + 1);
			frame.HasReportedAppliedCount = true;
			return;
		}

		if (!MatchesPowerAmountSource(frame.Effect, power))
		{
			return;
		}

		frame.ReportedAppliedAmount = ClampNonNegative(frame.ReportedAppliedAmount + amount);
		frame.HasReportedAppliedAmount = true;
		frame.ReportedAppliedCount = ClampNonNegative(frame.ReportedAppliedCount + 1);
		frame.HasReportedAppliedCount = true;
	}

	private static void ReportIfCurrentKindMatches(int amount, params CardExtraEffectKind[] expectedKinds)
	{
		if (amount <= 0 || expectedKinds == null || expectedKinds.Length == 0)
		{
			return;
		}

		EffectFrame? frame = GetCurrentFrame();
		if (frame?.Effect == null)
		{
			return;
		}

		if (frame.Effect.Kind == CardExtraEffectKind.RunEffectSourceCard)
		{
			frame.ReportedAppliedAmount = ClampNonNegative(frame.ReportedAppliedAmount + amount);
			frame.HasReportedAppliedAmount = true;
			frame.ReportedAppliedCount = ClampNonNegative(frame.ReportedAppliedCount + 1);
			frame.HasReportedAppliedCount = true;
			return;
		}

		foreach (CardExtraEffectKind expectedKind in expectedKinds)
		{
			if (frame.Effect.Kind != expectedKind)
			{
				continue;
			}

			frame.ReportedAppliedAmount = ClampNonNegative(frame.ReportedAppliedAmount + amount);
			frame.HasReportedAppliedAmount = true;
			frame.ReportedAppliedCount = ClampNonNegative(frame.ReportedAppliedCount + 1);
			frame.HasReportedAppliedCount = true;
			return;
		}
	}

	private static EffectFrame? GetCurrentFrame()
	{
		Session? session = _currentSession.Value;
		if (session == null || session.Frames.Count == 0)
		{
			return null;
		}

		return session.Frames.Peek();
	}

	private static bool MatchesPowerAmountSource(CardExtraEffect effect, PowerModel power)
	{
		if (effect == null || power == null)
		{
			return false;
		}

		if (effect.Kind == CardExtraEffectKind.ApplyPower)
		{
			if (string.IsNullOrWhiteSpace(effect.PowerId))
			{
				return false;
			}

			try
			{
				return string.Equals(power.Id.ToString(), effect.PowerId.Trim(), StringComparison.Ordinal);
			}
			catch
			{
				return false;
			}
		}

		return CardEditorExtraEffects.TryGetEffectPowerMatchStatus(effect.Kind, out CardExtraEffectEnemyStatus status)
			&& CardEditorExtraEffects.PowerMatchesStatus(power, status);
	}

	private static void AccumulateIntoParentRunEffectSource(Session session, int appliedAmount, int totalDamageAmount, int blockedDamageAmount, int overkillDamageAmount, int killCount, int appliedCount)
	{
		if (session == null || session.Frames.Count == 0)
		{
			return;
		}

		EffectFrame? parent = session.Frames.Peek();
		if (parent?.Effect?.Kind != CardExtraEffectKind.RunEffectSourceCard)
		{
			return;
		}

		if (appliedAmount > 0)
		{
			parent.ReportedAppliedAmount = ClampNonNegative(parent.ReportedAppliedAmount + appliedAmount);
			parent.HasReportedAppliedAmount = true;
		}

		if (totalDamageAmount > 0)
		{
			parent.ReportedTotalDamageAmount = ClampNonNegative(parent.ReportedTotalDamageAmount + totalDamageAmount);
			parent.HasReportedTotalDamageAmount = true;
		}

		if (blockedDamageAmount > 0)
		{
			parent.ReportedBlockedDamageAmount = ClampNonNegative(parent.ReportedBlockedDamageAmount + blockedDamageAmount);
			parent.HasReportedBlockedDamageAmount = true;
		}

		if (overkillDamageAmount > 0)
		{
			parent.ReportedOverkillDamageAmount = ClampNonNegative(parent.ReportedOverkillDamageAmount + overkillDamageAmount);
			parent.HasReportedOverkillDamageAmount = true;
		}

		if (killCount > 0)
		{
			parent.ReportedKillCount = ClampNonNegative(parent.ReportedKillCount + killCount);
			parent.HasReportedKillCount = true;
		}

		if (appliedCount > 0)
		{
			parent.ReportedAppliedCount = ClampNonNegative(parent.ReportedAppliedCount + appliedCount);
			parent.HasReportedAppliedCount = true;
		}
	}

	private static void AccumulateSelectedCardsIntoParentRunEffectSource(Session session, IEnumerable<CardModel>? selectedCards)
	{
		if (session == null || selectedCards == null || session.Frames.Count == 0)
		{
			return;
		}

		EffectFrame? parent = session.Frames.Peek();
		if (parent?.Effect?.Kind != CardExtraEffectKind.RunEffectSourceCard)
		{
			return;
		}

		foreach (CardModel? card in selectedCards)
		{
			if (card != null)
			{
				AddCardReference(parent.SelectedCards, card);
			}
		}
	}

	private static List<CardModel> CopySelectedCards(IEnumerable<CardModel>? selectedCards, bool cloneCards = false)
	{
		List<CardModel> copy = new();
		if (selectedCards == null)
		{
			return copy;
		}

		foreach (CardModel? card in selectedCards)
		{
			if (card != null)
			{
				CardModel? storedCard = cloneCards ? CloneSelectedCardSnapshot(card) : card;
				if (storedCard != null)
				{
					AddCardReference(copy, storedCard);
				}
			}
		}
		return copy;
	}

	private static CardModel? CloneSelectedCardSnapshot(CardModel card)
	{
		if (card == null)
		{
			return null;
		}

		try
		{
			CardModel clone = card.CreateClone();
			try
			{
				clone.Owner = card.Owner;
			}
			catch
			{
			}

			if (CardEditorOverrides.TryGetEffectiveOverride(card, out CardOverride effectiveOverride))
			{
				CardEditorOverrides.SetInstanceOverride(clone, effectiveOverride);
			}

			return clone;
		}
		catch
		{
			return card;
		}
	}

	private static void AddCardReference(List<CardModel> cards, CardModel card)
	{
		if (cards == null || card == null)
		{
			return;
		}

		foreach (CardModel existing in cards)
		{
			if (ReferenceEquals(existing, card))
			{
				return;
			}
		}
		cards.Add(card);
	}

	private static void RemoveFrame(Session session, EffectFrame frame)
	{
		if (session.Frames.Count == 0)
		{
			return;
		}

		if (ReferenceEquals(session.Frames.Peek(), frame))
		{
			session.Frames.Pop();
			return;
		}

		EffectFrame[] snapshot = session.Frames.ToArray();
		session.Frames.Clear();
		bool removed = false;
		for (int i = snapshot.Length - 1; i >= 0; i--)
		{
			EffectFrame candidate = snapshot[i];
			if (!removed && ReferenceEquals(candidate, frame))
			{
				removed = true;
				continue;
			}
			session.Frames.Push(candidate);
		}
	}

	private static int ClampNonNegative(int value)
	{
		return value <= 0 ? 0 : value;
	}
}
