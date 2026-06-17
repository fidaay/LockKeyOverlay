# ASUS Lighting Reverse Engineering Notes

## Scope

These notes capture the current LockKeyOverlay investigation into physical keyboard lighting control on the ASUS TUF Gaming F15 FX506LHB test machine.

## Local Findings

- Windows Dynamic Lighting / LampArray reports zero devices on this machine.
- PnP does not expose a connected ASUS RGB HID device such as `ACPI\ASUS7000` or `VID_0B05`.
- The built-in keyboard is enumerated as `ACPI\MSFT0001` / Standard PS/2 Keyboard, not as an ASUS RGB HID keyboard.
- TUFAuraCore is installed as `B9ECED6F.TUFAuraCore_2.0.4.0_x86__qmba6cd70vzyy`.
- `ACPIWMI.dll` exports only coarse ASUS WMI helpers: open/close, device status, device control, and two-argument device control.
- `Aura.exe` contains strings for `InitRGBKBDevice`, `ExecCmd2RGBKB`, `NB_Keyboard_LED`, HID `SetFeature`, `WASD`, `QWER`, and `4ZONE`.
- The TUFAuraCore config has all-keyboard and zone-oriented sections such as `allData`, `wasdData`, `qwerData`, and `fourData`; it does not expose a Num Lock key section.
- `Aura.ini` localizes only all-keyboard, WASD, QWER, and multi-zone modes for this package.

## Read-Only ACPIWMI Probe

Run:

```powershell
.\scripts\probe-asus-wmi-status.ps1
```

Observed on the test machine:

- `0x00050021` keyboard backlight: present.
- `0x00100056` TUF RGB mode/color: present.
- `0x00100057` TUF RGB state: present.
- `0x0010005A` TUF RGB mode variant: not present.
- `0x00020011` through `0x00020016` LED slots: not present.
- `AsWMI_NB_GetDeviceStatus_MoreBYTE` returns the same first status word and zeroes for the additional words for the known IDs tested with bounded arguments `0` and `1`.

These IDs line up with the Linux `asus-wmi` driver definitions for ASUS TUF RGB mode/state. They describe whole-keyboard RGB mode/state control, not per-key control.

## External Cross-Checks

- Joooch/TUF-Keyboard-Extension uses the same `ACPIWMI.dll` exports and the same `0x100056`/`0x100057` device-control IDs. Its keypress and music modes change the whole laptop RGB backlight color; it does not address individual keys.
- OpenRGB's ASUS Aura Core documentation describes a separate USB HID protocol for ROG Aura Core keyboards using ASUS USB IDs such as `0B05:1854`, `0B05:1869`, and `0B05:1866`. None of the known Aura Core USB IDs are present on this test machine.
- rogauracore targets the same USB Aura Core family (`0B05:1854`, `0B05:1869`, `0B05:1866`, `0B05:19B6`, and `0B05:1A30`) and supports single-zone or multi-zone messages, not a generic PS/2 Num Lock LED path.
- Microsoft Dynamic Lighting requires HID LampArray-compatible devices. The relevant Windows API can target virtual keys only when a LampArray device reports virtual-key support; this machine reports no LampArray devices at all.
- ASUS support documentation treats Num Lock indicator LEDs as toggle-state indicators, while notebook backlight documentation exposes keyboard-backlight brightness/effect controls separately.

## Static Binary Inspection

Run:

```powershell
.\scripts\reverse-engineer-asus-lighting.ps1
```

Observed on the test machine:

- `Aura.exe` imports HID and device-enumeration APIs: `HidD_SetFeature`, `HidD_GetFeature`, `SetupDiEnumDeviceInterfaces`, `SetupDiGetDeviceInterfaceDetailW`, and `CreateFileW`.
- `Aura.exe` contains strings for `ACPI\ASUS7000`, `InitRGBKBDevice`, `ExecCmd2RGBKB`, `GetRGBKBStatus`, `NB_Keyboard_LED`, `SetFeature (0x5D 0x41)`, and `SetFeature (0x5D 0x5)`.
- Those strings indicate the official ASUS binary has a USB/HID RGB keyboard code path for machines that expose the matching ASUS RGB device.
- This test machine does not expose that matching device: no `ACPI\ASUS7000`, no `VID_0B05`, and no known Aura Core USB keyboard IDs are present in PnP.
- `ACPIWMI.dll` imports generic file/device/COM helpers such as `CreateFileW`, `DeviceIoControl`, `CoCreateInstance`, and `CoSetProxyBlanket`, and exports only the ASUS WMI helper functions already probed.
- `ACPIWMI.dll` did not surface strings for `RGBKB`, `ACPI\ASUS7000`, `HidD_SetFeature`, `WASD`, `QWER`, `4ZONE`, `NumLock`, or `Num Lock`.
- Both `Aura.exe` and `ACPIWMI.dll` were searched for `NumLock`, `Num Lock`, `VK_NUMLOCK`, `LampArray`, and `SetColorsForKeys`; no matching strings were found.

