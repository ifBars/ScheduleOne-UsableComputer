using System;
using S1API.ExternalHosting;
using UsableComputer.API;
using UsableComputer.UI;
using UnityEngine;

namespace UsableComputer.Bridge;

/// <summary>Owns only the external session; it never invokes device open or close methods.</summary>
internal sealed class ExternalAppDesktopSession : IDesktopAppSession
{
    private readonly IExternalAppSession _session;
    private readonly GameObject _container;
    private bool _opened;
    private bool _disposed;

    internal ExternalAppDesktopSession(ExternalAppRegistration registration, DesktopAppContext context)
    {
        _container = context.Container.gameObject;
        _session = registration.Host.CreateExternalSession(context.Container.gameObject, context.RequestClose)
            ?? throw new InvalidOperationException($"External app '{registration.Id}' returned no session.");
        UiFactory.SetLayerRecursively(_container, Constants.UiLayer);
    }

    public void OnOpened()
    {
        if (_disposed)
            return;
        _opened = true;
        _session.Open();
        UiFactory.SetLayerRecursively(_container, Constants.UiLayer);
    }

    public void OnTick()
    {
        if (_opened && !_disposed)
        {
            _session.Tick();
            // An external app may add controls during its own update.
            UiFactory.SetLayerRecursively(_container, Constants.UiLayer);
        }
    }

    public void OnClosed()
    {
        if (!_opened || _disposed)
            return;
        _opened = false;
        _session.Close();
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        try
        {
            if (_opened)
            {
                _opened = false;
                _session.Close();
            }
        }
        finally
        {
            _session.Dispose();
        }
    }
}
