using MoonSharp.Interpreter;
using UsableComputer.Subsystems.Lua;

var failures = new List<string>();

Check("valid app table", () =>
{
    var script = new Script(CoreModules.Preset_HardSandbox);
    DynValue result = LuaExecutionBudget.RunSource(
        script,
        "return { id = 'test-app', title = 'Test', render = function() return {} end }",
        "valid.lua");
    if (result.Type != DataType.Table || result.Table.Get("id").String != "test-app")
        throw new InvalidOperationException("Expected returned app table.");
});

Check("hard sandbox hides system libraries", () =>
{
    var script = new Script(CoreModules.Preset_HardSandbox);
    DynValue result = LuaExecutionBudget.RunSource(
        script,
        "return { os = os, io = io, debug = debug, load = load, require = require }",
        "sandbox.lua");
    foreach (string name in new[] { "os", "io", "debug", "load", "require" })
    {
        if (!result.Table.Get(name).IsNil())
            throw new InvalidOperationException($"{name} unexpectedly exists in the hard sandbox.");
    }
});

Check("infinite loop is interrupted", () =>
{
    var script = new Script(CoreModules.Preset_HardSandbox);
    try
    {
        LuaExecutionBudget.RunSource(script, "while true do end", "loop.lua");
        throw new InvalidOperationException("Infinite loop completed without interruption.");
    }
    catch (ScriptRuntimeException exception) when (exception.Message.Contains("execution budget", StringComparison.Ordinal))
    {
    }
});

Check("custom icon sizes and palette", () =>
{
    foreach (int size in new[] { 8, 16, 32 })
    {
        var script = new Script(CoreModules.Preset_HardSandbox);
        var rows = new Table(script);
        for (int i = 1; i <= size; i++) rows.Set(i, DynValue.NewString(new string('.', size)));
        rows.Set(1, DynValue.NewString("0123456F" + new string('.', size - 8)));
        LuaIconPixels icon = LuaIconPixels.Parse(DynValue.NewTable(rows))!;
        if (icon.Size != size || icon.Pixels[7] != 15 || icon.Pixels[size] != 16)
            throw new InvalidOperationException("Palette, transparency, or size changed.");
        rows.Set(1, DynValue.NewString(new string('0', size)));
        if (icon.Pixels[7] != 15) throw new InvalidOperationException("Pixels alias the source table.");
    }
    if (LuaIconPixels.Parse(DynValue.Nil) != null) throw new InvalidOperationException("Absent pixels created an icon.");
});

Check("invalid icons report actionable errors", () =>
{
    foreach (string source in new[]
    {
        "return 'image.png'", "return {}", "return {'a'}",
        "local t = {}; for i=1,8 do t[i]='........' end; t[3]='bad'; return t",
        "local t = {}; for i=1,8 do t[i]='........' end; t[2]='.......!'; return t",
        "local t = {}; for i=1,8 do t[i]='........' end; t.extra=1; return t",
        "local t = {}; for i=1,8 do t[i]='........' end; t[100]=1; return t",
        "local t = {}; for i=1,8 do t[i]='........' end; t[2]=false; return t",
    })
    {
        var script = new Script(CoreModules.Preset_HardSandbox);
        DynValue input = LuaExecutionBudget.RunSource(script, source, "icon.lua");
        bool rejected = false;
        try { LuaIconPixels.Parse(input); }
        catch (ScriptRuntimeException exception) when (exception.Message.Contains("icon_pixels")) { rejected = true; }
        if (!rejected) throw new InvalidOperationException("Invalid icon was accepted: " + source);
    }
});

Check("callback arguments survive cooperative yields", () =>
{
    var script = new Script(CoreModules.Preset_HardSandbox);
    DynValue callback = script.DoString("return function(checked) local total=0; for i=1,20000 do total=total+i end; return checked end");
    if (!LuaExecutionBudget.RunFunction(script, callback, "checkbox", DynValue.True).Boolean)
        throw new InvalidOperationException("Boolean callback argument was lost across yields.");
    if (LuaExecutionBudget.RunFunction(script, callback, "checkbox", DynValue.False).Boolean)
        throw new InvalidOperationException("Unchecked state was converted to true.");
});

return failures.Count == 0 ? 0 : 1;

void Check(string name, Action action)
{
    try
    {
        action();
        Console.WriteLine($"PASS {name}");
    }
    catch (Exception exception)
    {
        failures.Add(name);
        Console.Error.WriteLine($"FAIL {name}: {exception.Message}");
    }
}
