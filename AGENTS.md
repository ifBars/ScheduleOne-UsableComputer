# Usable Computer contributor guide

## Project goals

Usable Computer adds a placeable, working computer to Schedule I. Keep the furniture believable in the game world, the XP-inspired desktop readable, and the app API small enough for other mods to use safely.

The mod builds its model and UI at runtime. Do not commit Schedule I assets, assemblies, decompiled source, generated wrappers, saves, logs, or disposable test output.

## Working agreements

- Read [CODING_STANDARDS.md](CODING_STANDARDS.md) before changing C# or Unity UI code.
- Preserve the 4x2 placement footprint, interaction cleanup, and save compatibility unless an issue explicitly changes them.
- Keep Mono and IL2CPP behavior aligned. Isolate runtime-specific types behind the existing compile-time aliases.
- Reuse native game art at runtime. Do not copy phone canvases or bundle extracted game assets.
- Treat `local.build.props` as machine-local configuration. Never commit it.
- Keep changes narrow and preserve unrelated work in the checkout.

## Source layout

`src/` is organized like the computer it builds. Each folder is a namespace under `UsableComputer`.

| Folder | Owns |
| --- | --- |
| `Boot/` | The MelonLoader entry point and the order services start and stop. |
| `Hardware/` | The placeable computer: its runtime model, shop listing, interaction, camera, and display profile. |
| `Kernel/` | The driver runtime and the app registry store. |
| `FileSystem/` | The virtual disk, Notes documents, and the disk's saveable. |
| `Shell/` | The desktop, icons, taskbar, Start menu, windows, power states, and shell preferences. |
| `UI/` | Shared widgets, listener cleanup, pointer helpers, and generated icons. |
| `Apps/` | Built-in app registration and one folder per app. `Apps/Games/` holds Doom, Egg Run, Noodle, and nested Schedule I. |
| `Subsystems/` | Hosts for apps written for another platform: `Lua/` and `S1Api/`. |
| `Native/` | Schedule I adapters and snapshots that more than one feature uses. |
| `API/` | The public contract for other mods. |

- Keep a type that one feature uses in that feature's folder. An app folder holds its window, view models, native adapter, background service, and saveable. Move a type to `Native/`, `UI/`, `Shell/`, or `Kernel/` when a second feature needs it.
- Code outside `Apps/` and `Boot/` must not reference a specific app type. Open apps by ID, and add optional window behavior through the capabilities in `API/DesktopSessionCapabilities.cs`.
- Moving code must not change persisted identifiers: saveable class names and field keys (S1API names save folders after the class name), app IDs, preference keys, and the embedded template name.
- When a type moves, update `tests/Shared/ModTypeNames.cs` for the smoke mods and the linked paths in verifier projects.

## Common workflow

1. Reproduce or measure the current behavior before editing.
2. Build the narrowest relevant verifier while iterating.
3. Build both runtime configurations unless the issue explicitly narrows validation:

   ```powershell
   dotnet build UsableComputer.csproj -c Mono
   dotnet build UsableComputer.csproj -c Il2cpp
   ```

4. Run the relevant project under `tests/`.
5. Validate gameplay changes in a disposable or backed-up save.
6. For UI changes, inspect a screenshot of the actual in-world computer. Check the bezel, screen edges, HUD, and surrounding scene.
7. Keep raw screenshots and smoke evidence outside the repository unless a curated documentation image is part of the change.

Pull requests should link the issue, explain the user-visible result and trade-offs, list exact validation, and include screenshots for UI work.

For GitHub screenshot attachments, prefer native `gh pr create/edit --attach <image>` (or the corresponding issue/comment command). Check the resolved CLI's version and help for support before considering browser uploads. Keep raw captures outside Git and verify the published attachment URLs. Browser/computer upload is a last resort after supported CLI/API options are unavailable.
