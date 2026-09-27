using UnityEngine;
using UsableComputer.API;
using UsableComputer.UI;

#if IL2CPPMELON
using S1Text = Il2CppTMPro.TextMeshProUGUI;
#elif MONOMELON
using S1Text = TMPro.TextMeshProUGUI;
#endif

namespace UsableComputer.Apps.About;

internal sealed class AboutApp : IDesktopAppSession
{
    internal AboutApp(DesktopAppContext context)
    {
        Build(context.Container, context.Listeners);
    }

    public void OnOpened()
    {
    }

    public void OnClosed()
    {
    }

    public void OnTick()
    {
    }

    public void Dispose()
    {
    }

    internal void Build(Transform parent, UiListenerRegistry listeners)
    {
        S1Text heading = UiFactory.CreateText(
            parent,
            "Heading",
            "About this computer",
            21f,
            UiFactory.TextPrimary,
            GetHeadingAlignment(),
            bold: true);
        heading.rectTransform.anchorMin = new Vector2(0f, 1f);
        heading.rectTransform.anchorMax = new Vector2(1f, 1f);
        heading.rectTransform.pivot = new Vector2(0.5f, 1f);
        heading.rectTransform.sizeDelta = new Vector2(-24f, 34f);
        heading.rectTransform.anchoredPosition = new Vector2(0f, -12f);

        S1Text body = UiFactory.CreateText(
            parent,
            "Body",
            "Usable Computer\n\nA working desktop and laptop for Schedule I.\n\nDocuments and app storage belong to your save. Appearance settings are shared across saves.\n\nRuntime: " + Constants.RuntimeName + "\nVersion: " + Constants.ModVersion,
            16f,
            UiFactory.TextMuted,
            GetBodyAlignment());
        body.rectTransform.anchorMin = Vector2.zero;
        body.rectTransform.anchorMax = Vector2.one;
        body.rectTransform.offsetMin = new Vector2(18f, 18f);
        body.rectTransform.offsetMax = new Vector2(-18f, -62f);
    }

#if IL2CPPMELON
    private static Il2CppTMPro.TextAlignmentOptions GetHeadingAlignment() => Il2CppTMPro.TextAlignmentOptions.MidlineLeft;
    private static Il2CppTMPro.TextAlignmentOptions GetBodyAlignment() => Il2CppTMPro.TextAlignmentOptions.TopLeft;
#else
    private static TMPro.TextAlignmentOptions GetHeadingAlignment() => TMPro.TextAlignmentOptions.MidlineLeft;
    private static TMPro.TextAlignmentOptions GetBodyAlignment() => TMPro.TextAlignmentOptions.TopLeft;
#endif
}
