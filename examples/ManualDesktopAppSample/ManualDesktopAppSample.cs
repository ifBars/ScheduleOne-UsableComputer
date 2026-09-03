using MelonLoader;
using UsableComputer.API;
using UnityEngine;
using UnityEngine.UI;

[assembly: MelonInfo(
    typeof(UsableComputer.ManualDesktopAppSample.ManualDesktopAppMod),
    "Usable Computer Manual Sample",
    "1.0.0",
    "Bars")]
[assembly: MelonGame("TVGS", "Schedule I")]

namespace UsableComputer.ManualDesktopAppSample;

public sealed class ManualDesktopAppMod : MelonMod
{
    private const string AppId = "manual-sample";
    private bool _registered;

    public override void OnInitializeMelon()
    {
        try
        {
            DesktopAppRegistry.Register(
                new DesktopAppDescriptor(
                    AppId,
                    "Sample App",
                    "S",
                    new Vector2(420f, 280f),
                    new Vector2(0f, 0f),
                    context => new ManualDesktopAppSession(context)));
            _registered = true;
        }
        catch (InvalidOperationException exception)
        {
            MelonLogger.Error($"Could not register '{AppId}': {exception.Message}");
        }
    }

    public override void OnDeinitializeMelon()
    {
        if (_registered)
            DesktopAppRegistry.Unregister(AppId);
    }
}

internal sealed class ManualDesktopAppSession : IDesktopAppSession
{
    private readonly Text _message;

    internal ManualDesktopAppSession(DesktopAppContext context)
    {
        GameObject panel = new GameObject("ManualSamplePanel");
        panel.transform.SetParent(context.Container, false);
        RectTransform panelRect = panel.AddComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = new Vector2(18f, 18f);
        panelRect.offsetMax = new Vector2(-18f, -18f);
        Image panelImage = panel.AddComponent<Image>();
        panelImage.color = new Color(0.97f, 0.96f, 0.9f, 1f);

        GameObject messageObject = new GameObject("Message");
        messageObject.transform.SetParent(panel.transform, false);
        RectTransform messageRect = messageObject.AddComponent<RectTransform>();
        messageRect.anchorMin = new Vector2(0f, 0.5f);
        messageRect.anchorMax = new Vector2(1f, 1f);
        messageRect.offsetMin = new Vector2(16f, 0f);
        messageRect.offsetMax = new Vector2(-16f, -16f);
        _message = messageObject.AddComponent<Text>();
        _message.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        _message.fontSize = 18;
        _message.alignment = TextAnchor.MiddleCenter;
        _message.color = new Color(0.08f, 0.12f, 0.2f, 1f);
        _message.text = "This app was registered by a separate mod.";

        GameObject buttonObject = new GameObject("Close");
        buttonObject.transform.SetParent(panel.transform, false);
        RectTransform buttonRect = buttonObject.AddComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(0.5f, 0f);
        buttonRect.anchorMax = new Vector2(0.5f, 0f);
        buttonRect.pivot = new Vector2(0.5f, 0f);
        buttonRect.sizeDelta = new Vector2(150f, 36f);
        buttonRect.anchoredPosition = new Vector2(0f, 18f);
        Image buttonImage = buttonObject.AddComponent<Image>();
        buttonImage.color = new Color(0.08f, 0.31f, 0.78f, 1f);
        Button button = buttonObject.AddComponent<Button>();
        button.targetGraphic = buttonImage;

        GameObject buttonLabelObject = new GameObject("Label");
        buttonLabelObject.transform.SetParent(buttonObject.transform, false);
        RectTransform buttonLabelRect = buttonLabelObject.AddComponent<RectTransform>();
        buttonLabelRect.anchorMin = Vector2.zero;
        buttonLabelRect.anchorMax = Vector2.one;
        buttonLabelRect.offsetMin = Vector2.zero;
        buttonLabelRect.offsetMax = Vector2.zero;
        Text buttonLabel = buttonLabelObject.AddComponent<Text>();
        buttonLabel.font = _message.font;
        buttonLabel.fontSize = 16;
        buttonLabel.alignment = TextAnchor.MiddleCenter;
        buttonLabel.color = Color.white;
        buttonLabel.text = "Close window";

        context.Bind(button, context.RequestClose);
        context.RegisterCleanup(() => UnityEngine.Object.Destroy(panel));
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
}
