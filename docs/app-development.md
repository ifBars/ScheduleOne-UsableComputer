# Make an app for Usable Computer

[Back to the README](../README.md)

Use App Studio for a Lua dashboard, or register a C# app from another mod.

For a practical starting point, choose **Templates → Shift Checklist** in App Studio, then **Save & Run**. Its checkboxes remember completed tasks in the host save. The [example source](../examples/Lua/shift-checklist.lua) is also available to edit outside the game. See the [Lua API contract](lua-api-v1.md) for checkbox callbacks and storage limits.

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

Lua apps cannot access the filesystem, operating system, CLR, Unity objects, or raw S1API types. Source is limited to 65,536 characters, rendered output is limited to 64 rows, and Lua execution has a cooperative 250 ms budget. See [Lua API v1](lua-api-v1.md) for the complete read surface, save-owned storage, quotas, compatibility, and sandbox limits. The [Shift Tally example](../examples/Lua/shift-tally.lua) demonstrates persistent button actions.

The Lua `icon` field selects a built-in image: `notes`, `calculator`, `about`, `studio`, `journal`, `products`, `settings`, `doom`, `schedule-one`, or `generic`. Omitted or unknown names use the generic image.

For custom artwork, add `icon_pixels` to the returned app table. It takes precedence over `icon` and accepts a square image of 8, 16, or 32 rows, with the same number of characters per row. Rows run from top to bottom. Use `.` for transparency and hexadecimal digits for the palette below. Invalid artwork reports its row and column where applicable; the previous installed app is preserved when validation fails.

```lua
icon_pixels = {
  "........", ".cccccc.", ".cffffc.", ".cfccfc.",
  ".cfccfc.", ".cffffc.", ".cccccc.", "........"
},
```

| Pixel | Color | Pixel | Color |
| --- | --- | --- | --- |
| `0` | Black | `8` | Gray |
| `1` | Maroon | `9` | Red |
| `2` | Green | `a` | Lime |
| `3` | Olive | `b` | Yellow |
| `4` | Navy | `c` | Blue |
| `5` | Purple | `d` | Magenta |
| `6` | Teal | `e` | Cyan |
| `7` | Silver | `f` | White |

The mod creates and caches the sprite, then releases it on app replacement or shutdown. Artwork stays within the existing source-size limit and adds no filesystem or Unity access to Lua. Use C# for externally loaded or live game sprites.

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
    createSession: context => new MyDesktopSession(context),
    resolveIcon: () => myCachedSprite));
```

Each window gets its own `IDesktopAppSession`. Apps registered after the computer opens appear immediately, and unregistering an app closes its windows and disposes its sessions. See [`examples/ManualDesktopAppSample`](../examples/ManualDesktopAppSample) for a complete buildable example.

`resolveIcon` accepts a `Func<Sprite?>` and runs when the desktop needs the app's image. Return a cached sprite owned by your mod, or resolve a live game sprite when its scene is ready. A null result or an exception falls back to the generic app image. The legacy `glyph` field does not draw a text icon.

The provider can be called repeatedly, so avoid creating a new texture on every call. Keep your sprite alive while the app is registered, unregister the app before destroying mod-owned sprites and textures, and never destroy borrowed game assets. The manual sample demonstrates lazy creation, caching, and cleanup without shipping an external image.

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


