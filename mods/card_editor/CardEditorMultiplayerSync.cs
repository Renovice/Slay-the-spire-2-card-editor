using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Multiplayer;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Multiplayer.Game.Lobby;
using MegaCrit.Sts2.Core.Multiplayer.Serialization;
using MegaCrit.Sts2.Core.Multiplayer.Transport;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
using MegaCrit.Sts2.Core.Nodes.Screens.Settings;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.addons.mega_text;

namespace SlayTheSpire2Mod.CardEditor;

internal sealed class CardEditorMultiplayerStateDto
{
	public int Version { get; set; } = 1;
	public CardEditorMultiplayerAuthorityMode AuthorityMode { get; set; } = CardEditorMultiplayerAuthorityMode.HostOnly;
	public Dictionary<string, CardEditorPresetStore.CardOverrideDto> Overrides { get; set; } = new(StringComparer.Ordinal);
	public Dictionary<string, List<string>> BaseDecks { get; set; } = new(StringComparer.Ordinal);
	public Dictionary<string, CardEditorMultiplayerCreatedCardDto> CreatedCards { get; set; } = new(StringComparer.Ordinal);
}

internal sealed class CardEditorMultiplayerCreatedCardDto
{
	public bool Enabled { get; set; }
	public string? Title { get; set; }
	public string? Pool { get; set; }
	public string? PoolTitle { get; set; }
	public string? Rarity { get; set; }
	public string? Type { get; set; }
	public string? TargetType { get; set; }
	public bool FullArt { get; set; }
	public string? Finish { get; set; }
	public Dictionary<string, decimal>? FinishParams { get; set; }
	public List<string>? EffectSourceCardIds { get; set; }
	public string? EffectSourcePlacement { get; set; }
	public string? PortraitSourceCardId { get; set; }
	public string? CustomPortraitFile { get; set; }
	public bool? CustomTextEnabled { get; set; }
	public string? CustomText { get; set; }
	public bool? CustomTextUpgradedEnabled { get; set; }
	public string? CustomTextUpgraded { get; set; }
	public CardEditorPresetStore.CardOverrideDto? Override { get; set; }

	public static CardEditorMultiplayerCreatedCardDto FromDefinition(CardEditorCreatedCardDefinition definition)
	{
		return new CardEditorMultiplayerCreatedCardDto
		{
			Enabled = definition.Enabled,
			Title = definition.Title,
			Pool = definition.Pool.ToString(),
			PoolTitle = definition.PoolTitle,
			Rarity = definition.Rarity.ToString(),
			Type = definition.Type.ToString(),
			TargetType = definition.TargetType.ToString(),
			FullArt = definition.FullArt,
			Finish = definition.Finish.ToString(),
			FinishParams = definition.FinishParams != null && definition.FinishParams.Count > 0
				? definition.FinishParams.ToDictionary(kvp => kvp.Key, kvp => (decimal)kvp.Value, StringComparer.Ordinal)
				: null,
			EffectSourceCardIds = definition.EffectSourceCardIds?
				.Select(id => id?.ToString())
				.Where(text => !string.IsNullOrWhiteSpace(text))
				.Select(text => text!)
				.ToList(),
			EffectSourcePlacement = definition.EffectSourcePlacement.ToString(),
			PortraitSourceCardId = definition.PortraitSourceCardId?.ToString(),
			CustomPortraitFile = definition.CustomPortraitFile,
			CustomTextEnabled = definition.CustomTextEnabled,
			CustomText = definition.CustomText,
			CustomTextUpgradedEnabled = definition.CustomTextUpgradedEnabled,
			CustomTextUpgraded = definition.CustomTextUpgraded,
			Override = CardEditorPresetStore.CardOverrideDto.FromOverride(definition.Override ?? new CardOverride())
		};
	}

	public CardEditorCreatedCardDefinition ToDefinitionSafe(ModelId cardId)
	{
		CardEditorCreatedCardDefinition definition = new CardEditorCreatedCardDefinition
		{
			Enabled = Enabled,
			Title = Title?.Trim() ?? string.Empty,
			PoolTitle = string.IsNullOrWhiteSpace(PoolTitle) ? null : PoolTitle.Trim(),
			FullArt = FullArt,
			Finish = CardEditorVisualFinish.None,
			Override = new CardOverride()
		};

		if (!string.IsNullOrWhiteSpace(Pool) && Enum.TryParse(Pool, out CardEditorCreatedCardPool parsedPool))
		{
			definition.Pool = parsedPool;
		}

		if (!string.IsNullOrWhiteSpace(Rarity) && Enum.TryParse(Rarity, out CardRarity parsedRarity))
		{
			definition.Rarity = parsedRarity;
		}

		if (!string.IsNullOrWhiteSpace(Type) && Enum.TryParse(Type, out CardType parsedType) && parsedType != CardType.None)
		{
			definition.Type = parsedType;
		}

		if (!string.IsNullOrWhiteSpace(TargetType) && Enum.TryParse(TargetType, out TargetType parsedTargetType))
		{
			definition.TargetType = parsedTargetType;
		}

		if (!string.IsNullOrWhiteSpace(Finish)
			&& Enum.TryParse(Finish, ignoreCase: true, out CardEditorVisualFinish parsedFinish))
		{
			definition.Finish = parsedFinish;
		}

		if (FinishParams != null && FinishParams.Count > 0)
		{
			definition.FinishParams = FinishParams.ToDictionary(kvp => kvp.Key, kvp => (float)kvp.Value, StringComparer.Ordinal);
		}

		if (EffectSourceCardIds != null)
		{
			foreach (string rawId in EffectSourceCardIds)
			{
				if (TryParseModelId(rawId, out ModelId parsedEffectSourceId))
				{
					definition.EffectSourceCardIds.Add(parsedEffectSourceId);
				}
			}
		}

		if (!string.IsNullOrWhiteSpace(EffectSourcePlacement)
			&& Enum.TryParse(EffectSourcePlacement, ignoreCase: true, out CardEditorEffectSourcePlacement parsedPlacement))
		{
			definition.EffectSourcePlacement = parsedPlacement;
		}

		if (!string.IsNullOrWhiteSpace(PortraitSourceCardId) && TryParseModelId(PortraitSourceCardId, out ModelId portraitId))
		{
			definition.PortraitSourceCardId = portraitId;
		}

		if (!string.IsNullOrWhiteSpace(CustomPortraitFile))
		{
			definition.CustomPortraitFile = CustomPortraitFile.Trim();
		}

		definition.CustomTextEnabled = CustomTextEnabled ?? (CustomText != null);
		definition.CustomTextUpgradedEnabled = CustomTextUpgradedEnabled ?? (CustomTextUpgraded != null);

		if (CustomText != null)
		{
			definition.CustomText = string.IsNullOrWhiteSpace(CustomText) ? string.Empty : CustomText;
		}

		if (CustomTextUpgraded != null)
		{
			definition.CustomTextUpgraded = string.IsNullOrWhiteSpace(CustomTextUpgraded) ? string.Empty : CustomTextUpgraded;
		}

		if (Override != null)
		{
			try
			{
				definition.Override = Override.ToOverrideSafe(cardId, fileVersion: 8) ?? new CardOverride();
			}
			catch
			{
				definition.Override = new CardOverride();
			}
		}

		return definition;
	}

