[CmdletBinding()]
param(
    [switch]$IncludeBinaryStringHits
)

$ErrorActionPreference = "Stop"

function Write-Section {
    param([Parameter(Mandatory = $true)][string]$Title)

    Write-Host ""
    Write-Host "== $Title =="
}

function Read-UInt16 {
    param([byte[]]$Bytes, [int]$Offset)

    return [BitConverter]::ToUInt16($Bytes, $Offset)
}

function Read-UInt32 {
    param([byte[]]$Bytes, [int]$Offset)

    return [BitConverter]::ToUInt32($Bytes, $Offset)
}

function Convert-RvaToFileOffset {
    param(
        [uint32]$Rva,
        [array]$Sections
    )

    foreach ($section in $Sections) {
        $span = [Math]::Max($section.VirtualSize, $section.SizeOfRawData)
        if ($Rva -ge $section.VirtualAddress -and $Rva -lt ($section.VirtualAddress + $span)) {
            return [int]($section.PointerToRawData + ($Rva - $section.VirtualAddress))
        }
    }

    return $null
}

function Read-NullTerminatedAscii {
    param([byte[]]$Bytes, [int]$Offset)

    $end = $Offset
    while ($end -lt $Bytes.Length -and $Bytes[$end] -ne 0) {
        $end++
    }

    if ($end -le $Offset) {
        return ""
    }

    return [Text.Encoding]::ASCII.GetString($Bytes, $Offset, $end - $Offset)
}

function Get-PeExportNames {
    param([Parameter(Mandatory = $true)][string]$Path)

    if (-not (Test-Path $Path)) {
        return @()
    }

    $bytes = [IO.File]::ReadAllBytes($Path)

    if ($bytes.Length -lt 0x40 -or $bytes[0] -ne 0x4D -or $bytes[1] -ne 0x5A) {
        return @()
    }

    $peOffset = Read-UInt32 $bytes 0x3C
    if ($peOffset -le 0 -or ($peOffset + 0x18) -ge $bytes.Length) {
        return @()
    }

    $sectionCount = Read-UInt16 $bytes ($peOffset + 6)
    $optionalHeaderSize = Read-UInt16 $bytes ($peOffset + 20)
    $optionalHeaderOffset = $peOffset + 24
    $optionalMagic = Read-UInt16 $bytes $optionalHeaderOffset

    $dataDirectoryOffset = switch ($optionalMagic) {
        0x10B { $optionalHeaderOffset + 96 }
        0x20B { $optionalHeaderOffset + 112 }
        default { return @() }
    }

    $exportTableRva = Read-UInt32 $bytes $dataDirectoryOffset
    if ($exportTableRva -eq 0) {
        return @()
    }

    $sectionOffset = $optionalHeaderOffset + $optionalHeaderSize
    $sections = for ($index = 0; $index -lt $sectionCount; $index++) {
        $offset = $sectionOffset + ($index * 40)
        [pscustomobject]@{
            VirtualSize      = Read-UInt32 $bytes ($offset + 8)
            VirtualAddress   = Read-UInt32 $bytes ($offset + 12)
            SizeOfRawData    = Read-UInt32 $bytes ($offset + 16)
            PointerToRawData = Read-UInt32 $bytes ($offset + 20)
        }
    }

    $exportOffset = Convert-RvaToFileOffset $exportTableRva $sections
    if ($null -eq $exportOffset) {
        return @()
    }

    $nameCount = Read-UInt32 $bytes ($exportOffset + 24)
    $addressOfNamesRva = Read-UInt32 $bytes ($exportOffset + 32)
    $addressOfNamesOffset = Convert-RvaToFileOffset $addressOfNamesRva $sections

    if ($null -eq $addressOfNamesOffset -or $nameCount -eq 0) {
        return @()
    }

    $names = for ($index = 0; $index -lt $nameCount; $index++) {
        $nameRva = Read-UInt32 $bytes ($addressOfNamesOffset + ($index * 4))
        $nameOffset = Convert-RvaToFileOffset $nameRva $sections

        if ($null -ne $nameOffset) {
            Read-NullTerminatedAscii $bytes $nameOffset
        }
    }

    return $names | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Sort-Object
}

function Get-RelevantBinaryStrings {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string[]]$Patterns
    )

    if (-not (Test-Path $Path)) {
        return @()
    }

    $bytes = [IO.File]::ReadAllBytes($Path)
    $regex = [regex]::new("[ -~]{4,}", [Text.RegularExpressions.RegexOptions]::Compiled)
    $patternRegex = [regex]::new(($Patterns | ForEach-Object { [regex]::Escape($_) }) -join "|", [Text.RegularExpressions.RegexOptions]::IgnoreCase)

    $ascii = [Text.Encoding]::ASCII.GetString($bytes)
    $unicode = [Text.Encoding]::Unicode.GetString($bytes)

    return @(
        $regex.Matches($ascii) | ForEach-Object { $_.Value }
        $regex.Matches($unicode) | ForEach-Object { $_.Value }
    ) |
        Where-Object { $patternRegex.IsMatch($_) } |
        ForEach-Object { $_.Trim() } |
        Where-Object { $_.Length -gt 0 } |
        Sort-Object -Unique
}

function Write-TableOrNone {
    param(
        [Parameter(Mandatory = $true)]$Rows,
        [string]$NoneMessage = "None found."
    )

    if ($null -eq $Rows -or @($Rows).Count -eq 0) {
        Write-Host $NoneMessage
        return
    }

    ($Rows | Format-Table -AutoSize | Out-String).TrimEnd() | Write-Host
}

Write-Section "ASUS packages"
$asusPackages = Get-AppxPackage |
    Where-Object { $_.Name -match "ASUS|Aura|TUF|Armoury" -or $_.Publisher -match "ASUSTeK" } |
    Select-Object Name, Version, InstallLocation
