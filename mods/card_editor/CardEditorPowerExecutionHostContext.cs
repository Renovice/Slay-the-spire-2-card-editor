using System;
using System.Threading;
using MegaCrit.Sts2.Core.Entities.Creatures;

namespace SlayTheSpire2Mod.CardEditor;

internal static class CardEditorPowerExecutionHostContext
{
	private static readonly AsyncLocal<Creature?> _current = new AsyncLocal<Creature?>();

	public static Creature? Current => _current.Value;

	public static IDisposable PushScoped(Creature? creature)
	{
		Creature? previous = _current.Value;
		_current.Value = creature;
		return new Scope(previous);
	}

	private sealed class Scope : IDisposable
	{
		private readonly Creature? _previous;
		private bool _disposed;

		public Scope(Creature? previous)
		{
			_previous = previous;
		}

		public void Dispose()
		{
			if (_disposed)
			{
				return;
			}

			_disposed = true;
			_current.Value = _previous;
		}
	}
}
