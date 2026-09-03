# Usable Computer

`UsableComputer` is a public-safe Schedule One mod that registers a four-by-two floor furniture item named **Usable Computer**. Its model is assembled at runtime from the native laundering-station table and computer branches:

- `plastictable_2x1`
- `oldcomputer`

The laundering-station donor root is never instantiated, and no proprietary model, texture, prefab, scene, AssetRipper export, or asset bundle is included in this project.
The registration path requires the native donor built root to be named `LaunderingStation_Built` so a changed game asset fails clearly instead of silently cloning the wrong object.

This is an unofficial community mod and is not affiliated with or endorsed by TVGS. A legally installed copy of Schedule I is required; game assemblies and assets are never redistributed by this repository.

## Features

- S1API `FurnitureCreator` registration with a native generated inventory icon.
- Four-by-two grid placement, metal build sound, 750 purchase price, and stack limit one.
- Availability at `Handy Hank's Hardware` and `Dan's Hardware`.
- Native `InteractableObject` prompt: **Use computer**.
- Native `MonoState` lifecycle with the game's exit-listener contract.
- Camera and FOV override aimed at runtime-created camera and screen anchors.
- Original Windows XP/Luna-inspired desktop styling: saturated blue title bars and taskbar, green Start button, light beveled controls, white title text, and a notification clock.
- Compact XP-inspired window chrome with app icons, grouped minimize/maximize/close controls, reversible desktop-area maximization, and icon-only running-app taskbar buttons.
- An original grassy-hill/blue-sky wallpaper generated entirely at runtime from C# pixels and a runtime `Sprite`.
- Registry-driven Notes, Calculator, About, Journal, Product Manager, App Studio, Settings, Doom, and Schedule I apps; Start menu, desktop icons, and taskbar identities use stable app IDs.
- Every built-in and third-party app receives an image icon. Journal and Product Manager reuse live native phone sprites; the remaining built-ins use original XP-inspired pixel icons, and apps without a provider receive a generic window icon.
- Native Journal and Product Manager phone icons are resolved from the live player UI and presented as XP-style desktop/Start-menu icons.
- Product Manager uses the native discovered-product sprites in a desktop grid, with a large preview, product type/value, listed state, and favourite state.
- Notes persist globally through MelonPreferences and are shared by every placed computer.
- Calculator operations are explicit button operations; it does not evaluate arbitrary expressions.
- App Studio includes runnable templates for the sandbox-compatible built-in desktop apps, then validates, saves, and hot-loads declarative Lua dashboard apps from `UserData/UsableComputer/Apps` without restarting the game.
- Display Properties provides persistent light/dark themes and three original runtime-generated backgrounds: Rolling hills, Blue sky, and Twilight.
- Doom runs the platform-neutral Managed Doom engine inside a desktop window with a Unity-rendered 640x400 framebuffer and keyboard/mouse controls.
- On Windows, the Schedule I app launches a real second game process and streams its framebuffer into the in-game desktop.

The wallpaper is an original procedural composition. It does not use Microsoft's Bliss image, a Windows logo, a PNG, GLB, prefab, scene, bundle, AssetRipper export, or any other proprietary game/UI asset. The generated texture and sprite are owned by each shell and destroyed during shell disposal.

The appearance settings are stored in the mod's MelonPreferences category and apply immediately to every active computer shell. Light/dark mode recolors windows, Start/taskbar surfaces, and app content without changing the selected desktop background. Wallpaper selection is an independent setting that regenerates pixels in the shell-owned texture.

## Desktop app API

Third-party mods can add an app without depending on Schedule One types or Mono/IL2CPP-specific TextMeshPro aliases:

```csharp
using UsableComputer.API;
using UnityEngine;

DesktopAppRegistry.Register(new DesktopAppDescriptor(
    id: "my-mod.app",
    title: "My Mod",
    glyph: "M",
    preferredWindowSize: new Vector2(440f, 300f),
    preferredWindowPosition: Vector2.zero,
    createSession: context => new MyDesktopSession(context)));
```

An app can optionally provide a runtime sprite without transferring ownership to Usable Computer:

```csharp
DesktopAppRegistry.Register(new DesktopAppDescriptor(
    id: "my-mod.app",
    title: "My Mod",
    glyph: "M",
    preferredWindowSize: new Vector2(440f, 300f),
    preferredWindowPosition: Vector2.zero,
    createSession: context => new MyDesktopSession(context),
    resolveIcon: () => myPhoneApp.AppIcon));
```

