using System;
using UnityEngine;
using UnityEngine.UI;

namespace UsableComputer.UI;

/// <summary>
/// Small original landscape generated at runtime so the desktop has a recognizable, asset-free backdrop.
/// </summary>
internal sealed class RuntimeWallpaper : IDisposable
{
    private readonly GameObject _gameObject;
    private readonly Image _image;
    private readonly Texture2D _texture;
    private readonly Sprite _sprite;
    private bool _disposed;

    private RuntimeWallpaper(
        GameObject gameObject,
        Image image,
        Texture2D texture,
        Sprite sprite)
    {
        _gameObject = gameObject;
        _image = image;
        _texture = texture;
        _sprite = sprite;
    }

    internal static RuntimeWallpaper Create(Transform parent)
    {
        const int width = 256;
        const int height = 170;
        Texture2D texture = GenerateTexture(width, height, PreferencesStore.Appearance.Wallpaper);
        Sprite? sprite = null;
        GameObject? gameObject = null;
        try
        {
            sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, width, height),
                new Vector2(0.5f, 0.5f),
                100f);
            sprite.name = "UsableComputer_RuntimeWallpaperSprite";
            sprite.hideFlags = HideFlags.HideAndDontSave;

            gameObject = new GameObject("RuntimeWallpaper");
            gameObject.transform.SetParent(parent, false);
            RectTransform rectTransform = gameObject.AddComponent<RectTransform>();
            UiFactory.Stretch(rectTransform, Vector2.zero);
            Image image = gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.type = Image.Type.Simple;
            image.preserveAspect = false;
            image.raycastTarget = false;
            UiFactory.SetLayerRecursively(gameObject, Constants.UiLayer);
            return new RuntimeWallpaper(gameObject, image, texture, sprite);
        }
        catch
        {
            if (gameObject != null)
                UnityEngine.Object.Destroy(gameObject);
            if (sprite != null)
                UnityEngine.Object.Destroy(sprite);
            UnityEngine.Object.Destroy(texture);
            throw;
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _image.sprite = null;
        UnityEngine.Object.Destroy(_sprite);
        UnityEngine.Object.Destroy(_texture);

        // The desktop root owns this child, but destroying it here makes ownership explicit
        // when a shell is disposed before its root is torn down.
        if (_gameObject != null)
            UnityEngine.Object.Destroy(_gameObject);
    }

    internal void Apply(WallpaperStyle style)
    {
        if (_disposed)
            return;

        var pixels = new Color32[_texture.width * _texture.height];
        for (int y = 0; y < _texture.height; y++)
        {
            float vertical = (float)y / (_texture.height - 1);
            for (int x = 0; x < _texture.width; x++)
            {
                float horizontal = (float)x / (_texture.width - 1);
                pixels[(y * _texture.width) + x] = CreatePixel(horizontal, vertical, style);
            }
        }

        _texture.SetPixels32(pixels);
        _texture.Apply(updateMipmaps: false, makeNoLongerReadable: false);
    }

    private static Texture2D GenerateTexture(int width, int height, WallpaperStyle style)
    {
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.name = "UsableComputer_RuntimeWallpaperTexture";
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;
        texture.hideFlags = HideFlags.HideAndDontSave;

        var pixels = new Color32[width * height];
        for (int y = 0; y < height; y++)
        {
            float vertical = (float)y / (height - 1);
            for (int x = 0; x < width; x++)
            {
                float horizontal = (float)x / (width - 1);
                pixels[(y * width) + x] = CreatePixel(horizontal, vertical, style);
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, false);
        return texture;
    }

    private static Color32 CreatePixel(float horizontal, float vertical, WallpaperStyle style)
    {
        if (style == WallpaperStyle.BlueSky)
        {
            Color32 sky = Lerp(
                new Color32(66, 142, 216, 255),
                new Color32(20, 81, 174, 255),
                vertical);
            return DrawClouds(sky, horizontal, vertical);
        }

        const float skyLine = 0.43f;
        float distantRidge = skyLine
            + (0.035f * Mathf.Sin((horizontal * 6.1f) + 0.6f))
            + (0.018f * Mathf.Sin((horizontal * 15.2f) + 1.4f));
        float foregroundRidge = 0.28f
            + (0.065f * Mathf.Sin((horizontal * 4.7f) + 1.9f))
            + (0.024f * Mathf.Sin((horizontal * 18.4f) + 0.2f));

        Color32 color;
        if (vertical < foregroundRidge)
        {
            float light = 0.5f + (0.5f * Mathf.Sin((horizontal * 21f) + (vertical * 8f)));
            color = Lerp(
                new Color32(47, 132, 53, 255),
                new Color32(91, 171, 60, 255),
                0.25f + (light * 0.2f));
        }
        else if (vertical < distantRidge)
        {
            color = new Color32(110, 177, 92, 255);
        }
        else
        {
            float skyAmount = Mathf.InverseLerp(skyLine, 1f, vertical);
            color = Lerp(
                new Color32(164, 218, 238, 255),
                new Color32(51, 143, 220, 255),
                skyAmount);
            color = DrawClouds(color, horizontal, vertical);
        }

        if (style == WallpaperStyle.Twilight)
        {
            Color32 dusk = Lerp(
                new Color32(26, 37, 73, 255),
                new Color32(112, 70, 112, 255),
                Mathf.Clamp01(vertical * 0.8f));
            color = Lerp(color, dusk, 0.72f);
        }

        return color;
    }

    private static Color32 DrawClouds(Color32 background, float horizontal, float vertical)
    {
        float firstCloud = CloudMask(horizontal, vertical, 0.2f, 0.76f, 0.12f, 0.035f);
        float secondCloud = CloudMask(horizontal, vertical, 0.73f, 0.68f, 0.15f, 0.04f);
        float cloudAmount = Mathf.Max(firstCloud, secondCloud) * 0.62f;
        return Lerp(background, new Color32(248, 252, 250, 255), cloudAmount);
    }

    private static float CloudMask(
        float horizontal,
        float vertical,
        float centerX,
        float centerY,
        float radiusX,
        float radiusY)
    {
        float x = (horizontal - centerX) / radiusX;
        float y = (vertical - centerY) / radiusY;
        float distance = (x * x) + (y * y);
        return Mathf.Clamp01(1f - distance);
    }

    private static Color32 Lerp(Color32 from, Color32 to, float amount)
    {
        amount = Mathf.Clamp01(amount);
        return new Color32(
            (byte)Mathf.RoundToInt(Mathf.Lerp(from.r, to.r, amount)),
            (byte)Mathf.RoundToInt(Mathf.Lerp(from.g, to.g, amount)),
            (byte)Mathf.RoundToInt(Mathf.Lerp(from.b, to.b, amount)),
            255);
    }
}
