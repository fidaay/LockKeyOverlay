[CmdletBinding()]
param(
    [int]$MaxStringHits = 120
)

$ErrorActionPreference = "Stop"

function Write-Section {
    param([Parameter(Mandatory = $true)][string]$Title)

    Write-Host ""
    Write-Host "== $Title =="
}

function Read-UInt16 {
    param([byte[]]$Bytes, [int]$Offset)

    if ($Offset -lt 0 -or ($Offset + 2) -gt $Bytes.Length) {
        return [uint16]0
    }

    return [BitConverter]::ToUInt16($Bytes, $Offset)
}

function Read-UInt32 {
    param([byte[]]$Bytes, [int]$Offset)

    if ($Offset -lt 0 -or ($Offset + 4) -gt $Bytes.Length) {
        return [uint32]0
    }

    return [BitConverter]::ToUInt32($Bytes, $Offset)
}

function Read-UInt64 {
    param([byte[]]$Bytes, [int]$Offset)

    if ($Offset -lt 0 -or ($Offset + 8) -gt $Bytes.Length) {
        return [uint64]0
    }

    return [BitConverter]::ToUInt64($Bytes, $Offset)
}

function Read-NullTerminatedAscii {
    param([byte[]]$Bytes, [int]$Offset)

    if ($Offset -lt 0 -or $Offset -ge $Bytes.Length) {
        return ""
    }

    $end = $Offset
    while ($end -lt $Bytes.Length -and $Bytes[$end] -ne 0) {
        $end++
    }

    if ($end -le $Offset) {
        return ""
    }

    return [Text.Encoding]::ASCII.GetString($Bytes, $Offset, $end - $Offset)
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

function Get-PeLayout {
    param([Parameter(Mandatory = $true)][string]$Path)

    if (-not (Test-Path $Path)) {
        return $null
    }

    $bytes = [IO.File]::ReadAllBytes($Path)
    if ($bytes.Length -lt 0x40 -or $bytes[0] -ne 0x4D -or $bytes[1] -ne 0x5A) {
        return $null
    }

    $peOffset = Read-UInt32 $bytes 0x3C
    if ($peOffset -le 0 -or ($peOffset + 0x18) -ge $bytes.Length) {
        return $null
    }

    $sectionCount = Read-UInt16 $bytes ($peOffset + 6)
    $optionalHeaderSize = Read-UInt16 $bytes ($peOffset + 20)
    $optionalHeaderOffset = $peOffset + 24
    $optionalMagic = Read-UInt16 $bytes $optionalHeaderOffset

    $dataDirectoryOffset = switch ($optionalMagic) {
        0x10B { $optionalHeaderOffset + 96 }
        0x20B { $optionalHeaderOffset + 112 }
        default { return $null }
    }

    $sectionOffset = $optionalHeaderOffset + $optionalHeaderSize
    $sections = for ($index = 0; $index -lt $sectionCount; $index++) {
        $offset = $sectionOffset + ($index * 40)
        $rawName = [Text.Encoding]::ASCII.GetString($bytes, $offset, 8).Trim([char]0)

        [pscustomobject]@{
            Name             = $rawName
            VirtualSize      = Read-UInt32 $bytes ($offset + 8)
            VirtualAddress   = Read-UInt32 $bytes ($offset + 12)
            SizeOfRawData    = Read-UInt32 $bytes ($offset + 16)
            PointerToRawData = Read-UInt32 $bytes ($offset + 20)
        }
    }

    $directories = for ($index = 0; $index -lt 16; $index++) {
        $offset = $dataDirectoryOffset + ($index * 8)
        [pscustomobject]@{
            Index = $index
            Rva   = Read-UInt32 $bytes $offset
            Size  = Read-UInt32 $bytes ($offset + 4)
        }
    }

    [pscustomobject]@{
        Path        = $Path
        Bytes       = $bytes
        IsPe32Plus  = ($optionalMagic -eq 0x20B)
        Sections    = $sections
        Directories = $directories
    }
}

function Get-PeExportNames {
    param([Parameter(Mandatory = $true)][object]$Layout)

    $directory = $Layout.Directories | Where-Object { $_.Index -eq 0 } | Select-Object -First 1
    if ($null -eq $directory -or $directory.Rva -eq 0) {
        return @()
    }

    $exportOffset = Convert-RvaToFileOffset $directory.Rva $Layout.Sections
    if ($null -eq $exportOffset) {
        return @()
    }

    $nameCount = Read-UInt32 $Layout.Bytes ($exportOffset + 24)
    $addressOfNamesRva = Read-UInt32 $Layout.Bytes ($exportOffset + 32)
    $addressOfNamesOffset = Convert-RvaToFileOffset $addressOfNamesRva $Layout.Sections
    if ($null -eq $addressOfNamesOffset -or $nameCount -eq 0) {
        return @()
    }

    for ($index = 0; $index -lt $nameCount; $index++) {
        $nameRva = Read-UInt32 $Layout.Bytes ($addressOfNamesOffset + ($index * 4))
        $nameOffset = Convert-RvaToFileOffset $nameRva $Layout.Sections
        if ($null -ne $nameOffset) {
            Read-NullTerminatedAscii $Layout.Bytes $nameOffset
        }
    }
}

function Get-PeImports {
    param([Parameter(Mandatory = $true)][object]$Layout)

    $directory = $Layout.Directories | Where-Object { $_.Index -eq 1 } | Select-Object -First 1
    if ($null -eq $directory -or $directory.Rva -eq 0) {
        return @()
    }

    $importOffset = Convert-RvaToFileOffset $directory.Rva $Layout.Sections
    if ($null -eq $importOffset) {
        return @()
    }

    $imports = New-Object System.Collections.Generic.List[object]
    $descriptorOffset = $importOffset
    $thunkSize = if ($Layout.IsPe32Plus) { 8 } else { 4 }
    $ordinalFlag = if ($Layout.IsPe32Plus) {
        [Convert]::ToUInt64("8000000000000000", 16)
    }
    else {
        [Convert]::ToUInt64("80000000", 16)
    }

    while (($descriptorOffset + 20) -le $Layout.Bytes.Length) {
        $originalFirstThunk = Read-UInt32 $Layout.Bytes $descriptorOffset
        $nameRva = Read-UInt32 $Layout.Bytes ($descriptorOffset + 12)
        $firstThunk = Read-UInt32 $Layout.Bytes ($descriptorOffset + 16)

        if ($originalFirstThunk -eq 0 -and $nameRva -eq 0 -and $firstThunk -eq 0) {
            break
        }

        $nameOffset = Convert-RvaToFileOffset $nameRva $Layout.Sections
        $dllName = if ($null -ne $nameOffset) { Read-NullTerminatedAscii $Layout.Bytes $nameOffset } else { "" }

        $thunkRva = if ($originalFirstThunk -ne 0) { $originalFirstThunk } else { $firstThunk }
        $thunkOffset = Convert-RvaToFileOffset $thunkRva $Layout.Sections

        if ($null -ne $thunkOffset) {
            $index = 0
            while (($thunkOffset + ($index * $thunkSize) + $thunkSize) -le $Layout.Bytes.Length) {
                $entryOffset = $thunkOffset + ($index * $thunkSize)
                $thunkValue = if ($Layout.IsPe32Plus) {
                    Read-UInt64 $Layout.Bytes $entryOffset
                }
                else {
                    [uint64](Read-UInt32 $Layout.Bytes $entryOffset)
                }

                if ($thunkValue -eq 0) {
                    break
                }

                if (($thunkValue -band $ordinalFlag) -ne 0) {
                    $imports.Add([pscustomobject]@{
                        Dll      = $dllName
                        Function = $null
                        Ordinal  = ($thunkValue -band 0xFFFF)
                    })
                }
                else {
                    $importByNameOffset = Convert-RvaToFileOffset ([uint32]$thunkValue) $Layout.Sections
                    if ($null -ne $importByNameOffset) {
                        $hint = Read-UInt16 $Layout.Bytes $importByNameOffset
                        $functionName = Read-NullTerminatedAscii $Layout.Bytes ($importByNameOffset + 2)
                        $imports.Add([pscustomobject]@{
                            Dll      = $dllName
                            Function = $functionName
                            Hint     = $hint
                            Ordinal  = $null
                        })
                    }
                }

                $index++
            }
        }

        $descriptorOffset += 20
    }

    return $imports
}

function Get-RelevantPrintableStrings {
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

    @(
        $regex.Matches($ascii) | ForEach-Object { $_.Value.Trim() }
        $regex.Matches($unicode) | ForEach-Object { $_.Value.Trim() }
    ) |
        Where-Object { $_.Length -gt 0 -and $patternRegex.IsMatch($_) } |
        Sort-Object -Unique
}

function Find-StringEvidence {
    param(
        [Parameter(Mandatory = $true)][string[]]$Strings,
        [Parameter(Mandatory = $true)][string[]]$Patterns
    )

    foreach ($pattern in $Patterns) {
        $matches = $Strings |
            Where-Object { $_ -match [regex]::Escape($pattern) } |
            Select-Object -First 8

        [pscustomobject]@{
            Pattern = $pattern
            Count   = @($matches).Count
            Samples = (($matches | ForEach-Object { $_ }) -join " | ")
        }
    }
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

    ($Rows | Format-Table -AutoSize -Wrap | Out-String).TrimEnd() | Write-Host
}

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$package = Get-AppxPackage -Name "B9ECED6F.TUFAuraCore" | Select-Object -First 1
if ($null -eq $package) {
    throw "TUF Aura Core package was not found."
}

$packagePath = $package.InstallLocation
$auraExePath = Join-Path $packagePath "Aura.exe"
$acpiWmiPath = Join-Path $packagePath "ACPIWMI.dll"
$auraIniPath = Join-Path $packagePath "Aura.ini"
$programDataIniDirectory = Join-Path $env:ProgramData "ASUS\TUFAuraCore\UWPIni"
$programDataCustomIniPath = Join-Path $programDataIniDirectory "Custom0.Ini"

$knownAuraCoreUsbIds = @(
    "VID_0B05&PID_1854",
    "VID_0B05&PID_1869",
    "VID_0B05&PID_1866",
    "VID_0B05&PID_19B6",
    "VID_0B05&PID_1A30"
)

$stringPatterns = @(
    "NumLock",
    "Num Lock",
    "VK_NUMLOCK",
    "LampArray",
    "VirtualKey",
    "SetColorsForKeys",
    "HidD_SetFeature",
    "HidD_GetFeature",
    "SetFeature",
    "GetFeature",
    "ACPI\ASUS7000",
    "VID_0B05",
    "0B05",
    "ASUS_AURA",
    "RGBKB",
    "ExecCmd2RGBKB",
    "InitRGBKBDevice",
    "NB_Keyboard_LED",
    "WASD",
    "QWER",
    "4ZONE",
    "fourData",
    "qwerData",
    "wasdData",
    "allData"
)

Write-Section "Scope"
Write-Host "Read-only reverse-engineering aid for ASUS/TUF keyboard lighting."
Write-Host "Repo: $repoRoot"
Write-Host "Package: $($package.Name) $($package.Version)"
Write-Host "InstallLocation: $packagePath"

Write-Section "File identity"
$files = @($auraExePath, $acpiWmiPath, $auraIniPath, $programDataCustomIniPath) | Where-Object { Test-Path $_ }
foreach ($file in $files) {
    $item = Get-Item -LiteralPath $file
    $hash = Get-FileHash -Algorithm SHA256 -LiteralPath $file
    $signature = if ($item.Extension -in @(".exe", ".dll")) {
        Get-AuthenticodeSignature -LiteralPath $file
    }
    else {
        $null
    }

    [pscustomobject]@{
        Name       = $item.Name
        Length     = $item.Length
        Sha256     = $hash.Hash
        Signed     = if ($signature) { $signature.Status } else { "n/a" }
        Signer     = if ($signature -and $signature.SignerCertificate) { $signature.SignerCertificate.Subject } else { "" }
        Path       = $item.FullName
    }
}

Write-Section "ACPIWMI exports"
$acpiLayout = Get-PeLayout $acpiWmiPath
$exports = if ($null -ne $acpiLayout) { Get-PeExportNames $acpiLayout | Sort-Object -Unique } else { @() }
$exports | ForEach-Object { Write-Host "  $_" }

Write-Section "Aura.exe relevant imports"
$auraLayout = Get-PeLayout $auraExePath
$imports = if ($null -ne $auraLayout) { Get-PeImports $auraLayout } else { @() }
$relevantImports = $imports |
    Where-Object {
        $_.Function -match "HidD|HidP|SetupDi|CM_|CreateFile|ReadFile|WriteFile|DeviceIoControl|RegisterDeviceNotification|CoCreateInstance|CoInitialize|CoSetProxyBlanket|GetPrivateProfile|WritePrivateProfile|MapVirtualKey|GetKeyState|GetKeyboardState"
    } |
    Select-Object Dll, Function, Ordinal |
    Sort-Object Dll, Function -Unique
Write-TableOrNone $relevantImports

Write-Section "ACPIWMI relevant imports"
$acpiImports = if ($null -ne $acpiLayout) { Get-PeImports $acpiLayout } else { @() }
$relevantAcpiImports = $acpiImports |
    Where-Object {
        $_.Function -match "Wmi|CoCreateInstance|CoInitialize|CoSetProxyBlanket|DeviceIoControl|CreateFile|ReadFile|WriteFile|MapVirtualKey|GetKeyState|GetKeyboardState|GetProcAddress|LoadLibrary"
    } |
    Select-Object Dll, Function, Ordinal |
    Sort-Object Dll, Function -Unique
Write-TableOrNone $relevantAcpiImports

Write-Section "Binary string evidence"
foreach ($binaryPath in @($auraExePath, $acpiWmiPath)) {
    Write-Host ""
    Write-Host (Split-Path $binaryPath -Leaf)
    $strings = @(Get-RelevantPrintableStrings -Path $binaryPath -Patterns $stringPatterns)
    $evidence = Find-StringEvidence -Strings $strings -Patterns $stringPatterns
    Write-TableOrNone ($evidence | Where-Object { $_.Count -gt 0 })

    $missing = $evidence | Where-Object { $_.Count -eq 0 } | Select-Object -ExpandProperty Pattern
    if (@($missing).Count -gt 0) {
        Write-Host "Missing searched patterns: $($missing -join ', ')"
    }
}

Write-Section "TUFAuraCore UI/config clues"
if (Test-Path $auraIniPath) {
    $auraIniHits = Select-String -Path $auraIniPath -Pattern "Num|Lock|WASD|QWER|4ZONE|ZONE|Keyboard|LED" -CaseSensitive:$false |
        Select-Object -First $MaxStringHits LineNumber, Line
    Write-Host "Aura.ini relevant lines:"
    Write-TableOrNone $auraIniHits
}

if (Test-Path $programDataCustomIniPath) {
    $sections = Select-String -Path $programDataCustomIniPath -Pattern "^\[(.+)\]$" |
        ForEach-Object { $_.Matches[0].Groups[1].Value }
    Write-Host ""
    Write-Host "Custom0.Ini sections: $($sections -join ', ')"
}

Write-Section "Connected keyboard/lighting devices"
$pnpDevices = Get-PnpDevice -PresentOnly |
    Where-Object {
        $_.Class -in @("Keyboard", "HIDClass") -or
        $_.InstanceId -match "VID_0B05|ASUS7000|ASUS9001|ASUS2018|MSFT0001|PNP0C14|ATK4002" -or
        $_.FriendlyName -match "ASUS|Aura|RGB|Lighting|Keyboard|HID"
    } |
    Select-Object Status, Class, FriendlyName, InstanceId |
    Sort-Object Class, FriendlyName, InstanceId
Write-TableOrNone $pnpDevices

$knownUsbMatches = $pnpDevices | Where-Object {
    $instanceId = $_.InstanceId
    $knownAuraCoreUsbIds | Where-Object { $instanceId -match [regex]::Escape($_) }
}

Write-Host ""
if ($null -eq $knownUsbMatches -or @($knownUsbMatches).Count -eq 0) {
    Write-Host "Known ASUS Aura Core USB IDs not present: $($knownAuraCoreUsbIds -join ', ')."
}
else {
    Write-Host "Known ASUS Aura Core USB IDs present:"
    Write-TableOrNone $knownUsbMatches
}

Write-Section "ASUS WMI classes"
$asusClasses = Get-CimClass -Namespace root\WMI -ClassName "*Asus*" -ErrorAction SilentlyContinue |
    Select-Object CimClassName, CimClassMethods
foreach ($class in $asusClasses) {
    $methodNames = @($class.CimClassMethods | ForEach-Object { $_.Name }) -join ", "
    [pscustomobject]@{
        Class   = $class.CimClassName
        Methods = $methodNames
    }
}

Write-Section "Interpretation"
Write-Host "- This script performed static/read-only inspection only."
Write-Host "- Aura.exe has strings for ASUS RGB keyboard and HID feature paths, but this machine does not expose the matching ASUS RGB HID/USB device."
Write-Host "- ACPIWMI.dll exposes ASUS WMI status/control helpers, not a per-key or Num Lock-specific export."
Write-Host "- Local TUF Aura Core UI/config assets reference all-keyboard, WASD, QWER, and multi-zone modes, not Num Lock or per-key indexes."
Write-Host "- The remaining unknown would be undocumented vendor commands; this script intentionally does not brute-force or write unknown WMI/ACPI values."
