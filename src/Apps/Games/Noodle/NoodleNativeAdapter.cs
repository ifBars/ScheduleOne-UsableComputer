using System;
using System.Reflection;
using UnityEngine;

#if IL2CPPMELON
using NativeHome = Il2CppScheduleOne.TV.TVHomeScreen;
using NativeSnake = Il2CppScheduleOne.TV.Snake;
#else
using NativeHome = ScheduleOne.TV.TVHomeScreen;
using NativeSnake = ScheduleOne.TV.Snake;
#endif

namespace UsableComputer.Apps.Games.Noodle;

internal static class NoodleNativeAdapter
{
    internal const string AppId = "native-tv.noodle";
    private static NativeSnake? _source;
    internal static NativeSnake? FindSource()
    {
        if (_source != null) return _source;
        foreach (NativeHome home in Resources.FindObjectsOfTypeAll<NativeHome>())
        {
            if (home.Apps == null) continue;
            foreach (var app in home.Apps)
            {
                if (app == null) continue;
                NativeSnake candidate = app.GetComponent<NativeSnake>();
                if (candidate != null && candidate.Canvas != null && candidate.Tiles != null && candidate.Tiles.Length == 240)
                    return _source = candidate;
            }
        }
        return null;
    }

    internal static Sprite? Icon() => FindSource()?.Icon;

    internal static MethodInfo Method(string name) => typeof(NativeSnake).GetMethod(name,
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
        ?? throw new NotSupportedException("This game version does not expose Noodle's " + name + " method.");

    internal static MethodInfo DirectionSetter() => typeof(NativeSnake).GetProperty("QueuedDirection",
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetSetMethod(true)
        ?? throw new NotSupportedException("This game version does not expose Noodle direction control.");
}