Interpretation:

- The official ASUS package has two relevant conceptual paths: a HID RGB keyboard path and an ACPI/WMI path.
- The HID RGB path is not usable on this test machine because the required ASUS RGB HID/USB device is not enumerated.
- The ACPI/WMI path is usable and is the path already used by LockKeyOverlay, but it exposes whole-keyboard/coarse TUF RGB controls, not Num Lock-specific addressing.

## Evidence Matrix

| Candidate path | Local result | Num Lock-only viability |
| --- | --- | --- |
| Windows Dynamic Lighting / LampArray | Zero devices reported | Not viable on this machine |
| LampArray virtual-key mapping | No LampArray device to inspect | Not viable on this machine |
| ASUS Aura Core USB HID | `Aura.exe` contains HID RGB code, but no matching `ACPI\ASUS7000`, `VID_0B05`, or known Aura Core USB device is present | Not viable on this machine |
| ASUS ACPIWMI TUF RGB | `KbdBacklight`, `TufRgbMode`, and `TufRgbState` present | Whole-keyboard/coarse control only |
| ASUS ACPIWMI LED slots | `Led1` through `Led6` not present | Not viable through known LED IDs |
| TUFAuraCore config/UI | All-keyboard, WASD, QWER, and four-zone concepts only | No Num Lock section or key index |
| Static string/import search | HID RGB path exists in ASUS binary, but no Num Lock/per-key symbols or local device path were found | No exposed Num Lock path found |
| Generic keyboard toggle LED | Tied to Num Lock state through keyboard behavior | Can blink only by toggling real Num Lock state |

## Conclusion

The supported physical path on this machine is whole-keyboard ASUS/TUF backlight control through `ACPIWMI.dll`.

Num Lock-only physical blinking would require either:

- Windows exposing a LampArray with virtual-key support.
- An ASUS RGB HID/per-key interface being connected and accessible.
- A currently unknown vendor command that targets individual keys.

The current local evidence points to whole-keyboard control and possibly coarse zones, not individual Num Lock control.

There is very high practical confidence that Num Lock-only physical RGB/backlight control is not exposed on this test machine through supported Windows, ASUS WMI, or known ASUS Aura Core paths. Absolute certainty would require ASUS internal protocol documentation or risky/invasive firmware-level reverse engineering. The remaining unknown category is therefore not a normal app capability; it would be speculative vendor-command discovery and is intentionally not brute-forced by this project.

## External References

- Linux `asus-wmi.h` defines `ASUS_WMI_DEVID_TUF_RGB_MODE` as `0x00100056`, `ASUS_WMI_DEVID_TUF_RGB_MODE2` as `0x0010005A`, and `ASUS_WMI_DEVID_TUF_RGB_STATE` as `0x00100057`: https://github.com/torvalds/linux/blob/master/include/linux/platform_data/x86/asus-wmi.h
- The original TUF RGB Linux patch describes mode/color as whole-keyboard RGB settings and state as boot/awake/sleep/keyboard power flags: https://lkml.org/lkml/2022/8/3/886
- OpenRGB documents ASUS Aura Core keyboards as USB HID devices with 17-byte zone messages: https://openrgb-wiki.readthedocs.io/en/latest/asus/ASUS-Aura-Core/
- rogauracore documents the known ROG Aura Core USB IDs and single/multi-zone commands: https://github.com/wroberts/rogauracore
- Joooch/TUF-Keyboard-Extension uses the same ASUS `ACPIWMI.dll` WMI control path for whole-keyboard effects: https://github.com/Joooch/TUF-Keyboard-Extension
- Microsoft documents Dynamic Lighting as using HID LampArray devices: https://learn.microsoft.com/en-us/windows-hardware/design/component-guidelines/dynamic-lighting-devices
- Microsoft documents `LampArray.SupportsVirtualKeys` as the flag for lamps mapped to virtual keys: https://learn.microsoft.com/en-us/uwp/api/windows.devices.lights.lamparray.supportsvirtualkeys
- ASUS documents Num Lock indicator LEDs as state indicators for the numeric keypad: https://www.asus.com/uk/support/faq/1054285/
