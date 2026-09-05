# Development

[Back to the README](../README.md)

Run commands from the repository root.

The focused verifiers cover the desktop layout and camera framing, calculator model, app registry, virtual filesystem, Lua host, and Doom adapter without starting Unity:

```powershell
dotnet run --project tests/UsableComputer.IconLayoutVerifier/UsableComputer.IconLayoutVerifier.csproj -c Release
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

Open work is tracked in [GitHub Issues](https://github.com/ifBars/ScheduleOne-UsableComputer/issues).

## Display size and rendering cost

The runtime model is 15% larger than the laundering-station donor, while its 4x2 placement footprint and collision setup stay unchanged. Interaction centers the view on the physical screen. It uses a 55-degree field of view at 16:9 and wider aspect ratios, and widens the view for narrower windows so the screen remains visible. Resizing while the computer is open updates the view automatically.

Nested Schedule I renders at 960x540, about 2.0 MiB per uncompressed RGBA frame. The shared-memory bridge supports frames up to 1280x720.

## Additional checks and persistence

Add `-IconLayout` to either `Run-VfsSmoke.ps1` command above to check icon sizes, ordering, both themes, desktop overflow, the Start menu, and camera framing at five resolutions. Keep Mono and IL2CPP results separate.

Notes and appearance preferences use MelonPreferences and are shared across computers and saves. Theme and wallpaper are stored independently. Icon ordering is maintained as apps and folders change; stable node IDs break ties between equal names.

The virtual filesystem belongs to the active Schedule I save. Virtual paths never map to arbitrary host files. Unavailable mod-app shortcuts remain in place so they recover when the app is installed again.

Journal and Product Manager use narrow adapters for native game state. They do not reparent or drive the phone canvas.

The computer model and UI are created at runtime from the game's laundering-station table and old computer. No prefab bundle or copy of the game's assets is embedded.
