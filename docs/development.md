# Development

[Back to the README](../README.md)

Run commands from the repository root.

The focused verifiers cover the desktop layout and camera framing, calculator model, app registry, virtual filesystem, Lua host, and Doom adapter without starting Unity:

```powershell
dotnet run --project tests/UsableComputer.IconLayoutVerifier/UsableComputer.IconLayoutVerifier.csproj -c Release
dotnet run --project tests/UsableComputer.ReportsVerifier/UsableComputer.ReportsVerifier.csproj -c Release
dotnet run --project tests\UsableComputer.CalculatorModelVerifier\UsableComputer.CalculatorModelVerifier.csproj -c Release
dotnet run --project tests\UsableComputer.RegistryVerifier\UsableComputer.RegistryVerifier.csproj -c Release
dotnet run --project tests\UsableComputer.FileSystemVerifier\UsableComputer.FileSystemVerifier.csproj -c Release
dotnet run --project tests\UsableComputer.LuaVerifier\UsableComputer.LuaVerifier.csproj -c Release
dotnet run --project tests\UsableComputer.DoomVerifier\UsableComputer.DoomVerifier.csproj -c Release -- C:\path\to\an\iwad.wad
```

Camera and display changes have a Mono smoke test that isolates the target install's mod directory, loads a disposable save copy, and captures the physical computer:

```powershell
.\tests\Run-DisplaySmoke.ps1 -GamePath C:\path\to\Schedule-I -SourceSavePath C:\path\to\SaveGame
```

The VFS smoke runner builds an isolated test mod, copies a completed save into a temporary fixture, proves persistence across two game processes, captures the Files window, and restores the target install afterward:

```powershell
.\tests\Run-VfsSmoke.ps1 -Runtime Mono -GamePath C:\path\to\ScheduleI -SourceSavePath C:\path\to\SaveGame_1
.\tests\Run-VfsSmoke.ps1 -Runtime Il2cpp -GamePath C:\path\to\ScheduleI -SourceSavePath C:\path\to\SaveGame_1
```

Pure verifier passes do not replace an in-game test. Test the matching build in a backed-up or disposable save before release.

Add `-Reports` to `Run-VfsSmoke.ps1` to create native bank transactions, check daily receipts across process reload, switch to seven-day summaries, export both reports without overwriting earlier files, and capture the Reports app in both themes. The runner backs up and restores appearance preferences for this mode.

Add `-AppIcons` to validate a Lua pixel icon through the real app registry and desktop UI. The probe checks sprite dimensions, caching, hot replacement, destruction of owned sprites and textures, and the UI layer of periodically refreshed Lua content. It captures the custom app on the in-world CRT and removes its temporary registration. Run the Lua verifier for malformed artwork and sandbox coverage.

Add `-Deliveries` to exercise the native delivery workflow in the disposable save: receipt creation, existing-cart refusal, confirmation/cancellation, one charge at the quoted price, matching items/destination/dock, repeated-input rejection, persistence, and both themes. It can be combined with `-Reports`.

Add `-Dealers` with a completed save containing a recruited dealer with spare capacity and an unassigned unlocked customer. The disposable-save probe checks assignment confirmation/cancellation, native assignment, persistence across process reload, removal, and unchanged native phone selection/hierarchy. It captures the customer and stock views. Customer assignment uses the native dealer network calls and expires an existing offer as the phone does; it does not remotely collect cash or alter inventory.

Add `-Drivers` to exercise System Monitor, background ticks, service dispatch, the taskbar-clock hook, stop/restart, driver-owned app cleanup, and fault isolation. The pure kernel verifier covers rollback, revocation, restart, cleanup failures, and thread constraints. See [driver development](driver-platform.md) for the public contract and buildable example.

Bank reports use a separate `UsableComputerReportsSave` saveable. The native bank ledger is session-only and has no timestamps; a narrow adapter observes newly appended entries and stamps them with game time. A save-start flush captures pending entries. Tracking belongs to the loaded-game lifecycle, not to an open desktop window. Only the host records persisted history. Reports do not infer sales, wages, or profit from balance changes.

Deliveries reads `DeliveryManager` records and delegates confirmed repeats to the native `DeliveryApp.Reorder` flow. It does not send custom purchase RPCs or move the phone canvas. The adapter additionally checks the original destination, item availability, vehicle capacity, current quote, and an empty native cart before submission. Cached native listing references belong to one desktop app session.

