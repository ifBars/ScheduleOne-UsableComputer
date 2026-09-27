using System;
using System.Collections.Generic;
using S1API.Building;
using UnityEngine;

namespace UsableComputer.Hardware;

internal static class PlacedComputers
{
    private static readonly Dictionary<int, ComputerController> Controllers = new();
    private static ComputerController? _activeController;

    internal static void Attach(BuildEventArgs args)
    {
        if (args == null ||
            (!string.Equals(args.ItemId, Constants.ItemId, StringComparison.Ordinal) &&
             !string.Equals(args.ItemId, Constants.LaptopItemId, StringComparison.Ordinal)))
            return;

        GameObject? builtObject = args.GameObject;
        if (builtObject == null)
            return;

        int instanceId = builtObject.GetInstanceID();
        if (Controllers.ContainsKey(instanceId))
            return;

        var controller = new ComputerController(builtObject);
        Controllers.Add(instanceId, controller);
        controller.TryInitialize();
    }

    internal static void Update()
    {
        if (Controllers.Count == 0)
            return;

        var snapshot = new List<KeyValuePair<int, ComputerController>>(Controllers);
        foreach (KeyValuePair<int, ComputerController> entry in snapshot)
        {
            entry.Value.Tick(Time.unscaledTime);
            if (entry.Value.IsDisposed)
                Controllers.Remove(entry.Key);
        }
    }

    internal static void CloseOther(ComputerController controller)
    {
        if (_activeController != null && _activeController != controller)
            _activeController.Close();

        _activeController = controller;
    }

    internal static void NotifyClosed(ComputerController controller)
    {
        if (_activeController == controller)
            _activeController = null;
    }

    internal static void DisposeAll()
    {
        var snapshot = new List<ComputerController>(Controllers.Values);
        foreach (ComputerController controller in snapshot)
            controller.Dispose();

        Controllers.Clear();
        _activeController = null;
    }
}
