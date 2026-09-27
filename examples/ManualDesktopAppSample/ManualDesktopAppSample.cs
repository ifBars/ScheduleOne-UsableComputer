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
    private Texture2D? _iconTexture;
    private Sprite? _icon;

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
                    context => new ManualDesktopAppSession(context),
                    resolveIcon: ResolveIcon));
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
        if (_icon != null)
            UnityEngine.Object.Destroy(_icon);
        if (_iconTexture != null)
            UnityEngine.Object.Destroy(_iconTexture);
        _icon = null;
        _iconTexture = null;
    }

    private Sprite ResolveIcon()
    {
        if (_icon != null)
            return _icon;

        // Create once on demand; the registering mod retains ownership of its artwork.
        const int size = 32;
        _iconTexture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "ManualSampleIconTexture",
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
        };
        var pixels = new Color32[size * size];
        for (int y = 4; y < 28; y++)
        for (int x = 4; x < 28; x++)
        {
            bool border = x < 7 || x > 24 || y < 7 || y > 24;
            bool mark = y >= 10 && y <= 21 && (x == 11 || x == 20 || y == 15);
            pixels[y * size + x] = border ? new Color32(21, 55, 109, 255)
                : mark ? new Color32(255, 255, 255, 255) : new Color32(38, 115, 196, 255);
        }
        _iconTexture.SetPixels32(pixels);
        _iconTexture.Apply(false, true);
        _icon = Sprite.Create(_iconTexture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        _icon.name = "ManualSampleIcon";
        return _icon;
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