	private static bool TryParseModelId(string text, out ModelId modelId)
	{
		try
		{
			modelId = ModelId.Deserialize(text);
			return true;
		}
		catch
		{
			modelId = ModelId.none;
			return false;
		}
	}
}

internal sealed class CardEditorMultiplayerSyncRequestMessage : INetMessage, IPacketSerializable
{
	public bool ShouldBroadcast => false;
	public NetTransferMode Mode => NetTransferMode.Reliable;
	public LogLevel LogLevel => LogLevel.Debug;

	public void Serialize(PacketWriter writer)
	{
	}

	public void Deserialize(PacketReader reader)
	{
	}
}

internal sealed class CardEditorMultiplayerSnapshotMessage : INetMessage, IPacketSerializable
{
	public int Sequence { get; set; }
	public string StateJson { get; set; } = string.Empty;

	public bool ShouldBroadcast => true;
	public NetTransferMode Mode => NetTransferMode.Reliable;
	public LogLevel LogLevel => LogLevel.Debug;

	public void Serialize(PacketWriter writer)
	{
		writer.WriteInt(Sequence);
		writer.WriteString(StateJson ?? string.Empty);
	}

	public void Deserialize(PacketReader reader)
	{
		Sequence = reader.ReadInt();
		StateJson = reader.ReadString();
	}
}

internal sealed class CardEditorMultiplayerEditRequestMessage : INetMessage, IPacketSerializable
{
	public string StateJson { get; set; } = string.Empty;

	public bool ShouldBroadcast => false;
	public NetTransferMode Mode => NetTransferMode.Reliable;
	public LogLevel LogLevel => LogLevel.Debug;

	public void Serialize(PacketWriter writer)
	{
		writer.WriteString(StateJson ?? string.Empty);
	}

	public void Deserialize(PacketReader reader)
	{
		StateJson = reader.ReadString();
	}
}

internal sealed class CardEditorMultiplayerLocalBackup
{
	public required Dictionary<ModelId, CardOverride> Overrides { get; init; }
	public required Dictionary<ModelId, List<ModelId>> BaseDecks { get; init; }
	public required Dictionary<ModelId, CardEditorCreatedCardDefinition> CreatedCards { get; init; }
}

internal partial class CardEditorMultiplayerSyncRunner : Node
{
	public override void _Ready()
	{
		Name = "CardEditorMultiplayerSyncRunner";
		ProcessMode = ProcessModeEnum.Always;
	}

	public override void _Process(double delta)
	{
		CardEditorMultiplayerSync.Update();
	}
}

internal static class CardEditorMultiplayerSync
{
	private const string TickboxTemplateLineName = "LimitFpsInBackground";
	private const string TickboxTemplateFallbackLineName = "CommonTooltips";
	private const string SyncLineName = "CardEditorMultiplayerSyncLine";
	private const string AuthorityLineName = "CardEditorMultiplayerAuthorityLine";
	private const string SyncDividerName = "CardEditorMultiplayerSyncRowDivider";
	private const string AuthorityDividerName = "CardEditorMultiplayerAuthorityRowDivider";
	private const string SyncLabelName = "CardEditorMultiplayerSyncLabel";
	private const string AuthorityLabelName = "CardEditorMultiplayerAuthorityLabel";
	private const string SyncTickboxName = "CardEditorMultiplayerSyncTickbox";
	private const string AuthorityTickboxName = "CardEditorMultiplayerAuthorityTickbox";
	private const string SendFeedbackLineName = "SendFeedback";
	private const string SyncHeaderLocKey = "CARD_EDITOR_MULTIPLAYER_SYNC_HEADER";
	private const string SyncDescriptionLocKey = "CARD_EDITOR_MULTIPLAYER_SYNC_DESCRIPTION";
	private const string AuthorityHeaderLocKey = "CARD_EDITOR_MULTIPLAYER_AUTHORITY_HEADER";
	private const string AuthorityDescriptionLocKey = "CARD_EDITOR_MULTIPLAYER_AUTHORITY_DESCRIPTION";

	private static readonly JsonSerializerOptions _jsonOptions = new()
	{
		WriteIndented = false
	};

	private static readonly MessageHandlerDelegate<CardEditorMultiplayerSyncRequestMessage> _syncRequestHandler = OnSyncRequestReceived;
	private static readonly MessageHandlerDelegate<CardEditorMultiplayerSnapshotMessage> _snapshotHandler = OnSnapshotReceived;
	private static readonly MessageHandlerDelegate<CardEditorMultiplayerEditRequestMessage> _editRequestHandler = OnEditRequestReceived;

	private static INetGameService? _netService;
	private static CardEditorMultiplayerSyncRunner? _runner;
	private static CardEditorMultiplayerLocalBackup? _localBackup;
	private static bool _requestedInitialSync;
	private static bool _forceImmediateBroadcast;
	private static int _lastAppliedSequence;
	private static int _lastSeenOverrideRevision = -1;
	private static int _lastSeenBaseDeckRevision = -1;
	private static int _lastSeenCreatedCardsRevision = -1;
	private static int _lastSeenSettingsRevision = -1;
	private static int _nextSequence = 1;
	private static CardEditorMultiplayerAuthorityMode _remoteAuthorityMode = CardEditorMultiplayerAuthorityMode.HostOnly;
	private static bool _isRefreshingSettingsUi;
	private static bool _settingsLocInjected;

	public static bool IsBoundToMultiplayerSession => _netService != null && _netService.Type.IsMultiplayer();

	public static bool IsHostSession => _netService?.Type == NetGameType.Host;

	public static bool IsSyncEnabledForCurrentSession => IsBoundToMultiplayerSession && CardEditorMultiplayerSettings.MultiplayerSyncEnabled;

	public static bool CanEditSharedState()
	{
		if (!IsSyncEnabledForCurrentSession)
		{
			return true;
		}

		return _netService!.Type switch
		{
			NetGameType.Host => true,
			NetGameType.Client => _remoteAuthorityMode == CardEditorMultiplayerAuthorityMode.AnyPlayer,
			_ => true
		};
	}

