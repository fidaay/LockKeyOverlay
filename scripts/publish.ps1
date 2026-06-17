[CmdletBinding()]
param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$projectPath = Join-Path $repoRoot "LockKeyOverlay\LockKeyOverlay.csproj"
$publishDir = Join-Path $repoRoot "artifacts\publish\LockKeyOverlay"

if (Test-Path $publishDir) {
    Remove-Item -LiteralPath $publishDir -Recurse -Force
}

New-Item -ItemType Directory -Force -Path $publishDir | Out-Null

dotnet publish $projectPath `
    --configuration $Configuration `
    --runtime win-x64 `
    --self-contained false `
    -p:PublishSingleFile=false `
    --output $publishDir

Write-Host "Published LockKeyOverlay to $publishDir"
