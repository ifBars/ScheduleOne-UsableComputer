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

## Virtual text files from C#

Use `DesktopFileSystem` on the game thread after a game save has loaded. All computers in that save share this virtual disk. Mutations update the save snapshot immediately; durable storage happens when the game saves. IDs belong to that save, so resolve them again after loading another save.

```csharp
DesktopFileEntry file = DesktopFileSystem.CreateTextFile(
    DesktopFileSystem.DesktopId, "Report.txt", "Today's report");
DesktopFileSystem.WriteText(file.Id, "Updated report");
string text = DesktopFileSystem.ReadText(file.Id);
context.OpenFile(file.Id); // Open in Notes.
```

`GetChildren`, `ResolvePath`, `GetPath`, `CreateDirectory`, `Rename`, `Move`, and `Delete` support file management. Paths such as `/Desktop/Report.txt` refer only to the virtual disk. Names are case-insensitively unique within each folder, and node IDs remain stable after moves and renames. Creation never overwrites an existing item. `WriteText` explicitly replaces a file's content; apps sharing a document should coordinate writes. Invalid operations throw with an actionable message and leave the existing data intact.

The limits are 64 KiB of UTF-8 per file, 1 MiB total text, 2,048 nodes, and 64 characters per name. Empty files are supported. Directories and app shortcuts cannot contain text. The API returns detached entry metadata, not mutable VFS nodes. Lua apps retain their existing sandbox; this file API is available to C# apps.


