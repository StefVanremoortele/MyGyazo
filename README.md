# My Gyazo

Project direction: [vision](docs/vision.md) · [architecture and design system](docs/architecture.md).

A personal Windows screenshot utility. Press **Ctrl+Shift+2**, drag a rectangle,
and release: the image is saved as a PNG and copied to the clipboard. Escape or
right-click cancels. Captures stay on your computer.

## Run

Open `MyGyazo.WinUI.exe` in `artifacts/winui-compact/win-arm64` for ARM Windows, or
`artifacts/winui-compact/win-x64` for Intel/AMD Windows. Exit any older version through its
tray menu before launching the WinUI 3 app. The self-contained releases include
.NET and the Windows App SDK runtime. Keep the whole release folder together.
The unsigned executable may prompt Windows to ask whether you trust it.

The app runs in the notification area; Windows may place its icon under the
hidden-icons arrow. Right-click it for **Capture**, **Library**, **Settings**, or
**Exit**. Double-click opens the library. Closing the window leaves the tray app running.
Only one instance runs per Windows session.

The WinUI 3 interface follows the Windows light/dark theme and accent color, uses
a Mica backdrop, and has native navigation, image commands, settings, and dialogs.
Launching manually opens the library; launch at login starts quietly in the tray.

Captures default to your Windows Pictures folder under `My Gyazo`. Settings let
you change the folder and shortcut and enable launch at login (off by default).
Move the release folder to its permanent location before enabling launch at login.
If you move it later, open Settings and save again to update the startup path.
After switching from the WPF version, save Settings once if launch at login was
enabled, so Windows starts the new executable. Existing captures and settings are
compatible and require no data migration.
Disable launch at login before removing the app.

The library uses a flat layout with search, a thumbnail list, and a large preview.
The sort button switches between newest first (the default) and oldest first;
the chosen order is retained while searching and refreshing until the app exits.
Capture using the hotkey or tray menu. Search by filename or date. Select a capture
to open, copy, reveal in Explorer, or move it to the Recycle Bin. Refresh updates
external changes; switching folders does not move old files. Captures are never
automatically deleted. Settings live in `%LOCALAPPDATA%\MyGyazo\settings.json`.

## Develop and validate

Requires the .NET 10 SDK on Windows. Microsoft Windows App SDK 2.4.0 and Windows
SDK build tools restore through NuGet. Commands below target ARM64; use `win-x64`
for Intel/AMD Windows.

```powershell
dotnet build MyGyazo.slnx -c Release -m:1
dotnet run --project tests/MyGyazo.Tests -c Release --no-build
dotnet run --project src/MyGyazo.WinUI -c Release -r win-arm64 --no-build
dotnet run --project src/MyGyazo.WinUI -c Release -r win-arm64 --no-build -- --ui-checks artifacts/winui-preview
dotnet publish src/MyGyazo.WinUI -c Release -r win-x64 --self-contained true -m:1 -o artifacts/winui-compact/win-x64
dotnet publish src/MyGyazo.WinUI -c Release -r win-arm64 --self-contained true -m:1 -o artifacts/winui-compact/win-arm64
```

The dependency-free test executable returns a nonzero exit code on failure.
It covers rectangle geometry, physical-pixel behavior across scaling boundaries,
settings persistence, filenames, history ordering, and independent save/copy failure.

The `--ui-checks` mode uses isolated sample PNGs and renders actual WinUI controls
offscreen to `artifacts/winui-preview`, including both light and dark themes. It
checks search, sorting, selection, empty states, invalid images, settings navigation,
validation, and closing to the tray. Results are in `checks.txt`; failures return
a nonzero exit code. Mica is outside the XAML render target, so preview PNGs use
its neutral theme fallback. Checks do not change your capture folder, settings,
or clipboard.

### Manual desktop acceptance checks

- Capture on each monitor at 100%, 125%, and 150% scaling. Check PNG dimensions
  against the selected physical pixels. Test monitors to the left/above the primary
  display and a rectangle spanning monitors with different scaling.
- Drag in every direction; cancel with Escape/right-click; click without dragging.
  Cancellation must not create a file or change the clipboard. Check that the
  dimming overlay and cursor are absent from the saved PNG.
- Paste into Paint and another application. Verify clipboard data persists after
  exiting My Gyazo. Confirm rapid captures receive distinct filenames.
- Take captures through both the shortcut and tray. Hold the shortcut: only one
  overlay should open. Register an occupied shortcut in Settings: the old shortcut
  should remain active and the conflict should be reported.
- Select an unwritable destination; verify copying still succeeds and the save
  error is shown. Hold the clipboard open in another program; verify a PNG is
  still saved and a clipboard error is shown after retries.
- Restart and browse history; open/copy/reveal/delete a PNG. Confirm deletion goes
  to the Recycle Bin. Remove a file externally and refresh. Test invalid PNG files
  and inaccessible/missing capture folders without crashing.
- Close History and Settings and verify tray capture still works. Launch a second
  instance and verify the first stays active. Exit and verify the shortcut is freed.
- Enable startup from a permanent release location, sign out/in, verify startup;
  disable and verify the registry entry is removed.

## Implementation

`MyGyazo.WinUI` uses the actual WinUI 3 framework, not WPF styling. Its shell is
defined in `Views/Shell.xaml`; native theme resources follow system appearance.
The app is unpackaged and self-contained. `MyGyazo.Desktop` isolates the Windows
Forms capture overlay and native hotkey/window helpers from WinUI's XAML tooling.
The icon can be regenerated with `tools/New-AppIcon.ps1`. A Windows Forms tray
icon integrates with Windows. The capture overlay draws a frozen desktop in
physical pixels with Per-Monitor V2 awareness. PNG writes use a temporary file followed by rename;
clipboard failure does not discard the saved capture, and save failure does not
prevent copying. No administrator access is required.

`MyGyazo.Core` contains storage/settings and capture coordination independently of
desktop UI. The earlier WPF app remains in `src/MyGyazo`, with its own checks in
`tests/MyGyazo.UiChecks`, and previous releases remain under `artifacts/fluent`.
The WinUI app has no reference to WPF. No server, telemetry, or screenshot uploads
are included.

Protected video, secure desktops, and some accelerated/HDR content may not capture
faithfully through the desktop-copy API. Editing, recording, uploads, shareable
links, an installer, and automatic updates are outside this version.
