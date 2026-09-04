# Coding standards

These standards cover the C#, Unity, and test code in Usable Computer. Project workflow and repository boundaries are in [AGENTS.md](AGENTS.md).

## C# style

- Use four spaces, file-scoped namespaces, nullable annotations, and implicit usings.
- Use `PascalCase` for types and methods, `camelCase` for locals, and descriptive constant names.
- Keep methods focused. Extract stateful behavior when a method mixes lifecycle, rendering, and persistence concerns.
- Prefer explicit types when they clarify a game or Unity boundary. Use `var` when the assigned type is obvious.
- Add comments for intent, constraints, or unusual game behavior. Do not narrate straightforward code.
- Use `nameof` for member names and stable constants for IDs shared across components.

## Runtime compatibility

- Keep shared behavior outside preprocessor blocks.
- Put Mono and IL2CPP aliases near the top of the file under `MONOMELON` and `IL2CPPMELON`.
- Do not expose runtime-specific Schedule I, TMPro, or Il2Cpp types through the public app API.
- Treat both configurations as supported products. A compile success in one runtime does not prove the other runtime compiles.

## Unity and game integration

- Prefer `[SerializeField] private` fields for Unity-owned references.
- Resolve native objects once during setup. Avoid hierarchy scans and `FindObjectOfType` calls in update loops.
- Pair every event registration, input listener, camera override, texture, process, and generated object with deterministic cleanup.
- Keep runtime-generated models free of inherited colliders and behaviors unless the feature requires them.
- Use the game's normal interaction and state systems. Restore camera, input, and UI state on every exit path.
- Log through MelonLoader with the mod name. Include enough context to diagnose a failure without logging personal data.

## UI standards

- Preserve the XP-inspired visual language and the 800x536 desktop coordinate space unless a deliberate migration is documented.
- Fit controls within the physical CRT screen at the production interaction camera and field of view.
- Use the existing UI factory, colors, icon pipeline, window manager, and listener registry before creating new primitives.
- Use supported text glyphs or generated/native icons. Missing-glyph boxes are release blockers.
- Test long labels, empty states, task-pane spacing, window resizing, and both light and dark themes when affected.
- A UI screenshot must show the actual physical computer in the game world, not a camera-mounted replacement canvas.

## Data, processes, and assets

- Keep persisted formats backward-compatible. Validate untrusted or player-authored data before use.
- Keep Lua access inside the documented sandbox and execution budget.
- Keep nested Schedule I in its isolated profile and loader directories. Dispose its process and shared-memory handles reliably.
- Load game sprites and models from the installed game at runtime. Do not add proprietary assets or generated wrappers to Git.

## Verification

- Add or update a focused verifier for non-trivial logic.
- Keep live tests deterministic, bounded, and isolated from the player's active save and installed mods.
- After an interrupted live test, terminate only the process started by that test and restore its exact mod directory backup.
- For camera or display changes, assert viewport fit and inspect the captured image for clipping or camera intersection.
- Run `git diff --check` before committing.

Do not describe a test as passed unless its command completed successfully and its evidence matches the final code.
