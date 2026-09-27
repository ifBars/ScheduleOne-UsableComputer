# Native app hosting investigation

This tracks work for issues [#3](https://github.com/ifBars/ScheduleOne-UsableComputer/issues/3) and [#13](https://github.com/ifBars/ScheduleOne-UsableComputer/issues/13). Native TV adapters are separate from the [S1API external app bridge](s1api-external-hosting.md); each has its own lifecycle and validation.

## Native TV lifecycle

The installed Mono game's TV namespace contains Pong, Snake, and RunnerGame. The loaded prefab registry exposes only **Egg Run** (`RunnerGame`) and **Noodle** (`Snake`); the existence of the Pong class does not establish availability. `TVHomeScreen.Apps` supplies native names and sprites. `TVApp.Canvas` and `CanvasGroup` identify the visual root; `PreviousScreen`, `State`, and `PauseScreen` participate in navigation. Both runtime smoke builds access those members directly through their respective aliases.

Opening a native TV app pushes its game UI state and subscribes to minute updates. Closing pops the state and can reopen `PreviousScreen`. Opening the containing TV interface also overrides the player's camera and adds a player to the TV. These operations cannot be forwarded unchanged from a desktop session.

| App | Hosting dependencies to resolve |
| --- | --- |
| Pong | Rigidbody ball and paddle behaviors, fixed updates, pointer conversion using the player camera, pause/resume velocity, score and win events. |
| Snake | Tile references, direction input, timed movement, pause state, start/eat/game-over/win events. |
| RunnerGame | Character animation, scrolling ground, cloud/obstacle spawners, input, collision callbacks, save-backed high score, pause state. |

Egg Run's supporting components have their own frame updates: spawners use game time and a spawn-rate multiplier, movers advance their rectangles, and the ground and character animation maintain independent timers. The [desktop adapter](egg-run.md) gates that entire owned group on focus and visibility. It disables native colliders and checks their geometry within its own playfield, preventing cross-device trigger effects. The native TV game root stays inactive and is destroyed before the cloned visuals are activated. High scores use the native variable only with local write authority. Both runtimes have passed movement/spawn, focus, collision/restart, high-score, source-isolation, and disposal checks.

Do not move an existing TV's canvas into the desktop. Prefer a separately owned runtime instance for each supported native app, with explicit handling of state, input, pause, events, and destruction. Resolve dependencies before activating a cloned hierarchy: native `Awake` can register global listeners and initialize game state. Borrowed sprites remain game-owned. No extracted assets or decompiled implementations belong in this repository.

The disposable fixture has no placed TV app instances in either runtime. Discovery must therefore also examine loaded prefab assets, rather than requiring ownership of a TV. `Run-VfsSmoke.ps1 -TvInventory` writes `tv-inventory.txt` outside the repository with asset versus scene status, content roots, native icon names, and attached game components. This is an inventory gate only. It does not prove keyboard input, desktop rendering, gameplay, or cleanup of an instantiated adapter.

Run the probe with the same runtime, installed-game path, and source-save arguments as the [other disposable-save checks](development.md). It checks both the initial load and a process reload. It does not activate native TV objects or invoke their navigation methods.

## S1API bridge lifecycle

The configured S1API hosting preview adds a public `ExternalAppCatalog` and an opt-in `IExternalAppHost` contract. Usable Computer mirrors eligible phone and TV registrations into its desktop registry and creates a fresh, independently owned `IExternalAppSession` for each open window. S1API's original phone and TV canvases remain on their devices. The desktop bridge never calls phone `OpenApp`/`CloseApp` or TV `Open`/`Close`.

The catalog announces late registration, replacement, removal, and scene reset. Its identity includes family, declaring assembly/type, and the app name. The bridge preserves existing desktop registrations on collision and logs the conflicting ID. Apps without an independent host contract are omitted with an actionable diagnostic. Apps that already have a native or manual desktop adapter should return false from `AllowExternalHosting` to avoid duplication.

This is an additive S1API API change in the local hosting preview; stock `3.2.1-beta.5` does not expose it. The packaged Usable Computer build must travel with its matching hosting-preview S1API runtime for the same Mono or IL2CPP backend. An arbitrary pre-existing phone or TV app cannot be safely hosted merely by reparenting its UI or forwarding device navigation. It remains on its original device until its author implements the S1API-owned independent host contract or a dedicated adapter is written.

## Remaining proof

1. Keep the native prefab availability probe passing in both runtimes; do not infer supported apps from assembly class names.
2. Source-instance isolation passes; simultaneous manual interaction with a placed TV remains additional coverage.
3. Noodle and Egg Run automated rendering/gameplay/lifecycle checks pass in both runtimes. Physical keyboard feel and audible sound are not established by programmatic input or sound-object disposal assertions.
4. The bridge passes disposable-save tests in both runtimes for compatible sample subclasses, late registration/removal, collisions, opt-out, dynamic UI layers, scene suspension, and failed-session cleanup. Other mods' session implementations still require their own testing.
