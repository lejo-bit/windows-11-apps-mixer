# Volume Mixer

A small Windows 11 tray application for **muting and controlling the volume of individual running programs**.

Built with **C# / WinForms (.NET 10)** and **NAudio 3.1.0** (Windows Core Audio API).

---

## Features

- Lists every active audio session (programs currently playing sound).
- **Per-program volume slider** (0–100%) and a **mute** toggle.
- **Master volume** slider + master mute at the top.
- **Auto-refresh** every 1.5 s — new programs appear, closed ones disappear.
- **Program icons** — with a built-in speaker icon as a fallback.
- **Rename** a program (double-click its name; leave the field empty to reset).
- **Remembers** the mute state and custom names across restarts.
- **System tray** support:
  - closing or minimizing the window sends it to the tray,
  - double-click the tray icon to restore,
  - right-click for a menu: *Show Volume Mixer* / *Exit*.
- Detects the *System Sounds* session (shows a friendly name instead of the raw resource reference).
- The window **auto-fits its height** to the number of programs.
- Custom application icon.

---

## Requirements

- **Windows 10/11** (uses the Windows Core Audio API).
- **.NET 10 runtime** — unless you use the self-contained build, which includes it.

---

## Build & Run

### Run from source
```powershell
dotnet run --project VolumeMixer
```

### Build (framework-dependent)
```powershell
dotnet build -c Release
```
Output: `bin\Release\net10.0-windows\VolumeMixer.exe` (plus NAudio DLLs next to it).

### Publish a single-file EXE

Self-contained (portable — runs without .NET installed):
```powershell
dotnet publish -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -o dist
```

Framework-dependent (smaller, requires the .NET runtime):
```powershell
dotnet publish -c Release -p:PublishSingleFile=true -o dist
```

---

## Usage

1. Launch the app. Every program that is playing sound is listed with an icon, name, volume slider and a **Mute** button.
2. Drag a slider to change that program's volume, or click **Mute** to silence it.
3. **Double-click a program name** to rename it (empty value restores the default name).
4. Close (`X`) or minimize the window → it hides to the **system tray** and keeps running.
5. **Double-click the tray icon** to bring the window back, or right-click it for *Show Volume Mixer* / *Exit*.

> Only **Exit** in the tray menu actually quits the app.

---

## Settings

Settings are stored in:
```
%APPDATA%\VolumeMixer\settings.json
```

They are keyed by **process name** (e.g. `spotify`), so the mute state and custom name are restored after a restart.

---

## Project structure

| File | Purpose |
|---|---|
| `Program.cs` | Entry point (STA, high-DPI). |
| `MainForm.cs` | Main window, tray logic, session refresh. |
| `SessionRow.cs` | One row per program (icon, name, slider, mute). |
| `SettingsStore.cs` | JSON settings persistence. |
| `RenameDialog.cs` | Rename dialog. |
| `app.ico` | Application icon. |

---

## Technical notes

- **NAudio 3.x** differs from 2.x: several `GetX()` methods are now properties (`GetProcessID`, `GetSessionIdentifier`), `SessionCollection` is indexer-only (no `IEnumerable`), and `AudioSessionManager.RefreshSessions()` is used to get fresh data.
- Muting/renaming is keyed by **process name**, so multiple audio sessions of the same process (e.g. several browser tabs) share one name and mute state.
- The *System Sounds* session returns a raw resource reference (`@%SystemRoot%\System32\AudioSrv.Dll,-202`) — it is displayed as **"System Sounds"**.
