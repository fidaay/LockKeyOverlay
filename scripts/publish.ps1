[CmdletBinding()]
param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$projectPath = Join-Path $repoRoot "LockKeyOverlay\LockKeyOverlay.csproj"
$asusAuraHelperProjectPath = Join-Path $repoRoot "LockKeyOverlay.AsusAuraHelper\LockKeyOverlay.AsusAuraHelper.csproj"
$publishDir = Join-Path $repoRoot "artifacts\publish\LockKeyOverlay"
$asusAuraPublishDir = Join-Path $publishDir "AsusAura"

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

dotnet build $asusAuraHelperProjectPath `
    --configuration $Configuration

New-Item -ItemType Directory -Force -Path $asusAuraPublishDir | Out-Null

$asusAuraHelperOutput = Join-Path $repoRoot "LockKeyOverlay.AsusAuraHelper\bin\$Configuration\net48\LockKeyOverlay.AsusAuraHelper.exe"
Copy-Item -LiteralPath $asusAuraHelperOutput -Destination $asusAuraPublishDir -Force

Write-Host "Published LockKeyOverlay to $publishDir"
