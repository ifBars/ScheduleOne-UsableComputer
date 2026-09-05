# Make an app for Usable Computer

[Back to the README](../README.md)

Use App Studio for a Lua dashboard, or register a C# app from another mod.

## Lua apps

App Studio saves Lua apps to `UserData/UsableComputer/Apps` and hot-loads them without restarting the game. Scripts can read player, money, property, employee, product, and quest data through a small S1API-backed surface.

```lua
return {
  id = "business-dashboard",
  title = "Business Dashboard",
  icon = "studio",

  render = function()
    local money = computer.money()

    return {
      { kind = "heading", text = "Business Dashboard" },
      { kind = "stat", label = "Cash", value = computer.currency(money.cash) },
      { kind = "stat", label = "Bank", value = computer.currency(money.online) },
    }
  end
}
```

Lua apps cannot access the filesystem, operating system, CLR, Unity objects, or raw S1API types. Source is limited to 64 KiB, rendered output is limited to 64 rows, and each invocation has a 250 ms execution budget.

## Add a C# app

Mods can reference `UsableComputer_Mono.dll` or `UsableComputer_Il2cpp.dll` and register an app without depending on runtime-specific TextMeshPro or Schedule I types:

```csharp
using UsableComputer.API;
using UnityEngine;

DesktopAppRegistry.Register(new DesktopAppDescriptor(
    id: "my-mod.app",
    title: "My App",
    glyph: "M",
    preferredWindowSize: new Vector2(440f, 300f),
    preferredWindowPosition: Vector2.zero,
    createSession: context => new MyDesktopSession(context)));
```

Each window gets its own `IDesktopAppSession`. Apps registered after the computer opens appear immediately, and unregistering an app closes its windows and disposes its sessions. See [`examples/ManualDesktopAppSample`](../examples/ManualDesktopAppSample) for a complete buildable example.


