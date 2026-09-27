using System;
using HarmonyLib;
using UnityEngine;

#if IL2CPPMELON
using S1IconGenerator = Il2CppScheduleOne.DevUtilities.IconGenerator;
#else
using S1IconGenerator = ScheduleOne.DevUtilities.IconGenerator;
#endif

namespace UsableComputer.Content;

/// <summary>Adapts our S1API furniture previews to the beta's renamed render layer.</summary>
[HarmonyPatch(typeof(S1IconGenerator), "GetTexture")]
internal static class ComputerIconCompatibility
{
    private static void Prefix(Transform model, out LayerState? __state)
    {
        __state = null;
        int layer = LayerMask.NameToLayer("RuntimePreviewGeneration");
        if (layer < 0 || model == null || !model.name.EndsWith("_IconPreview", StringComparison.Ordinal))
            return;

        Transform[] transforms = model.GetComponentsInChildren<Transform>(true);
        bool isComputer = false;
        foreach (Transform child in transforms)
            isComputer |= child.name == Constants.ScreenAnchorName;
        if (!isComputer)
            return;

        __state = new LayerState(transforms);
        foreach (Transform child in transforms)
            child.gameObject.layer = layer;
    }

    private static Exception? Finalizer(Exception? __exception, LayerState? __state)
    {
        __state?.Restore();
        return __exception;
    }

    private sealed class LayerState
    {
        private readonly Transform[] _transforms;
        private readonly int[] _layers;

        internal LayerState(Transform[] transforms)
        {
            _transforms = transforms;
            _layers = new int[transforms.Length];
            for (int index = 0; index < transforms.Length; index++)
                _layers[index] = transforms[index].gameObject.layer;
        }

        internal void Restore()
        {
            for (int index = 0; index < _transforms.Length; index++)
                if (_transforms[index] != null)
                    _transforms[index].gameObject.layer = _layers[index];
        }
    }
}