Write-TableOrNone $asusPackages

$tufAuraPackage = $asusPackages | Where-Object { $_.Name -eq "B9ECED6F.TUFAuraCore" } | Select-Object -First 1
$tufAuraPath = $tufAuraPackage.InstallLocation
$acpiWmiPath = if ($tufAuraPath) { Join-Path $tufAuraPath "ACPIWMI.dll" } else { $null }
$auraExePath = if ($tufAuraPath) { Join-Path $tufAuraPath "Aura.exe" } else { $null }

Write-Section "ASUS services"
$asusServices = Get-Service |
    Where-Object { $_.Name -match "ASUS|Aura|Armoury|TUF|ROG" -or $_.DisplayName -match "ASUS|Aura|Armoury|TUF|ROG" } |
    Select-Object Name, DisplayName, Status, StartType
Write-TableOrNone $asusServices

Write-Section "LampArray / Dynamic Lighting"
& (Join-Path $PSScriptRoot "diagnose-lamparray.ps1")

Write-Section "Relevant PnP devices"
$pnpDevices = Get-PnpDevice -PresentOnly |
    Where-Object {
        $_.InstanceId -match "ASUS7000|ASUS9001|ASUS2018|VID_0B05|ATK4002" -or
        $_.FriendlyName -match "Aura|RGB|Lighting|ASUS"
    } |
    Select-Object Status, Class, FriendlyName, InstanceId
Write-TableOrNone $pnpDevices

$rgbHid = $pnpDevices | Where-Object { $_.InstanceId -match "ASUS7000|VID_0B05" -or $_.FriendlyName -match "Aura|RGB|Lighting" }
if ($null -eq $rgbHid -or @($rgbHid).Count -eq 0) {
    Write-Host "No connected ASUS RGB HID device was found."
}

Write-Section "ASUS WMI metadata"
$asusWmiClass = Get-CimClass -Namespace root\WMI -ClassName AsusAtkWmi_WMNB -ErrorAction SilentlyContinue
if ($null -eq $asusWmiClass) {
    Write-Host "AsusAtkWmi_WMNB was not found."
}
else {
    Write-Host "Class: $($asusWmiClass.CimClassName)"
    Write-Host "Qualifiers: $((@($asusWmiClass.CimClassQualifiers) | ForEach-Object { "$($_.Name)=$($_.Value)" }) -join '; ')"
    foreach ($method in $asusWmiClass.CimClassMethods) {
        $parameters = @($method.Parameters) | ForEach-Object { "$($_.Name):$($_.CimType)" }
        Write-Host "Method: $($method.Name)($($parameters -join ', '))"
    }
}

Write-Section "TUFAuraCore config clues"
$iniDirectory = Join-Path $env:ProgramData "ASUS\TUFAuraCore\UWPIni"
$basicIni = Join-Path $iniDirectory "Basic.Ini"
$customIni = Join-Path $iniDirectory "Custom0.Ini"

if (Test-Path $basicIni) {
    Write-Host "Basic.Ini:"
    Get-Content $basicIni |
        Where-Object { $_ -match "^(4ZONE|ApplyMode|AURASYNC|Color0)=" } |
        ForEach-Object { Write-Host "  $_" }
}

if (Test-Path $customIni) {
    Write-Host "Custom0.Ini sections:"
    Select-String -Path $customIni -Pattern "^\[(.+)\]$" |
        Select-Object -First 24 |
        ForEach-Object { Write-Host "  $($_.Matches[0].Groups[1].Value)" }

    Write-Host "Custom0.Ini mode indexes:"
    Get-Content $customIni -TotalCount 8 |
        Where-Object { $_ -match "Index=" } |
        ForEach-Object { Write-Host "  $_" }
}

Write-Section "ACPIWMI exports"
if ($acpiWmiPath -and (Test-Path $acpiWmiPath)) {
    $exports = Get-PeExportNames $acpiWmiPath
    if (@($exports).Count -eq 0) {
        Write-Host "No exports could be read from $acpiWmiPath"
    }
    else {
        $exports | ForEach-Object { Write-Host "  $_" }
    }
}
else {
    Write-Host "ACPIWMI.dll was not found."
}

if ($IncludeBinaryStringHits) {
    Write-Section "Relevant binary string hits"
    $patterns = @(
        "NumLock",
        "Num Lock",
        "LampArray",
        "VirtualKey",
        "SetColorsForKeys",
        "per-key",
        "individual",
        "4ZONE",
        "ZONE",
        "WASD",
        "QWER",
        "RGBKB",
        "NB_Keyboard_LED",
        "SetFeature",
        "FeatureReportByteLength",
        "ACPI\ASUS7000",
        "VID_0B05",
        "device_ctrl",
        "device_status",
        "ASUS_AURA"
    )

    foreach ($binaryPath in @($auraExePath, $acpiWmiPath)) {
        if (-not $binaryPath -or -not (Test-Path $binaryPath)) {
            continue
        }

        Write-Host ""
        Write-Host $binaryPath
        $hits = Get-RelevantBinaryStrings -Path $binaryPath -Patterns $patterns | Select-Object -First 80
        if (@($hits).Count -eq 0) {
            Write-Host "  No relevant strings found."
        }
        else {
            $hits | ForEach-Object { Write-Host "  $_" }
        }
    }
}

Write-Section "Conclusion"
Write-Host "This diagnostic is read-only."
Write-Host "A Num Lock-only physical blink would require either LampArray virtual-key support or an ASUS per-key/per-index interface."
Write-Host "If LampArray count is 0, no ASUS RGB HID is present, and ACPIWMI only exposes coarse device-control functions, the supported path is whole-keyboard backlight control."