	public static void BindToNetService(INetGameService? netService)
	{
		CardEditorMultiplayerSettings.EnsureLoaded();
		EnsureRunner();

		if (ReferenceEquals(_netService, netService))
		{
			return;
		}

		DetachCurrentService(restoreLocalState: true);

		if (netService == null || !netService.Type.IsMultiplayer())
		{
			return;
		}

		_netService = netService;
		_netService.RegisterMessageHandler(_syncRequestHandler);
		_netService.RegisterMessageHandler(_snapshotHandler);
		_netService.RegisterMessageHandler(_editRequestHandler);
		_netService.Disconnected += OnServiceDisconnected;
		_requestedInitialSync = false;
		_forceImmediateBroadcast = _netService.Type == NetGameType.Host && CardEditorMultiplayerSettings.MultiplayerSyncEnabled;
		_lastAppliedSequence = 0;
		_remoteAuthorityMode = CardEditorMultiplayerAuthorityMode.HostOnly;
		CaptureRevisionCheckpoint();
		Log.Info($"[CardEditor][MultiplayerSync] Bound to {_netService.Type} service netId={_netService.NetId}");
	}

	public static void UnbindFromNetService(INetGameService? expectedService = null)
	{
		if (_netService == null)
		{
			return;
		}

		if (expectedService != null && !ReferenceEquals(_netService, expectedService))
		{
			return;
		}

		DetachCurrentService(restoreLocalState: true);
	}

	public static void NotifySharedStateMutatedLocally()
	{
		if (!IsSyncEnabledForCurrentSession || _netService == null)
		{
			return;
		}

		if (_netService.Type == NetGameType.Host)
		{
			_forceImmediateBroadcast = true;
			return;
		}

		if (_netService.Type == NetGameType.Client && _remoteAuthorityMode == CardEditorMultiplayerAuthorityMode.AnyPlayer)
		{
			SendEditRequestToHost();
		}
	}

	internal static void MarkSettingsDirtyForBroadcast()
	{
		if (_netService?.Type == NetGameType.Host && CardEditorMultiplayerSettings.MultiplayerSyncEnabled)
		{
			_forceImmediateBroadcast = true;
		}
	}

	internal static void ResetInitialSyncRequest()
	{
		if (_netService?.Type == NetGameType.Client)
		{
			_requestedInitialSync = false;
		}
	}

	public static void Update()
	{
		if (_netService == null || !_netService.IsConnected)
		{
			return;
		}

		if (_netService.Type == NetGameType.Client)
		{
			if (CardEditorMultiplayerSettings.MultiplayerSyncEnabled && !_requestedInitialSync)
			{
				_requestedInitialSync = true;
				_netService.SendMessage(new CardEditorMultiplayerSyncRequestMessage());
				Log.Info("[CardEditor][MultiplayerSync] Requested authoritative host snapshot.");
			}

			return;
		}

		if (_netService.Type != NetGameType.Host || !CardEditorMultiplayerSettings.MultiplayerSyncEnabled)
		{
			return;
		}

		if (_forceImmediateBroadcast || RevisionsChangedSinceCheckpoint())
		{
			BroadcastSnapshotToReadyPeers();
			CaptureRevisionCheckpoint();
			_forceImmediateBroadcast = false;
		}
	}

	public static void EnsureSettingsUi(NSettingsScreen screen)
	{
		if (screen == null || !GodotObject.IsInstanceValid(screen))
		{
			return;
		}

		VBoxContainer? content = screen.GetNodeOrNull<NSettingsPanel>("%GeneralSettings")?.Content as VBoxContainer;
		if (content == null)
		{
			return;
		}

		EnsureSettingsLocEntriesInjected();
		LogSettingsUiState(content, "EnsureSettingsUi:start");

		Node? legacyDivider = content.GetNodeOrNull("CardEditorMultiplayerSyncDivider");
		if (legacyDivider != null)
		{
			content.RemoveChild(legacyDivider);
			legacyDivider.QueueFree();
		}

		EnsureSettingsTickboxLine(
			content,
			SyncLineName,
			SyncLabelName,
			SyncTickboxName,
			GetSettingsUiText(SyncHeaderLocKey, "Multiplayer Sync"),
			ticked =>
			{
				if (_isRefreshingSettingsUi)
				{
					return;
				}

				CardEditorMultiplayerSettings.MultiplayerSyncEnabled = ticked;
				if (_netService?.Type == NetGameType.Host)
				{
					_forceImmediateBroadcast = ticked;
				}
				else if (_netService?.Type == NetGameType.Client)
				{
					_requestedInitialSync = false;
				}

				RefreshSettingsUi(content);
			});

		EnsureSettingsTickboxLine(
			content,
			AuthorityLineName,
			AuthorityLabelName,
			AuthorityTickboxName,
			GetSettingsUiText(AuthorityHeaderLocKey, "Any Player Control"),
			ticked =>
			{
				if (_isRefreshingSettingsUi)
				{
					return;
				}

				CardEditorMultiplayerSettings.AuthorityMode = ticked
					? CardEditorMultiplayerAuthorityMode.AnyPlayer
					: CardEditorMultiplayerAuthorityMode.HostOnly;
				if (_netService?.Type == NetGameType.Host && CardEditorMultiplayerSettings.MultiplayerSyncEnabled)
				{
					_forceImmediateBroadcast = true;
				}

				RefreshSettingsUi(content);
			});

		EnsureSettingsDivider(content, SyncDividerName);
		EnsureSettingsDivider(content, AuthorityDividerName);

		PositionSettingsLines(content);
		RefreshSettingsUi(content);
	}

	private static void RefreshSettingsUi(VBoxContainer content)
	{
		_isRefreshingSettingsUi = true;
		try
		{
			NSettingsTickbox? syncTickbox = FindSettingsTickbox(content, SyncLineName);
			NSettingsTickbox? authorityTickbox = FindSettingsTickbox(content, AuthorityLineName);
			RichTextLabel? authorityLabel = FindNamedDescendant<RichTextLabel>(content.GetNodeOrNull(AuthorityLineName), AuthorityLabelName);

			if (syncTickbox != null)
			{
				syncTickbox.IsTicked = CardEditorMultiplayerSettings.MultiplayerSyncEnabled;
			}

			CardEditorMultiplayerAuthorityMode displayedAuthorityMode = GetDisplayedAuthorityMode();
			bool authorityInteractive = CardEditorMultiplayerSettings.MultiplayerSyncEnabled && CanChangeAuthorityModeSetting();

			if (authorityTickbox != null)
			{
				authorityTickbox.IsTicked = displayedAuthorityMode == CardEditorMultiplayerAuthorityMode.AnyPlayer;
				if (authorityInteractive)
				{
					authorityTickbox.Enable();
					authorityTickbox.Modulate = Colors.White;
				}
				else
				{
					authorityTickbox.Disable();
					authorityTickbox.Modulate = StsColors.gray;
				}
			}

			if (authorityLabel != null)
			{
				authorityLabel.Modulate = authorityInteractive ? Colors.White : StsColors.gray;
			}

			LogSettingsUiState(content, "RefreshSettingsUi:end");
		}
		finally
		{
			_isRefreshingSettingsUi = false;
		}
	}

