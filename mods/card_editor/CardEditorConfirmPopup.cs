using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Multiplayer;

namespace SlayTheSpire2Mod.CardEditor;

internal static class CardEditorConfirmPopup
{
	public static Task<bool> ShowConfirmation(string title, string body)
	{
		CardEditorMod.VerboseLog($"[CardEditor][ConfirmPopup] ShowConfirmation title='{title}' modalInstance={(NModalContainer.Instance != null)}");
		if (NModalContainer.Instance == null)
		{
			Log.Warn($"[CardEditor][ConfirmPopup] Aborting popup '{title}' because NModalContainer.Instance is null.");
			return Task.FromResult(false);
		}

		NGenericPopup? popup = NGenericPopup.Create();
		if (popup == null)
		{
			Log.Warn($"[CardEditor][ConfirmPopup] Aborting popup '{title}' because NGenericPopup.Create() returned null.");
			return Task.FromResult(false);
		}

		try
		{
			CardEditorMod.VerboseLog($"[CardEditor][ConfirmPopup] Adding popup title='{title}' modalVisible={NModalContainer.Instance.Visible} modalChildrenBefore={NModalContainer.Instance.GetChildCount()}");
			NModalContainer.Instance.Add(popup);
			CardEditorMod.VerboseLog($"[CardEditor][ConfirmPopup] Added popup title='{title}' modalChildrenAfter={NModalContainer.Instance.GetChildCount()} popupInTree={popup.IsInsideTree()} popupVisible={popup.Visible}");
		}
		catch (System.Exception ex)
		{
			Log.Warn($"[CardEditor][ConfirmPopup] Failed adding popup '{title}' to modal container: {ex}");
			popup.QueueFree();
			return Task.FromResult(false);
		}

		NVerticalPopup verticalPopup;
		try
		{
			verticalPopup = popup.GetNode<NVerticalPopup>("VerticalPopup");
		}
		catch (System.Exception ex)
		{
			Log.Warn($"[CardEditor][ConfirmPopup] Failed resolving VerticalPopup for '{title}': {ex}");
			popup.QueueFree();
			return Task.FromResult(false);
		}

		CardEditorMod.VerboseLog($"[CardEditor][ConfirmPopup] Popup created title='{title}' popupName='{popup.Name}' childCount={popup.GetChildCount()}");
		TaskCompletionSource<bool> completion = new();

		void Finish(bool result)
		{
			CardEditorMod.VerboseLog($"[CardEditor][ConfirmPopup] Finish title='{title}' result={result} inTree={popup.IsInsideTree()} visible={popup.Visible}");
			try
			{
				if (NModalContainer.Instance != null && popup.GetParent() == NModalContainer.Instance)
				{
					NModalContainer.Instance.Clear();
				}
				else
				{
					popup.QueueFree();
				}
			}
			catch (System.Exception ex)
			{
				Log.Warn($"[CardEditor][ConfirmPopup] Failed closing popup '{title}': {ex}");
				popup.QueueFree();
			}
			if (!completion.Task.IsCompleted)
			{
				completion.SetResult(result);
			}
		}

		try
		{
			verticalPopup.SetText(title, body);
			verticalPopup.InitNoButton(new LocString("main_menu_ui", "GENERIC_POPUP.cancel"), _ => Finish(false));
			verticalPopup.InitYesButton(new LocString("main_menu_ui", "GENERIC_POPUP.confirm"), _ => Finish(true));
			CardEditorMod.VerboseLog($"[CardEditor][ConfirmPopup] Initialized popup title='{title}'");
		}
		catch (System.Exception ex)
		{
			Log.Warn($"[CardEditor][ConfirmPopup] Failed initializing popup '{title}': {ex}");
			popup.QueueFree();
			return Task.FromResult(false);
		}

		return completion.Task;
	}
}
