[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$project = Join-Path $PSScriptRoot 'CardEditor.TestHarness.csproj'

Write-Host 'Card Editor headless beta regression suite'
Write-Host 'Note: Sentry.Godot reports that its GDExtension is absent because this is intentionally not a Godot process.'

Push-Location $repoRoot
try {
    & dotnet run --project $project
    if ($LASTEXITCODE -ne 0) {
        throw "Card Editor regression suite failed with exit code $LASTEXITCODE."
    }
}
finally {
    Pop-Location
}
