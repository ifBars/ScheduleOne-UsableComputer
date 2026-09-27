# Lua API v1

[App development](app-development.md) · [Persistent tally example](../examples/Lua/shift-tally.lua)

Return an app table with numeric `api_version = 1`. `computer.api_version` exposes the supported version. Existing apps that omit the declaration remain v1 apps. Other declarations fail validation with a migration message; an invalid replacement does not replace the installed app.

## Read APIs

Calls return Lua values and detached tables, never live game objects. Array results use one-based indices. Availability depends on the loaded game; these are snapshots, not subscriptions. Mutating a returned table does not mutate the game.

| Call | Returned fields |
| --- | --- |
| `computer.money()` | `cash`, `online`, `net_worth` |
| `computer.player()` | `name`, `health`, `max_health`, `region`, `current_property` |
| `computer.properties()` | Array of `name`, `code`, `price`, `employee_count`, `employee_capacity` |
| `computer.employees()` | Array of property summaries: `property`, `count`, `capacity` |
| `computer.products()` | Array of `id`, `name`, `product_type`, `market_value`, `listed`, `favourited` |
| `computer.quests()` | Array of `id`, `title`, `subtitle`, `description`, `state`, `tracked`, `entry_count` |
| `computer.currency(number)` | Dollar-prefixed text with up to two decimal places |

The `render` callback returns up to 64 row tables. Supported kinds are `heading` and `text` (`text`), `stat` (`label`, `value`), `button` (`text`, `action` function), `checkbox` (described below), and `spacer`. A successful button action triggers a refresh. Render and action failures appear in the app window. General text is capped at 500 characters, stat labels at 80, and stat values at 120.

`checkbox` rows take `text`, a required boolean `value`, and an `action(checked)` function. The callback receives the new state as a Lua boolean and runs under the same execution budget as buttons. Initial rendering and periodic refreshes do not call the action. After a successful action, the app renders again using its own returned value; update your model or storage in the callback to retain the change. Errors replace the rows with an app error message. The checkbox is included in the existing 64-row limit.

The [Shift Checklist example](../examples/Lua/shift-checklist.lua) uses save-owned storage to remember manually completed tasks and reset them for the next shift. It does not automatically observe stock or change game state.

## Save-owned storage

Call storage inside `render` or button callbacks, after the app's validated ID has been bound. Top-level script access fails explicitly. Only the loaded host save supports storage; multiplayer clients receive an error instead of silently saving divergent data.

| Call | Behavior |
| --- | --- |
| `computer.storage.get(key)` | Returns a string, or `nil` if absent. |
| `computer.storage.set(key, value)` | Creates or replaces a string value; returns nothing. |
| `computer.storage.delete(key)` | Deletes a key; an absent key is a no-op. |

Keys must contain 1–64 ASCII letters, digits, dots, underscores, or hyphens. Values must be strings. Each app can store 64 keys, with at most 8 KiB of UTF-8 per value and 32 KiB of keys plus values. A save supports 64 nonempty app namespaces and 512 KiB of keys plus values in total. These byte quotas exclude app IDs and serialization overhead. Rejected writes preserve the previous value. Deleting the last key releases the namespace slot.

The namespace is the app's declared ID, bound by the host; callbacks cannot supply another namespace. Replacing an app with the same ID intentionally keeps its data. Consequently IDs are not an authentication boundary against another installed script declaring that same ID. Renaming an ID creates a separate namespace. Uninstalling an app does not automatically erase its stored data.

Writes update the save snapshot immediately; they reach disk when the game saves. Closing an app or restarting a computer does not erase them. Loading another game save switches the storage dataset. Keep writes in user actions rather than every render. Apps own their value formats and can use a key such as `schema_version` for migrations. The enclosing storage format is versioned separately; invalid or unsupported snapshots disable storage rather than silently replacing data.

## Execution and trust boundaries

Lua uses MoonSharp's hard sandbox. It exposes no filesystem, process execution, arbitrary CLR, raw Unity, or raw S1API objects. `os`, `io`, `debug`, `load`, and `require` are unavailable. Storage writes are the only new mutation in this contract; game-changing commands and permission grants are not exposed. C# drivers are trusted installed mods with process access and are outside this Lua boundary.

Source is capped at 65,536 characters. Lua execution yields every 5,000 VM instructions and checks a 250 ms budget between resumes. This limits ordinary runaway Lua loops, but is not hard preemption: host calls and native library operations can run before control returns. There is no separate process or hard heap quota, and large allocations can still affect the game. Storage quotas bound persisted data, not all script memory. Install scripts from sources you trust; do not describe this runtime as safe execution of arbitrary hostile code.

The storage verifier covers quota edges, failed-write preservation, namespaces, snapshots, and version declarations. `Run-VfsSmoke.ps1 -LuaStorage` exercises an actual Lua button write and a separate process reload in a disposable save. Multiplayer synchronization is not implemented by this contract.
