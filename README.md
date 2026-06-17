# LockKeyOverlay

LockKeyOverlay is a small Windows desktop utility that shows a movable Num Lock overlay and tray menu.

## Requirements

- Windows 10/11 x64.
- .NET SDK 10.0.301 for development.
- Microsoft Windows Desktop Runtime 10 x64 on target machines.
- Inno Setup with `ISCC.exe` available on `PATH` to build the installer.

## Development

```powershell
dotnet build
dotnet test
```

The active project is `LockKeyOverlay`. The old `NumLockIndicator` WinForms prototype was removed from this workspace.

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

The installer output is written to:

```text
artifacts\installer
```

Generated artifacts are ignored by git.
