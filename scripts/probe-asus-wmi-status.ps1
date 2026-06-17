[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [string[]]$DeviceId,
    [string[]]$MoreByteArgument = @("0", "1"),
    [switch]$SkipMoreByte
)

$ErrorActionPreference = "Stop"

function Convert-ToUInt32 {
    param([Parameter(Mandatory = $true)][string]$Value)

    if ($Value.StartsWith("0x", [StringComparison]::OrdinalIgnoreCase)) {
        return [Convert]::ToUInt32($Value.Substring(2), 16)
    }

    return [Convert]::ToUInt32($Value, 10)
}

function Get-AcpiWmiPath {
    $packagePath = powershell.exe -NoProfile -ExecutionPolicy Bypass -Command @"
`$package = Get-AppxPackage -Name 'B9ECED6F.TUFAuraCore' | Select-Object -First 1
if (`$package) { `$package.InstallLocation }
"@

    if (-not [string]::IsNullOrWhiteSpace($packagePath)) {
        $candidate = Join-Path $packagePath.Trim() "ACPIWMI.dll"
        if (Test-Path $candidate) {
            return $candidate
        }
    }

    $windowsApps = Join-Path $env:ProgramFiles "WindowsApps"
    $candidate = Get-ChildItem -Path $windowsApps -Directory -Filter "B9ECED6F.TUFAuraCore_*__qmba6cd70vzyy" -ErrorAction SilentlyContinue |
        ForEach-Object { Join-Path $_.FullName "ACPIWMI.dll" } |
        Where-Object { Test-Path $_ } |
        Select-Object -First 1

    return $candidate
}

function Read-StatusBits {
    param([uint32]$Status)

    [pscustomobject]@{
        RawHex     = "0x{0:X8}" -f $Status
        RawDecimal = $Status
        Status     = (($Status -band 0x00000001) -ne 0)
        Unknown    = (($Status -band 0x00000002) -ne 0)
        Present    = (($Status -band 0x00010000) -ne 0)
        User       = (($Status -band 0x00020000) -ne 0)
        Bios       = (($Status -band 0x00040000) -ne 0)
        Brightness = ($Status -band 0x000000FF)
        MaxRaw     = (($Status -band 0x0000FF00) -shr 8)
    }
}

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$helperProject = Join-Path $repoRoot "LockKeyOverlay.AsusAuraHelper\LockKeyOverlay.AsusAuraHelper.csproj"
$helperOutputDirectory = Join-Path $repoRoot "LockKeyOverlay.AsusAuraHelper\bin\$Configuration\net48"
$helperExe = Join-Path $helperOutputDirectory "LockKeyOverlay.AsusAuraHelper.exe"
$helperAcpiWmi = Join-Path $helperOutputDirectory "ACPIWMI.dll"

dotnet build $helperProject --configuration $Configuration | Out-Host

$acpiWmi = Get-AcpiWmiPath
if ([string]::IsNullOrWhiteSpace($acpiWmi) -or -not (Test-Path $acpiWmi)) {
    throw "ACPIWMI.dll from TUFAuraCore was not found."
}

Copy-Item -LiteralPath $acpiWmi -Destination $helperAcpiWmi -Force

$knownDeviceIds = [ordered]@{
    KbdBacklight = 0x00050021
    Lightbar     = 0x00050025
    TufRgbMode   = 0x00100056
    TufRgbMode2  = 0x0010005A
    TufRgbState  = 0x00100057
    Led1         = 0x00020011
    Led2         = 0x00020012
    Led3         = 0x00020013
    Led4         = 0x00020014
    Led5         = 0x00020015
    Led6         = 0x00020016
}

$requested = if ($DeviceId.Count -gt 0) {
    $index = 0
    foreach ($id in $DeviceId) {
        $index++
        [pscustomobject]@{
            Name = "Custom$index"
            Id   = Convert-ToUInt32 $id
        }
    }
}
else {
    foreach ($entry in $knownDeviceIds.GetEnumerator()) {
        [pscustomobject]@{
            Name = $entry.Key
            Id   = [uint32]$entry.Value
        }
    }
}

Write-Host ""
Write-Host "Read-only ACPIWMI status probe"
Write-Host "Helper: $helperExe"
Write-Host "ACPIWMI: $acpiWmi"
Write-Host ""

$rows = foreach ($item in $requested) {
    $output = & $helperExe get-status ("0x{0:X8}" -f $item.Id) 2>&1
    $text = ($output | Out-String).Trim()
    $statusMatch = [regex]::Match($text, "status=0x(?<status>[0-9A-Fa-f]{8})")

    if (-not $statusMatch.Success) {
        [pscustomobject]@{
            Name       = $item.Name
            DeviceId   = "0x{0:X8}" -f $item.Id
            RawHex     = $null
            Present    = $null
            Status     = $null
            Unknown    = $null
            Brightness = $null
            MaxRaw     = $null
            Output     = $text
        }
        continue
    }

    $status = [Convert]::ToUInt32($statusMatch.Groups["status"].Value, 16)
    $bits = Read-StatusBits $status

    [pscustomobject]@{
        Name       = $item.Name
        DeviceId   = "0x{0:X8}" -f $item.Id
        RawHex     = $bits.RawHex
        Present    = $bits.Present
        Status     = $bits.Status
        Unknown    = $bits.Unknown
        Brightness = $bits.Brightness
        MaxRaw     = $bits.MaxRaw
        Output     = $text
    }
}

$rows |
    Select-Object Name, DeviceId, RawHex, Present, Status, Unknown, Brightness, MaxRaw |
    Format-Table -AutoSize

if (-not $SkipMoreByte) {
    Write-Host ""
    Write-Host "Read-only MoreBYTE status probe"

    $moreRows = foreach ($item in $requested) {
        foreach ($argumentValue in $MoreByteArgument) {
            $argument = Convert-ToUInt32 $argumentValue
            $output = & $helperExe get-status-more ("0x{0:X8}" -f $item.Id) ("0x{0:X8}" -f $argument) 2>&1
            $text = ($output | Out-String).Trim()
            $match = [regex]::Match(
                $text,
                "result=(?<result>\d+); status1=0x(?<status1>[0-9A-Fa-f]{8}); status2=0x(?<status2>[0-9A-Fa-f]{8}); status3=0x(?<status3>[0-9A-Fa-f]{8})")

            if (-not $match.Success) {
                [pscustomobject]@{
                    Name      = $item.Name
                    DeviceId  = "0x{0:X8}" -f $item.Id
                    Argument  = "0x{0:X8}" -f $argument
                    Result    = $null
                    Status1   = $null
                    Status2   = $null
                    Status3   = $null
                    Output    = $text
                }
                continue
            }

            [pscustomobject]@{
                Name      = $item.Name
                DeviceId  = "0x{0:X8}" -f $item.Id
                Argument  = "0x{0:X8}" -f $argument
                Result    = [int]$match.Groups["result"].Value
                Status1   = "0x{0:X8}" -f ([Convert]::ToUInt32($match.Groups["status1"].Value, 16))
                Status2   = "0x{0:X8}" -f ([Convert]::ToUInt32($match.Groups["status2"].Value, 16))
                Status3   = "0x{0:X8}" -f ([Convert]::ToUInt32($match.Groups["status3"].Value, 16))
                Output    = $text
            }
        }
    }

    $moreRows |
        Select-Object Name, DeviceId, Argument, Result, Status1, Status2, Status3 |
        Format-Table -AutoSize
}

Write-Host ""
Write-Host "Notes:"
Write-Host "- This script only calls read-only ACPIWMI status functions; it does not write lighting settings."
Write-Host "- Unless -SkipMoreByte is passed, it includes AsWMI_NB_GetDeviceStatus_MoreBYTE."
Write-Host "- For TUF RGB, Linux documents 0x00100056/0x0010005A as RGB mode/color and 0x00100057 as RGB state."
Write-Host "- A present TUF RGB mode still represents coarse keyboard RGB control, not Num Lock per-key control."
