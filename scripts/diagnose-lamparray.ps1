[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"

if ($PSVersionTable.PSEdition -ne "Desktop") {
    $windowsPowerShell = Join-Path $env:SystemRoot "System32\WindowsPowerShell\v1.0\powershell.exe"

    if (-not (Test-Path $windowsPowerShell)) {
        throw "Windows PowerShell 5.1 was not found. LampArray WinRT diagnostics require Windows PowerShell."
    }

    & $windowsPowerShell -NoProfile -ExecutionPolicy Bypass -File $PSCommandPath
    exit $LASTEXITCODE
}

Add-Type -AssemblyName System.Runtime.WindowsRuntime

[Windows.Devices.Lights.LampArray, Windows.Devices.Lights, ContentType=WindowsRuntime] | Out-Null
[Windows.Devices.Enumeration.DeviceInformation, Windows.Devices.Enumeration, ContentType=WindowsRuntime] | Out-Null
[Windows.Devices.Enumeration.DeviceInformationCollection, Windows.Devices.Enumeration, ContentType=WindowsRuntime] | Out-Null

$asTaskGeneric = ([System.WindowsRuntimeSystemExtensions].GetMethods() |
    Where-Object {
        $_.Name -eq "AsTask" -and
        $_.GetParameters().Count -eq 1 -and
        $_.GetParameters()[0].ParameterType.Name -eq 'IAsyncOperation`1'
    })[0]

function Wait-WinRtOperation {
    param(
        [Parameter(Mandatory = $true)]
        $Operation,

        [Parameter(Mandatory = $true)]
        [Type]$ResultType
    )

    $asTask = $script:asTaskGeneric.MakeGenericMethod($ResultType)
    $task = $asTask.Invoke($null, @($Operation))
    $task.Wait()
    return $task.Result
}

$selector = [Windows.Devices.Lights.LampArray]::GetDeviceSelector()
$devices = Wait-WinRtOperation `
    -Operation ([Windows.Devices.Enumeration.DeviceInformation]::FindAllAsync($selector)) `
    -ResultType ([Windows.Devices.Enumeration.DeviceInformationCollection])

Write-Host "LampArray devices found: $($devices.Count)"

if ($devices.Count -eq 0) {
    Write-Host "Result: Windows is not exposing any Dynamic Lighting / LampArray keyboard device."
    Write-Host "Num Lock-only RGB blinking is not available through Windows Dynamic Lighting on this system."
    exit 0
}

for ($index = 0; $index -lt $devices.Count; $index++) {
    $device = $devices[$index]

    Write-Host ""
    Write-Host "Device #$($index + 1)"
    Write-Host "Name: $($device.Name)"
    Write-Host "Id: $($device.Id)"
    Write-Host "Enabled: $($device.IsEnabled)"

    try {
        $lampArray = Wait-WinRtOperation `
            -Operation ([Windows.Devices.Lights.LampArray]::FromIdAsync($device.Id)) `
            -ResultType ([Windows.Devices.Lights.LampArray])

        if ($null -eq $lampArray) {
            Write-Host "LampArray: unavailable"
            continue
        }

        Write-Host "Kind: $($lampArray.LampArrayKind)"
        Write-Host "LampCount: $($lampArray.LampCount)"
        Write-Host "SupportsVirtualKeys: $($lampArray.SupportsVirtualKeys)"
        Write-Host "IsAvailable: $($lampArray.IsAvailable)"
        Write-Host "IsConnected: $($lampArray.IsConnected)"
        Write-Host "HardwareVendorId: $($lampArray.HardwareVendorId)"
        Write-Host "HardwareProductId: $($lampArray.HardwareProductId)"

        if ($lampArray.SupportsVirtualKeys) {
            Write-Host "Result: this device may support targeting Num Lock through LampArray virtual keys."
        }
        else {
            Write-Host "Result: this device exposes lighting, but not per-key virtual-key mapping."
        }
    }
    catch {
        Write-Host "LampArray inspection failed: $($_.Exception.Message)"
    }
}
