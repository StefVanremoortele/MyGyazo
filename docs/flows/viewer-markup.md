# Viewer markup implementation plan

## Purpose

Let a user add simple annotations to a saved capture from the library preview. Keep the image central, the workflow local, and the tools limited to the rectangle and text markup already described in the product vision.

## Proposed user flow

1. Select a capture in the library.
2. Choose **Markup** beside the preview commands.
3. Draw rectangles by dragging, or choose **Text** and place text on the image.
4. Use **Undo** to remove the latest mark.
5. Choose **Save copy** to create a new annotated PNG, or **Cancel** to leave the source unchanged.
6. After saving, select the new capture in the library and show success or an actionable error in the existing status bar.

The original capture remains untouched. The edited result is saved in the configured capture folder using the existing capture filename and refresh behavior.

## First release scope

- Rectangle markup and placed text.
- One consistent, high-contrast default annotation style; defer style customization until the basic interaction is clear.
- Undo the latest mark, cancel the edit session, and save an annotated copy.
- No crop, blur, arrows, freehand drawing, or multi-image editing.
- Keep capture-time annotation as a follow-on use of the same editor, rather than creating a separate capture editor in this work.

## Implementation responsibilities

- **WinUI editor:** Add a labeled **Markup** action to the preview command bar in `src/MyGyazo.WinUI/Views/Shell.xaml`. Add an editing state and toolbar for Rectangle, Text, Undo, Cancel, and Save copy. Keep the overlay aligned with the uniformly scaled preview.
- **Window coordination:** In `src/MyGyazo.WinUI/MainWindow.cs`, track the selected source and edit state, validate that a preview is available, handle cancel/save, refresh the library, and select the saved copy.
- **Markup data and geometry:** Add platform-neutral rectangle/text annotation types and coordinate helpers in `src/MyGyazo.Core/`. Store annotation positions in source-image pixel coordinates so they remain stable when the preview is resized.
- **PNG export:** Use the Windows-specific layer in `src/MyGyazo.Desktop/` to reopen the original image at full resolution, composite the annotations, and save a new PNG through a temporary file followed by a final move. Do not export from the reduced-size preview image.
- **Documentation:** Extend `docs/screens/capture-library.md` with markup mode, actions, states, and keyboard behavior. The vision already describes rectangle and text annotation; update it only if the product scope changes. Update `docs/flows/capture.md` when capture-time integration is added.

## Interaction and state rules

- Markup is unavailable when there is no selected capture or the image cannot be loaded.
- While editing, the selected source stays fixed; cancel discards the draft annotations without changing any file.
- Escape cancels the editing session. Ctrl+Z undoes the latest annotation.
- Save errors leave the user in the editor with the source and draft annotations intact.
- Every tool has a visible label and keyboard focus. Keep status feedback in the existing InfoBar and preserve light/dark theme behavior.
- The viewer remains a flat canvas with image commands next to the preview; do not add a nested editor card.

## Acceptance criteria

- A user can draw a rectangle and place text on a selected capture from the preview.
- Marks stay attached to the same image pixels when the window size changes or the preview uses `Stretch="Uniform"`.
- The saved copy has the original image dimensions and retains the source capture unchanged.
- Cancel creates no output file. A save failure keeps the draft available for retry.
- The saved copy appears in the library and becomes the selected preview.
- Empty selection, invalid image, success, and save-error states remain understandable with keyboard and screen-reader labels, in both system themes.
