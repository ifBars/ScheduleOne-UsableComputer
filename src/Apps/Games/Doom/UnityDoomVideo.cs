using System;
using ManagedDoom;
using ManagedDoom.Video;
using UnityEngine;
using DoomRenderer = ManagedDoom.Video.Renderer;

namespace UsableComputer.Doom;

internal sealed class UnityDoomVideo : IVideo, IDisposable
{
    private readonly DoomRenderer _renderer;
    private readonly byte[] _frameBuffer;
    private bool _focused;
    private bool _disposed;

    internal UnityDoomVideo(Config config, GameContent content)
    {
        _renderer = new DoomRenderer(config, content);
        _frameBuffer = new byte[4 * _renderer.Width * _renderer.Height];
        Texture = new Texture2D(_renderer.Height, _renderer.Width, TextureFormat.RGBA32, mipChain: false)
        {
            name = "UsableComputer_DoomFrameBuffer",
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
        };
    }

    internal Texture2D Texture { get; }

    internal void SetFocused(bool focused) => _focused = focused;

    public void Render(ManagedDoom.Doom doom)
    {
        if (_disposed)
            return;

        _renderer.Render(doom, _frameBuffer);
        Texture.LoadRawTextureData(_frameBuffer);
        Texture.Apply(updateMipmaps: false, makeNoLongerReadable: false);
    }

    public void InitializeWipe() => _renderer.InitializeWipe();

    public bool HasFocus() => _focused;

    public int MaxWindowSize => _renderer.MaxWindowSize;

    public int WindowSize
    {
        get => _renderer.WindowSize;
        set => _renderer.WindowSize = value;
    }

    public bool DisplayMessage
    {
        get => _renderer.DisplayMessage;
        set => _renderer.DisplayMessage = value;
    }

    public int MaxGammaCorrectionLevel => _renderer.MaxGammaCorrectionLevel;

    public int GammaCorrectionLevel
    {
        get => _renderer.GammaCorrectionLevel;
        set => _renderer.GammaCorrectionLevel = value;
    }

    public int WipeBandCount => _renderer.WipeBandCount;

    public int WipeHeight => _renderer.WipeHeight;

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        if (Texture != null)
            UnityEngine.Object.Destroy(Texture);
    }
}
