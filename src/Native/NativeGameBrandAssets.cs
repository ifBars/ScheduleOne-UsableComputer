using System;
using UnityEngine;

namespace UsableComputer.Native;

/// <summary>
/// Resolves game-owned brand artwork already loaded by the active scene.
/// </summary>
internal static class NativeGameBrandAssets
{
    internal const string ScheduleOneLogoName = "S1 Logo Whiteout small";

    private static Sprite? _scheduleOneLogo;

    internal static Sprite? GetScheduleOneLogo()
    {
        if (_scheduleOneLogo != null)
            return _scheduleOneLogo;

        Sprite[] sprites = Resources.FindObjectsOfTypeAll<Sprite>();
        foreach (Sprite sprite in sprites)
        {
            if (sprite != null && string.Equals(sprite.name, ScheduleOneLogoName, StringComparison.Ordinal))
            {
                _scheduleOneLogo = sprite;
                break;
            }
        }

        return _scheduleOneLogo;
    }

    internal static void Reset()
    {
        // The sprite belongs to the game; release our reference without destroying the asset.
        _scheduleOneLogo = null;
    }
}