	private static CardEditorMultiplayerAuthorityMode GetDisplayedAuthorityMode()
	{
		if (_netService?.Type == NetGameType.Client && CardEditorMultiplayerSettings.MultiplayerSyncEnabled)
		{
			return _remoteAuthorityMode;
		}

		return CardEditorMultiplayerSettings.AuthorityMode;
	}

	private static bool CanChangeAuthorityModeSetting()
	{
		return _netService?.Type != NetGameType.Client;
	}

	private static void EnsureSettingsTickboxLine(
		VBoxContainer content,
		string lineName,
		string labelName,
		string tickboxName,
		string labelText,
		Action<bool> onToggled)
	{
		if (content.GetNodeOrNull(lineName) != null)
		{
			Log.Info($"[CardEditor][MultiplayerSettings] Reusing existing line '{lineName}'.");
			return;
		}

		Control? templateLine = content.GetNodeOrNull<Control>(TickboxTemplateLineName)
			?? content.GetNodeOrNull<Control>(TickboxTemplateFallbackLineName);
		if (templateLine == null)
		{
			Log.Warn("[CardEditor][MultiplayerSettings] Could not find a vanilla template line for multiplayer settings.");
			return;
		}

		Control duplicatedLine = templateLine.Duplicate() as Control
			?? throw new InvalidOperationException("Failed duplicating vanilla settings line template.");
		duplicatedLine.Name = lineName;

		MegaRichTextLabel? label = FindNamedDescendant<MegaRichTextLabel>(duplicatedLine, "Label");
		if (label != null)
		{
			label.Name = labelName;
			label.Text = labelText;
		}
		else
		{
			Log.Warn($"[CardEditor][MultiplayerSettings] Duplicated line '{lineName}' was missing vanilla Label node.");
		}

		NSettingsTickbox? tickbox = FindDescendantOfType<NSettingsTickbox>(duplicatedLine);
		if (tickbox != null)
		{
			tickbox.Name = tickboxName;
			tickbox.Toggled += toggled => onToggled(toggled.IsTicked);
		}
		else
		{
			Log.Warn($"[CardEditor][MultiplayerSettings] Duplicated line '{lineName}' did not contain a vanilla settings tickbox.");
		}

		content.AddChild(duplicatedLine);

		Log.Info(
			$"[CardEditor][MultiplayerSettings] Added line '{lineName}' label='{labelName}' tickbox='{tickboxName}' " +
			$"contentChildren={content.GetChildCount()} template='{templateLine.Name}' tickboxFound={(tickbox != null)}.");
	}

	private static void EnsureSettingsDivider(VBoxContainer content, string dividerName)
	{
		if (content.GetNodeOrNull(dividerName) != null)
		{
			return;
		}

		Control? templateDivider = FindSettingsDividerTemplate(content);
		if (templateDivider == null)
		{
			Log.Warn($"[CardEditor][MultiplayerSettings] Could not find a vanilla divider template for '{dividerName}'.");
			return;
		}

		Control duplicatedDivider = templateDivider.Duplicate() as Control
			?? throw new InvalidOperationException("Failed duplicating vanilla settings divider template.");
		duplicatedDivider.Name = dividerName;
		duplicatedDivider.Visible = true;
		content.AddChild(duplicatedDivider);
	}

	private static void PositionSettingsLines(VBoxContainer content)
	{
		Node? syncLine = content.GetNodeOrNull(SyncLineName);
		Node? syncDivider = content.GetNodeOrNull(SyncDividerName);
		Node? authorityLine = content.GetNodeOrNull(AuthorityLineName);
		Node? authorityDivider = content.GetNodeOrNull(AuthorityDividerName);
		Node? sendFeedbackLine = content.GetNodeOrNull(SendFeedbackLineName);
		if (sendFeedbackLine == null)
		{
			return;
		}

		if (syncLine != null)
		{
			content.MoveChild(syncLine, sendFeedbackLine.GetIndex());
		}

		if (syncDivider != null)
		{
			content.MoveChild(syncDivider, sendFeedbackLine.GetIndex());
		}

		if (authorityLine != null)
		{
			content.MoveChild(authorityLine, sendFeedbackLine.GetIndex());
		}

		if (authorityDivider != null)
		{
			content.MoveChild(authorityDivider, sendFeedbackLine.GetIndex());
		}
	}

	private static void EnsureSettingsLocEntriesInjected()
	{
		if (_settingsLocInjected)
		{
			return;
		}

		try
		{
			LocTable table = LocManager.Instance.GetTable("settings_ui");
			table.MergeWith(new Dictionary<string, string>
			{
				{ SyncHeaderLocKey, "Multiplayer Sync" },
				{ SyncDescriptionLocKey, "Automatically syncs the host's Card Editor changes for the current multiplayer session without overwriting your own local preset files." },
				{ AuthorityHeaderLocKey, "Any Player Control" },
				{ AuthorityDescriptionLocKey, "Lets any player push Card Editor changes to the shared multiplayer session. When disabled, only the host can sync live edits." }
			});
			_settingsLocInjected = true;
		}
		catch (Exception ex)
		{
			Log.Warn($"[CardEditor][MultiplayerSettings] Failed injecting settings loc entries: {ex}");
		}
	}

	private static string GetSettingsUiText(string key, string fallback)
	{
		try
		{
			return new LocString("settings_ui", key).GetFormattedText();
		}
		catch
		{
			return fallback;
		}
	}

	private static NSettingsTickbox? FindSettingsTickbox(VBoxContainer content, string lineName)
	{
		return FindDescendantOfType<NSettingsTickbox>(content.GetNodeOrNull(lineName));
	}

	private static Control? FindSettingsDividerTemplate(VBoxContainer content)
	{
		Control? templateLine = content.GetNodeOrNull<Control>(TickboxTemplateLineName)
			?? content.GetNodeOrNull<Control>(TickboxTemplateFallbackLineName);
		Control? siblingDivider = FindAdjacentDivider(content, templateLine);
		if (siblingDivider != null)
		{
			return siblingDivider;
		}

		foreach (Node child in content.GetChildren())
		{
			if (child is Control control && control.Name.ToString().Contains("Divider", StringComparison.OrdinalIgnoreCase))
			{
				return control;
			}
		}

		return null;
	}

	private static Control? FindAdjacentDivider(VBoxContainer content, Control? line)
	{
		if (line == null)
		{
			return null;
		}

		int index = line.GetIndex();
		if (index + 1 < content.GetChildCount() && content.GetChild(index + 1) is Control next && next.Name.ToString().Contains("Divider", StringComparison.OrdinalIgnoreCase))
		{
			return next;
		}

		if (index - 1 >= 0 && content.GetChild(index - 1) is Control previous && previous.Name.ToString().Contains("Divider", StringComparison.OrdinalIgnoreCase))
		{
			return previous;
		}

		return null;
	}

	private static T? FindNamedDescendant<T>(Node? root, string name) where T : Node
	{
		if (root == null)
		{
			return null;
		}

		if (root is T typedRoot && root.Name == name)
		{
			return typedRoot;
		}

		foreach (Node child in root.GetChildren())
		{
			T? match = FindNamedDescendant<T>(child, name);
			if (match != null)
			{
				return match;
			}
		}

		return null;
	}

