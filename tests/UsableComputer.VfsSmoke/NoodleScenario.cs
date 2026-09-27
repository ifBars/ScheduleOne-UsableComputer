using System.Collections;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

#if IL2CPP
using NativeSnake = Il2CppScheduleOne.TV.Snake;
using NativeTile = Il2CppScheduleOne.TV.SnakeTile;
#else
using NativeSnake = ScheduleOne.TV.Snake;
using NativeTile = ScheduleOne.TV.SnakeTile;
#endif

namespace UsableComputer.VfsSmoke;

public sealed partial class Core
{
    private IEnumerator RunNoodleScenario()
    {
        NativeSnake game;
        NativeSnake source;
        Vector2 sourceHead;
        Vector2 head;
        try
        {
            Type adapter = GetUsableComputerAssembly().GetType("UsableComputer.Native.NoodleNativeAdapter", true)!;
            source = (NativeSnake)adapter.GetMethod("FindSource", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, null)!;
            Require(source != null, "Noodle source missing.");
            sourceHead = source!.HeadPosition;
            CloseAllWindows();
            Type preferences = GetUsableComputerAssembly().GetType("UsableComputer.PreferencesStore", true)!;
            MethodInfo setTheme = preferences.GetMethod("SetTheme", BindingFlags.Static | BindingFlags.NonPublic)!;
            setTheme.Invoke(null, new[] { Enum.Parse(setTheme.GetParameters()[0].ParameterType, _phase == "seed" ? "Light" : "Dark") });
            _desktop!.GetType().GetMethod("OpenApp", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(_desktop, new object[] { "native-tv.noodle" });
        }
        catch (Exception error) { Fail("Noodle setup failed", Unwrap(error)); yield break; }
        yield return new WaitForSecondsRealtime(0.5f);
        try
        {
            object session = GetAppSession("native-tv.noodle");
            game = (NativeSnake)session.GetType().GetField("_game", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(session)!;
            Require(game != null && game.PlaySpace.gameObject.activeInHierarchy, "Noodle playfield is not visible.");
            Require(!game!.IsOpen && !source.IsOpen && !game.enabled, "Noodle used the native TV open/update lifecycle.");
            GameObject.Find("NoodleRight").GetComponent<Button>().onClick.Invoke();
            Require(game.GameState == NativeSnake.EGameState.Playing, "Direction button did not start Noodle.");
            head = game.HeadPosition;
        }
        catch (Exception error) { Fail("Noodle start failed", Unwrap(error)); yield break; }
        yield return new WaitForSecondsRealtime(0.6f);
        try
        {
            Require(game.HeadPosition != head, "Noodle did not advance.");
            Require(source.HeadPosition == sourceHead && !source.IsOpen, "The source TV game was changed.");
            _desktop!.GetType().GetMethod("OpenApp", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(_desktop, new object[] { "settings" });
            if (game == null || GetWindowList().Length != 2) throw new InvalidOperationException("Switching focus closed Noodle.");
            head = game.HeadPosition;
        }
        catch (Exception error) { Fail("Noodle movement failed", Unwrap(error)); yield break; }
        yield return new WaitForSecondsRealtime(0.6f);
        try
        {
            if (game == null || game.HeadPosition != head) throw new InvalidOperationException("Unfocused Noodle was destroyed or kept advancing.");
            _desktop!.GetType().GetMethod("OpenApp", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(_desktop, new object[] { "native-tv.noodle" });
            game.TimePerTile = 10f;
            foreach (var tile in game.Tiles)
                if (tile.Type == NativeTile.TileType.Food) tile.SetType(NativeTile.TileType.Empty);
            Vector2 target = game.HeadPosition + game.Direction;
            game.Tiles[(int)target.y * 20 + (int)target.x].SetType(NativeTile.TileType.Food);
            int tail = game.Tail.Count;
            typeof(NativeSnake).GetMethod("MoveSnake", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!.Invoke(game, null);
            Require(game.Tail.Count == tail + 1, "Noodle did not grow after eating food.");
            Require(game.Tiles[0] != source.Tiles[0], "Noodle retained source tile references.");
            RequireNoodleFits(game);
        }
        catch (Exception error) { Fail("Noodle focus failed", Unwrap(error)); yield break; }
        yield return new WaitForSecondsRealtime(0.2f);
        string screenshot = Path.Combine(_outputDirectory, "noodle.png");
        ScreenCapture.CaptureScreenshot(screenshot); yield return WaitForCapture(screenshot);
        if (_completed) yield break;
        try { GameObject.Find("Window_native-tv.noodle").transform.Find("TitleBar/Maximize").GetComponent<Button>().onClick.Invoke(); }
        catch (Exception error) { Fail("Noodle maximize failed", Unwrap(error)); yield break; }
        yield return new WaitForSecondsRealtime(0.2f);
        try { RequireNoodleFits(game); }
        catch (Exception error) { Fail("Noodle viewport failed", Unwrap(error)); yield break; }
        screenshot = Path.Combine(_outputDirectory, "noodle-maximized.png");
        ScreenCapture.CaptureScreenshot(screenshot); yield return WaitForCapture(screenshot);
        if (_completed) yield break;
        yield return RunNoodleLifecycle(game);
        if (_completed) yield break;
        try { CloseAllWindows(); }
        catch (Exception error) { Fail("Noodle close failed", Unwrap(error)); yield break; }
        yield return null;
        try
        {
            Require(game == null && source != null && source.HeadPosition == sourceHead, "Noodle cleanup changed the source or retained its clone.");
            LoggerInstance.Msg($"[UsableComputerNoodleSmoke] PASS Runtime={ConstantsRuntime()} Phase={_phase} Movement=True Food=True FocusPause=True SourceUnchanged=True Disposed=True");
        }
        catch (Exception error) { Fail("Noodle cleanup failed", Unwrap(error)); }
    }

    private static void RequireNoodleFits(NativeSnake game)
    {
        RectTransform host = game.PlaySpace.parent.GetComponent<RectTransform>();
        Rect rect = game.PlaySpace.rect;
        foreach (Vector3 corner in new[] { new Vector3(rect.xMin, rect.yMin), new Vector3(rect.xMax, rect.yMin),
            new Vector3(rect.xMin, rect.yMax), new Vector3(rect.xMax, rect.yMax) })
        {
            Vector3 point = host.InverseTransformPoint(game.PlaySpace.TransformPoint(corner));
            Require(point.x >= host.rect.xMin - 1f && point.x <= host.rect.xMax + 1f &&
                point.y >= host.rect.yMin - 1f && point.y <= host.rect.yMax + 1f,
                $"Noodle playfield extends outside its viewport. Host={host.rect} Point={point} Grid={rect} Scale={game.PlaySpace.localScale}");
        }
    }
}
