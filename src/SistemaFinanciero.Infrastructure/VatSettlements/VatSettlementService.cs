using Microsoft.EntityFrameworkCore;
using SistemaFinanciero.Application.Catalogs.Common;
using SistemaFinanciero.Application.VatSettlements;
using SistemaFinanciero.Domain.Audit;
using SistemaFinanciero.Domain.Catalogs;
using SistemaFinanciero.Domain.Ledger;
using SistemaFinanciero.Domain.Parameters;
using SistemaFinanciero.Domain.VatSettlements;
using SistemaFinanciero.Infrastructure.Catalogs;
using SistemaFinanciero.Infrastructure.Ledger;
using SistemaFinanciero.Infrastructure.Parameters;
using SistemaFinanciero.Infrastructure.Persistence;

namespace SistemaFinanciero.Infrastructure.VatSettlements;

/// <summary>
/// Calcula, registra, consulta y anula las liquidaciones mensuales del IVA (paquete v8, bloque 2).
/// </summary>
/// <remarks>
/// Conexiones: implementa <c>IVatSettlementService</c> (Application); se registra en
/// <c>DependencyInjection.AddInfrastructure</c>; lo consume <c>VatSettlementsController</c> (Web,
/// módulo Impuestos y retenciones). El cálculo lee directamente de <c>finanzas.AsientoLineas</c> de
/// las cuentas de sistema <see cref="LedgerAccountSystemRole.VatPayable"/> y
/// <see cref="LedgerAccountSystemRole.VatCreditable"/>, sin depender de cuándo se registraron las
/// liquidaciones anteriores: lo que se registre tarde con fecha de un mes ya liquidado entra
/// automáticamente en la liquidación siguiente, y el saldo a favor se arrastra solo.
/// </remarks>
internal sealed class VatSettlementService(
    FinancialDbContext dbContext,
    TimeProvider timeProvider) : IVatSettlementService
{
    public async Task<VatSettlementPreview> PreviewAsync(
        int periodYear,
        int periodMonth,
        CancellationToken cancellationToken = default)
    {
        DateOnly periodStart = new(periodYear, periodMonth, 1);
        DateOnly cutoff = periodStart.AddMonths(1).AddDays(-1);

        (decimal accumulatedPayable, decimal accumulatedCreditable) = await ComputeAccumulatedAmountsAsync(
            cutoff, cancellationToken);
        (decimal priorPayable, decimal priorCreditable) = await SumPriorActiveSettlementsAsync(
            periodYear, periodMonth, cancellationToken);
        (decimal thisPeriodPayable, decimal thisPeriodCreditable) = await ComputePeriodOwnAmountsAsync(
            periodStart, cutoff, cancellationToken);

        decimal vatPayableAmount = accumulatedPayable - priorPayable;
        decimal vatCreditAvailable = accumulatedCreditable - priorCreditable;

        (decimal creditApplied, decimal paymentAmount, decimal carriedForward) = Allocate(
            vatPayableAmount, vatCreditAvailable);

        bool canRegister = vatPayableAmount > 0m;

        return new VatSettlementPreview(
            periodYear,
            periodMonth,
            thisPeriodPayable,
            vatPayableAmount - thisPeriodPayable,
            vatPayableAmount,
            thisPeriodCreditable,
            vatCreditAvailable - thisPeriodCreditable,
            vatCreditAvailable,
            creditApplied,
            paymentAmount,
            carriedForward,
            canRegister,
            canRegister
                ? null
                : "No hay IVA por pagar en este período; el IVA acreditable se acumula para el siguiente.");
    }

    public async Task<VatSettlementModel?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        VatSettlement? settlement = await dbContext.Set<VatSettlement>()
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);

        if (settlement is null)
        {
            return null;
        }

        string? cashAccountName = settlement.CashAccountId is Guid cashAccountId
            ? await GetCashAccountNameAsync(cashAccountId, cancellationToken)
            : null;

        return Map(settlement, cashAccountName);
    }

    public async Task<PagedResult<VatSettlementModel>> SearchAsync(
        VatSettlementQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        IQueryable<VatSettlement> settlements = dbContext.Set<VatSettlement>().AsNoTracking();

        settlements = query.Status switch
        {
            VatSettlementStatusFilter.Active => settlements.Where(settlement => settlement.Status == VatSettlementStatus.Active),
            VatSettlementStatusFilter.Cancelled => settlements.Where(settlement => settlement.Status == VatSettlementStatus.Cancelled),
            VatSettlementStatusFilter.All => settlements,
            _ => throw new ArgumentOutOfRangeException(nameof(query)),
        };

        if (query.PeriodYear is int periodYear)
        {
            settlements = settlements.Where(settlement => settlement.PeriodYear == periodYear);
        }

        PagedResult<VatSettlement> page = await CatalogPersistence.ToPageAsync(
            settlements
                .OrderByDescending(settlement => settlement.PeriodYear)
                .ThenByDescending(settlement => settlement.PeriodMonth),
            query.Page,
            query.PageSize,
            cancellationToken);

        Guid[] cashAccountIds = page.Items
            .Where(settlement => settlement.CashAccountId is not null)
            .Select(settlement => settlement.CashAccountId!.Value)
            .Distinct()
            .ToArray();
        IReadOnlyDictionary<Guid, string> cashAccountNames = await dbContext.Set<LedgerAccount>()
            .AsNoTracking()
            .Where(account => cashAccountIds.Contains(account.Id))
            .ToDictionaryAsync(account => account.Id, account => account.Name, cancellationToken);

        return new PagedResult<VatSettlementModel>(
            page.Items
                .Select(settlement => Map(
                    settlement,
                    settlement.CashAccountId is Guid id ? cashAccountNames.GetValueOrDefault(id, "—") : null))
                .ToArray(),
            page.Page,
            page.PageSize,
            page.TotalCount);
    }

    public async Task<RegisterVatSettlementResult> RegisterAsync(
        RegisterVatSettlementCommand command,
        Guid actorId,
        bool actorIsManagement,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (command.PeriodMonth is < 1 or > 12)
        {
            return Rejected("El mes del período no es válido.");
        }

        VatSettlement? lastActive = await dbContext.Set<VatSettlement>()
            .AsNoTracking()
            .Where(settlement => settlement.Status == VatSettlementStatus.Active)
            .OrderByDescending(settlement => settlement.PeriodYear)
            .ThenByDescending(settlement => settlement.PeriodMonth)
            .FirstOrDefaultAsync(cancellationToken);

        if (lastActive is not null
            && PeriodKey(command.PeriodYear, command.PeriodMonth) <= PeriodKey(lastActive.PeriodYear, lastActive.PeriodMonth))
        {
            return Rejected(
                $"Ya existe una liquidación vigente de {lastActive.PeriodMonth:D2}/{lastActive.PeriodYear} o de un período posterior; "
                + "no se puede liquidar un período igual o anterior.");
        }

        DateOnly periodStart = new(command.PeriodYear, command.PeriodMonth, 1);
        DateOnly cutoff = periodStart.AddMonths(1).AddDays(-1);

        if (command.SettlementDate <= cutoff)
        {
            return Rejected("La fecha de la liquidación debe ser posterior a la fecha de corte del período.");
        }

        string? closingDateViolation = await ClosingDateGuard.GetViolationMessageAsync(
            dbContext, command.SettlementDate, timeProvider, cancellationToken);

        if (closingDateViolation is not null)
        {
            return Rejected(closingDateViolation);
        }

        (decimal accumulatedPayable, decimal accumulatedCreditable) = await ComputeAccumulatedAmountsAsync(
            cutoff, cancellationToken);
        (decimal priorPayable, decimal priorCreditable) = await SumPriorActiveSettlementsAsync(
            command.PeriodYear, command.PeriodMonth, cancellationToken);

        decimal vatPayableAmount = accumulatedPayable - priorPayable;
        decimal vatCreditAvailable = accumulatedCreditable - priorCreditable;

        if (vatPayableAmount <= 0m)
        {
            return Rejected(
                "No hay IVA por pagar en este período; el IVA acreditable se acumula para el siguiente.");
        }

        (decimal creditApplied, decimal paymentAmount, decimal carriedForward) = Allocate(
            vatPayableAmount, vatCreditAvailable);

        if (!actorIsManagement)
        {
            SystemParameters? parameters = await dbContext.Set<SystemParameters>()
                .AsNoTracking()
                .SingleOrDefaultAsync(cancellationToken);

            decimal limit = parameters?.AuthorizationLimitCrc ?? 0m;

            if (paymentAmount > limit)
            {
                return Rejected(
                    "El pago de la liquidación supera el límite de autorización; solo Gerencia puede registrarla.");
            }
        }

        if (paymentAmount > 0m)
        {
            if (command.CashAccountId is not Guid cashAccountId || string.IsNullOrWhiteSpace(command.Reference))
            {
                return Rejected("La cuenta de efectivo y la referencia son obligatorias cuando hay pago a Hacienda.");
            }

            bool isValidCashAccount = await dbContext.Set<LedgerAccount>()
                .AsNoTracking()
                .AnyAsync(
                    account => account.Id == cashAccountId && account.IsActive && account.CashKind != null,
                    cancellationToken);

            if (!isValidCashAccount)
            {
                return Rejected("La cuenta de efectivo seleccionada no existe, está inactiva o no es de efectivo.");
            }
        }

        bool openPeriodWarning = IsPeriodStillOpen(command.PeriodYear, command.PeriodMonth);

        try
        {
            DateTimeOffset now = timeProvider.GetUtcNow();
            string code = await CatalogPersistence.GenerateSequentialCodeAsync(
                dbContext,
                "finanzas.Seq_LiquidacionIvaCodigo",
                "LIQ",
                6,
                cancellationToken);

            VatSettlement settlement = new(
                Guid.NewGuid(),
                code,
                command.PeriodYear,
                command.PeriodMonth,
                command.SettlementDate,
                vatPayableAmount,
                creditApplied,
                paymentAmount,
                carriedForward,
                paymentAmount > 0m ? command.CashAccountId : null,
                paymentAmount > 0m ? command.Reference : null,
                now,
                actorId);

            dbContext.Add(settlement);

            await JournalPostingService.PostVatSettlementAsync(dbContext, settlement, now, actorId, cancellationToken);

            dbContext.Add(new OperationLogEntry(
                Guid.NewGuid(),
                OperationLogEntityType.VatSettlement,
                settlement.Id,
                OperationLogAction.Confirmed,
                reason: null,
                now,
                actorId));

            CatalogOperationResult result = await CatalogPersistence.SaveChangesAsync(
                dbContext,
                "No fue posible registrar la liquidación.",
                cancellationToken);

            return new RegisterVatSettlementResult(
                result,
                result.IsSuccess ? settlement.Id : null,
                openPeriodWarning);
        }
        catch (ArgumentException exception)
        {
            dbContext.ChangeTracker.Clear();
            return Rejected(exception.Message);
        }
        catch (InvalidOperationException exception)
        {
            dbContext.ChangeTracker.Clear();
            return Rejected(exception.Message);
        }
    }

    public async Task<CatalogOperationResult> CancelAsync(
        Guid id,
        string reason,
        string version,
        Guid actorId,
        CancellationToken cancellationToken = default)
    {
        VatSettlement? settlement = await dbContext.Set<VatSettlement>()
            .SingleOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);

        if (settlement is null)
        {
            return CatalogOperationResult.Missing();
        }

        if (settlement.Status == VatSettlementStatus.Active)
        {
            bool isMostRecentActive = !await dbContext.Set<VatSettlement>()
                .AsNoTracking()
                .AnyAsync(
                    other => other.Status == VatSettlementStatus.Active
                        && other.Id != settlement.Id
                        && (other.PeriodYear > settlement.PeriodYear
                            || (other.PeriodYear == settlement.PeriodYear && other.PeriodMonth > settlement.PeriodMonth)),
                    cancellationToken);

            if (!isMostRecentActive)
            {
                dbContext.ChangeTracker.Clear();
                return CatalogOperationResult.Invalid(
                    "Solo se puede anular la liquidación vigente más reciente.");
            }
        }

        if (!CatalogPersistence.TrySetOriginalVersion(dbContext, settlement, version))
        {
            return CatalogOperationResult.Invalid("El token de concurrencia no es válido.");
        }

        string? closingDateViolation = await ClosingDateGuard.GetViolationMessageAsync(
            dbContext, settlement.SettlementDate, timeProvider, cancellationToken);

        if (closingDateViolation is not null)
        {
            dbContext.ChangeTracker.Clear();
            return CatalogOperationResult.Invalid(closingDateViolation);
        }

        try
        {
            DateTimeOffset now = timeProvider.GetUtcNow();
            settlement.Cancel(reason, now, actorId);

            await JournalPostingService.ReverseAsync(
                dbContext, JournalEntrySourceType.VatSettlement, settlement.Id, now, actorId, cancellationToken);

            dbContext.Add(new OperationLogEntry(
                Guid.NewGuid(),
                OperationLogEntityType.VatSettlement,
                settlement.Id,
                OperationLogAction.Cancelled,
                reason,
                now,
                actorId));

            return await CatalogPersistence.SaveChangesAsync(
                dbContext,
                "No fue posible anular la liquidación.",
                cancellationToken);
        }
        catch (ArgumentException exception)
        {
            dbContext.ChangeTracker.Clear();
            return CatalogOperationResult.Invalid(exception.Message);
        }
        catch (InvalidOperationException exception)
        {
            dbContext.ChangeTracker.Clear();
            return CatalogOperationResult.Invalid(exception.Message);
        }
    }

    /// <summary>
    /// Movimientos acumulados hasta el corte de las cuentas de sistema de IVA por pagar y
    /// acreditable, sin contar los asientos de liquidación ni sus reversiones.
    /// </summary>
    private async Task<(decimal Payable, decimal Creditable)> ComputeAccumulatedAmountsAsync(
        DateOnly cutoff, CancellationToken cancellationToken)
    {
        Guid vatPayableAccountId = await GetSystemAccountIdAsync(LedgerAccountSystemRole.VatPayable, cancellationToken);
        Guid vatCreditableAccountId = await GetSystemAccountIdAsync(LedgerAccountSystemRole.VatCreditable, cancellationToken);

        decimal payable = await SumAccountMovementsAsync(
            vatPayableAccountId, null, cutoff, creditMinusDebit: true, cancellationToken);
        decimal creditable = await SumAccountMovementsAsync(
            vatCreditableAccountId, null, cutoff, creditMinusDebit: false, cancellationToken);

        return (payable, creditable);
    }

    /// <summary>Movimientos propios del período (entre su primer y su último día), informativos.</summary>
    private async Task<(decimal Payable, decimal Creditable)> ComputePeriodOwnAmountsAsync(
        DateOnly periodStart, DateOnly cutoff, CancellationToken cancellationToken)
    {
        Guid vatPayableAccountId = await GetSystemAccountIdAsync(LedgerAccountSystemRole.VatPayable, cancellationToken);
        Guid vatCreditableAccountId = await GetSystemAccountIdAsync(LedgerAccountSystemRole.VatCreditable, cancellationToken);

        decimal payable = await SumAccountMovementsAsync(
            vatPayableAccountId, periodStart, cutoff, creditMinusDebit: true, cancellationToken);
        decimal creditable = await SumAccountMovementsAsync(
            vatCreditableAccountId, periodStart, cutoff, creditMinusDebit: false, cancellationToken);

        return (payable, creditable);
    }

    private async Task<decimal> SumAccountMovementsAsync(
        Guid accountId,
        DateOnly? fromInclusive,
        DateOnly toInclusive,
        bool creditMinusDebit,
        CancellationToken cancellationToken)
    {
        IQueryable<JournalEntry> entries = dbContext.Set<JournalEntry>()
            .AsNoTracking()
            .Where(entry => entry.EntryDate <= toInclusive && entry.SourceType != JournalEntrySourceType.VatSettlement);

        if (fromInclusive is DateOnly from)
        {
            entries = entries.Where(entry => entry.EntryDate >= from);
        }

        var totals = await entries
            .SelectMany(entry => entry.Lines)
            .Where(line => line.AccountId == accountId)
            .GroupBy(line => 1)
            .Select(group => new { Debit = group.Sum(line => line.Debit), Credit = group.Sum(line => line.Credit) })
            .SingleOrDefaultAsync(cancellationToken);

        decimal debit = totals?.Debit ?? 0m;
        decimal credit = totals?.Credit ?? 0m;
        return creditMinusDebit ? credit - debit : debit - credit;
    }

    private async Task<(decimal Payable, decimal Creditable)> SumPriorActiveSettlementsAsync(
        int periodYear, int periodMonth, CancellationToken cancellationToken)
    {
        List<VatSettlement> priorActive = await dbContext.Set<VatSettlement>()
            .AsNoTracking()
            .Where(settlement => settlement.Status == VatSettlementStatus.Active)
            .ToListAsync(cancellationToken);

        int currentKey = PeriodKey(periodYear, periodMonth);
        List<VatSettlement> applicable = priorActive
            .Where(settlement => PeriodKey(settlement.PeriodYear, settlement.PeriodMonth) < currentKey)
            .ToList();

        return (
            applicable.Sum(settlement => settlement.VatPayableAmount),
            applicable.Sum(settlement => settlement.VatCreditApplied));
    }

    private bool IsPeriodStillOpen(int periodYear, int periodMonth)
    {
        DateOnly today = DateOnly.FromDateTime(timeProvider.GetLocalNow().DateTime);
        return PeriodKey(periodYear, periodMonth) >= PeriodKey(today.Year, today.Month);
    }

    private static int PeriodKey(int year, int month) => (year * 12) + month;

    private static (decimal CreditApplied, decimal PaymentAmount, decimal CarriedForward) Allocate(
        decimal vatPayableAmount, decimal vatCreditAvailable)
    {
        return vatPayableAmount > vatCreditAvailable
            ? (vatCreditAvailable, vatPayableAmount - vatCreditAvailable, 0m)
            : (vatPayableAmount, 0m, vatCreditAvailable - vatPayableAmount);
    }

    private async Task<Guid> GetSystemAccountIdAsync(LedgerAccountSystemRole role, CancellationToken cancellationToken)
    {
        LedgerAccount? account = await dbContext.Set<LedgerAccount>()
            .AsNoTracking()
            .Where(candidate => candidate.SystemRole == role)
            .SingleOrDefaultAsync(cancellationToken);

        if (account is null)
        {
            throw new InvalidOperationException($"No existe una cuenta contable para el rol de sistema {role}.");
        }

        return account.Id;
    }

    private async Task<string> GetCashAccountNameAsync(Guid cashAccountId, CancellationToken cancellationToken)
    {
        return await dbContext.Set<LedgerAccount>()
            .AsNoTracking()
            .Where(account => account.Id == cashAccountId)
            .Select(account => account.Name)
            .SingleOrDefaultAsync(cancellationToken) ?? "—";
    }

    private static RegisterVatSettlementResult Rejected(string message) =>
        new(CatalogOperationResult.Invalid(message), null, false);

    private static VatSettlementModel Map(VatSettlement settlement, string? cashAccountName)
    {
        return new VatSettlementModel(
            settlement.Id,
            settlement.Code,
            settlement.PeriodYear,
            settlement.PeriodMonth,
            settlement.SettlementDate,
            settlement.VatPayableAmount,
            settlement.VatCreditApplied,
            settlement.PaymentAmount,
            settlement.CreditCarriedForward,
            settlement.CashAccountId,
            cashAccountName,
            settlement.Reference,
            settlement.Status,
            settlement.CancellationReason,
            settlement.CreatedAtUtc,
            CatalogPersistence.EncodeVersion(settlement.RowVersion));
    }
}
