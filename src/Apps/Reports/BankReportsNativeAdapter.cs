using System;

#if IL2CPPMELON
using S1MoneyManager = Il2CppScheduleOne.Money.MoneyManager;
using S1TimeManager = Il2CppScheduleOne.GameTime.TimeManager;
using S1LoadManager = Il2CppScheduleOne.Persistence.LoadManager;
#else
using S1MoneyManager = ScheduleOne.Money.MoneyManager;
using S1TimeManager = ScheduleOne.GameTime.TimeManager;
using S1LoadManager = ScheduleOne.Persistence.LoadManager;
#endif

namespace UsableComputer.Apps.Reports;

internal sealed class BankReportsNativeAdapter
{
    private S1MoneyManager? _manager;
    private int _readCount;

    internal bool TryRead(BankReportBook book, out bool changed, out bool isHost, out float cash, out float bank)
    {
        changed = false;
        isHost = false;
        cash = bank = 0;
        var load = S1LoadManager.Instance;
        var manager = S1MoneyManager.Instance;
        var clock = S1TimeManager.Instance;
        if (load == null || !load.IsGameLoaded || load.IsLoading || manager == null || clock == null)
            return false;

        cash = manager.cashBalance;
        bank = manager.sync___get_value_onlineBalance();
        if (float.IsNaN(cash) || float.IsInfinity(cash) || float.IsNaN(bank) || float.IsInfinity(bank))
            return false;
        isHost = manager.IsServerInitialized;
        if (!isHost)
            return true;

        var ledger = manager.ledger;
        if (ledger == null)
            return false;
        // Native entries have no date. Never assign transactions from before observation to today.
        if (_manager != manager || _readCount > ledger.Count)
        {
            _manager = manager;
            _readCount = ledger.Count;
        }
        int day = clock.ElapsedDays;
        int time = clock.CurrentTime;
        changed = book.Observe(day, time);
        while (_readCount < ledger.Count)
        {
            var transaction = ledger[_readCount];
            if (transaction != null)
                changed |= book.Record(day, time, transaction.transaction_Name, transaction.total_Amount);
            _readCount++;
        }
        return true;
    }
}
