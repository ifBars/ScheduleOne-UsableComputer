using System;
using UnityEngine;
using Object = UnityEngine.Object;

#if IL2CPPMELON
using S1GridItem = Il2CppScheduleOne.EntityFramework.GridItem;
#elif MONOMELON
using S1GridItem = ScheduleOne.EntityFramework.GridItem;
#endif

namespace UsableComputer.Hardware;

/// <summary>
/// Builds the runtime visuals from game-owned objects without retaining their behavior.
/// </summary>
internal static class NativeComputerModelFactory
{
    private const string TableBranchName = "plastictable_2x1";
    private const string ComputerBranchName = "oldcomputer";

    internal static GameObject Create(GameObject donorBuiltItem) =>
        CreateModel(donorBuiltItem, null);

    internal static GameObject CreateLaptop(GameObject donorBuiltItem, GameObject laptopSource) =>
        CreateModel(donorBuiltItem, laptopSource ?? throw new ArgumentNullException(nameof(laptopSource)));

    private static GameObject CreateModel(GameObject donorBuiltItem, GameObject? laptopSource)
    {
        if (donorBuiltItem == null)
            throw new ArgumentNullException(nameof(donorBuiltItem));

        Transform donorRoot = donorBuiltItem.transform;
        Transform table = RequireDirectBranch(donorRoot, TableBranchName);
        Transform? computer = laptopSource == null
            ? RequireDirectBranch(donorRoot, ComputerBranchName)
            : null;
        S1GridItem gridItem = donorBuiltItem.GetComponent<S1GridItem>();
        if (gridItem == null || gridItem.OriginFootprint == null)
            throw new InvalidOperationException("The native computer donor has no ground footprint.");

        float groundHeight = donorRoot.InverseTransformPoint(gridItem.OriginFootprint.transform.position).y;

        var modelRoot = new GameObject(Constants.ModelRootName);
        modelRoot.SetActive(false);
        modelRoot.transform.localScale = Vector3.one * DisplayProfile.ModelScale;

        try
        {
            GameObject tableVisual = CloneVisualBranch(table, modelRoot.transform);
            if (laptopSource == null)
            {
                CloneVisualBranch(computer!, modelRoot.transform);
                CreateDesktopAnchors(modelRoot.transform);
            }
            else
            {
                GameObject laptop = CloneVisualBranch(laptopSource.transform, modelRoot.transform);
                laptop.transform.localPosition = Vector3.zero;
                laptop.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                laptop.transform.localScale = Vector3.one;
                laptop.SetActive(true);
                float tableTop = GetVerticalExtent(tableVisual, modelRoot.transform).y;
                float laptopBottom = GetVerticalExtent(laptop, modelRoot.transform).x;
                laptop.transform.localPosition = Vector3.up * (tableTop - laptopBottom);
                CreateLaptopAnchors(modelRoot.transform, laptop);
            }

            // The donor is centered above its footprint; S1API furniture uses a ground-level
            // origin. Shift the visuals and their anchors together, before model scaling.
            for (int index = 0; index < modelRoot.transform.childCount; index++)
                modelRoot.transform.GetChild(index).localPosition -= Vector3.up * groundHeight;

            if (modelRoot.GetComponentsInChildren<Renderer>(true).Length == 0)
                throw new InvalidOperationException("The native donor branches contain no renderers.");

            if (modelRoot.GetComponentsInChildren<Collider>(true).Length != 0)
                throw new InvalidOperationException("The runtime computer model still contains a collider.");

            return modelRoot;
        }
        catch
        {
            Object.Destroy(modelRoot);
            throw;
        }
    }

    private static Transform RequireDirectBranch(Transform donorRoot, string branchName)
    {
        Transform? branch = donorRoot.Find(branchName);
        if (branch == null || branch.parent != donorRoot)
        {
            throw new InvalidOperationException(
                $"Laundering station donor is missing direct visual branch '{branchName}'.");
        }

        return branch;
    }

    private static GameObject CloneVisualBranch(Transform source, Transform parent)
    {
        GameObject clone = Object.Instantiate(source.gameObject, parent, false);
        clone.name = source.name;
        RemoveInheritedRuntimeComponents(clone);
        return clone;
    }

    private static void RemoveInheritedRuntimeComponents(GameObject clone)
    {
        foreach (Collider collider in clone.GetComponentsInChildren<Collider>(true))
            Object.DestroyImmediate(collider);

        foreach (MonoBehaviour behaviour in clone.GetComponentsInChildren<MonoBehaviour>(true))
            Object.DestroyImmediate(behaviour);
    }