	private static T? FindDescendantOfType<T>(Node? root) where T : Node
	{
		if (root == null)
		{
			return null;
		}

		if (root is T typedRoot)
		{
			return typedRoot;
		}

		foreach (Node child in root.GetChildren())
		{
			T? match = FindDescendantOfType<T>(child);
			if (match != null)
			{
				return match;
			}
		}

		return null;
	}

	private static void LogSettingsUiState(VBoxContainer content, string context)
	{
		try
		{
			Node? syncLine = content.GetNodeOrNull("CardEditorMultiplayerSyncLine");
			Node? authorityLine = content.GetNodeOrNull("CardEditorMultiplayerAuthorityLine");
			NSettingsTickbox? syncTickbox = FindDescendantOfType<NSettingsTickbox>(syncLine);
			NSettingsTickbox? authorityTickbox = FindDescendantOfType<NSettingsTickbox>(authorityLine);
			RichTextLabel? syncLabel = FindNamedDescendant<RichTextLabel>(syncLine, "CardEditorMultiplayerSyncLabel");
			RichTextLabel? authorityLabel = FindNamedDescendant<RichTextLabel>(authorityLine, "CardEditorMultiplayerAuthorityLabel");

			Log.Info(
				$"[CardEditor][MultiplayerSettings] {context} " +
				$"contentChildren={content.GetChildCount()} " +
				$"syncLine={DescribeNode(syncLine)} syncLabel={DescribeControl(syncLabel)} syncTickbox={DescribeControl(syncTickbox)} " +
				$"authorityLine={DescribeNode(authorityLine)} authorityLabel={DescribeControl(authorityLabel)} authorityTickbox={DescribeControl(authorityTickbox)}");
		}
		catch (Exception ex)
		{
			Log.Warn($"[CardEditor][MultiplayerSettings] Failed logging settings UI state during '{context}': {ex}");
		}
	}

	private static string DescribeNode(Node? node)
	{
		if (node == null)
		{
			return "<null>";
		}

		return $"{node.Name}<{node.GetType().Name}> insideTree={node.IsInsideTree()} children={node.GetChildCount()}";
	}

	private static string DescribeControl(Control? control)
	{
		if (control == null)
		{
			return "<null>";
		}

		return
			$"{control.Name}<{control.GetType().Name}> insideTree={control.IsInsideTree()} visible={control.Visible} " +
			$"position={control.Position} size={control.Size} min={control.CustomMinimumSize} " +
			$"parent={(control.GetParent()?.Name.ToString() ?? "<null>")}";
	}

	private static void EnsureRunner()
	{
		if (_runner != null && GodotObject.IsInstanceValid(_runner))
		{
			return;
		}

		if (Engine.GetMainLoop() is not SceneTree sceneTree || sceneTree.Root == null)
		{
			return;
		}

		_runner = new CardEditorMultiplayerSyncRunner();
		sceneTree.Root.AddChild(_runner);
	}

	private static void DetachCurrentService(bool restoreLocalState)
	{
		if (_netService == null)
		{
			return;
		}

		try
		{
			_netService.UnregisterMessageHandler(_syncRequestHandler);
			_netService.UnregisterMessageHandler(_snapshotHandler);
			_netService.UnregisterMessageHandler(_editRequestHandler);
			_netService.Disconnected -= OnServiceDisconnected;
		}
		catch (Exception ex)
		{
			Log.Warn($"[CardEditor][MultiplayerSync] Failed detaching handlers cleanly: {ex}");
		}

		if (restoreLocalState)
		{
			RestoreLocalStateIfNeeded();
		}

		Log.Info($"[CardEditor][MultiplayerSync] Detached from {_netService.Type} service.");
		_netService = null;
		_requestedInitialSync = false;
		_forceImmediateBroadcast = false;
		_lastAppliedSequence = 0;
		_remoteAuthorityMode = CardEditorMultiplayerAuthorityMode.HostOnly;
	}

	private static void OnServiceDisconnected(NetErrorInfo info)
	{
		Log.Info($"[CardEditor][MultiplayerSync] Net service disconnected: {info.GetErrorString()}");
		UnbindFromNetService();
	}

	private static void OnSyncRequestReceived(CardEditorMultiplayerSyncRequestMessage _, ulong senderId)
	{
		if (_netService?.Type != NetGameType.Host || !CardEditorMultiplayerSettings.MultiplayerSyncEnabled)
		{
			return;
		}

		SendSnapshotToPeer(senderId);
	}

	private static void OnSnapshotReceived(CardEditorMultiplayerSnapshotMessage message, ulong senderId)
	{
		if (_netService?.Type != NetGameType.Client || !CardEditorMultiplayerSettings.MultiplayerSyncEnabled)
		{
			return;
		}

		if (message.Sequence <= _lastAppliedSequence)
		{
			return;
		}

		if (!TryDeserializeState(message.StateJson, out CardEditorMultiplayerStateDto? state) || state == null)
		{
			Log.Warn($"[CardEditor][MultiplayerSync] Failed deserializing snapshot from {senderId}.");
			return;
		}

		Log.Info($"[CardEditor][MultiplayerSync] Applying authoritative snapshot seq={message.Sequence} from host={senderId}.");
		ApplyState(state, preserveLocalState: true);
		_remoteAuthorityMode = state.AuthorityMode;
		_lastAppliedSequence = message.Sequence;
	}

	private static void OnEditRequestReceived(CardEditorMultiplayerEditRequestMessage message, ulong senderId)
	{
		if (_netService?.Type != NetGameType.Host
			|| !CardEditorMultiplayerSettings.MultiplayerSyncEnabled
			|| CardEditorMultiplayerSettings.AuthorityMode != CardEditorMultiplayerAuthorityMode.AnyPlayer)
		{
			return;
		}

		if (!TryDeserializeState(message.StateJson, out CardEditorMultiplayerStateDto? state) || state == null)
		{
			Log.Warn($"[CardEditor][MultiplayerSync] Failed deserializing client edit request from {senderId}.");
			return;
		}

		Log.Info($"[CardEditor][MultiplayerSync] Applying client-authored sync request from {senderId}.");
		ApplyState(state, preserveLocalState: false);
		CaptureRevisionCheckpoint();
		BroadcastSnapshotToReadyPeers();
	}

	private static void SendSnapshotToPeer(ulong peerId)
	{
		if (_netService is not INetHostGameService hostService)
		{
			return;
		}

		CardEditorMultiplayerSnapshotMessage message = new()
		{
			Sequence = _nextSequence++,
			StateJson = SerializeState(BuildCurrentState())
		};
		hostService.SendMessage(message, peerId);
		Log.Info($"[CardEditor][MultiplayerSync] Sent snapshot seq={message.Sequence} to peer {peerId}.");
	}

