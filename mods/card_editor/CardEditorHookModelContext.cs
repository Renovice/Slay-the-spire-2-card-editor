using System.Collections.Generic;
using System.Threading;
using HarmonyLib;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace SlayTheSpire2Mod.CardEditor;

internal static class CardEditorHookModelContext
{
	private static readonly AsyncLocal<Stack<AbstractModel>?> _stack = new AsyncLocal<Stack<AbstractModel>?>();

	public static AbstractModel? Current
	{
		get
		{
			Stack<AbstractModel>? stack = _stack.Value;
			return stack != null && stack.Count > 0 ? stack.Peek() : null;
		}
	}

	public static void Push(AbstractModel model)
	{
		if (model == null)
		{
			return;
		}

		Stack<AbstractModel> stack = _stack.Value ??= new Stack<AbstractModel>();
		stack.Push(model);
	}

	public static void Pop(AbstractModel model)
	{
		Stack<AbstractModel>? stack = _stack.Value;
		if (stack == null || stack.Count == 0 || model == null)
		{
			return;
		}
		if (ReferenceEquals(stack.Peek(), model))
		{
			stack.Pop();
			return;
		}

		AbstractModel[] snapshot = stack.ToArray();
		stack.Clear();
		bool removed = false;
		for (int i = snapshot.Length - 1; i >= 0; i--)
		{
			AbstractModel item = snapshot[i];
			if (!removed && ReferenceEquals(item, model))
			{
				removed = true;
				continue;
			}
			stack.Push(item);
		}
	}
}

[HarmonyPatch(typeof(PlayerChoiceContext), nameof(PlayerChoiceContext.PushModel))]
internal static class PlayerChoiceContext_PushModel_CardEditorHookModelContext_Patch
{
	public static void Prefix(AbstractModel model)
	{
		CardEditorHookModelContext.Push(model);
	}
}

[HarmonyPatch(typeof(PlayerChoiceContext), nameof(PlayerChoiceContext.PopModel))]
internal static class PlayerChoiceContext_PopModel_CardEditorHookModelContext_Patch
{
	public static void Prefix(AbstractModel model)
	{
		CardEditorHookModelContext.Pop(model);
	}
}