The provider is evaluated when the desktop or Start menu is rebuilt. Return `null` while the phone app is unavailable and the shell uses its owned generic window icon. The shell references a returned provider sprite but never destroys it.

`DesktopAppDescriptor` contains the stable ID, title, short fallback glyph, preferred geometry, a per-window session factory, and the optional icon provider. `DesktopAppContext` exposes a window-owned `Transform`, `RectTransform`, optional event camera, common Unity UI button/input binding, close/typing requests, and cleanup registration. A session receives `OnOpened`, `OnClosed`, `OnTick`, and `Dispose` hooks.

IDs are ordinal and deterministic. Duplicate registration throws `InvalidOperationException` naming the conflicting ID. `DesktopAppRegistry.Changed` is raised after successful register/unregister operations. Existing placed computers subscribe while alive, so an app registered after a shell was constructed appears on that shell; unregistering removes its desktop/Start entries and closes any matching windows before disposing their sessions. Shells unsubscribe from the static event during teardown.

The compileable manual mod is under `examples/ManualDesktopAppSample`. It is intentionally a separate assembly and is not registered or installed by the main mod:

```powershell
dotnet build examples\ManualDesktopAppSample\ManualDesktopAppSample.csproj -c Mono
dotnet build examples\ManualDesktopAppSample\ManualDesktopAppSample.csproj -c Il2cpp
```

## Native Journal and Product Manager scope

The Journal adapter reads the native active quest collection and presents each quest's title, subtitle, description, state, and entry states. Track/untrack calls the native quest tracking method using the quest's stable GUID. The Product Manager adapter reads discovered products, product names/types, market values, product sprites, listed state, and favourite state from the native collections. Listing and favourite toggles call the native Product Manager service when its network singleton is available; if it is not ready, the view remains read-only and reports the limitation.

These adapters are deliberately narrow. They do not open, reparent, clone, or drive the native phone `App<T>` UI, and they do not mutate phone active-app, canvas, home-screen, orientation, or protected UI state. There are no reflection-based guesses. Unsupported service state is reported rather than simulated.

### Reusing an existing PhoneApp mod

An existing S1API phone app should share domain/controller logic, then register an explicit desktop provider. Keep the controller independent of presentation and give each surface its own adapter:

```csharp
public sealed class FeatureController
{
    public string GetSummary() => "Shared feature state";
}

public sealed class FeatureDesktopSession : IDesktopAppSession
{
    private readonly FeatureController _controller;

    public FeatureDesktopSession(DesktopAppContext context)
    {
        _controller = new FeatureController();
        // Build the desktop view under context.Container.
    }

    public void OnOpened() { }
    public void OnClosed() { }
    public void OnTick() { _ = _controller.GetSummary(); }
    public void Dispose() { }
}

DesktopAppRegistry.Register(new DesktopAppDescriptor(
    "my-mod.feature-desktop",
    "Feature",
    "F",
    new Vector2(520f, 340f),
    Vector2.zero,
    context => new FeatureDesktopSession(context),
    resolveIcon: () => featurePhoneApp.AppIcon));
```

The phone provider remains registered through the phone app's normal S1API path. Desktop registration is explicit; Usable Computer does not automatically discover phone apps and does not reflect into `PhoneAppRegistry` or protected `OnCreatedUI` methods.

## Lua App Studio

App Studio is an in-game editor for small desktop dashboards. A file returns metadata plus a `render` function; the function returns rows rather than receiving raw Unity objects:

```lua
return {
  id = "business-dashboard",
  title = "Business Dashboard",
  icon = "studio",

  render = function()
    local money = computer.money()
    local properties = computer.properties()
    return {
      { kind = "heading", text = "Business Dashboard" },
      { kind = "stat", label = "Cash", value = computer.currency(money.cash) },
      { kind = "stat", label = "Bank", value = computer.currency(money.online) },
      { kind = "text", text = "Owned properties: " .. #properties },
    }
  end
}
```

Supported row kinds are `heading`, `text`, `stat`, `spacer`, and `button`. A button may provide an `action` function and the view refreshes after it runs. Saved source is capped at 64 KiB; rendering is capped at 64 rows and each Lua invocation has a 250 ms execution budget.

The initial API is intentionally read-only:

