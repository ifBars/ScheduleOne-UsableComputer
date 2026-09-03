using MoonSharp.Interpreter;
using UsableComputer.Scripting;

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
