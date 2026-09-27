using System;
using MoonSharp.Interpreter;
using UnityEngine;
using UsableComputer.Native;
using UsableComputer.UI;

namespace UsableComputer.Subsystems.Lua;

internal sealed class LuaAppDefinition : IDisposable
{
    private Sprite? _customIcon;
    private Texture2D? _customTexture;
    internal LuaAppDefinition(
        string id,
        string title,
        string icon,
        Vector2 windowSize,
        Script script,
        DynValue render,
        string source,
        string sourcePath,
        LuaIconPixels? iconPixels = null)
    {
        Id = id;
        Title = title;
        Icon = icon;
        WindowSize = windowSize;
        Script = script;
        Render = render;
        Source = source;
        SourcePath = sourcePath;
        IconPixels = iconPixels;
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
    internal LuaIconPixels? IconPixels { get; }

    internal Sprite ResolveIcon()
    {
        if (IconPixels == null)
            return ResolveBuiltInIcon();
        if (_customIcon != null)
            return _customIcon;
        var pixels = new Color32[IconPixels.Pixels.Length];
        for (int y = 0; y < IconPixels.Size; y++)
        for (int x = 0; x < IconPixels.Size; x++)
            pixels[(IconPixels.Size - y - 1) * IconPixels.Size + x] = Palette[IconPixels.Pixels[y * IconPixels.Size + x]];
        _customTexture = new Texture2D(IconPixels.Size, IconPixels.Size, TextureFormat.RGBA32, false)
        {
            name = $"LuaIcon_{Id}", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp,
        };
        _customTexture.SetPixels32(pixels);
        _customTexture.Apply(false, true);
        _customIcon = Sprite.Create(_customTexture, new Rect(0, 0, IconPixels.Size, IconPixels.Size), new Vector2(0.5f, 0.5f), IconPixels.Size);
        return _customIcon;
    }

    public void Dispose()
    {
        if (_customIcon != null) UnityEngine.Object.Destroy(_customIcon);
        if (_customTexture != null) UnityEngine.Object.Destroy(_customTexture);
        _customIcon = null;
        _customTexture = null;
    }

    private static readonly Color32[] Palette =
    {
        new(0, 0, 0, 255), new(128, 0, 0, 255), new(0, 128, 0, 255), new(128, 128, 0, 255),
        new(0, 0, 128, 255), new(128, 0, 128, 255), new(0, 128, 128, 255), new(192, 192, 192, 255),
        new(128, 128, 128, 255), new(255, 0, 0, 255), new(0, 255, 0, 255), new(255, 255, 0, 255),
        new(0, 0, 255, 255), new(255, 0, 255, 255), new(0, 255, 255, 255), new(255, 255, 255, 255),
        new(0, 0, 0, 0),
    };

    private Sprite ResolveBuiltInIcon() => Icon.ToLowerInvariant() switch
    {
        "notes" => RuntimeAppIcons.Get(BuiltInIcon.Notes),
        "calculator" => RuntimeAppIcons.Get(BuiltInIcon.Calculator),
        "about" => RuntimeAppIcons.Get(BuiltInIcon.About),
        "studio" => RuntimeAppIcons.Get(BuiltInIcon.Studio),
        "journal" => NativePhoneAppAssets.GetJournalIcon() ?? RuntimeAppIcons.Get(BuiltInIcon.Generic),
        "products" => NativePhoneAppAssets.GetProductManagerIcon() ?? RuntimeAppIcons.Get(BuiltInIcon.Generic),
        "settings" => RuntimeAppIcons.Get(BuiltInIcon.Settings),
        "doom" => RuntimeAppIcons.Get(BuiltInIcon.Doom),
        "schedule-one" => NativeGameBrandAssets.GetScheduleOneLogo()
            ?? RuntimeAppIcons.Get(BuiltInIcon.ScheduleOne),
        _ => RuntimeAppIcons.Get(BuiltInIcon.Generic),
    };
}
