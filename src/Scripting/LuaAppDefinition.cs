using System;
using MoonSharp.Interpreter;
using UnityEngine;
using UsableComputer.Native;
using UsableComputer.UI;

namespace UsableComputer.Scripting;

internal sealed class LuaAppDefinition
{
    internal LuaAppDefinition(
        string id,
        string title,
        string icon,
        Vector2 windowSize,
        Script script,
        DynValue render,
        string source,
        string sourcePath)
    {
        Id = id;
        Title = title;
        Icon = icon;
        WindowSize = windowSize;
        Script = script;
        Render = render;
        Source = source;
        SourcePath = sourcePath;
    }

    internal string Id { get; }
    internal string RegistryId => $"lua.{Id}";
    internal string Title { get; }
    internal string Icon { get; }
    internal Vector2 WindowSize { get; }
    internal Script Script { get; }
    internal DynValue Render { get; }
    internal string Source { get; }
    internal string SourcePath { get; }

    internal Sprite ResolveIcon() => Icon.ToLowerInvariant() switch
    {
        "notes" => RuntimeAppIcons.Get(BuiltInIcon.Notes),
        "calculator" => RuntimeAppIcons.Get(BuiltInIcon.Calculator),
        "about" => RuntimeAppIcons.Get(BuiltInIcon.About),
        "studio" => RuntimeAppIcons.Get(BuiltInIcon.Studio),
        "journal" => NativePhoneAppAssets.GetJournalIcon() ?? RuntimeAppIcons.Get(BuiltInIcon.Generic),
        "products" => NativePhoneAppAssets.GetProductManagerIcon() ?? RuntimeAppIcons.Get(BuiltInIcon.Generic),
        "settings" => RuntimeAppIcons.Get(BuiltInIcon.Settings),
        "doom" => RuntimeAppIcons.Get(BuiltInIcon.Doom),
        "schedule-one" => RuntimeAppIcons.Get(BuiltInIcon.ScheduleOne),
        _ => RuntimeAppIcons.Get(BuiltInIcon.Generic),
    };
}
