using System;
using System.Globalization;
using UnityEngine;

#if IL2CPPMELON
using NativeHome = Il2CppScheduleOne.TV.TVHomeScreen;
using NativeRunner = Il2CppScheduleOne.TV.RunnerGame;
using NativeVariables = Il2CppScheduleOne.Variables.VariableDatabase;
using NativeVariableMode = Il2CppScheduleOne.Variables.EVariableMode;
#else
using NativeHome = ScheduleOne.TV.TVHomeScreen;
using NativeRunner = ScheduleOne.TV.RunnerGame;
using NativeVariables = ScheduleOne.Variables.VariableDatabase;
using NativeVariableMode = ScheduleOne.Variables.EVariableMode;
#endif

namespace UsableComputer.Apps.Games.EggRun;

internal static class EggRunNativeAdapter
{
    internal const string AppId = "native-tv.egg-run";
    private static NativeRunner? _source;

    internal static NativeRunner? FindSource()
    {
        if (_source != null && HasRequiredContent(_source)) return _source;
        _source = null;
        foreach (NativeHome home in Resources.FindObjectsOfTypeAll<NativeHome>())
        {
            if (home.Apps == null) continue;
            foreach (var app in home.Apps)
            {
                if (app == null) continue;
                NativeRunner candidate = app.GetComponent<NativeRunner>();
                if (candidate != null && HasRequiredContent(candidate))
                    return _source = candidate;
            }
        }
        return null;
    }

    internal static Sprite? Icon() => FindSource()?.Icon;

    internal static bool TryGetHighScore(out float score, out bool canSave)
    {
        score = 0f;
        canSave = false;
        try
        {
            NativeVariables database = NativeVariables.Instance;
            if (database == null) return false;
            var variable = database.GetVariable("RunGameHighScore");
            if (variable == null) return false;
            score = database.GetValue<float>("RunGameHighScore");
            if (float.IsNaN(score) || float.IsInfinity(score)) return false;
            canSave = variable.Persistent && (variable.VariableMode == NativeVariableMode.Global
                ? database.IsServerInitialized
                : variable.Owner != null && variable.Owner.IsOwner);
            return true;
        }
        catch (Exception error)
        {
            MelonLoader.MelonLogger.Warning("[Usable Computer] Egg Run high score unavailable: " + error.Message);
            return false;
        }
    }

    internal static bool TrySaveHighScore(float score)
    {
        if (!TryGetHighScore(out float previous, out bool canSave) || !canSave || score <= previous)
            return false;
        try
        {
            NativeVariables.Instance.SetVariableValue("RunGameHighScore", score.ToString(CultureInfo.InvariantCulture));
            return true;
        }
        catch (Exception error)
        {
            MelonLoader.MelonLogger.Warning("[Usable Computer] Egg Run high score was not saved: " + error.Message);
            return false;
        }
    }

    internal static bool HasRequiredContent(NativeRunner game)
    {
        if (game == null || game.Canvas == null || game.Character == null ||
            game.CharacterFlipboard == null || game.Ground == null ||
            game.CloudSpawner == null || game.ObstacleSpawner == null ||
            game.ScoreLabel == null || game.HighScoreLabel == null ||
            game.StartScreen == null || game.GameOverScreen == null)
            return false;

        Transform root = game.Canvas.transform;
        return root == game.transform &&
            game.Character.IsChildOf(root) && game.CharacterFlipboard.transform.IsChildOf(root) &&
            game.Ground.transform.IsChildOf(root) && game.CloudSpawner.transform.IsChildOf(root) &&
            game.ObstacleSpawner.transform.IsChildOf(root) && game.ScoreLabel.transform.IsChildOf(root) &&
            game.HighScoreLabel.transform.IsChildOf(root) && game.StartScreen.transform.IsChildOf(root) &&
            game.GameOverScreen.transform.IsChildOf(root) &&
            game.CloudSpawner.Prefabs != null && game.CloudSpawner.Prefabs.Length > 0 &&
            game.ObstacleSpawner.Prefabs != null && game.ObstacleSpawner.Prefabs.Length > 0;
    }
}
