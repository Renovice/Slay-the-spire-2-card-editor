# Steam Workshop Uploader

Updates the existing Card Editor Workshop item through the Steam client session.
It refuses to upload when Steam is not signed into the known owner account and
requires `card_editor.json`, `card_editor.dll`, and `card_editor.pck` in the content
folder.

```powershell
dotnet run --project .\tools\SteamWorkshopUploader\SteamWorkshopUploader.csproj -- `
  ".\built cfiles" `
  "Version 10.1.4: fixed remaining audited Run Effect Source lifecycle behavior and persistent source scaling."
```

The tool reports success only after the `SubmitItemUpdateResult_t` callback returns
`k_EResultOK` for Workshop item `3748283746`.