Open work is tracked in [GitHub Issues](https://github.com/ifBars/ScheduleOne-UsableComputer/issues). The [release readiness checklist](release-readiness.md) distinguishes local progress from remaining validation and packaging work.

## Additional runtime scenarios

Use `-EggRun` for native scenery, collision/restart, focus pause, high scores, source isolation, and disposal; use `-Bridge` for automatic phone/TV registrations, opt-out, collision recovery, session cleanup, and scene suspension. Both switches run on initial load and process reload.

`-DoomRuntime -DoomIwadPath C:\path\to\freedoom2.wad` temporarily stages a free IWAD, verifies a visible Doom frame, and checks texture cleanup. The runner restores the previous file. `-NestedRuntime` starts a separate game process, requires a visible streamed frame, then verifies Stop terminates that process. It backs up and restores the nested loader/profile directory. These prove startup/rendering/cleanup, not a complete playthrough or physical input. Use `-PackagePath` to exercise actual archived DLLs, including the bundled S1API preview.

## Display size and rendering cost

The runtime model is 15% larger than the laundering-station donor, while its 4x2 placement footprint and collision setup stay unchanged. Interaction centers the view on the physical screen. It uses a 55-degree field of view at 16:9 and wider aspect ratios, and widens the view for narrower windows so the screen remains visible. Resizing while the computer is open updates the view automatically.

The laptop canvas follows the native screen submesh's size and incline, with a 1 mm inset at each edge. Its camera faces the front of the lid, and mesh bounds place the base on the tabletop. These anchors belong to the model, so the shared desktop/controller needs no laptop-specific layout branch.

`Run-VfsSmoke.ps1 -Laptop` checks the laptop on the selected runtime, including the screen plane, all four edges, camera direction, tabletop contact, and actual 1920x1080, 1024x768, and 1600x675 resolutions. It captures front, oblique, and inactive-screen views and checks both generated shop thumbnails. Run Mono and IL2CPP separately against matching beta installs.

The 0.4.7f6 game renamed the icon-rendering layer to `RuntimePreviewGeneration`. `ComputerIconCompatibility` temporarily selects that layer only for previews containing this mod's screen anchor, restoring the layers after capture. This keeps S1API-generated thumbnails working without changing gameplay model layers or other mods' previews.

Nested Schedule I renders at 960x540, about 2.0 MiB per uncompressed RGBA frame. The shared-memory bridge supports frames up to 1280x720.

## Additional checks and persistence

`Run-VfsSmoke.ps1 -Studio` covers the [App Studio editor](app-studio.md), including long/wrapped source, error navigation, draft switching, and save/rerun with temporary scripts. `dotnet run --project tests/UsableComputer.StudioVerifier` checks diagnostic parsing and draft ownership without starting the game.

`Run-VfsSmoke.ps1 -Products` exercises native product search, type/listed filters, value sorting, favourite changes with restoration, compact/large grids, and normal/maximized windows. Seed uses the light theme and reload uses dark. The standalone `dotnet run --project tests/UsableComputer.ProductCatalogueVerifier` covers combined filters, stable tie ordering, numeric sorting, and a 1,000-product catalogue without loading Unity.

Add `-IconLayout` to either `Run-VfsSmoke.ps1` command above to check icon sizes, ordering, both themes, desktop overflow, the Start menu, and camera framing at five resolutions. Keep Mono and IL2CPP results separate.

Notes and appearance preferences use MelonPreferences and are shared across computers and saves. Theme and wallpaper are stored independently. Icon ordering is maintained as apps and folders change; stable node IDs break ties between equal names.

The virtual filesystem belongs to the active Schedule I save. Virtual paths never map to arbitrary host files. Unavailable mod-app shortcuts remain in place so they recover when the app is installed again.

Journal and Product Manager use narrow adapters for native game state. They do not reparent or drive the phone canvas.

The computer models and UI are created at runtime. The desktop uses the game's laundering-station table and old computer; the laptop uses that table and the special-customer laptop found in the loaded Main scene before save data loads. No prefab bundle or copy of the game's assets is embedded. The registered IDs are `usable_computer` and `usable_laptop`; the original ID remains compatible with existing saves.