	private static void BroadcastSnapshotToReadyPeers()
	{
		if (_netService == null || _netService.Type != NetGameType.Host)
		{
			return;
		}

		CardEditorMultiplayerSnapshotMessage message = new()
		{
			Sequence = _nextSequence++,
			StateJson = SerializeState(BuildCurrentState())
		};
		_netService.SendMessage(message);
		Log.Info($"[CardEditor][MultiplayerSync] Broadcast snapshot seq={message.Sequence}.");
	}

	private static void SendEditRequestToHost()
	{
		if (_netService?.Type != NetGameType.Client)
		{
			return;
		}

		CardEditorMultiplayerEditRequestMessage message = new()
		{
			StateJson = SerializeState(BuildCurrentState())
		};
		_netService.SendMessage(message);
		Log.Info("[CardEditor][MultiplayerSync] Sent client edit request to host.");
	}

	private static CardEditorMultiplayerStateDto BuildCurrentState()
	{
		CardEditorMultiplayerStateDto dto = new()
		{
			AuthorityMode = CardEditorMultiplayerSettings.AuthorityMode
		};

		foreach ((ModelId cardId, CardOverride overrideData) in CardEditorOverrides.ExportSnapshot())
		{
			if (cardId == null || overrideData == null || overrideData.IsEmpty())
			{
				continue;
			}

			dto.Overrides[cardId.ToString()] = CardEditorPresetStore.CardOverrideDto.FromOverride(overrideData);
		}

		foreach ((ModelId characterId, List<ModelId> cardIds) in CardEditorBaseDeckStore.ExportSnapshot())
		{
			if (characterId == null || !CardEditorBaseDeckStore.IsSupportedCharacterId(characterId))
			{
				continue;
			}

			dto.BaseDecks[characterId.ToString()] = (cardIds ?? new List<ModelId>())
				.Where(cardId => cardId != null && cardId != ModelId.none)
				.Select(cardId => cardId.ToString())
				.ToList();
		}

		foreach ((ModelId cardId, CardEditorCreatedCardDefinition definition) in CardEditorCreatedCardsStore.ExportSnapshot())
		{
			if (cardId == null || !CardEditorCreatedCardsStore.IsCreatedCardId(cardId))
			{
				continue;
			}

			dto.CreatedCards[cardId.ToString()] = CardEditorMultiplayerCreatedCardDto.FromDefinition(definition);
		}

		return dto;
	}

	private static void ApplyState(CardEditorMultiplayerStateDto state, bool preserveLocalState)
	{
		if (preserveLocalState)
		{
			EnsureLocalBackup();
		}

		Dictionary<ModelId, CardEditorCreatedCardDefinition> createdCards = DeserializeCreatedCards(state.CreatedCards);
		Dictionary<ModelId, CardOverride> overrides = DeserializeOverrides(state.Overrides);
		Dictionary<ModelId, List<ModelId>> baseDecks = DeserializeBaseDecks(state.BaseDecks);

		HashSet<ModelId> previousOverrideIds = CardEditorOverrides.AllOverrides.Keys.ToHashSet();
		bool previousPersistenceSuspended = CardEditorCreatedCardsStore.PersistenceSuspended;

		try
		{
			if (_netService?.Type == NetGameType.Client)
			{
				CardEditorCreatedCardsStore.PersistenceSuspended = true;
			}

			CardEditorCreatedCardsStore.ImportSnapshot(createdCards);
			CardEditorOverrides.ReplaceAll(overrides);
			HashSet<ModelId> currentOverrideIds = CardEditorOverrides.AllOverrides.Keys.ToHashSet();
			previousOverrideIds.ExceptWith(currentOverrideIds);
			CardEditorOverrides.ResetExistingCardsForIds(previousOverrideIds);
			CardEditorOverrides.ApplyAllToExistingCards();

			CardEditorBaseDeckStore.ImportSnapshot(baseDecks);
			CardEditorBaseDeckUiState.ClearTransientState();
			CardEditorBaseDeckUiState.EnsureValidCharacter();

			if (CardEditorUiState.TryGetLastLibrary(out MegaCrit.Sts2.Core.Nodes.Screens.CardLibrary.NCardLibrary? library) && library != null)
			{
				CardEditorUiState.RefreshLibrary(library);
			}

			CardEditorBaseDeckUiState.RefreshAll();
		}
		finally
		{
			CardEditorCreatedCardsStore.PersistenceSuspended = previousPersistenceSuspended;
		}
	}

	private static void EnsureLocalBackup()
	{
		if (_localBackup != null)
		{
			return;
		}

		_localBackup = new CardEditorMultiplayerLocalBackup
		{
			Overrides = CardEditorOverrides.ExportSnapshot(),
			BaseDecks = CardEditorBaseDeckStore.ExportSnapshot(),
			CreatedCards = CardEditorCreatedCardsStore.ExportSnapshot()
		};
		Log.Info("[CardEditor][MultiplayerSync] Backed up local card editor state before applying host snapshot.");
	}

	private static void RestoreLocalStateIfNeeded()
	{
		if (_localBackup == null)
		{
			CardEditorCreatedCardsStore.PersistenceSuspended = false;
			return;
		}

		Log.Info("[CardEditor][MultiplayerSync] Restoring local card editor state after multiplayer session.");
		bool previousPersistenceSuspended = CardEditorCreatedCardsStore.PersistenceSuspended;

		try
		{
			CardEditorCreatedCardsStore.PersistenceSuspended = true;
			CardEditorCreatedCardsStore.ImportSnapshot(_localBackup.CreatedCards);
			CardEditorOverrides.ReplaceAll(_localBackup.Overrides);
			CardEditorBaseDeckStore.ImportSnapshot(_localBackup.BaseDecks);
			CardEditorBaseDeckUiState.ClearTransientState();
			CardEditorBaseDeckUiState.EnsureValidCharacter();

			if (CardEditorUiState.TryGetLastLibrary(out MegaCrit.Sts2.Core.Nodes.Screens.CardLibrary.NCardLibrary? library) && library != null)
			{
				CardEditorUiState.RefreshLibrary(library);
			}

			CardEditorBaseDeckUiState.RefreshAll();
		}
		finally
		{
			CardEditorCreatedCardsStore.PersistenceSuspended = previousPersistenceSuspended;
		}

		_localBackup = null;
	}

	private static bool RevisionsChangedSinceCheckpoint()
	{
		return _lastSeenOverrideRevision != CardEditorOverrides.Revision
			|| _lastSeenBaseDeckRevision != CardEditorBaseDeckStore.Revision
			|| _lastSeenCreatedCardsRevision != CardEditorCreatedCardsStore.Revision
			|| _lastSeenSettingsRevision != CardEditorMultiplayerSettings.Revision;
	}