- `computer.money()` returns cash, online balance, and net worth through S1API.
- `computer.player()` returns the local player's name, health, region, and current property through S1API.
- `computer.properties()` returns owned property identity, price, and employee counts through S1API.
- `computer.employees()` currently returns workforce count/capacity grouped by owned property because S1API does not yet expose a stable individual-employee query.
- `computer.products()` returns discovered product identity, type, market value, listing state, and favourite state from the same native adapter used by Products.
- `computer.quests()` returns active quest identity, text, state, tracking state, and objective count from the same native adapter used by Journal.
- `computer.currency(number)` formats a currency value.

The Templates menu contains a blank starter plus Notes, Calculator, About,
Journal, Products, App Studio, and Settings examples. Each example is valid
executable Lua and demonstrates the corresponding sandbox-compatible built-in
app. Renderer/process apps such as Doom and nested Schedule I intentionally have
no Lua template; those belong in C# mods that reference `UsableComputer.dll` and
register an app through the desktop app API.

Apps run with MoonSharp's hard sandbox: filesystem, operating-system, debug, dynamic loading, CLR import, and arbitrary Unity/game object access are not exposed. Game mutations should be added later as narrow, permissioned commands rather than exposing raw S1API or CLR objects to scripts.

## Doom

The Doom app uses the vendored, GPL-licensed Managed Doom engine. It does not
contain an IWAD or other Doom game data. Copy a compatible IWAD into:

```text
UserData/UsableComputer/Doom
```

