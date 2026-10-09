# Settings screen spec

## Purpose

Let the user change where captures are saved, choose the global shortcut, and decide whether My Gyazo starts in the tray when they sign in.

## Structure

The settings page is left aligned with a maximum width of 760 px. It contains three separated sections followed by validation feedback and actions.

1. **Capture folder** — absolute path field, Browse button, and a hint that changing folders does not move existing captures.
2. **Keyboard shortcut** — Ctrl, Shift, and Alt checkboxes plus a key selector.
3. **Startup** — “Launch when I sign in” toggle and a hint to move the app to a permanent folder first.
4. **Actions** — Cancel and the accent “Save changes” button.

Use a bottom divider and 16 px bottom padding for each section. Keep 8 px between a section heading, its description, and its controls.

## Interactions and validation

- Browse opens a folder picker and replaces the path only after a folder is chosen.
- Saving requires an absolute capture folder path, at least one modifier, and a selected key.
- A shortcut already registered by another application leaves the current shortcut active and shows an error.
- If saving preferences fails, preserve the entered values and show the error in the settings InfoBar.
- Cancel leaves preferences unchanged and returns to the library.
- Successful save returns to the library, refreshes it, and reports that preferences were saved.
- Enabling launch at login writes the current executable path; disabling it removes the startup entry.

## States

| State | Expected behavior |
| --- | --- |
| Normal | Current preferences are populated when the page opens. |
| Invalid folder | Keep the field focused when possible and explain that an absolute path is required. |
| Missing modifier | Explain that at least one shortcut modifier is required. |
| Missing key | Explain that a shortcut key must be selected. |
| Shortcut conflict | Keep the old registration active and report that the new shortcut is unavailable. |
| Save error | Keep edits in place and show a recoverable error. |
| Saved | Return to the library and show a success status. |

## Acceptance criteria

- All fields have visible labels or automation names and are reachable with the keyboard.
- The accent style is used only for “Save changes”.
- Validation does not discard the user’s edits.
- The page remains readable at the minimum window size and in both system themes.
- Saving does not move existing captures when the folder changes.
