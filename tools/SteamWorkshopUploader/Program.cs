using Steamworks;

const uint appId = 2868840;
const ulong publishedFileId = 3748283746;
const ulong expectedOwnerSteamId = 76561198090139392;

if (args.Length != 2)
{
	Console.Error.WriteLine("Usage: SteamWorkshopUploader <content-folder> <change-note>");
	return 2;
}

string contentFolder = Path.GetFullPath(args[0]);
string changeNote = args[1].Trim();
if (!Directory.Exists(contentFolder))
{
	Console.Error.WriteLine($"Content folder does not exist: {contentFolder}");
	return 2;
}
if (!File.Exists(Path.Combine(contentFolder, "card_editor.json"))
	|| !File.Exists(Path.Combine(contentFolder, "card_editor.dll"))
	|| !File.Exists(Path.Combine(contentFolder, "card_editor.pck")))
{
	Console.Error.WriteLine("Content folder is missing card_editor.json, card_editor.dll, or card_editor.pck.");
	return 2;
}
if (string.IsNullOrWhiteSpace(changeNote))
{
	Console.Error.WriteLine("A non-empty Workshop change note is required.");
	return 2;
}

Environment.SetEnvironmentVariable("SteamAppId", appId.ToString());
Environment.SetEnvironmentVariable("SteamGameId", appId.ToString());

if (!SteamAPI.Init())
{
	Console.Error.WriteLine("SteamAPI.Init failed. Ensure Steam is running and the Workshop owner account is signed in.");
	return 3;
}

try
{
	CSteamID activeSteamId = SteamUser.GetSteamID();
	Console.WriteLine($"Steam user: {SteamFriends.GetPersonaName()} ({activeSteamId.m_SteamID})");
	if (activeSteamId.m_SteamID != expectedOwnerSteamId)
	{
		Console.Error.WriteLine($"Refusing upload: expected Workshop owner {expectedOwnerSteamId}, got {activeSteamId.m_SteamID}.");
		return 4;
	}

	UGCUpdateHandle_t update = SteamUGC.StartItemUpdate(new AppId_t(appId), new PublishedFileId_t(publishedFileId));
	if (update == UGCUpdateHandle_t.Invalid)
	{
		Console.Error.WriteLine("SteamUGC.StartItemUpdate returned an invalid handle.");
		return 5;
	}
	if (!SteamUGC.SetItemContent(update, contentFolder))
	{
		Console.Error.WriteLine("SteamUGC.SetItemContent rejected the release folder.");
		return 5;
	}

	TaskCompletionSource<SubmitItemUpdateResult_t> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
	CallResult<SubmitItemUpdateResult_t> callback = CallResult<SubmitItemUpdateResult_t>.Create(
		(result, ioFailure) =>
		{
			if (ioFailure)
			{
				completion.TrySetException(new IOException("Steam reported an I/O failure while submitting the Workshop item."));
				return;
			}
			completion.TrySetResult(result);
		});
	callback.Set(SteamUGC.SubmitItemUpdate(update, changeNote));

	DateTime deadline = DateTime.UtcNow.AddMinutes(10);
	ulong lastProcessed = ulong.MaxValue;
	while (!completion.Task.IsCompleted && DateTime.UtcNow < deadline)
	{
		SteamAPI.RunCallbacks();
		EItemUpdateStatus status = SteamUGC.GetItemUpdateProgress(update, out ulong processed, out ulong total);
		if (processed != lastProcessed)
		{
			Console.WriteLine($"{status}: {processed}/{total} bytes");
			lastProcessed = processed;
		}
		Thread.Sleep(100);
	}

	if (!completion.Task.IsCompleted)
	{
		Console.Error.WriteLine("Timed out waiting for Steam Workshop submission.");
		return 6;
	}

	SubmitItemUpdateResult_t submitted = await completion.Task;
	Console.WriteLine($"Steam result: {submitted.m_eResult}; item: {submitted.m_nPublishedFileId.m_PublishedFileId}");
	if (submitted.m_bUserNeedsToAcceptWorkshopLegalAgreement)
	{
		Console.Error.WriteLine("Steam requires the Workshop legal agreement to be accepted before publication.");
		return 7;
	}
	if (submitted.m_eResult != EResult.k_EResultOK || submitted.m_nPublishedFileId.m_PublishedFileId != publishedFileId)
	{
		return 8;
	}

	Console.WriteLine("WORKSHOP_UPLOAD_SUCCESS");
	return 0;
}
finally
{
	SteamAPI.Shutdown();
}
