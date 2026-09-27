using MoonSharp.Interpreter;
using UsableComputer.Subsystems.Lua;

int checks = 0;
void Check(bool value) { if (!value) throw new Exception($"Assertion {checks + 1} failed"); checks++; }
void Reject(Action action) { try { action(); } catch (ArgumentException) { checks++; return; } throw new Exception("Invalid storage accepted"); }
var book = new LuaStorageBook();
book.Set("a", "key", "old");
Check(book.Get("b", "key") == null);
Reject(() => book.Set("a", "key", new string('\u20ac', 2731)));
Check(book.Get("a", "key") == "old");
Reject(() => book.Set("a", "../escape", "x"));
var copy = book.Snapshot();
copy.Entries[0].Value = "changed";
Check(book.Get("a", "key") == "old");
Check(new LuaStorageBook(book.Snapshot()).Get("a", "key") == "old");
Reject(() => new LuaStorageBook(new LuaStorageSnapshot { Version = 2 }));
for (int i = 0; i < 64; i++) book.Set("keys", i.ToString(), "");
Reject(() => book.Set("keys", "extra", ""));
book.Delete("a", "key"); Check(book.Get("a", "key") == null);
var full = new LuaStorageBook();
for (int app = 0; app < 16; app++)
    for (int key = 0; key < 4; key++) full.Set(app.ToString(), key.ToString(), new string('x', 8191));
Reject(() => full.Set("new", "k", "v"));
Reject(() => full.Set("0", "extra", ""));
full.Delete("0", "0"); full.Set("new", "k", "v"); Check(full.Get("new", "k") == "v");
var script = new Script(CoreModules.Preset_HardSandbox);
var computer = new Table(script); script.Globals.Set("computer", DynValue.NewTable(computer));
LuaApiContract.BindStorage(script, computer, () => "owner", book.Get, book.Set, book.Delete);
script.DoString("computer.storage.set('schema', '1'); computer.storage.set('answer', '42')");
Check(book.Get("owner", "answer") == "42" && book.Get("other", "answer") == null);
Check(script.DoString("return computer.api_version").Number == 1);
LuaApiContract.Validate(script.DoString("return {}").Table); checks++;
LuaApiContract.Validate(script.DoString("return {api_version=1}").Table); checks++;
foreach (string value in new[] { "2", "'1'", "false", "1.5" })
{
    try { LuaApiContract.Validate(script.DoString("return {api_version=" + value + "}").Table); throw new Exception("Invalid version accepted"); }
    catch (ScriptRuntimeException) { checks++; }
}
var example = script.DoString(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "shift-tally.lua"))).Table;
LuaApiContract.Validate(example);
Table rows = script.Call(example.Get("render")).Table;
Check(rows.Get(3).Table.Get("value").String == "0");
script.Call(rows.Get(4).Table.Get("action"));
Check(book.Get("owner", "completed") == "1");
Check(script.Call(example.Get("render")).Table.Get(3).Table.Get("value").String == "1");
script.Call(rows.Get(5).Table.Get("action"));
Check(book.Get("owner", "completed") == null);
var checklist = script.DoString(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "shift-checklist.lua"))).Table;
LuaApiContract.Validate(checklist);
Table tasks = script.Call(checklist.Get("render")).Table;
Check(tasks.Get(3).Table.Get("value").Type == DataType.Boolean && !tasks.Get(3).Table.Get("value").Boolean);
script.Call(tasks.Get(3).Table.Get("action"), DynValue.True);
Check(book.Get("owner", "supplies") == "done" && book.Get("owner", "deliveries") == null);
script.Call(tasks.Get(4).Table.Get("action"), DynValue.True);
script.Call(tasks.Get(3).Table.Get("action"), DynValue.False);
Check(book.Get("owner", "supplies") == null && book.Get("owner", "deliveries") == "done");
script.Call(tasks.Get(6).Table.Get("action"));
Check(book.Get("owner", "deliveries") == null);
Console.WriteLine($"PASS | Lua storage and contract | {checks} assertions");
