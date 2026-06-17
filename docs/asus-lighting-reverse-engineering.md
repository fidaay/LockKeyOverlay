# ASUS Lighting Reverse Engineering Notes

## Scope

These notes capture the current LockKeyOverlay investigation into physical keyboard lighting control on the ASUS TUF Gaming F15 FX506LHB test machine.

## Local Findings

- Windows Dynamic Lighting / LampArray reports zero devices on this machine.
- PnP does not expose a connected ASUS RGB HID device such as `ACPI\ASUS7000` or `VID_0B05`.
- TUFAuraCore is installed as `B9ECED6F.TUFAuraCore_2.0.4.0_x86__qmba6cd70vzyy`.
- `ACPIWMI.dll` exports only coarse ASUS WMI helpers: open/close, device status, device control, and two-argument device control.
- `Aura.exe` contains strings for `InitRGBKBDevice`, `ExecCmd2RGBKB`, `NB_Keyboard_LED`, HID `SetFeature`, `WASD`, `QWER`, and `4ZONE`.
- The TUFAuraCore config has all-keyboard and zone-oriented sections such as `allData`, `wasdData`, `qwerData`, and `fourData`; it does not expose a Num Lock key section.

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

These IDs line up with the Linux `asus-wmi` driver definitions for ASUS TUF RGB mode/state. They describe whole-keyboard RGB mode/state control, not per-key control.

## Conclusion

The supported physical path on this machine is whole-keyboard ASUS/TUF backlight control through `ACPIWMI.dll`.

Num Lock-only physical blinking would require either:

- Windows exposing a LampArray with virtual-key support.
- An ASUS RGB HID/per-key interface being connected and accessible.
- A currently unknown vendor command that targets individual keys.

The current local evidence points to whole-keyboard control and possibly coarse zones, not individual Num Lock control.

## External References

- Linux `asus-wmi.h` defines `ASUS_WMI_DEVID_TUF_RGB_MODE` as `0x00100056`, `ASUS_WMI_DEVID_TUF_RGB_MODE2` as `0x0010005A`, and `ASUS_WMI_DEVID_TUF_RGB_STATE` as `0x00100057`: https://github.com/torvalds/linux/blob/master/include/linux/platform_data/x86/asus-wmi.h
- The original TUF RGB Linux patch describes mode/color as whole-keyboard RGB settings and state as boot/awake/sleep/keyboard power flags: https://lkml.org/lkml/2022/8/3/886
