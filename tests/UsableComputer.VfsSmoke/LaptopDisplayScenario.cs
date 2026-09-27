using System.Collections;
using System.Reflection;
using UnityEngine;

#if IL2CPP
using S1Registry = Il2CppScheduleOne.Registry;
using S1PlayerCamera = Il2CppScheduleOne.PlayerScripts.PlayerCamera;
using S1CameraSingleton = Il2CppScheduleOne.DevUtilities.PlayerSingleton<Il2CppScheduleOne.PlayerScripts.PlayerCamera>;
#else
using S1Registry = ScheduleOne.Registry;
using S1PlayerCamera = ScheduleOne.PlayerScripts.PlayerCamera;
using S1CameraSingleton = ScheduleOne.DevUtilities.PlayerSingleton<ScheduleOne.PlayerScripts.PlayerCamera>;
#endif

namespace UsableComputer.VfsSmoke;

public sealed partial class Core
{
    private IEnumerator RunLaptopDisplayScenario()
    {
        foreach (Vector2Int resolution in new[]
                 { new Vector2Int(1920, 1080), new Vector2Int(1024, 768), new Vector2Int(1600, 675) })
        {
            Screen.SetResolution(resolution.x, resolution.y, false);
            yield return new WaitForSecondsRealtime(1f);
            try
            {
                Require(Screen.width == resolution.x && Screen.height == resolution.y,
                    $"Requested resolution {resolution} was not applied: {Screen.width}x{Screen.height}.");
                VerifyLaptopDisplayGeometry();
            }
            catch (Exception exception)
            {
                Fail("Laptop screen fit failed", Unwrap(exception));
                yield break;
            }

            yield return CaptureLaptopView($"laptop-front-{resolution.x}x{resolution.y}.png");
            if (_completed) yield break;
        }

        Screen.SetResolution(1920, 1080, false);
        yield return new WaitForSecondsRealtime(1f);
        S1PlayerCamera playerCamera = S1CameraSingleton.Instance;
        Transform screen = _screenAnchor!.transform;
        Vector3 outward = -screen.forward;
        Vector3 right = screen.right;
        Vector3 up = screen.up;

        // The oblique view exposes a floating canvas or a canvas on the rear of the lid.
        Vector3 oblique = screen.position + outward * 0.65f + right * 0.3f + up * 0.12f;
        playerCamera.OverrideTransform(oblique, Quaternion.LookRotation(screen.position - oblique, up), 0f);
        yield return new WaitForSecondsRealtime(0.3f);
        yield return CaptureLaptopView("laptop-oblique.png");
        if (_completed) yield break;

        _controller!.GetType().GetMethod("Close", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(_controller, null);
        Vector3 overview = screen.position + outward * 1.3f + right * 0.7f + up * 0.45f;
        playerCamera.OverrideTransform(overview, Quaternion.LookRotation(screen.position - overview, up), 0f);
        playerCamera.OverrideFOV(55f, 0f);
        yield return new WaitForSecondsRealtime(0.4f);
        yield return CaptureLaptopView("laptop-native-screen-and-table.png");
        if (_completed) yield break;

        _controller.GetType().GetMethod("Open", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(_controller, null);
        Screen.SetResolution(1280, 720, false);
        yield return new WaitForSecondsRealtime(0.5f);
        try
        {
            VerifyComputerThumbnails();
        }
        catch (Exception exception)
        {
            Fail("Computer thumbnail generation failed", Unwrap(exception));
            yield break;
        }
        LoggerInstance.Msg($"[UsableComputerLaptopSmoke] PASS Runtime={ConstantsRuntime()} Phase={_phase} " +
            "FrontFacing=True ScreenPlane=True ScreenEdges=True TableContact=True AspectRatios=3 Thumbnails=True");
    }

    private void VerifyComputerThumbnails()
    {
        foreach (string id in new[] { "usable_computer", "usable_laptop" })
        {
            Sprite icon = S1Registry.GetItem(id).Icon;
            Require(icon != null && icon.texture != null, $"No generated icon for {id}.");
            Texture2D texture = icon!.texture!;
            int visible = 0;
            foreach (Color32 pixel in texture.GetPixels32())
                if (pixel.a > 0) visible++;
            Require(visible > texture.width * texture.height / 100,
                $"Generated icon for {id} contains no visible model.");
            File.WriteAllBytes(Path.Combine(_outputDirectory, id + "-icon.png"),
                ImageConversion.EncodeToPNG(texture));
            LoggerInstance.Msg($"[UsableComputerLaptopSmoke] Icon={id} VisiblePixels={visible}");
        }
    }

    private IEnumerator CaptureLaptopView(string name)
    {
        string path = Path.Combine(_outputDirectory, name);
        ScreenCapture.CaptureScreenshot(path);
        float deadline = Time.realtimeSinceStartup + 10f;
        while ((!File.Exists(path) || new FileInfo(path).Length == 0) && Time.realtimeSinceStartup < deadline)
            yield return null;
        if (!File.Exists(path) || new FileInfo(path).Length == 0)
            Fail("Laptop screenshot was not written", new IOException(path));
    }

    private void VerifyLaptopDisplayGeometry()
    {
        MeshFilter mesh = FindDescendant(_computerModel!.transform, "Laptop_LOD0")!.GetComponent<MeshFilter>();
        Bounds screenBounds = mesh.sharedMesh.GetSubMesh(1).bounds;
        // Independently measured normal of the native screen quad in the f6 laptop mesh.
        Vector3 outward = new Vector3(0f, 0.17364818f, -0.98480775f);
        Vector3 up = new Vector3(0f, 0.98480775f, 0.17364818f);
        float halfWidth = screenBounds.extents.x;
        float halfHeight = new Vector2(screenBounds.extents.y, screenBounds.extents.z).magnitude;
        RectTransform canvas = _screenAnchor!.GetComponentInChildren<Canvas>(true).GetComponent<RectTransform>();
        Vector3[] corners = GetWorldCorners(canvas);
        Camera camera = Camera.main!;
        Vector3 normal = mesh.transform.TransformDirection(outward);
        Require(Vector3.Dot(camera.transform.position - mesh.transform.TransformPoint(screenBounds.center), normal) > 0f,
            "Camera is behind the laptop screen.");
        Require(Vector3.Dot(-canvas.forward, normal) > 0.9999f, "Canvas does not follow the native screen plane.");

        for (int i = 0; i < corners.Length; i++)
        {
            Vector3 local = mesh.transform.InverseTransformPoint(corners[i]) - screenBounds.center;
            float clearance = Vector3.Dot(local, outward);
            Require(clearance > 0.0003f && clearance < 0.002f,
                $"Canvas corner {i} is not immediately in front of the screen: {clearance}m.");
            float xInset = halfWidth - Math.Abs(local.x);
            float yInset = halfHeight - Math.Abs(Vector3.Dot(local, up));
            Require(Math.Abs(xInset - 0.001f) < 0.0002f && Math.Abs(yInset - 0.001f) < 0.0002f,
                $"Canvas corner {i} does not fit the native screen: inset=({xInset},{yInset}).");
            Vector3 viewport = camera.WorldToViewportPoint(corners[i]);
            Require(viewport.z > 0f && viewport.x > 0.03f && viewport.x < 0.97f &&
                    viewport.y > 0.03f && viewport.y < 0.97f,
                $"Laptop screen clips at {Screen.width}x{Screen.height}: {viewport}.");
        }

        MeshFilter tabletop = FindDescendant(_computerModel.transform, "Table")!.GetComponent<MeshFilter>();
        Bounds tableBounds = tabletop.sharedMesh.bounds;
        float tableTop = _computerModel.transform.InverseTransformPoint(tabletop.transform.TransformPoint(
            new Vector3(tableBounds.center.x, tableBounds.max.y, tableBounds.center.z))).y;
        Bounds laptopBounds = mesh.sharedMesh.bounds;
        float laptopBottom = _computerModel.transform.InverseTransformPoint(mesh.transform.TransformPoint(
            new Vector3(laptopBounds.center.x, laptopBounds.min.y, laptopBounds.center.z))).y;
        Require(Math.Abs(tableTop - laptopBottom) < 0.001f,
            $"Laptop does not rest on the table: table={tableTop}, laptop={laptopBottom}.");
        LoggerInstance.Msg($"[UsableComputerLaptopSmoke] FIT {Screen.width}x{Screen.height} " +
            "ScreenInset=1mm Clearance<2mm TableGap<1mm");
    }
}
