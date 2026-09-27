using System;
using System.Collections.Generic;
using S1API.Utils;
using UnityEngine.Events;
using UnityEngine.EventSystems;

namespace UsableComputer.UI;

/// <summary>
/// Keeps every S1API-managed UI subscription owned by one desktop instance.
/// </summary>
internal sealed class UiListenerRegistry : IDisposable
{
    private readonly List<Action> _removers = new();
    private bool _disposed;

    internal void Add(Action listener, UnityEvent unityEvent)
    {
        if (_disposed)
            return;

        // S1API keys subscriptions by delegate, not by event. Give each binding its
        // own delegate so shared actions (Back/Up, PointerUp/EndDrag) all subscribe.
        Action ownedListener = () => listener();
        EventHelper.AddListener(ownedListener, unityEvent);
        _removers.Add(() => EventHelper.RemoveListener(ownedListener, unityEvent));
    }

    internal void Add<T>(Action<T> listener, UnityEvent<T> unityEvent)
    {
        if (_disposed)
            return;

        Action<T> ownedListener = value => listener(value);
        EventHelper.AddListener(ownedListener, unityEvent);
        _removers.Add(() => EventHelper.RemoveListener(ownedListener, unityEvent));
    }

    internal void AddTrigger(
        EventTrigger trigger,
        EventTriggerType eventType,
        Action<BaseEventData> listener)
    {
        if (_disposed)
            return;

        Action<BaseEventData> ownedListener = value => listener(value);
        EventHelper.AddEventTrigger(trigger, eventType, ownedListener);
        _removers.Add(() => EventHelper.RemoveEventTrigger(trigger, eventType, ownedListener));
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        for (int index = _removers.Count - 1; index >= 0; index--)
        {
            try
            {
                _removers[index]();
            }
            catch
            {
                // The Unity object may already have been destroyed during a scene change.
            }
        }

        _removers.Clear();
    }
}
