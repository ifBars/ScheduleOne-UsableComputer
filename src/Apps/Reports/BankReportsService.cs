using System;
using UnityEngine;

namespace UsableComputer.Apps.Reports;

internal static class BankReportsService
{
    private static BankReportsNativeAdapter _adapter = new();
    private static float _nextRefresh;
    private static bool _active;
    private static bool _reportedFailure;

    internal static BankReportBook Book { get; private set; } = new();
    internal static bool Available { get; private set; }
    internal static bool IsHost { get; private set; }
    internal static decimal Cash { get; private set; }
    internal static decimal Bank { get; private set; }
    internal static int Revision { get; private set; }

    internal static void PrepareForLoad()
    {
        Stop();
        Book = new BankReportBook();
        UsableComputerReportsSave.Capture(Book.CreateSnapshot());
        Revision++;
    }

    internal static void Start()
    {
        _active = true;
        _nextRefresh = 0;
        Update();
    }

    internal static void Stop()
    {
        _active = Available = IsHost = false;
        Cash = Bank = 0;
        _adapter = new BankReportsNativeAdapter();
        _reportedFailure = false;
    }

    internal static void Load(BankReportSnapshot? snapshot)
    {
        try { Book = new BankReportBook(snapshot); }
        catch (ArgumentException exception)
        {
            MelonLoader.MelonLogger.Warning($"[{Constants.ModName}] Bank reports could not be loaded: {exception.Message}");
            Book = new BankReportBook();
        }
        Revision++;
    }

    internal static void Update()
    {
        if (!_active || Time.unscaledTime < _nextRefresh)
            return;
        _nextRefresh = Time.unscaledTime + 0.5f;
        try
        {
            bool available = _adapter.TryRead(Book, out bool changed, out bool host, out float cash, out float bank);
            decimal newCash = available ? (decimal)cash : 0;
            decimal newBank = available ? (decimal)bank : 0;
            if (changed || Available != available || IsHost != host || Cash != newCash || Bank != newBank)
                Revision++;
            Available = available;
            IsHost = host;
            Cash = newCash;
            Bank = newBank;
            if (changed)
                UsableComputerReportsSave.Capture(Book.CreateSnapshot());
        }
        catch (Exception exception)
        {
            if (Available)
                Revision++;
            Available = false;
            if (!_reportedFailure)
            {
                _reportedFailure = true;
                MelonLoader.MelonLogger.Warning($"[{Constants.ModName}] Bank reporting unavailable: {exception.Message}");
            }
        }
    }

    internal static void Flush()
    {
        _nextRefresh = 0;
        Update();
    }
}
