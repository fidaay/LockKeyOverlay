[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [string]$InnoCompiler = "ISCC.exe"
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$installerDir = Join-Path $repoRoot "Installer"
$issPath = Join-Path $installerDir "LockKeyOverlayInstaller.iss"
$compiler = Get-Command $InnoCompiler -ErrorAction SilentlyContinue

if (-not $compiler) {
    throw "ISCC.exe was not found. Install Inno Setup and make sure ISCC.exe is available on PATH."
}

& (Join-Path $PSScriptRoot "publish.ps1") -Configuration $Configuration

Push-Location $installerDir
try {
    & $compiler.Source $issPath
}
finally {
    Pop-Location
}
