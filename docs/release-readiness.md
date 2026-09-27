# Release readiness

This local candidate targets Schedule I **0.4.7f6**, Mono and IL2CPP, with MelonLoader 0.7.3. It includes a modified S1API 3.2.1-beta.5 hosting preview; stock beta.5 lacks the new hosting contract. No public release or issue closure is implied by this checklist.

## Player workflows

- Reports provides daily receipts, seven-day summaries, coverage labels, and text exports. History belongs to the save and records host-observed bank activity only. Transfers are not classified as profit.
- Deliveries supports active orders and confirmed repeats of previous orders through the native service. An unfinished phone cart blocks a repeat. Dealers adds stock, held cash, and confirmed customer assignment/removal.
- Custom app icons use a C# sprite provider or bounded Lua `icon_pixels` artwork. The C# sample demonstrates asset ownership; Lua replacement disposes its generated sprite and texture.
- Lua content reapplies the computer's UI layer after refresh, including error output, so rows remain visible after opening.
- Driver API v1 and System Monitor provide background services, events, owned-app cleanup, start/stop/restart, and visible fault state. The session-clock example exercises a live shell customization; see the [driver contract](driver-platform.md).

## Open issue scope

| Issue | Local progress | Remaining work |
| --- | --- | --- |
| [#22 Manual app icons](https://github.com/ifBars/ScheduleOne-UsableComputer/issues/22) | C# provider example/documentation and custom Lua artwork implemented. | Review the changes and publish when authorized. |
| [#12 Native phone apps](https://github.com/ifBars/ScheduleOne-UsableComputer/issues/12) | Dealers and Deliveries implemented and exercised in disposable saves. | Multiplayer validation. |
| [#9 Product Manager](https://github.com/ifBars/ScheduleOne-UsableComputer/issues/9) | Search, state/type filters, numeric value sorting, adaptive tile density, selection highlighting, action feedback, and theme-aware colours implemented. Both runtimes exercised in light/dark themes, normal/maximized windows, and save reloads. | Review the changes and publish when authorized. |
| [#11 App Studio](https://github.com/ifBars/ScheduleOne-UsableComputer/issues/11) | Wrapped-line gutter alignment, viewport clipping, draft switching, source-line diagnostics, focus-safe navigation, keyboard shortcuts, and actual Save & run implemented. Both runtimes passed long-source, theme, resize, template, and file/rerun checks. | Physical shortcut input pass, review, and publication. Syntax colouring deliberately deferred. |
| [#6 Lua platform](https://github.com/ifBars/ScheduleOne-UsableComputer/issues/6) | API v1 compatibility checks, documented threat model, quota-bound per-app host-save storage, checkbox callbacks, visible action errors, and a bundled Shift Checklist template. | Additional controls and permissioned game mutations remain future scope; client storage requires synchronization. See the [contract](lua-api-v1.md). |
| [#3 Native TV apps](https://github.com/ifBars/ScheduleOne-UsableComputer/issues/3) | [Noodle](noodle.md) and [Egg Run](egg-run.md) have independent native-art playfields. Both runtimes passed gameplay, focus pause, source isolation, disposal, and process reload. Egg Run additionally verifies spawns, collisions, restart, and authorized native high-score writes. Physical CRT screenshots were reviewed. | Manual keyboard/audio feel and simultaneous placed-TV play are additional coverage, not automated claims. Pong is not advertised: no supported prefab was found. |
| [#13 Automatic S1API bridge](https://github.com/ifBars/ScheduleOne-UsableComputer/issues/13) | S1API-owned independent-host contract and desktop bridge implemented. Both runtimes passed late phone/TV registration, removal, opt-out, collision recovery, scene suspension, dynamic UI layers, source-state preservation, and failed-session cleanup. | Requires the bundled hosting preview. Existing device-bound apps need the S1API contract; arbitrary original phone/TV canvases are not reparented. |

## Validation gates

Computer power controls now separate leaving the desk from shutdown. Restart runs the short boot flow; `Run-VfsSmoke.ps1 -Power` covers app closure, power-on, restart, and leaving during boot. See [computer power](computer-power.md) for the current session-only power state and shared-kernel behavior.

Run both `Mono` and `Il2cpp` builds, the focused verifiers under `tests/`, and disposable-save tests for changed gameplay. `Run-VfsSmoke.ps1 -Reports` covers daily/summary exports, both themes, and process reload. `-AppIcons` covers rendering, caching, replacement, and disposal; `-Deliveries` covers confirmed native reorders. See [development](development.md) for commands and evidence requirements.

Runtime packages contain UsableComputer, the matching S1API hosting preview, three companion libraries, installation instructions, licenses, and per-file hashes. Separate source archives capture both repositories, including the unpublished hosting API. Package validation checks missing dependencies, corruption, extra files, duplicate/traversal entries, and runtime mismatch. See [packaging](packaging.md) for repeatable commands. Test the archived contents after building a fresh candidate; older candidate checks do not establish readiness of new code.

Before a release, validate host/client behavior separately and finish or explicitly scope out remaining issue work. Run Doom and nested-game checks from the packages as well; their full workflows are not proved by installing companion DLLs. Keep proprietary game assets, saves, generated wrappers, and raw smoke evidence out of packages and Git. No release has been published by these packaging checks.

The S1API preview's full contract suites passed 729 Mono and 715 IL2CPP tests. Its public changes are additive; existing source/binary signatures, save identities, and network payloads are retained. No separate Microsoft ApiCompat run is claimed.

Checkbox and template validation passed both runtimes through initial load and process reload: boolean callbacks, no mutation during rendering, saved state, invalid-value and callback-error isolation, and App Studio template discovery/rendering. Light/dark CRT screenshots were reviewed. The focused Lua verifier passed six checks; storage and example behavior passed 28 assertions.
