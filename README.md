# LockKeyOverlay

LockKeyOverlay is a small Windows desktop utility that shows a movable Num Lock overlay and tray menu.

## Requirements

- Windows 10/11 x64.
- .NET SDK 10.0.301 for development.
- Microsoft Windows Desktop Runtime 10 x64 on target machines.
- Inno Setup to build the installer. The packaging script autodetects common Inno Setup install paths and also accepts a manual `ISCC.exe` path.

## Development

```powershell
dotnet build
dotnet test
```

The active project is `LockKeyOverlay`. The old `NumLockIndicator` WinForms prototype was removed from this workspace.

## ASUS TUF Backlight Blink

The tray option `Parpadear backlight ASUS con Num Lock activo` is experimental and only targets the keyboard backlight through ASUS Aura Core / Armoury Crate's `ACPIWMI.dll`. It does not add Caps Lock or Scroll Lock behavior.

To check whether Windows exposes per-key RGB control through Dynamic Lighting / LampArray, run:

```powershell
.\scripts\diagnose-lamparray.ps1
```

If the diagnostic reports no LampArray devices, Num Lock-only RGB blinking is not available through the Windows per-key lighting API on that machine.

For a deeper read-only ASUS/TUF lighting capability report, run:

```powershell
.\scripts\diagnose-asus-lighting.ps1
```

For read-only ACPIWMI device status probes based on known ASUS/Linux device IDs, run:

```powershell
.\scripts\probe-asus-wmi-status.ps1
```

See `docs/asus-lighting-reverse-engineering.md` for the current reverse-engineering notes.

## Publish

```powershell
.\scripts\publish.ps1
```

This creates a framework-dependent publish output at:

```text
artifacts\publish\LockKeyOverlay
```

## Installer

```powershell
.\scripts\package-installer.ps1
```

If Inno Setup is installed somewhere custom, pass the compiler path:

```powershell
.\scripts\package-installer.ps1 -InnoCompiler "C:\Path\To\ISCC.exe"
```

The installer output is written to:

```text
artifacts\installer
```

Generated artifacts are ignored by git.
