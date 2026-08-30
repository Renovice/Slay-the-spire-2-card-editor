$ErrorActionPreference = 'Stop'

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$project = Join-Path $repoRoot 'mods\card_editor\card_editor.csproj'
$buildDir = Join-Path $repoRoot 'mods\card_editor\build\net9.0'
$gameDir = 'C:\Program Files (x86)\Steam\steamapps\common\Slay the Spire 2'
$liveModDir = Join-Path $gameDir 'mods\Card Editor2'
$steamExe = 'C:\Program Files (x86)\Steam\steam.exe'
$report = Join-Path $env:APPDATA 'SlayTheSpire2\card_editor\engine_ui_selftest_report.txt'

if (Get-Process -Name SlayTheSpire2 -ErrorAction SilentlyContinue) {
	throw 'Close Slay the Spire 2 before running the engine UI tests.'
}

dotnet build $project -c Debug --nologo
if ($LASTEXITCODE -ne 0) {
	throw "Card Editor build failed with exit code $LASTEXITCODE."
}

Copy-Item -LiteralPath (Join-Path $buildDir 'card_editor.dll') -Destination (Join-Path $liveModDir 'card_editor.dll') -Force
Copy-Item -LiteralPath (Join-Path $buildDir 'card_editor.pdb') -Destination (Join-Path $liveModDir 'card_editor.pdb') -Force
Remove-Item -LiteralPath $report -Force -ErrorAction SilentlyContinue

$startedAt = Get-Date
Start-Process -FilePath $steamExe `
	-ArgumentList '-applaunch','2868840','--headless','--audio-driver','Dummy','--card-editor-engine-self-test' `
	-WindowStyle Hidden

$deadline = $startedAt.AddMinutes(3)
while ((Get-Date) -lt $deadline -and -not (Test-Path -LiteralPath $report)) {
	Start-Sleep -Seconds 2
}

if (-not (Test-Path -LiteralPath $report)) {
	throw 'No engine UI report was produced. Confirm Steam is signed in and Card Editor is enabled, then inspect the newest SlayTheSpire2 log.'
}

$contents = Get-Content -LiteralPath $report -Raw
Write-Host $contents
if ($contents -notmatch 'RESULT: PASS') {
	throw 'Card Editor engine UI tests failed. See the report printed above.'
}
