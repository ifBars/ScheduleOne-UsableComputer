using System;
using System.Collections.Generic;
using S1API.Building;
using UnityEngine;

namespace UsableComputer.Runtime;

internal static class UsableComputerRuntime
{
    private static readonly Dictionary<int, UsableComputerController> Controllers = new();
    private static UsableComputerController? _activeController;

    internal static void Attach(BuildEventArgs args)
    {
        if (args == null || !string.Equals(args.ItemId, Constants.ItemId, StringComparison.Ordinal))
            return;

        GameObject? builtObject = args.GameObject;
        if (builtObject == null)
            return;

        int instanceId = builtObject.GetInstanceID();
        if (Controllers.ContainsKey(instanceId))
            return;

        var controller = new UsableComputerController(builtObject);
        Controllers.Add(instanceId, controller);
        controller.TryInitialize();
    }

    internal static void Update()
    {
        if (Controllers.Count == 0)
            return;

        var snapshot = new List<KeyValuePair<int, UsableComputerController>>(Controllers);
        foreach (KeyValuePair<int, UsableComputerController> entry in snapshot)
        {
            entry.Value.Tick(Time.unscaledTime);
            if (entry.Value.IsDisposed)
                Controllers.Remove(entry.Key);
        }
    }

    internal static void CloseOther(UsableComputerController controller)
    {
        if (_activeController != null && _activeController != controller)
            _activeController.Close();

        _activeController = controller;
    }

    internal static void NotifyClosed(UsableComputerController controller)
    {
        if (_activeController == controller)
            _activeController = null;
    }

    internal static void DisposeAll()
    {
        var snapshot = new List<UsableComputerController>(Controllers.Values);
        foreach (UsableComputerController controller in snapshot)
            controller.Dispose();

        Controllers.Clear();
        _activeController = null;
    }
}