	private static void CaptureRevisionCheckpoint()
	{
		_lastSeenOverrideRevision = CardEditorOverrides.Revision;
		_lastSeenBaseDeckRevision = CardEditorBaseDeckStore.Revision;
		_lastSeenCreatedCardsRevision = CardEditorCreatedCardsStore.Revision;
		_lastSeenSettingsRevision = CardEditorMultiplayerSettings.Revision;
	}

	private static string SerializeState(CardEditorMultiplayerStateDto state)
	{
		return JsonSerializer.Serialize(state, _jsonOptions);
	}

	private static bool TryDeserializeState(string json, out CardEditorMultiplayerStateDto? state)
	{
		state = null;
		if (string.IsNullOrWhiteSpace(json))
		{
			return false;
		}

		try
		{
			state = JsonSerializer.Deserialize<CardEditorMultiplayerStateDto>(json, _jsonOptions);
			return state != null && state.Version == 1;
		}
		catch (Exception ex)
		{
			Log.Warn($"[CardEditor][MultiplayerSync] Failed parsing multiplayer snapshot: {ex}");
			return false;
		}
	}

	private static Dictionary<ModelId, CardOverride> DeserializeOverrides(Dictionary<string, CardEditorPresetStore.CardOverrideDto>? serialized)
	{
		Dictionary<ModelId, CardOverride> overrides = new();
		if (serialized == null)
		{
			return overrides;
		}

		foreach ((string rawId, CardEditorPresetStore.CardOverrideDto dto) in serialized)
		{
			if (!TryParseModelId(rawId, out ModelId modelId) || ModelDb.GetByIdOrNull<MegaCrit.Sts2.Core.Models.CardModel>(modelId) == null)
			{
				continue;
			}

			try
			{
				CardOverride overrideData = dto.ToOverrideSafe(modelId, 8);
				if (overrideData != null && !overrideData.IsEmpty())
				{
					overrides[modelId] = overrideData;
				}
			}
			catch (Exception ex)
			{
				Log.Warn($"[CardEditor][MultiplayerSync] Failed deserializing override for {rawId}: {ex}");
			}
		}

		return overrides;
	}

	private static Dictionary<ModelId, List<ModelId>> DeserializeBaseDecks(Dictionary<string, List<string>>? serialized)
	{
		Dictionary<ModelId, List<ModelId>> baseDecks = new();
		if (serialized == null)
		{
			return baseDecks;
		}

		foreach ((string rawCharacterId, List<string> rawCards) in serialized)
		{
			if (!TryParseModelId(rawCharacterId, out ModelId characterId) || !CardEditorBaseDeckStore.IsSupportedCharacterId(characterId))
			{
				continue;
			}

			List<ModelId> cardIds = new();
			foreach (string rawCardId in rawCards ?? new List<string>())
			{
				if (TryParseModelId(rawCardId, out ModelId cardId) && cardId != ModelId.none && ModelDb.GetByIdOrNull<MegaCrit.Sts2.Core.Models.CardModel>(cardId) != null)
				{
					cardIds.Add(cardId);
				}
			}

			baseDecks[characterId] = cardIds;
		}

		return baseDecks;
	}

	private static Dictionary<ModelId, CardEditorCreatedCardDefinition> DeserializeCreatedCards(Dictionary<string, CardEditorMultiplayerCreatedCardDto>? serialized)
	{
		Dictionary<ModelId, CardEditorCreatedCardDefinition> createdCards = new();
		if (serialized == null)
		{
			return createdCards;
		}

		foreach ((string rawCardId, CardEditorMultiplayerCreatedCardDto dto) in serialized)
		{
			if (!TryParseModelId(rawCardId, out ModelId cardId) || !CardEditorCreatedCardsStore.IsCreatedCardId(cardId))
			{
				continue;
			}

			try
			{
				createdCards[cardId] = dto.ToDefinitionSafe(cardId);
			}
			catch (Exception ex)
			{
				Log.Warn($"[CardEditor][MultiplayerSync] Failed deserializing created card {rawCardId}: {ex}");
			}
		}

		return createdCards;
	}

	private static bool TryParseModelId(string text, out ModelId modelId)
	{
		try
		{
			modelId = ModelId.Deserialize(text);
			return true;
		}
		catch
		{
			modelId = ModelId.none;
			return false;
		}
	}

	internal static bool TryGetMultiplayerSettingsLineKind(Node? node, out CardEditorMultiplayerSettingsLineKind lineKind)
	{
		lineKind = CardEditorMultiplayerSettingsLineKind.None;
		for (Node? current = node; current != null; current = current.GetParent())
		{
			if (current.Name == SyncLineName)
			{
				lineKind = CardEditorMultiplayerSettingsLineKind.Sync;
				return true;
			}

			if (current.Name == AuthorityLineName)
			{
				lineKind = CardEditorMultiplayerSettingsLineKind.Authority;
				return true;
			}
		}

		return false;
	}

	internal static void ApplyMultiplayerSettingsTick(CardEditorMultiplayerSettingsLineKind lineKind, bool ticked)
	{
		switch (lineKind)
		{
			case CardEditorMultiplayerSettingsLineKind.Sync:
				CardEditorMultiplayerSettings.MultiplayerSyncEnabled = ticked;
				if (IsBoundToMultiplayerSession)
				{
					if (IsHostSession)
					{
						MarkSettingsDirtyForBroadcast();
					}
					else
					{
						ResetInitialSyncRequest();
					}
				}
				break;
			case CardEditorMultiplayerSettingsLineKind.Authority:
				CardEditorMultiplayerSettings.AuthorityMode = ticked
					? CardEditorMultiplayerAuthorityMode.AnyPlayer
					: CardEditorMultiplayerAuthorityMode.HostOnly;
				if (IsHostSession && CardEditorMultiplayerSettings.MultiplayerSyncEnabled)
				{
					MarkSettingsDirtyForBroadcast();
				}
				break;
		}
	}

	internal static bool TryShowSettingsHoverTip(Control owner)
	{
		if (!TryBuildSettingsHoverTip(owner, out HoverTip hoverTip))
		{
			return false;
		}

		NHoverTipSet nHoverTipSet = NHoverTipSet.CreateAndShow(owner, hoverTip);
		nHoverTipSet.GlobalPosition = owner.GlobalPosition + NSettingsScreen.settingTipsOffset;
		return true;
	}

	internal static bool TryHideSettingsHoverTip(Control owner)
	{
		if (!TryGetMultiplayerSettingsLineKind(owner, out _))
		{
			return false;
		}

		NHoverTipSet.Remove(owner);
		return true;
	}

