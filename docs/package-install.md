# Install Usable Computer

These are local development packages unless they came from a published release. Check `manifest.json` for the runtime, assembly version, source revision, and whether the source had local changes.

1. Close the game. Install MelonLoader separately (validation uses 0.7.3). This candidate includes a matching **S1API hosting preview**, based on 3.2.1-beta.5 with the new external-app lifecycle. Stock 3.2.1-beta.5 does not include that lifecycle.
2. This candidate targets **Schedule I 0.4.7f6**. Choose **Mono** for `alternate-beta` or **Il2cpp** for `beta`, using the runtime your installed game actually runs. The public 0.4.6 build is not the target of these packages.
3. Back up your existing S1API DLL, then copy the package's `Mods` and `UserLibs` folders into your game directory, merging the folders. Keep exactly one S1API DLL and one UsableComputer DLL for your runtime; remove older duplicates with other filenames. Keep unrelated mods and libraries.
4. Start the game. Buy a Usable Computer from a hardware shop, place it, and interact with it.

Install only one UsableComputer runtime DLL. Keep all three companion libraries in `UserLibs`: ManagedDoom.Core, MoonSharp.Interpreter, and UsableComputer.ChildHost. The included S1API build is a local preview, not a new official S1API release. Players installing a binary package do not need the .NET SDK; building from source does.

Back up your save before testing a development build. Updates should preserve `UserData/UsableComputer`, which contains local apps and settings, and the game's save folders. Doom requires a separately supplied compatible IWAD; no game data is bundled. The nested Schedule I app is Windows-only and launches a separate game process/profile.

Matching UsableComputer and S1API source archives and `SHA256SUMS.txt` accompany the runtime packages. They include buildable source, vendored Managed Doom source, licenses, and local-path configuration examples. Supply your own installed-game references to build them. See `THIRD_PARTY_NOTICES.md`, `LICENSE.txt`, and `S1API-LICENSE.txt` for notices and licenses.
