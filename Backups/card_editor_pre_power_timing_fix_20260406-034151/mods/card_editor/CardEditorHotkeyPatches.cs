using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Nodes.Screens.Capstones;
using MegaCrit.Sts2.Core.Nodes.Screens.CardLibrary;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;
using MegaCrit.Sts2.Core.Nodes.Screens.Settings;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;

namespace SlayTheSpire2Mod.CardEditor;

internal static class CardEditorQuickOpenHotkey
{
	public static readonly StringName InputName = new StringName("card_editor_toggle");

	private const string SettingsUiTable = "settings_ui";
	private const string SettingsLocTitleSuffix = "cardEditor";
	private const string SettingsLocTitleKey = "INPUT_SETTINGS.INPUT_TITLE." + SettingsLocTitleSuffix;
	private const string SettingsLocTitleValue = "Open Editor";

	private static readonly Key DefaultKey = Key.F11;

	private static bool _baseInstalled;
	private static bool _openedCapstoneViaHotkey;

	private static FieldInfo? _inputSettingsEntryLocMapField;

	public static void EnsureInstalled()
	{
		try
		{
			if (!_baseInstalled)
			{
				_baseInstalled = true;
				EnsureRemappableKeyboardInput();
				EnsureInputSettingsTitleMapping();
			}
		}
		catch (Exception ex)
		{
			Log.Warn($"[CardEditor] Failed to install Editor hotkey base wiring: {ex}");
		}

		EnsureGodotInputAction();
		EnsureLocalizationKey();
		EnsureHotkeyBinding();
	}

	private static void EnsureGodotInputAction()
	{
		try
		{
			if (!InputMap.HasAction(InputName))
			{
				InputMap.AddAction(InputName);
			}
		}
		catch (Exception ex)
		{
			Log.Warn($"[CardEditor] Failed to register Godot input action '{InputName}': {ex}");
		}
	}

	private static void EnsureRemappableKeyboardInput()
	{
		if (NInputManager.remappableKeyboardInputs is List<StringName> list && !list.Contains(InputName))
		{
			list.Add(InputName);
		}
	}

	private static void EnsureInputSettingsTitleMapping()
	{
		_inputSettingsEntryLocMapField ??= typeof(NInputSettingsEntry).GetField("_commandToLocTitle", BindingFlags.Static | BindingFlags.NonPublic);
		Dictionary<StringName, string>? map = _inputSettingsEntryLocMapField?.GetValue(null) as Dictionary<StringName, string>;
		if (map == null)
		{
			return;
		}
		map[InputName] = SettingsLocTitleSuffix;
	}

	public static void EnsureLocalizationKey()
	{
		try
		{
			LocTable? table = LocManager.Instance?.GetTable(SettingsUiTable);
			if (table == null)
			{
				return;
			}

			// If we ship a proper localization file for this key, don't overwrite it at runtime.
			if (table.HasEntry(SettingsLocTitleKey))
			{
				return;
			}

			table.MergeWith(new Dictionary<string, string> { { SettingsLocTitleKey, SettingsLocTitleValue } });
		}
		catch (Exception ex)
		{
			Log.Warn($"[CardEditor] Failed to merge hotkey localization: {ex}");
		}
	}

	private static void EnsureHotkeyBinding()
	{
		NHotkeyManager? hotkeys = NHotkeyManager.Instance;
		if (hotkeys == null)
		{
			return;
		}

		hotkeys.PushHotkeyPressedBinding(InputName.ToString(), ToggleEditorCompendium);
	}

	public static void EnsureDefaultKeyInInputManager(NInputManager inputManager)
	{
		if (inputManager == null)
		{
			return;
		}

		try
		{
			Dictionary<string, string> saved = SaveManager.Instance.SettingsSave.KeyboardMapping;
			if (saved == null || saved.Count == 0)
			{
				return;
			}

			string key = InputName.ToString();
			if (saved.ContainsKey(key))
			{
				return;
			}

			bool f11Used = saved.Any(kvp => string.Equals(kvp.Value, DefaultKey.ToString(), StringComparison.OrdinalIgnoreCase));
			saved[key] = f11Used ? Key.None.ToString() : DefaultKey.ToString();
			SaveManager.Instance.SaveSettings();
		}
		catch (Exception ex)
		{
			Log.Warn($"[CardEditor] Failed ensuring Editor hotkey default binding: {ex}");
		}
	}

