using System;

namespace UsableComputer.API;

/// <summary>
/// Lifecycle owned by one desktop window. A new instance is created for every opened window.
/// </summary>
public interface IDesktopAppSession : IDisposable
{
    void OnOpened();

    void OnClosed();

    void OnTick();
}
