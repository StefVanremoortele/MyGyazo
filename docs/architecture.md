# Architecture

The main app is C# on .NET 10 with WinUI 3 and Windows App SDK 2.4. WinUI XAML defines the shell; code behind handles navigation, the library, and settings. It uses the system theme, Mica, and native controls. The unpackaged release is self-contained for Windows ARM64 and x64.

`MyGyazo.Desktop` holds the Windows Forms tray icon and capture overlay plus Win32 hotkey and window helpers. `MyGyazo.Core` holds selection geometry, settings, file history, and save/copy coordination. PNGs live in the chosen Pictures folder; JSON settings live under `%LOCALAPPDATA%\MyGyazo`. There is no server or database. The earlier WPF app remains in `src/MyGyazo` as a legacy version.

## Design system

The detailed, implementation-ready design system is maintained in [design-system.md](design-system.md), with reusable values in [design-tokens.json](design-tokens.json). Screen and workflow specifications live in [screens/](screens/) and [flows/](flows/). Update those documents when changing an intentional product or visual rule.

- **Purpose:** Keep the screenshot or current task visually dominant. Remove headings, counts, and metadata that repeat what the navigation or image already tells the user.
- **Layout:** Use a flat canvas, one structural divider between the library and preview, and no nested cards. The library is 248 px wide, or 216 px in a compact window; the navigation pane is 180 px wide.
- **Spacing:** Use a 4 px rhythm. Prefer 4–8 px within a control or list item, 8–12 px between related controls, and 16–24 px only between major sections. Use 12 px page insets.
- **Type:** Use the native WinUI typeface. Use 18 px for section headings, 13–14 px for primary content, and 12 px for secondary text. Avoid decorative copy and duplicate page titles.
- **Color:** Use WinUI theme resources for surfaces, text, dividers, focus, and accent. Follow the Windows light/dark setting; do not hard-code theme colors.
- **Controls:** Use native WinUI navigation, search, sort menus, lists, buttons, and dialogs. Put image commands next to the preview. Keep capture available through the global shortcut and tray menu. Give icon-only controls a name and tooltip.
- **States:** Show a concise empty or error message only when needed. Keep the selected capture when filtering, sorting, or refreshing where possible. Make keyboard focus and selection visible.
