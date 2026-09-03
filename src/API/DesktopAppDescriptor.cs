using System;
using UnityEngine;

namespace UsableComputer.API;

/// <summary>
/// Stable metadata and a per-window factory for an app shown by Usable Computer.
/// </summary>
public sealed class DesktopAppDescriptor
{
    public DesktopAppDescriptor(
        string id,
        string title,
        string glyph,
        Vector2 preferredWindowSize,
        Vector2 preferredWindowPosition,
        Func<DesktopAppContext, IDesktopAppSession> createSession)
        : this(
            id,
            title,
            glyph,
            preferredWindowSize,
            preferredWindowPosition,
            createSession,
            null)
    {
    }

    public DesktopAppDescriptor(
        string id,
        string title,
        string glyph,
        Vector2 preferredWindowSize,
        Vector2 preferredWindowPosition,
        Func<DesktopAppContext, IDesktopAppSession> createSession,
        Func<Sprite?>? resolveIcon)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("An app id is required.", nameof(id));
        if (!string.Equals(id, id.Trim(), StringComparison.Ordinal))
            throw new ArgumentException("An app id cannot start or end with whitespace.", nameof(id));
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("An app title is required.", nameof(title));
        if (preferredWindowSize.x <= 0f || preferredWindowSize.y <= 0f)
            throw new ArgumentOutOfRangeException(nameof(preferredWindowSize), "Window size must be positive.");

        Id = id;
        Title = title.Trim();
        Glyph = glyph?.Trim() ?? string.Empty;
        PreferredWindowSize = preferredWindowSize;
        PreferredWindowPosition = preferredWindowPosition;
        CreateSession = createSession ?? throw new ArgumentNullException(nameof(createSession));
        ResolveIcon = resolveIcon;
    }

    public string Id { get; }

    public string Title { get; }

    /// <summary>
    /// Legacy text icon metadata retained for source compatibility. The desktop always renders an image icon.
    /// </summary>
    public string Glyph { get; }

    public Vector2 PreferredWindowSize { get; }

    public Vector2 PreferredWindowPosition { get; }

    public Func<DesktopAppContext, IDesktopAppSession> CreateSession { get; }

    /// <summary>
    /// Optional runtime icon provider. This supports native and mod-owned sprites without taking asset ownership.
    /// </summary>
    public Func<Sprite?>? ResolveIcon { get; }
}
