# Capture flow spec

## Purpose

Capture a physical-pixel region from anywhere on the desktop, save it locally, and make it ready to paste with minimal interruption.

## Entry points

- Global shortcut, configured in Settings and defaulting to Ctrl+Shift+2.
- Tray menu item: Capture.

Both entry points use the same capture operation. If a capture is already in progress, a second trigger is ignored.

## Sequence

1. Hide the library window when it is visible.
2. Wait briefly for the window to leave the desktop.
3. Show the selection overlay over the frozen desktop using physical pixels.
4. Drag in any direction to choose a rectangle. Escape or right-click cancels.
5. Save the PNG through a temporary file and rename it into the configured folder.
6. Copy the selected bitmap to the clipboard, retrying transient clipboard locks.
7. Report save and clipboard results independently.
8. Refresh the library and restore the window when it was previously visible.

## States and failure behavior

| State | Result |
| --- | --- |
| Cancelled | No file is created and the clipboard is unchanged. |
| Save succeeds, copy succeeds | PNG saved and image copied. |
| Save fails, copy succeeds | Image copied; show the save error. |
| Save succeeds, copy fails | PNG remains available; show the clipboard error. |
| Both fail | Report both errors and keep the app usable. |
| Shortcut unavailable | Tray capture remains available and Settings explains the conflict. |

## Acceptance criteria

- Saved PNG dimensions match the selected physical pixels across 100%, 125%, and 150% scaling.
- Dragging in every direction produces the same normalized rectangle.
- The dimming overlay and cursor are absent from the saved image.
- Cancellation creates no file and does not change the clipboard.
- Save and clipboard errors are independent and actionable.
