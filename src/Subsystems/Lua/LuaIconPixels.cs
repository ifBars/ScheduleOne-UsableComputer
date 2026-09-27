using System;
using MoonSharp.Interpreter;

namespace UsableComputer.Subsystems.Lua;

/// <summary>A bounded pixel-art image; parsing never loads files or exposes Unity objects.</summary>
internal sealed class LuaIconPixels
{
    internal int Size { get; }
    internal byte[] Pixels { get; }

    private LuaIconPixels(int size, byte[] pixels)
    {
        Size = size;
        Pixels = pixels;
    }

    internal static LuaIconPixels? Parse(DynValue value)
    {
        if (value.IsNil())
            return null;
        if (value.Type != DataType.Table)
            throw new ScriptRuntimeException("icon_pixels must be an array of 8, 16, or 32 pixel rows.");
        int size = value.Table.Length;
        if (size != 8 && size != 16 && size != 32)
            throw new ScriptRuntimeException("icon_pixels must contain 8, 16, or 32 rows.");
        int keys = 0;
        foreach (TablePair pair in value.Table.Pairs)
        {
            keys++;
            if (keys > size || pair.Key.Type != DataType.Number || pair.Key.Number < 1 ||
                pair.Key.Number > size || pair.Key.Number != Math.Truncate(pair.Key.Number))
                throw new ScriptRuntimeException("icon_pixels must be a contiguous array without extra keys.");
        }
        var pixels = new byte[size * size];
        for (int row = 0; row < size; row++)
        {
            DynValue line = value.Table.Get(row + 1);
            if (line.Type != DataType.String || line.String.Length != size)
                throw new ScriptRuntimeException($"icon_pixels row {row + 1} must be a string of {size} pixels.");
            for (int column = 0; column < size; column++)
            {
                char symbol = char.ToLowerInvariant(line.String[column]);
                int color = symbol == '.' ? 16 : symbol >= '0' && symbol <= '9' ? symbol - '0'
                    : symbol >= 'a' && symbol <= 'f' ? symbol - 'a' + 10 : -1;
                if (color < 0)
                    throw new ScriptRuntimeException($"icon_pixels row {row + 1}, column {column + 1}: use 0-9, a-f, or '.' for transparency.");
                pixels[row * size + column] = (byte)color;
            }
        }
        return new LuaIconPixels(size, pixels);
    }
}