Recognized file names are `doom.wad`, `doom1.wad`, `doom2.wad`,
`freedoom1.wad`, and `freedoom2.wad`. You may use a Doom IWAD you own or a
compatible free IWAD such as [Freedoom](https://github.com/freedoom/freedoom).
If no IWAD is present, the app shows the exact target directory and a Rescan
button.

Click the framebuffer to give Doom input focus. Use WASD to move, arrow keys to
turn, Ctrl or left click to fire, Space or right click to use, Shift to toggle
run speed, number keys 1-7 to select weapons, and F10 to release Doom input.
The first integration uses `NullSound` and `NullMusic`, so gameplay is currently
silent.

`ManagedDoom.Core.dll` must be installed into `UserLibs` beside
`MoonSharp.Interpreter.dll`. Its pinned source, upstream commit metadata, and
license are under `vendor/ManagedDoom.Core`. Because Managed Doom is GPLv2-or-
later, this combined mod is distributed under the GPL license in `LICENSE`.

## Schedule I inside Schedule I

The Schedule I desktop app launches the currently running game executable as a
second Windows process. A small companion MelonMod redirects the child cameras
and overlay canvases to a 640x360 off-screen Unity render target, copies that
target into shared memory, and lets the parent render it in a normal desktop
window. Click the framebuffer to give the nested game keyboard and mouse focus,
and press F10 to return focus to the outer desktop. Closing the app or pressing
Stop terminates only the child process launched by that app.

The child uses an isolated MelonLoader base and loads only
`UsableComputer.ChildHost.dll`; it does not load DLLs from the normal `Mods`
folder. On first launch, the mod copies the installed MelonLoader runtime into
the isolated base, then reuses it on later launches. Nested save data is kept at:

```text
UserData/UsableComputer/NestedScheduleOne/Profile
```

This is intentionally a separate profile, so its first launch presents the
normal intro and character creation instead of opening an outer-game save. The
feature is Windows-only and runs two complete game instances, so it has a
substantial CPU, GPU, and memory cost. Save in the nested game before pressing
Stop; process termination is not a graceful in-game save operation.

`UsableComputer.ChildHost.dll` must be installed in the outer game's `UserLibs`
folder. The launcher copies that exact companion DLL into the isolated child's
`Mods` folder at runtime. No game assets, executable, prefab, or asset bundle are
packaged by this feature.

## Build

Copy `local.build.props.example` to `local.build.props`, then point it at the
local alternate Mono installation, public IL2CPP installation, and locally
built S1API outputs. `local.build.props` is intentionally ignored because those
paths are machine-specific and the referenced game assemblies may not be
redistributed.

```powershell
dotnet build UsableComputer.csproj -c Mono
dotnet build UsableComputer.csproj -c Il2cpp
dotnet run --project tests\UsableComputer.CalculatorModelVerifier\UsableComputer.CalculatorModelVerifier.csproj -c Release
dotnet run --project tests\UsableComputer.RegistryVerifier\UsableComputer.RegistryVerifier.csproj -c Release
dotnet run --project tests\UsableComputer.LuaVerifier\UsableComputer.LuaVerifier.csproj -c Release
dotnet run --project tests\UsableComputer.DoomVerifier\UsableComputer.DoomVerifier.csproj -c Release -- C:\path\to\an\iwad.wad
```

Deployment is disabled by default. Set `AutomateLocalDeployment=true` explicitly if a local deployment copy is desired; the normal validation build does not touch either game installation.

## Runtime model contract

The runtime factory clones only the two named direct children of the native `launderingstation` `BuiltItem`. It preserves their native local poses, strips inherited colliders and behaviours before activation, and adds clean anchors with the measured native poses. S1API then composes that clean source into its generic furniture prefab and owns the resulting build/save graph.

The desktop canvas is built at 800x536 with scale `0.00046875`, which preserves the native 400x268 screen area. It is parented to the clean screen anchor with local Z offset `0.00015` and identity local rotation; the screen anchor already carries the native 180-degree orientation.

## Lifecycle and teardown

`Core` registers built-in descriptors during Melon initialization. Each placed computer creates its own shell and subscribes to registry changes. Window sessions are per-window, while note storage remains process/global by design. On scene changes or mod shutdown, controllers close first, remove camera/FOV/exit/state overrides, dispose shells and windows, unsubscribe registry/listener callbacks, destroy generated wallpaper objects, and finally release built-in registrations. No native phone UI state is part of this teardown path.

Registrations and UI changes are expected on the game/main thread. A registration made after a shell is visible refreshes that shell immediately; a registration made before shell creation is included in the initial deterministic catalog.

## Verification

The focused verifiers do not require Unity or native game assemblies for their tested logic:

```powershell
dotnet run --project UsableComputer\tests\UsableComputer.CalculatorModelVerifier\UsableComputer.CalculatorModelVerifier.csproj -c Release
dotnet run --project UsableComputer\tests\UsableComputer.RegistryVerifier\UsableComputer.RegistryVerifier.csproj -c Release
```

Build each runtime separately because the native aliases and generated wrappers are intentionally kept distinct:

```powershell
dotnet build UsableComputer.csproj -c Mono
dotnet build UsableComputer.csproj -c Il2cpp
```

These checks prove compilation and pure model/registry behavior only. They do not claim an in-game smoke pass; use the manual checklist below for a backed-up disposable world.

## Manual smoke checklist

Validate once in each supported runtime after installing the matching mod DLL into `Mods` and the matching build's `MoonSharp.Interpreter.dll`, `ManagedDoom.Core.dll`, and `UsableComputer.ChildHost.dll` into `UserLibs`:

1. Start a disposable test world and confirm **Usable Computer** appears in both hardware shops.
2. Buy and place the item; confirm the 4x2 footprint and table/computer visuals.
3. Approach it and confirm the **Use computer** prompt and camera framing.
4. Open each desktop app, drag a window, minimize/restore it from the taskbar, and close it.
5. Save a note, close the computer, reopen another placed computer, and confirm the note is shared.
6. Exercise calculator chaining, decimal input, clear, negative results, and division by zero.
7. Press the game's exit action while open and confirm camera, FOV, movement/look, input typing state, and interaction state are restored.
8. Change scenes and confirm no desktop, state host, listener, or camera override remains.
9. Open Journal and verify active quest details, entry states, and native track/untrack behavior.
10. Open Products and verify discovered product identity, market value, listed state, and the read-only message when Product Manager is unavailable.
11. Register the manual sample from a separate mod, verify it appears on an already-open shell, then unregister it and verify its icon, Start entry, window, and session cleanup disappear.
12. Open App Studio, save the starter dashboard, confirm its icon appears immediately, reopen it after a game restart, and verify malformed or non-terminating scripts report an error without freezing the game.
13. Open Settings from the Start menu; switch between light/dark themes and all three backgrounds, then reopen the computer and confirm the choices persisted.
14. Open Doom with no IWAD and confirm the setup screen is actionable. Add a compatible IWAD, rescan, enter a level, verify keyboard/mouse focus and controls, then press F10 and confirm desktop input is restored.
15. On Windows, open Schedule I, start the nested game, and confirm its intro/menu renders in the desktop window. Click it, verify keyboard and mouse reach only the child, press F10 to restore outer-desktop input, save inside the child, and Stop it. Confirm the outer game remains running and the nested profile is under the documented isolated path.

Do not run this checklist against a real save unless the save has been backed up first.
