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
