# Capture library screen spec

## Purpose

Let the user find a saved capture, inspect it at a useful size, and copy, open, reveal, or delete it.

## Structure

```text
NavigationView (180 px expanded)
└── Capture library
    ├── Library column (248 px regular / 216 px compact)
    │   ├── Search captures
    │   ├── Sort menu + Refresh
    │   └── Single-select thumbnail list
    ├── 1 px divider
    └── Preview column
        ├── Selected screenshot
        └── Copy / Open / Show in folder / Delete
```

The page uses a 12 px inset. The library and preview sit on one flat canvas; do not wrap either side in a card.

## Content and interactions

- Search matches the filename and displayed date. Filtering is case-insensitive.
- Sort defaults to “Newest first”. “Oldest first” is available from a radio menu.
- Refresh rereads the configured folder and keeps the selected path when it still exists.
- Selecting a row loads the preview and enables image commands.
- Copy puts the selected image on the clipboard and reports success or failure in the status bar.
- Open launches the image with the system default application.
- Show in folder opens Explorer with the capture selected.
- Delete requires confirmation and moves the image to the Recycle Bin.

## States

| State | Library | Preview and feedback |
| --- | --- | --- |
| Captures exist | Thumbnail rows with date and time | Image preview and enabled commands for the selected row |
| No captures | Empty list | “No captures yet” and the configured shortcut hint |
| Search has no matches | Empty filtered list | “No matching captures” and a prompt to change or clear the search |
| Folder unavailable | Empty list | “Folder unavailable”, a recovery hint, and an error status |
| Invalid image | Row remains selectable | Preview error text; open, reveal, and delete remain available |
| Action succeeds | Selection remains | Success status in an InfoBar |
| Action fails | Selection remains | Error status with the operation and next action where known |

## Responsive behavior

- At widths below 1000 px, use the compact 216 px library column.
- At widths below the normal command-bar capacity, let native overflow move secondary image commands into the menu.
- Keep the preview centered and use `Stretch="Uniform"`; never crop the screenshot to fit.

## Acceptance criteria

- The selected path remains selected after search, sorting, and refresh when it remains visible.
- Search, sort, refresh, list selection, preview commands, and status feedback are keyboard accessible.
- Light and dark themes use readable native theme colors without hard-coded theme-specific values.
- Empty, no-match, unavailable-folder, and invalid-image states are distinguishable without relying on color alone.
- Icon-only refresh has an automation name and tooltip; every preview action has a label.