	private static void ToggleEditorCompendium()
	{
		try
		{
			if (TryToggleInRun())
			{
				return;
			}
			TryToggleInMainMenu();
		}
		catch (Exception ex)
		{
			Log.Warn($"[CardEditor] Editor hotkey failed: {ex}");
		}
	}

	private static bool TryToggleInRun()
	{
		NRun? run = NRun.Instance;
		if (run == null)
		{
			return false;
		}

		NCapstoneSubmenuStack? capstoneSubmenus = run.GlobalUi?.SubmenuStack;
		NCapstoneContainer? capstoneContainer = NCapstoneContainer.Instance;
		if (capstoneSubmenus == null || capstoneContainer == null)
		{
			return false;
		}

		NRunSubmenuStack stack = capstoneSubmenus.Stack;
		bool isOurCapstoneOpen = ReferenceEquals(capstoneContainer.CurrentCapstoneScreen, capstoneSubmenus);
		if (!isOurCapstoneOpen)
		{
			capstoneSubmenus.ShowScreen(CapstoneSubmenuType.Compendium);
			_openedCapstoneViaHotkey = true;
		}

		NSubmenu? top = stack.Peek();
		if (top is NCardLibrary library && CardEditorUiState.IsActive)
		{
			stack.Pop();
			if (_openedCapstoneViaHotkey && stack.Peek() is NCompendiumSubmenu)
			{
				stack.Pop();
				_openedCapstoneViaHotkey = false;
			}
			return true;
		}

		OpenEditorLibrary(stack);
		return true;
	}

	private static void TryToggleInMainMenu()
	{
		NMainMenu? menu = NGame.Instance?.MainMenu;
		if (menu == null)
		{
			return;
		}

		NMainMenuSubmenuStack stack = menu.SubmenuStack;
		NSubmenu? top = stack.Peek();
		if (top is NCardLibrary library && CardEditorUiState.IsActive)
		{
			stack.Pop();
			return;
		}

		OpenEditorLibrary(stack);
	}

	private static void OpenEditorLibrary(NSubmenuStack stack)
	{
		if (stack == null)
		{
			return;
		}

		IRunState? runState = RunManager.Instance.DebugOnlyGetState();
		NSubmenu? top = stack.Peek();
		if (top is NCardLibrary alreadyOpen)
		{
			CardEditorUiState.Mode = CardEditorLibraryMode.Editor;
			if (runState != null)
			{
				alreadyOpen.Initialize(runState);
			}
			CardEditorUiState.RefreshLibrary(alreadyOpen);
			CardEditorPresetPanelHooks.Sync(alreadyOpen);
			return;
		}

		CardEditorUiState.Mode = CardEditorLibraryMode.Editor;
		NCardLibrary library = stack.GetSubmenuType<NCardLibrary>();
		if (runState != null)
		{
			library.Initialize(runState);
		}
		Callable.From(() => stack.Push(library)).CallDeferred();
	}
}

[HarmonyPatch(typeof(LocManager), nameof(LocManager.SetLanguage))]
internal static class LocManager_SetLanguage_CardEditorHotkeyLoc_Patch
{
	public static void Postfix()
	{
		CardEditorQuickOpenHotkey.EnsureLocalizationKey();
	}
}

[HarmonyPatch(typeof(NGame), nameof(NGame._EnterTree))]
internal static class NGame_EnterTree_CardEditorHotkey_Patch
{
	public static void Postfix()
	{
		CardEditorQuickOpenHotkey.EnsureInstalled();
	}
}

[HarmonyPatch(typeof(NInputManager), nameof(NInputManager._Ready))]
internal static class NInputManager_Ready_CardEditorHotkey_Patch
{
	public static void Prefix(NInputManager __instance)
	{
		CardEditorQuickOpenHotkey.EnsureInstalled();
		CardEditorQuickOpenHotkey.EnsureDefaultKeyInInputManager(__instance);
	}
}

[HarmonyPatch(typeof(NInputManager), "get_DefaultKeyboardInputMap")]
internal static class NInputManager_get_DefaultKeyboardInputMap_CardEditorHotkey_Patch
{
	public static void Postfix(ref Dictionary<StringName, Key> __result)
	{
		if (__result == null)
		{
			return;
		}
		__result[CardEditorQuickOpenHotkey.InputName] = Key.F11;
	}
}

[HarmonyPatch(typeof(NInputSettingsPanel), nameof(NInputSettingsPanel._Ready))]
internal static class NInputSettingsPanel_Ready_CardEditorHotkey_Patch
{
	public static void Prefix()
	{
		CardEditorQuickOpenHotkey.EnsureInstalled();
	}
}
