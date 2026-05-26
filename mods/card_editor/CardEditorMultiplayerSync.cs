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
	public int CreatedCardSlotCount { get; set; }
	public Dictionary<string, CardEditorCreatedCardsStore.CreatedCardDto> CreatedCards { get; set; } = new(StringComparer.Ordinal);
	public List<CardEditorPresetStore.CustomKeywordDefinitionDto> KeywordDefinitions { get; set; } = new();
	public List<CardEditorPresetStore.CustomStatusDefinitionDto> StatusDefinitions { get; set; } = new();
	public Dictionary<string, CardEditorRelicOverrideStore.RelicOverrideDto> RelicOverrides { get; set; } = new(StringComparer.Ordinal);
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
	public string? BorderFinish { get; set; }
	public Dictionary<string, decimal>? BorderFinishParams { get; set; }
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
			BorderFinish = definition.BorderFinish != CardEditorVisualFinish.None ? definition.BorderFinish.ToString() : null,
			BorderFinishParams = definition.BorderFinishParams != null && definition.BorderFinishParams.Count > 0
				? definition.BorderFinishParams.ToDictionary(kvp => kvp.Key, kvp => (decimal)kvp.Value, StringComparer.Ordinal)
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

		if (!string.IsNullOrWhiteSpace(BorderFinish)
			&& Enum.TryParse(BorderFinish, ignoreCase: true, out CardEditorVisualFinish parsedBorderFinish))
		{
			definition.BorderFinish = parsedBorderFinish;
		}

		if (BorderFinishParams != null && BorderFinishParams.Count > 0)
		{
			definition.BorderFinishParams = BorderFinishParams.ToDictionary(kvp => kvp.Key, kvp => (float)kvp.Value, StringComparer.Ordinal);
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
	public bool ShouldBuffer => true;
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
	public bool ShouldBuffer => true;
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
	public bool ShouldBuffer => true;
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
	public required List<CardEditorCustomKeywordDefinition> KeywordDefinitions { get; init; }
	public required List<CardEditorCustomStatusDefinition> StatusDefinitions { get; init; }
	public required Dictionary<ModelId, RelicOverride> RelicOverrides { get; init; }
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
	private const string PreloadLineName = "CardEditorPreloadOnLaunchLine";
	private const string LegacyPreloadLineName = "CardEditorLegacyPreloadLine";
	private const string SyncLineName = "CardEditorMultiplayerSyncLine";
	private const string AuthorityLineName = "CardEditorMultiplayerAuthorityLine";
	private const string PreloadDividerName = "CardEditorPreloadOnLaunchRowDivider";
	private const string LegacyPreloadDividerName = "CardEditorLegacyPreloadRowDivider";
	private const string SyncDividerName = "CardEditorMultiplayerSyncRowDivider";
	private const string AuthorityDividerName = "CardEditorMultiplayerAuthorityRowDivider";
	private const string PreloadLabelName = "CardEditorPreloadOnLaunchLabel";
	private const string LegacyPreloadLabelName = "CardEditorLegacyPreloadLabel";
	private const string SyncLabelName = "CardEditorMultiplayerSyncLabel";
	private const string AuthorityLabelName = "CardEditorMultiplayerAuthorityLabel";
	private const string PreloadTickboxName = "CardEditorPreloadOnLaunchTickbox";
	private const string LegacyPreloadTickboxName = "CardEditorLegacyPreloadTickbox";
	private const string SyncTickboxName = "CardEditorMultiplayerSyncTickbox";
	private const string AuthorityTickboxName = "CardEditorMultiplayerAuthorityTickbox";
	private const string SendFeedbackLineName = "SendFeedback";
	private const string PreloadHeaderLocKey = "CARD_EDITOR_PRELOAD_ON_LAUNCH_HEADER";
	private const string PreloadDescriptionLocKey = "CARD_EDITOR_PRELOAD_ON_LAUNCH_DESCRIPTION";
	private const string LegacyPreloadHeaderLocKey = "CARD_EDITOR_LEGACY_PRELOAD_HEADER";
	private const string LegacyPreloadDescriptionLocKey = "CARD_EDITOR_LEGACY_PRELOAD_DESCRIPTION";
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
	private static bool _remoteSyncActive;
	private static int _lastAppliedSequence;
	private static int _lastSeenOverrideRevision = -1;
	private static int _lastSeenBaseDeckRevision = -1;
	private static int _lastSeenCreatedCardsRevision = -1;
	private static int _lastSeenDefinitionRevision = -1;
	private static int _lastSeenRelicRevision = -1;
	private static int _lastSeenSettingsRevision = -1;
	private static int _nextSequence = 1;
	private static CardEditorMultiplayerAuthorityMode _remoteAuthorityMode = CardEditorMultiplayerAuthorityMode.HostOnly;
	private static bool _isRefreshingSettingsUi;
	private static bool _settingsLocInjected;

	public static bool IsBoundToMultiplayerSession => _netService != null && _netService.Type.IsMultiplayer();

	public static bool IsHostSession => _netService?.Type == NetGameType.Host;

	public static bool IsSyncEnabledForCurrentSession
	{
		get
		{
			if (!IsBoundToMultiplayerSession)
			{
				return false;
			}

			return _netService?.Type == NetGameType.Client
				? _remoteSyncActive || CardEditorMultiplayerSettings.MultiplayerSyncEnabled
				: CardEditorMultiplayerSettings.MultiplayerSyncEnabled;
		}
	}

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
		_remoteSyncActive = false;
		CaptureRevisionCheckpoint();
		CardEditorMod.VerboseLog($"[CardEditor][MultiplayerSync] Bound to {_netService.Type} service netId={_netService.NetId}");
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
		if (_netService == null)
		{
			return;
		}

		if (_netService.Type == NetGameType.Host && CardEditorMultiplayerSettings.MultiplayerSyncEnabled)
		{
			_forceImmediateBroadcast = true;
			return;
		}

		if (_netService.Type == NetGameType.Client
			&& _remoteSyncActive
			&& _remoteAuthorityMode == CardEditorMultiplayerAuthorityMode.AnyPlayer)
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
			if (!_requestedInitialSync)
			{
				_requestedInitialSync = true;
				_netService.SendMessage(new CardEditorMultiplayerSyncRequestMessage());
				CardEditorMod.VerboseLog("[CardEditor][MultiplayerSync] Requested authoritative host snapshot.");
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
			PreloadLineName,
			PreloadLabelName,
			PreloadTickboxName,
			GetSettingsUiText(PreloadHeaderLocKey, "Preload On Launch"),
			ticked =>
			{
				if (_isRefreshingSettingsUi)
				{
					return;
				}

				CardEditorPerformanceSettings.SetPreloadEditorPopupsOnLaunch(ticked);
				RefreshSettingsUi(content);
			});

		EnsureSettingsTickboxLine(
			content,
			LegacyPreloadLineName,
			LegacyPreloadLabelName,
			LegacyPreloadTickboxName,
			GetSettingsUiText(LegacyPreloadHeaderLocKey, "Legacy Popup Preload"),
			ticked =>
			{
				if (_isRefreshingSettingsUi)
				{
					return;
				}

				CardEditorPerformanceSettings.SetLegacyPreloadEveryEditorPopupOnLaunch(ticked);
				RefreshSettingsUi(content);
			});

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

		EnsureSettingsDivider(content, PreloadDividerName);
		EnsureSettingsDivider(content, LegacyPreloadDividerName);
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
			NSettingsTickbox? preloadTickbox = FindSettingsTickbox(content, PreloadLineName);
			NSettingsTickbox? legacyPreloadTickbox = FindSettingsTickbox(content, LegacyPreloadLineName);
			NSettingsTickbox? syncTickbox = FindSettingsTickbox(content, SyncLineName);
			NSettingsTickbox? authorityTickbox = FindSettingsTickbox(content, AuthorityLineName);
			RichTextLabel? legacyPreloadLabel = FindNamedDescendant<RichTextLabel>(content.GetNodeOrNull(LegacyPreloadLineName), LegacyPreloadLabelName);
			RichTextLabel? authorityLabel = FindNamedDescendant<RichTextLabel>(content.GetNodeOrNull(AuthorityLineName), AuthorityLabelName);

			if (preloadTickbox != null)
			{
				preloadTickbox.IsTicked = CardEditorPerformanceSettings.PreloadEditorPopupsOnLaunch;
				SetSettingsLineInteractive(preloadTickbox, null, interactive: true);
			}

			if (legacyPreloadTickbox != null)
			{
				bool preloadEnabled = CardEditorPerformanceSettings.PreloadEditorPopupsOnLaunch;
				legacyPreloadTickbox.IsTicked = CardEditorPerformanceSettings.LegacyPreloadEveryEditorPopupOnLaunch;
				SetSettingsLineInteractive(legacyPreloadTickbox, legacyPreloadLabel, preloadEnabled);
			}

			if (syncTickbox != null)
			{
				bool displayedSyncEnabled = _netService?.Type == NetGameType.Client
					? _remoteSyncActive || CardEditorMultiplayerSettings.MultiplayerSyncEnabled
					: CardEditorMultiplayerSettings.MultiplayerSyncEnabled;
				syncTickbox.IsTicked = displayedSyncEnabled;
				SetSettingsLineInteractive(syncTickbox, null, interactive: true);
			}

			CardEditorMultiplayerAuthorityMode displayedAuthorityMode = GetDisplayedAuthorityMode();
			bool authorityInteractive = CanChangeAuthorityModeSetting();

			if (authorityTickbox != null)
			{
				authorityTickbox.IsTicked = displayedAuthorityMode == CardEditorMultiplayerAuthorityMode.AnyPlayer;
				SetSettingsLineInteractive(authorityTickbox, authorityLabel, authorityInteractive);
			}

			LogSettingsUiState(content, "RefreshSettingsUi:end");
		}
		finally
		{
			_isRefreshingSettingsUi = false;
		}
	}

	private static void SetSettingsLineInteractive(NSettingsTickbox tickbox, RichTextLabel? label, bool interactive)
	{
		if (interactive)
		{
			tickbox.Enable();
			tickbox.Modulate = Colors.White;
			if (label != null)
			{
				label.Modulate = Colors.White;
			}

			return;
		}

		tickbox.Disable();
		tickbox.Modulate = StsColors.gray;
		if (label != null)
		{
			label.Modulate = StsColors.gray;
		}
	}

	private static CardEditorMultiplayerAuthorityMode GetDisplayedAuthorityMode()
	{
		if (_netService?.Type == NetGameType.Client && _remoteSyncActive)
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
			CardEditorMod.VerboseLog($"[CardEditor][MultiplayerSettings] Reusing existing line '{lineName}'.");
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
			IsolateDuplicatedSettingsTickbox(tickbox);
			tickbox.Toggled += toggled => onToggled(toggled.IsTicked);
		}
		else
		{
			Log.Warn($"[CardEditor][MultiplayerSettings] Duplicated line '{lineName}' did not contain a vanilla settings tickbox.");
		}

		content.AddChild(duplicatedLine);

		CardEditorMod.VerboseLog(
			$"[CardEditor][MultiplayerSettings] Added line '{lineName}' label='{labelName}' tickbox='{tickboxName}' " +
			$"contentChildren={content.GetChildCount()} template='{templateLine.Name}' tickboxFound={(tickbox != null)}.");
	}

	private static void IsolateDuplicatedSettingsTickbox(NSettingsTickbox tickbox)
	{
		try
		{
			tickbox.FocusNeighborLeft = new NodePath(".");
			tickbox.FocusNeighborRight = new NodePath(".");
			tickbox.FocusNeighborTop = new NodePath(".");
			tickbox.FocusNeighborBottom = new NodePath(".");

			Control? visuals = tickbox.GetNodeOrNull<Control>("%TickboxVisuals")
				?? FindNamedDescendant<Control>(tickbox, "TickboxVisuals");
			if (visuals?.Material is Material material)
			{
				visuals.Material = (Material)material.Duplicate(true);
			}
		}
		catch (Exception ex)
		{
			Log.Warn($"[CardEditor][Settings] Failed isolating duplicated settings tickbox '{tickbox.Name}': {ex}");
		}
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
		Node? preloadLine = content.GetNodeOrNull(PreloadLineName);
		Node? preloadDivider = content.GetNodeOrNull(PreloadDividerName);
		Node? legacyPreloadLine = content.GetNodeOrNull(LegacyPreloadLineName);
		Node? legacyPreloadDivider = content.GetNodeOrNull(LegacyPreloadDividerName);
		Node? syncLine = content.GetNodeOrNull(SyncLineName);
		Node? syncDivider = content.GetNodeOrNull(SyncDividerName);
		Node? authorityLine = content.GetNodeOrNull(AuthorityLineName);
		Node? authorityDivider = content.GetNodeOrNull(AuthorityDividerName);
		Node? sendFeedbackLine = content.GetNodeOrNull(SendFeedbackLineName);
		if (sendFeedbackLine == null)
		{
			return;
		}

		if (preloadLine != null)
		{
			content.MoveChild(preloadLine, sendFeedbackLine.GetIndex());
		}

		if (preloadDivider != null)
		{
			content.MoveChild(preloadDivider, sendFeedbackLine.GetIndex());
		}

		if (legacyPreloadLine != null)
		{
			content.MoveChild(legacyPreloadLine, sendFeedbackLine.GetIndex());
		}

		if (legacyPreloadDivider != null)
		{
			content.MoveChild(legacyPreloadDivider, sendFeedbackLine.GetIndex());
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
				{ PreloadHeaderLocKey, "Preload On Launch" },
				{ PreloadDescriptionLocKey, "Warms Card Editor popup UI during startup. Disable this for faster startup; the first editor opens will do more work." },
				{ LegacyPreloadHeaderLocKey, "Legacy Popup Preload" },
				{ LegacyPreloadDescriptionLocKey, "Uses the old per-card popup prebuild path instead of the shared popup cache. This can make editor opens instant, but may create a very large hidden UI tree and cause hitches." },
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
			Node? preloadLine = content.GetNodeOrNull(PreloadLineName);
			Node? legacyPreloadLine = content.GetNodeOrNull(LegacyPreloadLineName);
			Node? syncLine = content.GetNodeOrNull(SyncLineName);
			Node? authorityLine = content.GetNodeOrNull(AuthorityLineName);
			NSettingsTickbox? preloadTickbox = FindDescendantOfType<NSettingsTickbox>(preloadLine);
			NSettingsTickbox? legacyPreloadTickbox = FindDescendantOfType<NSettingsTickbox>(legacyPreloadLine);
			NSettingsTickbox? syncTickbox = FindDescendantOfType<NSettingsTickbox>(syncLine);
			NSettingsTickbox? authorityTickbox = FindDescendantOfType<NSettingsTickbox>(authorityLine);
			RichTextLabel? preloadLabel = FindNamedDescendant<RichTextLabel>(preloadLine, PreloadLabelName);
			RichTextLabel? legacyPreloadLabel = FindNamedDescendant<RichTextLabel>(legacyPreloadLine, LegacyPreloadLabelName);
			RichTextLabel? syncLabel = FindNamedDescendant<RichTextLabel>(syncLine, SyncLabelName);
			RichTextLabel? authorityLabel = FindNamedDescendant<RichTextLabel>(authorityLine, AuthorityLabelName);

			CardEditorMod.VerboseLog(
				$"[CardEditor][MultiplayerSettings] {context} " +
				$"contentChildren={content.GetChildCount()} " +
				$"preloadLine={DescribeNode(preloadLine)} preloadLabel={DescribeControl(preloadLabel)} preloadTickbox={DescribeControl(preloadTickbox)} " +
				$"legacyPreloadLine={DescribeNode(legacyPreloadLine)} legacyPreloadLabel={DescribeControl(legacyPreloadLabel)} legacyPreloadTickbox={DescribeControl(legacyPreloadTickbox)} " +
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

		CardEditorMod.VerboseLog($"[CardEditor][MultiplayerSync] Detached from {_netService.Type} service.");
		_netService = null;
		_requestedInitialSync = false;
		_forceImmediateBroadcast = false;
		_lastAppliedSequence = 0;
		_remoteAuthorityMode = CardEditorMultiplayerAuthorityMode.HostOnly;
		_remoteSyncActive = false;
	}

	private static void OnServiceDisconnected(NetErrorInfo info)
	{
		CardEditorMod.VerboseLog($"[CardEditor][MultiplayerSync] Net service disconnected: {info.GetErrorString()}");
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
		if (_netService?.Type != NetGameType.Client)
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

		CardEditorMod.VerboseLog($"[CardEditor][MultiplayerSync] Applying authoritative snapshot seq={message.Sequence} from host={senderId}.");
		ApplyState(state, preserveLocalState: true);
		_remoteAuthorityMode = state.AuthorityMode;
		_remoteSyncActive = true;
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

		CardEditorMod.VerboseLog($"[CardEditor][MultiplayerSync] Applying client-authored sync request from {senderId}.");
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
		CardEditorMod.VerboseLog($"[CardEditor][MultiplayerSync] Sent snapshot seq={message.Sequence} to peer {peerId}.");
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
		CardEditorMod.VerboseLog($"[CardEditor][MultiplayerSync] Broadcast snapshot seq={message.Sequence}.");
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
		CardEditorMod.VerboseLog("[CardEditor][MultiplayerSync] Sent client edit request to host.");
	}

	private static CardEditorMultiplayerStateDto BuildCurrentState()
	{
		CardEditorMultiplayerStateDto dto = new()
		{
			AuthorityMode = CardEditorMultiplayerSettings.AuthorityMode,
			CreatedCardSlotCount = Math.Clamp(CardEditorCreatedCardsStore.SlotCount, 1, CardEditorCreatedCardsStore.MaxSlotCount),
			KeywordDefinitions = CardEditorDefinitionStore.GetKeywordDefinitions()
				.Select(CardEditorPresetStore.CustomKeywordDefinitionDto.FromDefinition)
				.ToList(),
			StatusDefinitions = CardEditorDefinitionStore.GetStatusDefinitions()
				.Select(CardEditorPresetStore.CustomStatusDefinitionDto.FromDefinition)
				.ToList()
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

			dto.CreatedCards[cardId.ToString()] = CardEditorCreatedCardsStore.CreatedCardDto.FromDefinition(definition);
		}

		foreach ((ModelId relicId, RelicOverride overrideData) in CardEditorRelicOverrides.ExportSnapshot())
		{
			if (relicId == null || ModelDb.GetByIdOrNull<RelicModel>(relicId) == null || overrideData == null || overrideData.IsEmpty())
			{
				continue;
			}

			dto.RelicOverrides[relicId.ToString()] = CardEditorRelicOverrideStore.RelicOverrideDto.FromOverride(overrideData);
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
		List<CardEditorCustomKeywordDefinition> keywords = DeserializeKeywordDefinitions(state.KeywordDefinitions);
		List<CardEditorCustomStatusDefinition> statuses = DeserializeStatusDefinitions(state.StatusDefinitions);
		Dictionary<ModelId, RelicOverride> relicOverrides = DeserializeRelicOverrides(state.RelicOverrides);

		HashSet<ModelId> previousOverrideIds = CardEditorOverrides.AllOverrides.Keys.ToHashSet();
		bool previousPersistenceSuspended = CardEditorCreatedCardsStore.PersistenceSuspended;
		bool previousDefinitionPersistenceSuspended = CardEditorDefinitionStore.PersistenceSuspended;

		try
		{
			if (_netService?.Type == NetGameType.Client)
			{
				CardEditorCreatedCardsStore.PersistenceSuspended = true;
				CardEditorDefinitionStore.PersistenceSuspended = true;
			}

			CardEditorDefinitionStore.ReplaceDefinitions(keywords, statuses);
			if (state.CreatedCardSlotCount > 0)
			{
				CardEditorCreatedCardsStore.SetSlotCountForNextRun(state.CreatedCardSlotCount);
			}
			CardEditorCreatedCardsStore.ImportSnapshot(createdCards);
			CardEditorOverrides.ReplaceAll(overrides);
			CardEditorRelicOverrides.ReplaceAll(relicOverrides);
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
			CardEditorDefinitionStore.PersistenceSuspended = previousDefinitionPersistenceSuspended;
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
			CreatedCards = CardEditorCreatedCardsStore.ExportSnapshot(),
			KeywordDefinitions = CardEditorDefinitionStore.GetKeywordDefinitions().ToList(),
			StatusDefinitions = CardEditorDefinitionStore.GetStatusDefinitions().ToList(),
			RelicOverrides = CardEditorRelicOverrides.ExportSnapshot()
		};
		CardEditorMod.VerboseLog("[CardEditor][MultiplayerSync] Backed up local card editor state before applying host snapshot.");
	}

	private static void RestoreLocalStateIfNeeded()
	{
		if (_localBackup == null)
		{
			CardEditorCreatedCardsStore.PersistenceSuspended = false;
			return;
		}

			CardEditorMod.VerboseLog("[CardEditor][MultiplayerSync] Restoring local card editor state after multiplayer session.");
		bool previousPersistenceSuspended = CardEditorCreatedCardsStore.PersistenceSuspended;
		bool previousDefinitionPersistenceSuspended = CardEditorDefinitionStore.PersistenceSuspended;

		try
		{
			CardEditorCreatedCardsStore.PersistenceSuspended = true;
			CardEditorDefinitionStore.PersistenceSuspended = true;
			CardEditorDefinitionStore.ReplaceDefinitions(_localBackup.KeywordDefinitions, _localBackup.StatusDefinitions);
			CardEditorCreatedCardsStore.ImportSnapshot(_localBackup.CreatedCards);
			CardEditorOverrides.ReplaceAll(_localBackup.Overrides);
			CardEditorRelicOverrides.ReplaceAll(_localBackup.RelicOverrides);
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
			CardEditorDefinitionStore.PersistenceSuspended = previousDefinitionPersistenceSuspended;
		}

		_localBackup = null;
	}

	private static bool RevisionsChangedSinceCheckpoint()
	{
		return _lastSeenOverrideRevision != CardEditorOverrides.Revision
			|| _lastSeenBaseDeckRevision != CardEditorBaseDeckStore.Revision
			|| _lastSeenCreatedCardsRevision != CardEditorCreatedCardsStore.Revision
			|| _lastSeenDefinitionRevision != CardEditorDefinitionStore.Revision
			|| _lastSeenRelicRevision != CardEditorRelicOverrides.Revision
			|| _lastSeenSettingsRevision != CardEditorMultiplayerSettings.Revision;
	}

	private static void CaptureRevisionCheckpoint()
	{
		_lastSeenOverrideRevision = CardEditorOverrides.Revision;
		_lastSeenBaseDeckRevision = CardEditorBaseDeckStore.Revision;
		_lastSeenCreatedCardsRevision = CardEditorCreatedCardsStore.Revision;
		_lastSeenDefinitionRevision = CardEditorDefinitionStore.Revision;
		_lastSeenRelicRevision = CardEditorRelicOverrides.Revision;
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

	private static Dictionary<ModelId, CardEditorCreatedCardDefinition> DeserializeCreatedCards(Dictionary<string, CardEditorCreatedCardsStore.CreatedCardDto>? serialized)
	{
		Dictionary<ModelId, CardEditorCreatedCardDefinition> createdCards = new();
		if (serialized == null)
		{
			return createdCards;
		}

		foreach ((string rawCardId, CardEditorCreatedCardsStore.CreatedCardDto dto) in serialized)
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

	private static Dictionary<ModelId, RelicOverride> DeserializeRelicOverrides(Dictionary<string, CardEditorRelicOverrideStore.RelicOverrideDto>? serialized)
	{
		Dictionary<ModelId, RelicOverride> relicOverrides = new();
		if (serialized == null)
		{
			return relicOverrides;
		}

		foreach ((string rawRelicId, CardEditorRelicOverrideStore.RelicOverrideDto dto) in serialized)
		{
			if (!TryParseModelId(rawRelicId, out ModelId relicId) || ModelDb.GetByIdOrNull<RelicModel>(relicId) == null)
			{
				continue;
			}

			try
			{
				RelicOverride overrideData = dto.ToOverride();
				if (overrideData != null && !overrideData.IsEmpty())
				{
					relicOverrides[relicId] = overrideData;
				}
			}
			catch (Exception ex)
			{
				Log.Warn($"[CardEditor][MultiplayerSync] Failed deserializing relic override {rawRelicId}: {ex}");
			}
		}

		return relicOverrides;
	}

	private static List<CardEditorCustomKeywordDefinition> DeserializeKeywordDefinitions(List<CardEditorPresetStore.CustomKeywordDefinitionDto>? serialized)
	{
		List<CardEditorCustomKeywordDefinition> definitions = new();
		foreach (CardEditorPresetStore.CustomKeywordDefinitionDto dto in serialized ?? new List<CardEditorPresetStore.CustomKeywordDefinitionDto>())
		{
			try
			{
				CardEditorCustomKeywordDefinition? definition = dto.ToDefinitionSafe();
				if (definition != null)
				{
					definitions.Add(definition);
				}
			}
			catch (Exception ex)
			{
				Log.Warn($"[CardEditor][MultiplayerSync] Failed deserializing custom keyword definition: {ex}");
			}
		}

		return definitions;
	}

	private static List<CardEditorCustomStatusDefinition> DeserializeStatusDefinitions(List<CardEditorPresetStore.CustomStatusDefinitionDto>? serialized)
	{
		List<CardEditorCustomStatusDefinition> definitions = new();
		foreach (CardEditorPresetStore.CustomStatusDefinitionDto dto in serialized ?? new List<CardEditorPresetStore.CustomStatusDefinitionDto>())
		{
			try
			{
				CardEditorCustomStatusDefinition? definition = dto.ToDefinitionSafe();
				if (definition != null)
				{
					definitions.Add(definition);
				}
			}
			catch (Exception ex)
			{
				Log.Warn($"[CardEditor][MultiplayerSync] Failed deserializing custom status definition: {ex}");
			}
		}

		return definitions;
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
			if (current.Name == PreloadLineName)
			{
				lineKind = CardEditorMultiplayerSettingsLineKind.PreloadOnLaunch;
				return true;
			}

			if (current.Name == LegacyPreloadLineName)
			{
				lineKind = CardEditorMultiplayerSettingsLineKind.LegacyPreload;
				return true;
			}

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
			case CardEditorMultiplayerSettingsLineKind.PreloadOnLaunch:
				CardEditorPerformanceSettings.SetPreloadEditorPopupsOnLaunch(ticked);
				break;
			case CardEditorMultiplayerSettingsLineKind.LegacyPreload:
				CardEditorPerformanceSettings.SetLegacyPreloadEveryEditorPopupOnLaunch(ticked);
				break;
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
			CardEditorMultiplayerSettingsLineKind.PreloadOnLaunch => new HoverTip(
				new LocString("settings_ui", PreloadHeaderLocKey),
				GetSettingsUiText(PreloadDescriptionLocKey, "Warms Card Editor popup UI during startup. Disable this for faster startup; the first editor opens will do more work.")),
			CardEditorMultiplayerSettingsLineKind.LegacyPreload => new HoverTip(
				new LocString("settings_ui", LegacyPreloadHeaderLocKey),
				GetSettingsUiText(LegacyPreloadDescriptionLocKey, "Uses the old per-card popup prebuild path instead of the shared popup cache. This can make editor opens instant, but may create a very large hidden UI tree and cause hitches.")),
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
			CardEditorMultiplayerSettingsLineKind.PreloadOnLaunch => CardEditorPerformanceSettings.PreloadEditorPopupsOnLaunch,
			CardEditorMultiplayerSettingsLineKind.LegacyPreload => CardEditorPerformanceSettings.LegacyPreloadEveryEditorPopupOnLaunch,
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
	PreloadOnLaunch = 1,
	LegacyPreload = 2,
	Sync = 3,
	Authority = 4
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
