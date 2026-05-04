using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;

namespace SlayTheSpire2Mod.CardEditor;

internal sealed class CardEditorVisibleExtraEffectPower : PowerModel
{
	private sealed record PendingPayload(long EntryId, CardModel SourceCard, CardExtraEffect Effect, int VisibleAmount, bool ShowAmountLabel);

	private sealed class PendingPayloadScope(PendingPayload? previous) : IDisposable
	{
		public void Dispose()
		{
			_pendingPayload.Value = previous;
		}
	}

	private static readonly AsyncLocal<PendingPayload?> _pendingPayload = new AsyncLocal<PendingPayload?>();

	private long _entryId;
	private CardModel? _sourceCard;
	private CardExtraEffect? _sourceEffect;
	private int _visibleAmount;
	private bool _showAmountLabel;

	public long EntryId => _entryId;

	public CardModel? SourceCard => _sourceCard;

	public CardExtraEffect? SourceEffect => _sourceEffect;

	public override LocString Title
	{
		get
		{
			if (!string.IsNullOrWhiteSpace(_sourceEffect?.CustomPowerName))
			{
				return CreateRuntimeTitleLocString(_sourceEffect.CustomPowerName.Trim());
			}

			return _sourceCard?.TitleLocString ?? base.Title;
		}
	}

	public override PowerType Type => PowerType.Buff;

	public override PowerStackType StackType => _showAmountLabel ? PowerStackType.Counter : PowerStackType.Single;

	public override bool IsInstanced => true;

	public override bool ShouldPlayVfx => false;

	public override bool ShouldReceiveCombatHooks => false;

	protected override bool IsVisibleInternal => true;

	public override int DisplayAmount => _visibleAmount;

	public static IDisposable PushPendingPayload(long entryId, CardModel sourceCard, CardExtraEffect effect, int visibleAmount, bool showAmountLabel)
	{
		PendingPayload? previous = _pendingPayload.Value;
		_pendingPayload.Value = new PendingPayload(
			entryId,
			sourceCard,
			CardEditorExtraEffects.CloneEffect(effect),
			visibleAmount,
			showAmountLabel);
		return new PendingPayloadScope(previous);
	}

	public override Task BeforeApplied(Creature target, decimal amount, Creature? applier, CardModel? cardSource)
	{
		ApplyPayload(_pendingPayload.Value);
		return Task.CompletedTask;
	}

	protected override void DeepCloneFields()
	{
		base.DeepCloneFields();
		if (_sourceEffect != null)
		{
			_sourceEffect = CardEditorExtraEffects.CloneEffect(_sourceEffect);
		}
	}

	internal void SyncFromEntry(long entryId, CardModel sourceCard, CardExtraEffect effect, int visibleAmount, bool showAmountLabel)
	{
		int previousAmount = _visibleAmount;
		bool previousShowAmount = _showAmountLabel;
		ApplyPayload(new PendingPayload(
			entryId,
			sourceCard,
			CardEditorExtraEffects.CloneEffect(effect),
			visibleAmount,
			showAmountLabel));

		if (previousAmount != _visibleAmount || previousShowAmount != _showAmountLabel)
		{
			InvokeDisplayAmountChanged();
		}
	}

	internal string GetTooltipBody()
	{
		if (!string.IsNullOrWhiteSpace(_sourceEffect?.CustomPowerDescription))
		{
			return _sourceEffect.CustomPowerDescription.Trim();
		}

		if (_sourceCard != null && _sourceEffect != null)
		{
			string? formatted = CardEditorExtraEffects.FormatSingleEffectLine(
				_sourceCard,
				_sourceEffect,
				useLimitSourceInstance: CardEditorAutoPlayLoopGuard.BuildPowerUseLimitInstanceKey(_entryId));
			if (!string.IsNullOrWhiteSpace(formatted))
			{
				return formatted.Trim();
			}
		}

		if (_sourceCard != null)
		{
			try
			{
				string description = string.Empty;
				if (CardEditorExtraEffects.TryAppendDescription(_sourceCard, ref description, _sourceCard.CurrentTarget, isUpgradePreview: false)
					&& !string.IsNullOrWhiteSpace(description))
				{
					return description.Trim();
				}
			}
			catch
			{
			}
		}

		if (_sourceCard?.Title != null)
		{
			string title = _sourceCard.Title;
			if (!string.IsNullOrWhiteSpace(title))
			{
				return title;
			}
		}

		return base.Title.GetFormattedText();
	}

	internal string GetDefaultTooltipTitle()
	{
		if (!string.IsNullOrWhiteSpace(_sourceCard?.Title))
		{
			return _sourceCard.Title.Trim();
		}

		try
		{
			string? title = _sourceCard?.TitleLocString.GetFormattedText();
			if (!string.IsNullOrWhiteSpace(title))
			{
				return title.Trim();
			}
		}
		catch
		{
		}

		return string.Empty;
	}

	private void ApplyPayload(PendingPayload? payload)
	{
		if (payload == null)
		{
			return;
		}

		_entryId = payload.EntryId;
		_sourceCard = payload.SourceCard;
		_sourceEffect = payload.Effect;
		_visibleAmount = payload.VisibleAmount;
		_showAmountLabel = payload.ShowAmountLabel;
	}

	private static LocString CreateRuntimeTitleLocString(string title)
	{
		string safeTitle = string.IsNullOrWhiteSpace(title) ? "Card Editor Power" : title.Trim();
		string hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(safeTitle)));
		string titleKey = "CARD_EDITOR.RUNTIME_POWER_TITLE." + hash;

		if (LocManager.Instance != null)
		{
			try
			{
				LocTable table = LocManager.Instance.GetTable("extensions");
				table.MergeWith(new Dictionary<string, string>
				{
					[titleKey] = safeTitle
				});
			}
			catch
			{
			}
		}

		return new LocString("extensions", titleKey);
	}
}
