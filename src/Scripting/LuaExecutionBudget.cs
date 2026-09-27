using System;
using System.Diagnostics;
using MoonSharp.Interpreter;

namespace UsableComputer.Scripting;

internal static class LuaExecutionBudget
{
    private const int AutoYieldInstructions = 5_000;
    private static readonly TimeSpan MaximumExecutionTime = TimeSpan.FromMilliseconds(250);

    internal static DynValue RunSource(Script script, string source, string sourcePath)
    {
        DynValue function = script.LoadString(source, null, sourcePath);
        return RunFunction(script, function, "app startup");
    }

    internal static DynValue RunFunction(Script script, DynValue function, string label, params DynValue[] arguments)
    {
        if (function.Type == DataType.ClrFunction)
            return script.Call(function, arguments);

        DynValue coroutineValue = script.CreateCoroutine(function);
        Coroutine coroutine = coroutineValue.Coroutine;
        coroutine.AutoYieldCounter = AutoYieldInstructions;
        var stopwatch = Stopwatch.StartNew();
        DynValue result = DynValue.Nil;
        bool firstResume = true;
        while (coroutine.State != CoroutineState.Dead)
        {
            result = firstResume ? coroutine.Resume(arguments) : coroutine.Resume();
            firstResume = false;
            if (stopwatch.Elapsed > MaximumExecutionTime)
            {
                throw new ScriptRuntimeException(
                    $"{label} exceeded the {MaximumExecutionTime.TotalMilliseconds:0} ms execution budget.");
            }
        }

        return result;
    }
}
