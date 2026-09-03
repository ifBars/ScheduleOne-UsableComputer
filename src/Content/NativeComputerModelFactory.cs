using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace UsableComputer.Content;

/// <summary>
/// Builds the public-safe visual source by cloning only the two native donor branches
/// required for the computer table. The donor's build root is never instantiated.
/// </summary>
internal static class NativeComputerModelFactory
{
    private const string TableBranchName = "plastictable_2x1";
    private const string ComputerBranchName = "oldcomputer";

    internal static GameObject Create(GameObject donorBuiltItem)
    {
        if (donorBuiltItem == null)
            throw new ArgumentNullException(nameof(donorBuiltItem));

        Transform donorRoot = donorBuiltItem.transform;
        Transform table = RequireDirectBranch(donorRoot, TableBranchName);
        Transform computer = RequireDirectBranch(donorRoot, ComputerBranchName);

        var modelRoot = new GameObject(Constants.ModelRootName);
        modelRoot.SetActive(false);

        try
        {
            CloneVisualBranch(table, modelRoot.transform);
            CloneVisualBranch(computer, modelRoot.transform);
            CreateAnchors(modelRoot.transform);

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

    private static void CloneVisualBranch(Transform source, Transform parent)
    {
        GameObject clone = Object.Instantiate(source.gameObject, parent, false);
        clone.name = source.name;
        RemoveInheritedRuntimeComponents(clone);
    }

    private static void RemoveInheritedRuntimeComponents(GameObject clone)
    {
        foreach (Collider collider in clone.GetComponentsInChildren<Collider>(true))
            Object.DestroyImmediate(collider);

        foreach (MonoBehaviour behaviour in clone.GetComponentsInChildren<MonoBehaviour>(true))
            Object.DestroyImmediate(behaviour);
    }

    private static void CreateAnchors(Transform modelRoot)
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
}
