using S1API.Internal.Abstraction;
using S1API.Saveables;
using UsableComputer.Reports;

namespace UsableComputer.Persistence;

public sealed class UsableComputerReportsSave : Saveable
{
    private static UsableComputerReportsSave? _instance;

    [SaveableField("bank-reports")]
    private BankReportSnapshot _snapshot = new();

    public UsableComputerReportsSave() => _instance = this;

    internal static void Capture(BankReportSnapshot snapshot)
    {
        if (_instance != null)
            _instance._snapshot = snapshot;
    }

    protected override void OnCreated() => BankReportsService.Load(_snapshot);
    protected override void OnLoaded() => BankReportsService.Load(_snapshot);
}