	private static bool TryBuildSettingsHoverTip(Control owner, out HoverTip hoverTip)
	{
		hoverTip = default;
		if (!TryGetMultiplayerSettingsLineKind(owner, out CardEditorMultiplayerSettingsLineKind lineKind))
		{
			return false;
		}

		hoverTip = lineKind switch
		{
			CardEditorMultiplayerSettingsLineKind.Sync => new HoverTip(
				new LocString("settings_ui", SyncHeaderLocKey),
				GetSettingsUiText(SyncDescriptionLocKey, "Automatically syncs the host's Card Editor changes for the current multiplayer session without overwriting your own local preset files.")),
			CardEditorMultiplayerSettingsLineKind.Authority => new HoverTip(
				new LocString("settings_ui", AuthorityHeaderLocKey),
				GetSettingsUiText(AuthorityDescriptionLocKey, "Lets any player push Card Editor changes to the shared multiplayer session. When disabled, only the host can sync live edits.")),
			_ => default
		};
		return lineKind != CardEditorMultiplayerSettingsLineKind.None;
	}
}

[HarmonyPatch(typeof(NBackgroundModeTickbox), "OnTick")]
internal static class CardEditorMultiplayerSettingsTickboxTickPatch
{
	private static bool Prefix(NBackgroundModeTickbox __instance)
	{
		if (!CardEditorMultiplayerSync.TryGetMultiplayerSettingsLineKind(__instance, out CardEditorMultiplayerSettingsLineKind lineKind))
		{
			return true;
		}

		CardEditorMultiplayerSync.ApplyMultiplayerSettingsTick(lineKind, ticked: true);
		return false;
	}
}

[HarmonyPatch(typeof(NBackgroundModeTickbox), "OnUntick")]
internal static class CardEditorMultiplayerSettingsTickboxUntickPatch
{
	private static bool Prefix(NBackgroundModeTickbox __instance)
	{
		if (!CardEditorMultiplayerSync.TryGetMultiplayerSettingsLineKind(__instance, out CardEditorMultiplayerSettingsLineKind lineKind))
		{
			return true;
		}

		CardEditorMultiplayerSync.ApplyMultiplayerSettingsTick(lineKind, ticked: false);
		return false;
	}
}

[HarmonyPatch(typeof(NBackgroundModeTickbox), nameof(NBackgroundModeTickbox.SetFromSettings))]
internal static class CardEditorMultiplayerSettingsTickboxSetFromSettingsPatch
{
	private static bool Prefix(NBackgroundModeTickbox __instance)
	{
		if (!CardEditorMultiplayerSync.TryGetMultiplayerSettingsLineKind(__instance, out CardEditorMultiplayerSettingsLineKind lineKind))
		{
			return true;
		}

		__instance.IsTicked = lineKind switch
		{
			CardEditorMultiplayerSettingsLineKind.Sync => CardEditorMultiplayerSettings.MultiplayerSyncEnabled,
			CardEditorMultiplayerSettingsLineKind.Authority => CardEditorMultiplayerSettings.AuthorityMode == CardEditorMultiplayerAuthorityMode.AnyPlayer,
			_ => __instance.IsTicked
		};
		return false;
	}
}

[HarmonyPatch(typeof(NBackgroundModeHoverTip), "OnHovered")]
internal static class CardEditorMultiplayerSettingsHoverTipShowPatch
{
	private static bool Prefix(NBackgroundModeHoverTip __instance)
	{
		return !CardEditorMultiplayerSync.TryShowSettingsHoverTip(__instance);
	}
}

[HarmonyPatch(typeof(NBackgroundModeHoverTip), "OnUnhovered")]
internal static class CardEditorMultiplayerSettingsHoverTipHidePatch
{
	private static bool Prefix(NBackgroundModeHoverTip __instance)
	{
		return !CardEditorMultiplayerSync.TryHideSettingsHoverTip(__instance);
	}
}

[HarmonyPatch(typeof(NCommonTooltipsHoverTip), "OnHovered")]
internal static class CardEditorMultiplayerCommonTooltipsHoverTipShowPatch
{
	private static bool Prefix(NCommonTooltipsHoverTip __instance)
	{
		return !CardEditorMultiplayerSync.TryShowSettingsHoverTip(__instance);
	}
}

[HarmonyPatch(typeof(NCommonTooltipsHoverTip), "OnUnhovered")]
internal static class CardEditorMultiplayerCommonTooltipsHoverTipHidePatch
{
	private static bool Prefix(NCommonTooltipsHoverTip __instance)
	{
		return !CardEditorMultiplayerSync.TryHideSettingsHoverTip(__instance);
	}
}

internal enum CardEditorMultiplayerSettingsLineKind
{
	None = 0,
	Sync = 1,
	Authority = 2
}

[HarmonyPatch(typeof(StartRunLobby), MethodType.Constructor, typeof(GameMode), typeof(INetGameService), typeof(IStartRunLobbyListener), typeof(int))]
internal static class CardEditorStartRunLobbyCtorPatch
{
	private static void Postfix(INetGameService netService)
	{
		CardEditorMultiplayerSync.BindToNetService(netService);
	}
}

[HarmonyPatch(typeof(StartRunLobby), MethodType.Constructor, typeof(GameMode), typeof(INetGameService), typeof(IStartRunLobbyListener), typeof(MegaCrit.Sts2.Core.Daily.TimeServerResult), typeof(int))]
internal static class CardEditorStartRunLobbyDailyCtorPatch
{
	private static void Postfix(INetGameService netService)
	{
		CardEditorMultiplayerSync.BindToNetService(netService);
	}
}

[HarmonyPatch(typeof(LoadRunLobby), MethodType.Constructor, typeof(INetGameService), typeof(ILoadRunLobbyListener), typeof(MegaCrit.Sts2.Core.Saves.SerializableRun))]
internal static class CardEditorLoadRunLobbyCtorPatch
{
	private static void Postfix(INetGameService netService)
	{
		CardEditorMultiplayerSync.BindToNetService(netService);
	}
}

[HarmonyPatch(typeof(LoadRunLobby), MethodType.Constructor, typeof(INetGameService), typeof(ILoadRunLobbyListener), typeof(MegaCrit.Sts2.Core.Multiplayer.Messages.Lobby.ClientLoadJoinResponseMessage))]
internal static class CardEditorLoadRunLobbyResponseCtorPatch
{
	private static void Postfix(INetGameService netService)
	{
		CardEditorMultiplayerSync.BindToNetService(netService);
	}
}

[HarmonyPatch(typeof(RunManager), "InitializeShared")]
internal static class CardEditorRunManagerInitializeSharedPatch
{
	private static void Postfix(INetGameService netService)
	{
		CardEditorMultiplayerSync.BindToNetService(netService);
	}
}

[HarmonyPatch(typeof(RunManager), nameof(RunManager.CleanUp))]
internal static class CardEditorRunManagerCleanUpPatch
{
	private static void Postfix()
	{
		CardEditorMultiplayerSync.UnbindFromNetService();
	}
}

[HarmonyPatch(typeof(NSettingsScreen), nameof(NSettingsScreen._Ready))]
internal static class CardEditorMultiplayerSettingsScreenPatch
{
	private static void Postfix(NSettingsScreen __instance)
	{
		CardEditorMultiplayerSettings.EnsureLoaded();
		CardEditorMultiplayerSync.EnsureSettingsUi(__instance);
	}
}
