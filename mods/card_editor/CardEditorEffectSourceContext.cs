using System;
using System.Collections.Generic;
using System.Threading;
using MegaCrit.Sts2.Core.Models;

namespace SlayTheSpire2Mod.CardEditor;

internal static class CardEditorEffectSourceContext
{
	private sealed class Scope : IDisposable
	{
		private readonly CardModel? _card;
		private bool _disposed;

		public Scope(CardModel? card)
		{
			_card = card;
		}

		public void Dispose()
		{
			if (_disposed)
			{
				return;
			}
			_disposed = true;
			if (_card != null)
			{
				Pop(_card);
			}
		}
	}

	private static readonly AsyncLocal<Stack<CardModel>?> _stack = new AsyncLocal<Stack<CardModel>?>();

	public static CardModel? Current
	{
		get
		{
			Stack<CardModel>? stack = _stack.Value;
			return stack != null && stack.Count > 0 ? stack.Peek() : null;
		}
	}

	public static IDisposable PushScoped(CardModel card)
	{
		Push(card);
		return new Scope(card);
	}

	public static void Push(CardModel card)
	{
		if (card == null)
		{
			return;
		}
		Stack<CardModel> stack = _stack.Value ??= new Stack<CardModel>();
		stack.Push(card);
	}

	public static void Pop(CardModel card)
	{
		Stack<CardModel>? stack = _stack.Value;
		if (stack == null || stack.Count == 0 || card == null)
		{
			return;
		}
		if (ReferenceEquals(stack.Peek(), card))
		{
			stack.Pop();
			return;
		}

		CardModel[] snapshot = stack.ToArray();
		stack.Clear();
		bool removed = false;
		for (int i = snapshot.Length - 1; i >= 0; i--)
		{
			CardModel item = snapshot[i];
			if (!removed && ReferenceEquals(item, card))
			{
				removed = true;
				continue;
			}
			stack.Push(item);
		}
	}
}

