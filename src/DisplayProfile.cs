using System;

namespace UsableComputer;

/// <summary>
/// Defines the physical and rendered dimensions of the usable computer display.
/// </summary>
internal static class DisplayProfile
{
    internal const float ModelScale = 1.15f;
    internal const float InteractionFieldOfView = 55f;
    internal const int NestedWidth = 960;
    internal const int NestedHeight = 540;

    internal static float GetInteractionFieldOfView(float aspect)
    {
        const float referenceAspect = 16f / 9f;
        if (float.IsNaN(aspect) || float.IsInfinity(aspect) || aspect <= 0f)
            aspect = referenceAspect;
        // Preserve the established vertical view on wide screens and horizontal coverage on narrow ones.
        double halfAngle = InteractionFieldOfView * Math.PI / 360d;
        return (float)(2d * Math.Atan(Math.Tan(halfAngle) * Math.Max(1d, referenceAspect / aspect)) * 180d / Math.PI);
    }
}
