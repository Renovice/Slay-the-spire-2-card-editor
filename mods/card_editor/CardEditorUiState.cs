using System.Reflection;
using System.Collections.Generic;
using Godot;
using MegaCrit.Sts2.Core.Nodes.Screens.CardLibrary;
using MegaCrit.Sts2.Core.Models;

namespace SlayTheSpire2Mod.CardEditor;

public enum CardEditorLibraryMode
{
	None = 0,
	Editor = 1,
	Creator = 2,
	BaseDeck = 3,
	BaseDeckAdd = 4
}

public static class CardEditorUiState
{
	private static MethodInfo? _updateFilterMethod;
	private static readonly Dictionary<ModelId, CardOverride> _draftOverrides = new();
	private static WeakReference<NCardLibrary>? _lastLibrary;

	public static CardEditorLibraryMode Mode { get; set; } = CardEditorLibraryMode.None;

	public static int DraftRevision { get; private set; }

	public static bool IsActive => Mode != CardEditorLibraryMode.None;

	public static bool IsEditorActive => Mode == CardEditorLibraryMode.Editor;

	public static bool IsCreatorActive => Mode == CardEditorLibraryMode.Creator;

	public static bool IsBaseDeckActive => Mode == CardEditorLibraryMode.BaseDeck;

	public static bool IsBaseDeckAddActive => Mode == CardEditorLibraryMode.BaseDeckAdd;

	public static void SetDraftOverride(ModelId cardId, CardOverride overrideData)
	{
		if (overrideData == null)
		{
			if (_draftOverrides.Remove(cardId))
			{
				DraftRevision++;
			}
			return;
		}
		_draftOverrides[cardId] = overrideData;
		DraftRevision++;
	}

	public static bool TryGetDraftOverride(ModelId cardId, out CardOverride overrideData)
	{
		return _draftOverrides.TryGetValue(cardId, out overrideData);
	}

	public static void ClearDraftOverride(ModelId cardId)
	{
		if (_draftOverrides.Remove(cardId))
		{
			DraftRevision++;
		}
	}

	public static void RefreshLibrary(NCardLibrary library)
	{
		if (library == null)
		{
			return;
		}
		_updateFilterMethod ??= typeof(NCardLibrary).GetMethod("UpdateFilter", BindingFlags.Instance | BindingFlags.NonPublic);
		_updateFilterMethod?.Invoke(library, new object[] { false });
		SyncLibraryChrome(library);
		Callable.From(() =>
		{
			if (GodotObject.IsInstanceValid(library))
			{
				SyncLibraryChrome(library);
			}
		}).CallDeferred();
	}

	private static void SyncLibraryChrome(NCardLibrary library)
	{
		CardEditorBaseDeckPanelHooks.Sync(library);
		CardEditorPresetPanelHooks.Sync(library);
		CardEditorBaseDeckBookmarkHooks.Sync(library);
		CardEditorBaseDeckBookmarkHooks.SyncDeferred(library);
	}

	public static void SetLastLibrary(NCardLibrary? library)
	{
		if (library == null)
		{
			_lastLibrary = null;
			return;
		}

		_lastLibrary = new WeakReference<NCardLibrary>(library);
	}

	public static bool TryGetLastLibrary(out NCardLibrary? library)
	{
		library = null;
		if (_lastLibrary == null)
		{
			return false;
		}

		return _lastLibrary.TryGetTarget(out library) && library != null;
	}

	public static void RefreshLastLibraryIfActive()
	{
		if (!IsActive)
		{
			return;
		}

		if (TryGetLastLibrary(out NCardLibrary? library) && library != null)
		{
			RefreshLibrary(library);
		}
	}
}