    private static void CreateDesktopAnchors(Transform modelRoot)
    {
        var cameraAnchor = new GameObject(Constants.CameraAnchorName).transform;
        cameraAnchor.SetParent(modelRoot, false);
        cameraAnchor.localPosition = new Vector3(-0.00000143f, 0.838f, 0.41700268f);
        cameraAnchor.localRotation = new Quaternion(
            -5.5879354e-09f,
            0.99831754f,
            -0.05798383f,
            0f);

        var screenAnchor = new GameObject(Constants.ScreenAnchorName).transform;
        screenAnchor.SetParent(modelRoot, false);
        screenAnchor.localPosition = new Vector3(0f, 0.78f, 0.02475f);
        screenAnchor.localRotation = new Quaternion(0f, -1f, 0f, 0f);
    }

    private static Vector2 GetVerticalExtent(GameObject visual, Transform relativeTo)
    {
        float minimum = float.PositiveInfinity;
        float maximum = float.NegativeInfinity;
        foreach (MeshFilter filter in visual.GetComponentsInChildren<MeshFilter>(true))
        {
            if (filter.sharedMesh == null)
                continue;

            Bounds bounds = filter.sharedMesh.bounds;
            for (int corner = 0; corner < 8; corner++)
            {
                var point = new Vector3(
                    (corner & 1) == 0 ? bounds.min.x : bounds.max.x,
                    (corner & 2) == 0 ? bounds.min.y : bounds.max.y,
                    (corner & 4) == 0 ? bounds.min.z : bounds.max.z);
                float height = relativeTo.InverseTransformPoint(filter.transform.TransformPoint(point)).y;
                minimum = Mathf.Min(minimum, height);
                maximum = Mathf.Max(maximum, height);
            }
        }

        if (float.IsInfinity(minimum))
            throw new InvalidOperationException($"Native visual '{visual.name}' has no mesh bounds.");

        return new Vector2(minimum, maximum);
    }

    private static void CreateLaptopAnchors(Transform modelRoot, GameObject laptop)
    {
        MeshFilter? screenMesh = laptop.transform.Find("Laptop_LOD0")?.GetComponent<MeshFilter>();
        MeshRenderer? renderer = screenMesh?.GetComponent<MeshRenderer>();
        if (screenMesh == null || screenMesh.sharedMesh == null ||
            screenMesh.sharedMesh.subMeshCount < 2 || renderer == null ||
            renderer.sharedMaterials.Length < 2 || renderer.sharedMaterials[1] == null ||
            renderer.sharedMaterials[1].name != "Laptop_Screens")
            throw new InvalidOperationException("The native laptop screen submesh is unavailable.");

        // The screen quad slopes toward +Z at its top and faces the keyboard (-Z).
        // Submesh bounds remain available even though the game mesh is not CPU-readable.
        Bounds screen = screenMesh.sharedMesh.GetSubMesh(1).bounds;
        Vector3 screenUp = new Vector3(0f, screen.size.y, screen.size.z).normalized;
        Vector3 outward = -Vector3.Cross(Vector3.right, screenUp);
        float width = screen.size.x;
        float height = new Vector2(screen.size.y, screen.size.z).magnitude;
        const float edgeInset = 0.001f;
        if (width <= edgeInset * 2f || height <= edgeInset * 2f)
            throw new InvalidOperationException("The native laptop screen has invalid dimensions.");

        var screenAnchor = new GameObject(Constants.ScreenAnchorName).transform;
        screenAnchor.SetParent(screenMesh.transform, false);
        screenAnchor.localPosition = screen.center + outward * 0.001f;
        screenAnchor.localRotation = Quaternion.LookRotation(-outward, screenUp);
        screenAnchor.localScale = new Vector3(
            (width - edgeInset * 2f) / (DisplayProfile.CanvasWidth * DisplayProfile.CanvasScale),
            (height - edgeInset * 2f) / (DisplayProfile.CanvasHeight * DisplayProfile.CanvasScale),
            1f);

        Vector3 worldUp = screenMesh.transform.TransformDirection(screenUp);
        Vector3 viewDirection = screenMesh.transform.TransformDirection(-outward);
        float worldHeight = screenMesh.transform.TransformVector(screenUp * height).magnitude;
        const float viewportHeightFraction = 0.74f;
        float distance = worldHeight / (2f * Mathf.Tan(
            DisplayProfile.InteractionFieldOfView * Mathf.Deg2Rad * 0.5f) * viewportHeightFraction);
        var cameraAnchor = new GameObject(Constants.CameraAnchorName).transform;
        cameraAnchor.SetParent(modelRoot, false);
        cameraAnchor.position = screenAnchor.position - viewDirection * distance;
        cameraAnchor.rotation = Quaternion.LookRotation(viewDirection, worldUp);
    }
}
