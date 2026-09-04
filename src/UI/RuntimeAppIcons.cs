using System;
using System.Collections.Generic;
using UsableComputer.API;
using UnityEngine;

namespace UsableComputer.UI;

internal enum BuiltInIcon
{
    Notes,
    Folder,
    Calculator,
    About,
    Studio,
    Settings,
    Doom,
    ScheduleOne,
    Generic,
}

internal static class RuntimeAppIcons
{
    private const int Size = 64;
    private static readonly Dictionary<BuiltInIcon, Sprite> Sprites = new();
    private static readonly List<Texture2D> Textures = new();

    internal static Sprite Get(BuiltInIcon icon)
    {
        if (Sprites.TryGetValue(icon, out Sprite? sprite) && sprite != null)
            return sprite;

        Texture2D texture = Build(icon);
        texture.name = $"UsableComputer_{icon}_IconTexture";
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.Apply(updateMipmaps: false, makeNoLongerReadable: true);
        sprite = Sprite.Create(texture, new Rect(0f, 0f, Size, Size), new Vector2(0.5f, 0.5f), 64f);
        sprite.name = $"UsableComputer_{icon}_Icon";
        Textures.Add(texture);
        Sprites.Add(icon, sprite);
        return sprite;
    }

    internal static Sprite Resolve(DesktopAppDescriptor descriptor)
    {
        try
        {
            Sprite? provided = descriptor.ResolveIcon?.Invoke();
            if (provided != null)
                return provided;
        }
        catch (Exception exception)
        {
            MelonLoader.MelonLogger.Warning(
                $"[{Constants.ModName}] Could not resolve icon for '{descriptor.Id}': {exception.Message}");
        }

        return Get(BuiltInIcon.Generic);
    }

    internal static void Dispose()
    {
        foreach (Sprite sprite in Sprites.Values)
        {
            if (sprite != null)
                UnityEngine.Object.Destroy(sprite);
        }

        foreach (Texture2D texture in Textures)
        {
            if (texture != null)
                UnityEngine.Object.Destroy(texture);
        }

        Sprites.Clear();
        Textures.Clear();
    }

    private static Texture2D Build(BuiltInIcon icon)
    {
        var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, mipChain: false);
        var pixels = new Color32[Size * Size];
        Fill(pixels, new Color32(0, 0, 0, 0));

        switch (icon)
        {
            case BuiltInIcon.Notes:
                DrawNotes(pixels);
                break;
            case BuiltInIcon.Folder:
                DrawFolder(pixels);
                break;
            case BuiltInIcon.Calculator:
                DrawCalculator(pixels);
                break;
            case BuiltInIcon.About:
                DrawAbout(pixels);
                break;
            case BuiltInIcon.Studio:
                DrawStudio(pixels);
                break;
            case BuiltInIcon.Settings:
                DrawSettings(pixels);
                break;
            case BuiltInIcon.Doom:
                DrawDoom(pixels);
                break;
            case BuiltInIcon.ScheduleOne:
                DrawScheduleOne(pixels);
                break;
            default:
                DrawGeneric(pixels);
                break;
        }

