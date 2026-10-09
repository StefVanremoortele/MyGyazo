# Project destinations and screenshot handoff plan

## Purpose

Let the user configure a short list of current projects once, then send a selected screenshot to the right project folder from the library. Keep project files local and under the user's control, and make the destination clear before copying.

This extends the vision's planned folder and repository destinations. It does not add hosted links or automatic uploads.

## Project profile

Each configured project has:

- **Name** — a short label shown in the destination picker.
- **Project folder** — an existing absolute path selected with a folder picker.
- **Screenshot folder** — a relative path inside the project, such as `docs/screenshots` or `assets/captures`.
- **Default project** — an optional profile used by the main Send action for one-step handoff.
- **Project context** — optional notes about the purpose, stack, or constraints. Keep this local. It is reserved for a future, explicit LLM handoff and is not sent anywhere by project copy.

The profile is a routing preference, not a project integration. It does not require a Git repository and does not edit project documents or source files.

## Configure projects

Add a **Projects** manager under Settings. Show each project's name and resolved screenshot destination. Let the user add, edit, set or clear the default, and remove a profile.

The add/edit form contains the profile fields above and previews the resolved destination. Let the user type a relative screenshot-folder path or use **Browse** to choose an existing subfolder under the project root. Typing also supports a folder that should be created on first send. Reject a Browse selection outside the root.

Removing a profile only removes it from My Gyazo. It does not delete the project folder or screenshots already sent there.

## Send a screenshot

1. Select a capture in the library. A previously annotated copy works like any other PNG.
2. Choose **Send to project** beside the preview commands.
3. Send directly to the default project, or choose another profile from a menu showing its name and screenshot folder.
4. Copy the PNG into the configured destination. Preserve the library source and never overwrite an existing file; choose a unique destination filename on collision.
5. Show confirmation with **Open folder** and **Copy Markdown reference** actions. The reference is relative to the project folder, for example `![Screenshot](docs/screenshots/Capture-....png)`.

Keep the existing **Copy** command and clipboard contents unchanged during project copy. This lets the user separately paste the image into a chat or other application.

## Path and file safety

- Require a valid project root and a relative screenshot folder. Normalize separators and reject paths that escape the project root, including `..` traversal.
- Allow the screenshot folder to be created on send when it is inside the configured project root.
- If the root is missing or inaccessible, mark the profile unavailable and offer **Relink** from the manager. Do not silently fall back to another folder.
- Write via a temporary file followed by a final move, matching the existing capture-write behavior.
- Report the project name and destination after a successful send. On failure, keep the source capture and provide the next useful action.
- Saving or editing a profile changes only My Gyazo settings. No repository file is modified automatically.

## Optional project context and LLM handoff

The profile's context field can store a short reusable project brief, such as “WinUI 3 desktop app; UI lives in `src/MyGyazo.WinUI`; prefer native WinUI controls.” It remains inert in the first project-copy flow. A later **Copy for LLM** action can explicitly combine the selected image with this context and any per-image note. My Gyazo should not upload the image or context itself.

## Implementation sequence

1. **Define the profile and settings shape.** Add a project-destination model with a stable ID, name, root, relative screenshot folder, default marker, and optional context. Extend the existing local settings JSON so older settings files with no project list continue to load.
2. **Build the Settings manager.** Add, edit, remove, and set-default interactions using native WinUI controls. Show validation and unavailable-root states; removing a profile never removes project files.
3. **Add the viewer destination action.** List configured projects with their resolved folders. Use the default for the direct action and keep the other profiles available from the picker.
4. **Implement safe copy and feedback.** Create the target directory when needed, copy without overwriting, generate the relative Markdown reference, and show **Open folder** and **Copy Markdown reference** actions.
5. **Document the screen behavior.** Update `docs/screens/capture-library.md` and `docs/screens/settings.md` when implementation starts. Update `docs/architecture.md` only if project file transfer introduces a lasting layer or responsibility not represented there.
6. **Defer active-project detection.** Keep v1 explicit and predictable. Detecting the foreground IDE or inferring a project can be considered after configured destinations prove useful.

## Acceptance criteria

- Multiple named project profiles can be configured, edited, selected as default, and removed.
- The destination picker clearly shows where each project will receive the screenshot.
- Sending copies the selected PNG to the configured folder, leaves the library source unchanged, and never overwrites a prior file.
- The project folder can be moved and relinked without losing its relative screenshot-folder setting or context.
- A missing project root or a copy failure produces a recoverable message and does not affect the source capture.
- The generated Markdown reference points to the copied file relative to the project root.
- Profile data stays in My Gyazo's local settings. Project context is only used by a later explicit LLM handoff action.
- The manager and send flow follow the WinUI design system, remain keyboard accessible, and work in both system themes.
