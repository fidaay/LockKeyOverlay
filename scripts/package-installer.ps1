[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [string]$InnoCompiler = ""
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$installerDir = Join-Path $repoRoot "Installer"
$issPath = Join-Path $installerDir "LockKeyOverlayInstaller.iss"

function Resolve-InnoCompiler {
    param(
        [string]$RequestedCompiler
    )

    if (-not [string]::IsNullOrWhiteSpace($RequestedCompiler)) {
        if (Test-Path -LiteralPath $RequestedCompiler) {
            return (Resolve-Path -LiteralPath $RequestedCompiler).Path
        }

        $requestedCommand = Get-Command $RequestedCompiler -ErrorAction SilentlyContinue
        if ($requestedCommand) {
            return $requestedCommand.Source
        }

        throw "Inno Setup compiler was not found from -InnoCompiler '$RequestedCompiler'. Pass a valid ISCC.exe path or command name."
    }

    $pathCommand = Get-Command "ISCC.exe" -ErrorAction SilentlyContinue
    if ($pathCommand) {
        return $pathCommand.Source
    }

    $commonPaths = @(
        "C:\Program Files (x86)\Inno Setup 6\ISCC.exe",
        "C:\Program Files\Inno Setup 6\ISCC.exe",
        "C:\Program Files (x86)\Inno Setup 5\ISCC.exe",
        "C:\Program Files\Inno Setup 5\ISCC.exe"
    )

    foreach ($path in $commonPaths) {
        if (Test-Path -LiteralPath $path) {
            return (Resolve-Path -LiteralPath $path).Path
        }
    }

    throw "ISCC.exe was not found on PATH or in common Inno Setup install locations. Install Inno Setup or pass -InnoCompiler with the full ISCC.exe path."
}

$compilerPath = Resolve-InnoCompiler -RequestedCompiler $InnoCompiler

& (Join-Path $PSScriptRoot "publish.ps1") -Configuration $Configuration

Push-Location $installerDir
try {
    & $compilerPath $issPath
}
finally {
    Pop-Location
}
