param(
	[string]$GameDir = 'C:\Program Files (x86)\Steam\steamapps\common\Slay the Spire 2',
	[int]$TimeoutSeconds = 180
)

$ErrorActionPreference = 'Stop'

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$project = Join-Path $repoRoot 'mods\card_editor\card_editor.csproj'
$buildDir = Join-Path $repoRoot 'mods\card_editor\build\net9.0'
$liveModDir = Join-Path $GameDir 'mods\Card Editor2'
$gameExe = Join-Path $GameDir 'SlayTheSpire2.exe'
$releaseInfoPath = Join-Path $GameDir 'release_info.json'
$report = Join-Path $env:APPDATA 'SlayTheSpire2\card_editor\engine_ui_selftest_report.txt'
$requiredGameVersion = 'v0.111.0'
$steamAppIdPath = Join-Path $GameDir 'steam_appid.txt'
$backupDir = Join-Path $env:TEMP "card-editor-engine-test-$PID"
$gameProcess = $null
$previousSelfTestEnvironment = [Environment]::GetEnvironmentVariable('CARD_EDITOR_ENGINE_SELF_TEST', 'Process')

if (Get-Process -Name SlayTheSpire2 -ErrorAction SilentlyContinue) {
	throw 'Close Slay the Spire 2 before running the engine UI tests.'
}
if (-not (Test-Path -LiteralPath $releaseInfoPath)) {
	throw "Cannot verify the installed game version because $releaseInfoPath is missing."
}
if (-not (Test-Path -LiteralPath $gameExe)) {
	throw "The game executable was not found at $gameExe."
}
if (-not (Get-Process -Name steam -ErrorAction SilentlyContinue)) {
	throw 'Steam must be running and signed in before the engine UI tests start.'
}
if (-not (Test-Path -LiteralPath $liveModDir)) {
	throw "The live Card Editor folder was not found at $liveModDir."
}
$liveDll = Join-Path $liveModDir 'card_editor.dll'
$livePdb = Join-Path $liveModDir 'card_editor.pdb'
if (-not (Test-Path -LiteralPath $liveDll)) {
	throw "The live Card Editor DLL was not found at $liveDll."
}

$releaseInfo = Get-Content -LiteralPath $releaseInfoPath -Raw | ConvertFrom-Json
if ($releaseInfo.version -ne $requiredGameVersion) {
	throw "Engine tests require beta $requiredGameVersion, but the installed game reports $($releaseInfo.version). No files were changed."
}

$resolvedBackupDir = [IO.Path]::GetFullPath($backupDir)
$resolvedTempDir = [IO.Path]::GetFullPath($env:TEMP).TrimEnd('\') + '\'
if (-not $resolvedBackupDir.StartsWith($resolvedTempDir, [StringComparison]::OrdinalIgnoreCase)) {
	throw "Refusing to use backup directory outside the system temp folder: $resolvedBackupDir"
}

New-Item -ItemType Directory -Path $resolvedBackupDir | Out-Null
$backupDll = Join-Path $resolvedBackupDir 'card_editor.dll'
$backupPdb = Join-Path $resolvedBackupDir 'card_editor.pdb'
$backupSteamAppId = Join-Path $resolvedBackupDir 'steam_appid.txt'
$hadLivePdb = Test-Path -LiteralPath $livePdb
$hadSteamAppId = Test-Path -LiteralPath $steamAppIdPath

Copy-Item -LiteralPath $liveDll -Destination $backupDll
if ($hadLivePdb) {
	Copy-Item -LiteralPath $livePdb -Destination $backupPdb
}
if ($hadSteamAppId) {
	Copy-Item -LiteralPath $steamAppIdPath -Destination $backupSteamAppId
}

try {
	dotnet build $project -c Debug --nologo
	if ($LASTEXITCODE -ne 0) {
		throw "Card Editor build failed with exit code $LASTEXITCODE."
	}

	Copy-Item -LiteralPath (Join-Path $buildDir 'card_editor.dll') -Destination $liveDll -Force
	Copy-Item -LiteralPath (Join-Path $buildDir 'card_editor.pdb') -Destination $livePdb -Force
	[IO.File]::WriteAllText($steamAppIdPath, '2868840')
	Remove-Item -LiteralPath $report -Force -ErrorAction SilentlyContinue

	[Environment]::SetEnvironmentVariable('CARD_EDITOR_ENGINE_SELF_TEST', '1', 'Process')
	$startedAt = Get-Date
	$gameProcess = Start-Process -FilePath $gameExe `
		-ArgumentList '--headless','--audio-driver','Dummy' `
		-WorkingDirectory $GameDir `
		-WindowStyle Hidden `
		-PassThru

	$deadline = $startedAt.AddSeconds($TimeoutSeconds)
	while ((Get-Date) -lt $deadline -and -not (Test-Path -LiteralPath $report)) {
		if ($gameProcess.HasExited) {
			throw "The game exited before producing an engine UI report (exit code $($gameProcess.ExitCode)). Inspect the newest SlayTheSpire2 log."
		}
		Start-Sleep -Seconds 2
	}

	if (-not (Test-Path -LiteralPath $report)) {
		throw 'No engine UI report was produced. Confirm Steam is signed in, Card Editor is enabled, and the game process did not exit early; then inspect the newest SlayTheSpire2 log.'
	}

	$contents = Get-Content -LiteralPath $report -Raw
	Write-Host $contents
	if ($contents -notmatch 'RESULT: PASS') {
		throw 'Card Editor engine UI tests failed. See the report printed above.'
	}
}
finally {
	if ($null -ne $gameProcess) {
		Stop-Process -Id $gameProcess.Id -Force -ErrorAction SilentlyContinue
	}
	[Environment]::SetEnvironmentVariable('CARD_EDITOR_ENGINE_SELF_TEST', $previousSelfTestEnvironment, 'Process')

	Copy-Item -LiteralPath $backupDll -Destination $liveDll -Force
	if ($hadLivePdb) {
		Copy-Item -LiteralPath $backupPdb -Destination $livePdb -Force
	}
	else {
		Remove-Item -LiteralPath $livePdb -Force -ErrorAction SilentlyContinue
	}
	if ($hadSteamAppId) {
		Copy-Item -LiteralPath $backupSteamAppId -Destination $steamAppIdPath -Force
	}
	else {
		Remove-Item -LiteralPath $steamAppIdPath -Force -ErrorAction SilentlyContinue
	}

	Remove-Item -LiteralPath $resolvedBackupDir -Recurse -Force
}
