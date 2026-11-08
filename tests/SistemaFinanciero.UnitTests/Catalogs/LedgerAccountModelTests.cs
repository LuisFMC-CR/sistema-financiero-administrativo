using SistemaFinanciero.Application.Catalogs.LedgerAccounts;
using SistemaFinanciero.Domain.Catalogs;

namespace SistemaFinanciero.UnitTests.Catalogs;

public sealed class LedgerAccountModelTests
{
    [Fact]
    public void CanDeactivate_ShowsTheActionForASystemAccountWithHistoricalTransactions()
    {
        DateTimeOffset now = new(2026, 11, 7, 21, 0, 0, TimeSpan.Zero);
        LedgerAccountModel account = new(
            Guid.NewGuid(),
            "4.1.01",
            "Ingresos por servicios",
            LedgerAccountType.Income,
            Guid.NewGuid(),
            "4.1",
            "Ingresos por servicios",
            CashKind: null,
            Reference: null,
            Description: null,
            IsActive: true,
            IsProtected: false,
            IsMovement: true,
            SystemRole: LedgerAccountSystemRole.ReceivablesFromCustomers,
            Balance: 150_000m,
            HasTransactions: true,
            CreatedAtUtc: now,
            UpdatedAtUtc: now,
            Version: "version");

        Assert.True(account.CanDeactivate);
    }
}
