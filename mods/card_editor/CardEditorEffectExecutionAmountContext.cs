using System;
using System.Collections.Generic;
using System.Threading;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Models;

namespace SlayTheSpire2Mod.CardEditor;

internal static class CardEditorEffectExecutionAmountContext
{
	private sealed class Session
	{
		public Dictionary<string, int> AppliedAmountsByEffectId { get; } = new(StringComparer.Ordinal);
		public Stack<EffectFrame> Frames { get; } = new();
	}

	private sealed class EffectFrame
	{
		public required CardExtraEffect Effect { get; init; }
		public required int FallbackAppliedAmount { get; init; }
		public int ReportedAppliedAmount { get; set; }
		public bool HasReportedAppliedAmount { get; set; }
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
			_session.AppliedAmountsByEffectId[effectId] = ClampNonNegative(appliedAmount);
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

	public static IDisposable PushEffectScoped(CardExtraEffect effect, int fallbackAppliedAmount)
	{
		Session? session = _currentSession.Value;
		if (session == null || effect == null)
		{
			return NoopScope.Instance;
		}

		EffectFrame frame = new EffectFrame
		{
			Effect = effect,
			FallbackAppliedAmount = ClampNonNegative(fallbackAppliedAmount)
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

	public static void ReportCurrentDamageApplied(int amount)
	{
		ReportIfCurrentKindMatches(amount, CardExtraEffectKind.DealDamage, CardExtraEffectKind.LoseHp);
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

		if (!MatchesPowerAmountSource(frame.Effect, power))
		{
			return;
		}

		frame.ReportedAppliedAmount = ClampNonNegative(frame.ReportedAppliedAmount + amount);
		frame.HasReportedAppliedAmount = true;
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

		foreach (CardExtraEffectKind expectedKind in expectedKinds)
		{
			if (frame.Effect.Kind != expectedKind)
			{
				continue;
			}

			frame.ReportedAppliedAmount = ClampNonNegative(frame.ReportedAppliedAmount + amount);
			frame.HasReportedAppliedAmount = true;
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
