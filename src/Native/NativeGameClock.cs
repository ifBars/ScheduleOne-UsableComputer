using System;

#if IL2CPPMELON
using S1TimeManager = Il2CppScheduleOne.GameTime.TimeManager;
#elif MONOMELON
using S1TimeManager = ScheduleOne.GameTime.TimeManager;
#endif

namespace UsableComputer.Native;

internal sealed class NativeGameClock
{
    private S1TimeManager? _manager;
    private int _lastTime = -1;
    private string _lastFormattedTime = string.Empty;

    internal bool TryGetFormattedTime(out string formattedTime)
    {
        S1TimeManager? manager = _manager;
        if (manager == null)
        {
            manager = S1TimeManager.Instance;
            _manager = manager;
        }

        if (manager == null)
        {
            formattedTime = string.Empty;
            return false;
        }

        try
        {
            int currentTime = manager.CurrentTime;
            if (currentTime != _lastTime)
            {
                _lastTime = currentTime;
                _lastFormattedTime = S1TimeManager.Get12HourTime(currentTime, true);
            }

            formattedTime = _lastFormattedTime;
            return !string.IsNullOrWhiteSpace(formattedTime);
        }
        catch (Exception)
        {
            // A scene transition can invalidate the cached native manager for one frame.
            _manager = null;
            _lastTime = -1;
            _lastFormattedTime = string.Empty;
            formattedTime = string.Empty;
            return false;
        }
    }
}
