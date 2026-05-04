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
		public Dictionary<string, int> AppliedAmountsByEffectId { get; } = new(StringComparer.Ordinal);
		public Dictionary<string, int> TotalDamageAmountsByEffectId { get; } = new(StringComparer.Ordinal);
		public Dictionary<string, int> BlockedDamageAmountsByEffectId { get; } = new(StringComparer.Ordinal);
		public Dictionary<string, int> OverkillDamageAmountsByEffectId { get; } = new(StringComparer.Ordinal);
		public Dictionary<string, int> KillCountsByEffectId { get; } = new(StringComparer.Ordinal);
		public Dictionary<string, int> AppliedCountsByEffectId { get; } = new(StringComparer.Ordinal);
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
			AccumulateIntoParentRunEffectSource(_session, appliedAmount, totalDamageAmount, blockedDamageAmount, overkillDamageAmount, killCount, appliedCount);
			_session.AppliedAmountsByEffectId[effectId] = ClampNonNegative(appliedAmount);
			_session.TotalDamageAmountsByEffectId[effectId] = ClampNonNegative(totalDamageAmount);
			_session.BlockedDamageAmountsByEffectId[effectId] = ClampNonNegative(blockedDamageAmount);
			_session.OverkillDamageAmountsByEffectId[effectId] = ClampNonNegative(overkillDamageAmount);
			_session.KillCountsByEffectId[effectId] = ClampNonNegative(killCount);
			_session.AppliedCountsByEffectId[effectId] = ClampNonNegative(appliedCount);
			}
		}

	private sealed class NoopScope : IDisposable
	{
		public static readonly NoopScope Instance = new();

		public void Dispose()
		{
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
		session.Frames.Push(frame);
		return new EffectScope(session, frame);
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