        texture.SetPixels32(pixels);
        return texture;
    }

    private static void DrawNotes(Color32[] pixels)
    {
        DrawRect(pixels, 11, 6, 45, 52, new Color32(232, 178, 31, 255));
        DrawRect(pixels, 15, 10, 37, 44, new Color32(255, 249, 180, 255));
        DrawRect(pixels, 18, 18, 30, 3, new Color32(77, 126, 190, 255));
        DrawRect(pixels, 18, 27, 27, 3, new Color32(77, 126, 190, 255));
        DrawRect(pixels, 18, 36, 31, 3, new Color32(77, 126, 190, 255));
        DrawRect(pixels, 18, 45, 21, 3, new Color32(77, 126, 190, 255));
        for (int x = 17; x <= 49; x += 8)
            DrawRect(pixels, x, 54, 4, 6, new Color32(70, 70, 74, 255));
    }

    private static void DrawFolder(Color32[] pixels)
    {
        DrawRect(pixels, 7, 12, 25, 10, new Color32(224, 166, 34, 255));
        DrawRect(pixels, 5, 18, 54, 38, new Color32(239, 188, 55, 255));
        DrawRect(pixels, 8, 24, 48, 28, new Color32(255, 211, 86, 255));
        DrawLine(pixels, 8, 25, 56, 25, new Color32(255, 231, 140, 255), 2);
    }

    private static void DrawCalculator(Color32[] pixels)
    {
        DrawRect(pixels, 9, 5, 46, 55, new Color32(60, 67, 78, 255));
        DrawRect(pixels, 14, 42, 36, 11, new Color32(181, 220, 185, 255));
        for (int row = 0; row < 3; row++)
        {
            for (int column = 0; column < 4; column++)
            {
                Color32 color = column == 3
                    ? new Color32(239, 155, 43, 255)
                    : new Color32(215, 218, 224, 255);
                DrawRect(pixels, 14 + (column * 9), 10 + (row * 9), 7, 7, color);
            }
        }
    }

    private static void DrawAbout(Color32[] pixels)
    {
        DrawRect(pixels, 6, 15, 52, 37, new Color32(39, 72, 125, 255));
        DrawRect(pixels, 10, 19, 44, 29, new Color32(73, 169, 228, 255));
        DrawRect(pixels, 27, 8, 10, 7, new Color32(66, 68, 76, 255));
        DrawRect(pixels, 20, 5, 24, 4, new Color32(66, 68, 76, 255));
        DrawCircle(pixels, 32, 34, 8, new Color32(245, 245, 248, 255));
        DrawRect(pixels, 30, 25, 4, 11, new Color32(39, 72, 125, 255));
        DrawRect(pixels, 30, 39, 4, 4, new Color32(39, 72, 125, 255));
    }

    private static void DrawStudio(Color32[] pixels)
    {
        DrawRect(pixels, 5, 8, 54, 48, new Color32(32, 35, 45, 255));
        DrawRect(pixels, 5, 49, 54, 7, new Color32(28, 91, 180, 255));
        DrawLine(pixels, 24, 22, 15, 31, new Color32(103, 205, 255, 255), 3);
        DrawLine(pixels, 15, 31, 24, 40, new Color32(103, 205, 255, 255), 3);
        DrawLine(pixels, 40, 22, 49, 31, new Color32(249, 196, 81, 255), 3);
        DrawLine(pixels, 49, 31, 40, 40, new Color32(249, 196, 81, 255), 3);
        DrawLine(pixels, 36, 18, 28, 44, new Color32(196, 137, 255, 255), 3);
    }

    private static void DrawGeneric(Color32[] pixels)
    {
        DrawRect(pixels, 6, 10, 52, 44, new Color32(225, 230, 239, 255));
        DrawRect(pixels, 6, 46, 52, 8, new Color32(39, 104, 207, 255));
        DrawRect(pixels, 11, 16, 18, 24, new Color32(121, 178, 234, 255));
        DrawRect(pixels, 34, 31, 18, 9, new Color32(245, 196, 69, 255));
        DrawRect(pixels, 34, 18, 18, 9, new Color32(120, 193, 105, 255));
    }

    private static void DrawSettings(Color32[] pixels)
    {
        Color32 outline = new(32, 49, 76, 255);
        Color32 bezel = new(88, 110, 143, 255);
        Color32 screen = new(225, 238, 247, 255);
        Color32 track = new(86, 105, 128, 255);

        // A display with color controls reads as "Display Properties" even at taskbar size.
        DrawRect(pixels, 5, 13, 54, 39, outline);
        DrawRect(pixels, 8, 16, 48, 33, bezel);
        DrawRect(pixels, 11, 19, 42, 27, screen);
        DrawRect(pixels, 11, 41, 42, 5, new Color32(48, 113, 190, 255));

        DrawRect(pixels, 15, 23, 11, 14, new Color32(66, 153, 219, 255));
        DrawRect(pixels, 15, 23, 11, 5, new Color32(102, 190, 102, 255));
        DrawRect(pixels, 15, 32, 11, 5, new Color32(241, 186, 66, 255));

        DrawRect(pixels, 31, 25, 17, 2, track);
        DrawCircle(pixels, 39, 26, 3, new Color32(48, 113, 190, 255));
        DrawRect(pixels, 31, 32, 17, 2, track);
        DrawCircle(pixels, 44, 33, 3, new Color32(102, 171, 92, 255));

        DrawRect(pixels, 27, 8, 10, 5, outline);
        DrawRect(pixels, 20, 5, 24, 4, outline);
    }

    private static void DrawDoom(Color32[] pixels)
    {
        DrawRect(pixels, 5, 7, 54, 50, new Color32(28, 19, 18, 255));
        DrawRect(pixels, 8, 10, 48, 10, new Color32(140, 31, 23, 255));
        DrawRect(pixels, 8, 44, 48, 10, new Color32(91, 21, 18, 255));
        DrawRect(pixels, 13, 23, 7, 18, new Color32(236, 176, 48, 255));
        DrawRect(pixels, 20, 20, 8, 24, new Color32(246, 205, 67, 255));
        DrawRect(pixels, 36, 20, 8, 24, new Color32(246, 205, 67, 255));
        DrawRect(pixels, 44, 23, 7, 18, new Color32(236, 176, 48, 255));
        DrawRect(pixels, 26, 25, 12, 14, new Color32(191, 50, 29, 255));
    }

    private static void DrawScheduleOne(Color32[] pixels)
    {
        DrawRect(pixels, 4, 7, 56, 50, new Color32(24, 43, 59, 255));
        DrawRect(pixels, 8, 11, 48, 42, new Color32(73, 151, 197, 255));
        DrawCircle(pixels, 19, 39, 10, new Color32(243, 224, 91, 255));
        DrawRect(pixels, 8, 12, 48, 17, new Color32(63, 139, 61, 255));
        DrawLine(pixels, 7, 23, 26, 34, new Color32(83, 173, 72, 255), 5);
        DrawLine(pixels, 23, 33, 42, 20, new Color32(45, 111, 49, 255), 6);
        DrawLine(pixels, 40, 21, 57, 29, new Color32(76, 157, 64, 255), 5);
        DrawRect(pixels, 45, 39, 5, 10, new Color32(241, 241, 230, 255));
        DrawRect(pixels, 42, 36, 11, 4, new Color32(241, 241, 230, 255));
    }

    private static void Fill(Color32[] pixels, Color32 color)
    {
        for (int index = 0; index < pixels.Length; index++)
            pixels[index] = color;
    }

    private static void DrawRect(Color32[] pixels, int x, int y, int width, int height, Color32 color)
    {
        for (int row = Math.Max(0, y); row < Math.Min(Size, y + height); row++)
        {
            for (int column = Math.Max(0, x); column < Math.Min(Size, x + width); column++)
                pixels[(row * Size) + column] = color;
        }
    }

    private static void DrawCircle(Color32[] pixels, int centerX, int centerY, int radius, Color32 color)
    {
        int radiusSquared = radius * radius;
        for (int y = centerY - radius; y <= centerY + radius; y++)
        {
            for (int x = centerX - radius; x <= centerX + radius; x++)
            {
                int dx = x - centerX;
                int dy = y - centerY;
                if ((dx * dx) + (dy * dy) <= radiusSquared && x >= 0 && x < Size && y >= 0 && y < Size)
                    pixels[(y * Size) + x] = color;
            }
        }
    }

    private static void DrawLine(
        Color32[] pixels,
        int x0,
        int y0,
        int x1,
        int y1,
        Color32 color,
        int thickness)
    {
        int dx = Math.Abs(x1 - x0);
        int sx = x0 < x1 ? 1 : -1;
        int dy = -Math.Abs(y1 - y0);
        int sy = y0 < y1 ? 1 : -1;
        int error = dx + dy;
        while (true)
        {
            DrawRect(pixels, x0 - (thickness / 2), y0 - (thickness / 2), thickness, thickness, color);
            if (x0 == x1 && y0 == y1)
                break;
            int doubled = 2 * error;
            if (doubled >= dy)
            {
                error += dy;
                x0 += sx;
            }
            if (doubled <= dx)
            {
                error += dx;
                y0 += sy;
            }
        }
    }
}
