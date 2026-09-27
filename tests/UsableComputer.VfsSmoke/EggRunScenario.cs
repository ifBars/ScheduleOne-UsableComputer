using System.Collections;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

#if IL2CPP
using NativeRunner = Il2CppScheduleOne.TV.RunnerGame;
using NativeSpawner = Il2CppScheduleOne.UI.UISpawner;
using NativeMover = Il2CppScheduleOne.UI.UIMover;
using NativeVariables = Il2CppScheduleOne.Variables.VariableDatabase;
#else
using NativeRunner = ScheduleOne.TV.RunnerGame;
using NativeSpawner = ScheduleOne.UI.UISpawner;
using NativeMover = ScheduleOne.UI.UIMover;
using NativeVariables = ScheduleOne.Variables.VariableDatabase;
#endif

namespace UsableComputer.VfsSmoke;

public sealed partial class Core
{
    private IEnumerator RunEggRunScenario()
    {
        NativeRunner source;
        Vector2 sourceCharacter;
        bool sourceOpen;
        object session;
        RectTransform playfield;
        RectTransform character;
        NativeSpawner clouds;
        NativeSpawner obstacles;
        int nativeRunnerCount;
        float highScoreBefore;
        bool canSaveHighScore;
        try
        {
            Type adapter = GetUsableComputerAssembly().GetType("UsableComputer.Native.EggRunNativeAdapter", true)!;
            source = (NativeRunner)adapter.GetMethod("FindSource", BindingFlags.Static | BindingFlags.NonPublic)!
                .Invoke(null, null)! ?? throw new InvalidOperationException("Egg Run native source missing.");
            sourceCharacter = source.Character.anchoredPosition;
            sourceOpen = source.IsOpen;
            nativeRunnerCount = Resources.FindObjectsOfTypeAll<NativeRunner>().Length;
            object?[] scoreArgs = { 0f, false };
            bool hasHighScore = (bool)adapter.GetMethod("TryGetHighScore", BindingFlags.Static | BindingFlags.NonPublic)!
                .Invoke(null, scoreArgs)!;
            Require(hasHighScore, "The native Egg Run high score variable is unavailable.");
            highScoreBefore = (float)scoreArgs[0]!;
            canSaveHighScore = (bool)scoreArgs[1]!;
            CloseAllWindows();
            _desktop!.GetType().GetMethod("OpenApp", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(_desktop, new object[] { "native-tv.egg-run" });
            session = GetAppSession("native-tv.egg-run");
            playfield = EggField<RectTransform>(session, "_playfield")
                ?? throw new InvalidOperationException("Egg Run has no playfield.");
            character = EggField<RectTransform>(session, "_character")
                ?? throw new InvalidOperationException("Egg Run has no character.");
            clouds = EggField<NativeSpawner>(session, "_cloudSpawner")
                ?? throw new InvalidOperationException("Egg Run has no cloud spawner.");
            obstacles = EggField<NativeSpawner>(session, "_obstacleSpawner")
                ?? throw new InvalidOperationException("Egg Run has no obstacle spawner.");
            Require(playfield != null && playfield.gameObject.activeInHierarchy,
                "Egg Run playfield was not hosted in the desktop.");
            Require(character != source.Character && obstacles != source.ObstacleSpawner,
                "Egg Run reused a source TV component.");
            Require(!source.IsOpen && source.IsOpen == sourceOpen && source.Character.anchoredPosition == sourceCharacter,
                "Opening Egg Run changed the source TV app.");
            clouds.MinInterval = clouds.MaxInterval = 0.15f;
            obstacles.MinInterval = obstacles.MaxInterval = 0.15f;
            GameObject.Find("EggRunJump").GetComponent<Button>().onClick.Invoke();
            Require(!EggField<bool>(session, "_ready"), "Jump button did not start Egg Run.");
        }
        catch (Exception error) { Fail("Egg Run setup failed", Unwrap(error)); yield break; }

        yield return new WaitForSecondsRealtime(0.65f);
        try
        {
            Require(EggField<float>(session, "_score") > 0f, "Egg Run score did not advance.");
            Require(clouds.transform.childCount > EggField<int>(session, "_baseCloudCount") ||
                obstacles.transform.childCount > EggField<int>(session, "_baseObstacleCount"),
                "Native Egg Run spawners did not create desktop-owned scenery.");
            Require(source.Character.anchoredPosition == sourceCharacter && source.IsOpen == sourceOpen,
                "Running Egg Run modified the source TV app.");
            Require(Resources.FindObjectsOfTypeAll<NativeRunner>().Length == nativeRunnerCount,
                "Egg Run retained a cloned native TV game root.");
            RequireEggRunFits(playfield ?? throw new InvalidOperationException("Egg Run playfield was lost."));
        }
        catch (Exception error) { Fail("Egg Run movement failed", Unwrap(error)); yield break; }

        string screenshot = Path.Combine(_outputDirectory, "egg-run.png");
        ScreenCapture.CaptureScreenshot(screenshot);
        yield return WaitForCapture(screenshot);
        if (_completed) yield break;

        try
        {
            _desktop!.GetType().GetMethod("OpenApp", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(_desktop, new object[] { "settings" });
        }
        catch (Exception error) { Fail("Egg Run focus setup failed", Unwrap(error)); yield break; }
        yield return new WaitForSecondsRealtime(0.1f);
        float pausedScore = EggField<float>(session, "_score");
        Vector2 pausedCharacter = character.anchoredPosition;
        int pausedObstacleCount = obstacles.transform.childCount;
        yield return new WaitForSecondsRealtime(0.35f);
        try
        {
            Require(EggField<float>(session, "_score") == pausedScore &&
                character.anchoredPosition == pausedCharacter &&
                obstacles.transform.childCount == pausedObstacleCount && !obstacles.enabled,
                "Unfocused Egg Run kept updating.");
            _desktop!.GetType().GetMethod("OpenApp", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(_desktop, new object[] { "native-tv.egg-run" });
        }
        catch (Exception error) { Fail("Egg Run focus pause failed", Unwrap(error)); yield break; }
        yield return new WaitForSecondsRealtime(0.25f);
        try
        {
            Require(EggField<float>(session, "_score") > pausedScore && obstacles.enabled,
                "Egg Run did not resume after refocus.");
            GameObject obstacle = UnityEngine.Object.Instantiate(obstacles.Prefabs[0], obstacles.transform);
            foreach (Collider collider in obstacle.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
            NativeMover mover = obstacle.GetComponent<NativeMover>()
                ?? throw new InvalidOperationException("Native obstacle has no movement component.");
            mover.enabled = false;
            mover.Rect.position = character.position;
            float winningScore = Mathf.Max(highScoreBefore + 1000f, EggField<float>(session, "_score"));
            session.GetType().GetField("_score", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(session, winningScore);
            session.GetType().GetMethod("OnTick")!.Invoke(session, null);
            Require(EggField<bool>(session, "_ready") && EggField<GameObject>(session, "_gameOverScreen").activeSelf,
                "Obstacle overlap did not end the round.");
            if (canSaveHighScore)
                Require(NativeVariables.Instance.GetValue<float>("RunGameHighScore") >= winningScore,
                    "The host did not save Egg Run's new native high score.");
            GameObject.Find("EggRunJump").GetComponent<Button>().onClick.Invoke();
            Require(!EggField<bool>(session, "_ready") && !EggField<GameObject>(session, "_gameOverScreen").activeSelf,
                "Egg Run did not restart after a collision.");
            GameObject.Find("Window_native-tv.egg-run").transform.Find("TitleBar/Maximize")
                .GetComponent<Button>().onClick.Invoke();
            RequireEggRunFits(playfield ?? throw new InvalidOperationException("Egg Run playfield was lost."));
        }
        catch (Exception error) { Fail("Egg Run collision/restart failed", Unwrap(error)); yield break; }

        screenshot = Path.Combine(_outputDirectory, "egg-run-maximized.png");
        ScreenCapture.CaptureScreenshot(screenshot);
        yield return WaitForCapture(screenshot);
        if (_completed) yield break;
        try
        {
            CloseAllWindows();
            Require(source.Character.anchoredPosition == sourceCharacter && source.IsOpen == sourceOpen,
                "Closing desktop Egg Run changed the source TV app.");
        }
        catch (Exception error) { Fail("Egg Run close failed", Unwrap(error)); yield break; }
        yield return null;
        try
        {
            Require(playfield == null && clouds == null && obstacles == null,
                "Egg Run retained native visuals or spawners after close.");
            LoggerInstance.Msg($"[UsableComputerEggRunSmoke] PASS Runtime={ConstantsRuntime()} Phase={_phase} NativeSpawns=True FocusPause=True Collision=True Restart=True HighScoreHostSave={canSaveHighScore} SourceUnchanged=True Disposed=True");
        }
        catch (Exception error) { Fail("Egg Run cleanup failed", Unwrap(error)); }
    }

    private static T EggField<T>(object session, string name) => (T)session.GetType()
        .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(session)!;

    private static void RequireEggRunFits(RectTransform playfield)
    {
        RectTransform viewport = playfield.parent.GetComponent<RectTransform>();
        Rect rect = playfield.rect;
        foreach (Vector3 corner in new[] { new Vector3(rect.xMin, rect.yMin), new Vector3(rect.xMax, rect.yMin),
            new Vector3(rect.xMin, rect.yMax), new Vector3(rect.xMax, rect.yMax) })
        {
            Vector3 point = viewport.InverseTransformPoint(playfield.TransformPoint(corner));
            Require(point.x >= viewport.rect.xMin - 1f && point.x <= viewport.rect.xMax + 1f &&
                point.y >= viewport.rect.yMin - 1f && point.y <= viewport.rect.yMax + 1f,
                $"Egg Run extends outside its viewport. Viewport={viewport.rect} Point={point} Field={rect}");
        }
    }
}
