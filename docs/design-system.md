# My Gyazo design system

This document defines the visual and interaction rules that Codex should use when creating or changing My Gyazo UI. It describes the intent behind the values in [design-tokens.json](design-tokens.json); screen-specific behavior belongs in [screens/](screens/) and workflow behavior belongs in [flows/](flows/).

## Product principle

Keep the screenshot or the user's current task visually dominant. The app should feel like a quiet desktop companion: a capture is quick to start, easy to find, and ready to paste without an account or upload.

Remove headings, counts, metadata, and decorative copy when navigation or the image already communicates the same thing. Every control should help the user capture, find, inspect, or use an image.

## Visual language

- Use WinUI 3 native controls and the system typeface.
- Use Mica for the window backdrop and theme resources for surfaces, text, dividers, focus, and accent.
- Support the Windows light and dark settings without hard-coded light or dark colors.
- Use a flat canvas with one structural divider between the library and the preview. Do not introduce nested cards.
- Use the 4 px spacing rhythm and the values in `design-tokens.json`. A one-off value needs a reason in the screen spec.
- Keep the screenshot larger and more visually prominent than surrounding metadata.

## Layout rules

- The navigation pane is 180 px when expanded.
- The library column is 248 px in a regular window and 216 px in a compact window.
- Page content uses a 12 px inset.
- Use 4–8 px inside controls and list rows, 8–12 px between related controls, and 16–24 px between major sections.
- Put image commands next to the preview in a horizontal command bar. Let commands move to overflow when the window is narrow.
- Keep settings content left aligned with a maximum width of 760 px so forms remain readable.

## Type and content

| Role | Size | Use |
| --- | ---: | --- |
| Section heading | 18 px | Settings sections and concise empty-state titles |
| Primary content | 13–14 px | Capture dates, control labels, and main body copy |
| Secondary content | 12 px | Times, hints, descriptions, and supporting messages |
| Window identity | Native style | Title bar and navigation identity only |

Use sentence case. Prefer short, direct labels such as “Search captures”, “Show in folder”, and “Save changes”. Error text should say what happened and what the user can do next.

## Components

Use the native WinUI control that matches the interaction. The following contracts apply across screens:

| Component | Contract |
| --- | --- |
| NavigationView | Library is the primary destination. Settings is the system navigation destination. Keep the pane compact when space is limited. |
| TextBox | Use a visible placeholder only to describe the query or expected value. Give it an `AutomationProperties.Name`. |
| ListView | Use single selection for captures. Preserve the selected path when filtering, sorting, or refreshing if it remains visible. |
| CommandBar | Place commands beside the preview. Every icon-only action needs a label, tooltip, or automation name. |
| Button | Use an accent button only for the primary commit action. Use regular buttons for browse, cancel, refresh, and secondary actions. |
| InfoBar | Show concise success or error feedback in context. Keep it dismissible when it persists beyond the immediate action. |
| ContentDialog | Confirm destructive actions and state the consequence plainly. Default to Cancel for delete confirmation. |
| ToggleSwitch | Use for a persistent preference with an explicit on/off consequence. |

## State rules

Every screen spec must define the following states when they apply:

- normal content
- empty content
- filtered content with no matches
- unavailable or invalid content
- busy or pending action
- success and recoverable error feedback
- keyboard focus and selected item

Keep the selected capture when possible. Do not replace the preview with a generic loading or empty state while an unrelated status message is shown.

## Accessibility and keyboard behavior

- Every interactive control has a visible label or an `AutomationProperties.Name`.
- Icon-only buttons have a tooltip.
- Focus remains visible using the native theme focus visuals.
- The library, search box, sort menu, refresh button, preview commands, and settings controls must be reachable with the keyboard.
- Do not use color alone to communicate an error, selection, or success state.
- Keep error messages adjacent to the control or action that caused them.

## Implementation checklist

Before considering a UI change complete, verify:

1. The screen follows the relevant spec in `docs/screens/`.
2. Values come from the tokens or native WinUI theme resources.
3. Empty, error, focus, and narrow-window states are handled.
4. Automation names and tooltips exist for icon-only controls.
5. Light and dark themes retain readable contrast.
6. The UI check renderer or a manual Windows run covers the changed state.

## How to use this with Codex

For implementation work, start the task with: “Read `docs/design-system.md`, `docs/design-tokens.json`, and the relevant screen spec before changing UI.” For a new screen, copy the structure of an existing file in `docs/screens/`, define the states and acceptance criteria first, then implement it.
