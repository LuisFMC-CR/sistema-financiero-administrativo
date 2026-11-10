using Microsoft.EntityFrameworkCore;
using SistemaFinanciero.Application.Catalogs.Common;
using SistemaFinanciero.Application.IncomeTax;
using SistemaFinanciero.Application.Reports;
using SistemaFinanciero.Domain.Audit;
using SistemaFinanciero.Domain.Catalogs;
using SistemaFinanciero.Domain.IncomeTax;
using SistemaFinanciero.Domain.Ledger;
using SistemaFinanciero.Domain.Parameters;
using SistemaFinanciero.Infrastructure.Catalogs;
using SistemaFinanciero.Infrastructure.Common;
using SistemaFinanciero.Infrastructure.Ledger;
using SistemaFinanciero.Infrastructure.Parameters;
using SistemaFinanciero.Infrastructure.Persistence;

namespace SistemaFinanciero.Infrastructure.IncomeTax;

/// <summary>
/// Pagos de impuesto sobre la renta, escala de tramos y estimación del impuesto de un período fiscal
/// (RF-99).
/// </summary>
/// <remarks>
/// Conexiones: implementa <c>IIncomeTaxService</c> (Application); se registra en
/// <c>DependencyInjection.AddInfrastructure</c>. Los pagos siguen el mismo flujo que
/// <c>CashTransferService</c>: fila, asiento y bitácora en un solo <c>SaveChangesAsync</c>. La
/// estimación toma la renta bruta y la utilidad de <c>IReportService.GetIncomeStatementReportAsync</c>
/// (así coincide siempre con el estado de resultados), las retenciones de los movimientos de la cuenta
/// de sistema "Retenciones de renta a favor" y los pagos vigentes del mismo período, y delega el cálculo
/// en <see cref="IncomeTaxEstimate"/> (Domain).
/// </remarks>
internal sealed class IncomeTaxService(
    FinancialDbContext dbContext,
    IReportService reportService,
    TimeProvider timeProvider) : IIncomeTaxService
{
    private const string EstimateReportName = "Estimación del impuesto sobre la renta";

    public async Task<IncomeTaxPaymentModel?> GetPaymentAsync(Guid id, CancellationToken cancellationToken = default)
    {
        IncomeTaxPayment? payment = await dbContext.Set<IncomeTaxPayment>()
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);

        if (payment is null)
        {
            return null;
        }

        IReadOnlyList<IncomeTaxPaymentModel> mapped = await MapAllAsync([payment], cancellationToken);
        return mapped[0];
    }

    public async Task<PagedResult<IncomeTaxPaymentModel>> SearchPaymentsAsync(
        IncomeTaxPaymentQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        IQueryable<IncomeTaxPayment> payments = dbContext.Set<IncomeTaxPayment>().AsNoTracking();

        payments = query.Status switch
        {
            IncomeTaxPaymentStatusFilter.Active => payments.Where(payment => payment.Status == IncomeTaxPaymentStatus.Active),
            IncomeTaxPaymentStatusFilter.Cancelled => payments.Where(payment => payment.Status == IncomeTaxPaymentStatus.Cancelled),
            IncomeTaxPaymentStatusFilter.All => payments,
            _ => throw new ArgumentOutOfRangeException(nameof(query)),
        };

        if (query.Code is not null)
        {
            string code = query.Code;
            payments = payments.Where(payment => payment.Code.Contains(code));
        }

        if (query.FiscalPeriodStartDate is DateOnly fiscalPeriodStartDate)
        {
            payments = payments.Where(payment => payment.FiscalPeriodStartDate == fiscalPeriodStartDate);
        }

        if (query.CashAccountId is Guid cashAccountId)
        {
            payments = payments.Where(payment => payment.CashAccountId == cashAccountId);
        }

        if (query.PaymentDateFrom is DateOnly paymentDateFrom)
        {
            payments = payments.Where(payment => payment.PaymentDate >= paymentDateFrom);
        }

        if (query.PaymentDateTo is DateOnly paymentDateTo)
        {
            payments = payments.Where(payment => payment.PaymentDate <= paymentDateTo);
        }

        PagedResult<IncomeTaxPayment> page = await CatalogPersistence.ToPageAsync(
            payments.OrderByDescending(payment => payment.CreatedAtUtc),
            query.Page,
            query.PageSize,
            cancellationToken);

        return new PagedResult<IncomeTaxPaymentModel>(
            await MapAllAsync(page.Items, cancellationToken),
            page.Page,
            page.PageSize,
            page.TotalCount);
    }

    public async Task<RegisterIncomeTaxPaymentResult> RegisterPaymentAsync(
        RegisterIncomeTaxPaymentCommand command,
        Guid actorId,
        bool actorIsManagement,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        bool isValidCashAccount = await dbContext.Set<LedgerAccount>()
            .AsNoTracking()
            .AnyAsync(
                account => account.Id == command.CashAccountId && account.IsActive && account.CashKind != null,
                cancellationToken);

        if (!isValidCashAccount)
        {
            return Rejected("La cuenta de efectivo no existe, está inactiva o no es de efectivo.");
        }

        string? closingDateViolation = await ClosingDateGuard.GetViolationMessageAsync(
            dbContext, command.PaymentDate, timeProvider, cancellationToken);

        if (closingDateViolation is not null)
        {
            return Rejected(closingDateViolation);
        }

        if (!actorIsManagement)
        {
            SystemParameters? parameters = await dbContext.Set<SystemParameters>()
                .AsNoTracking()
                .SingleOrDefaultAsync(cancellationToken);

            // Sin parámetros configurados no hay límite que aplicar de forma segura: se exige Gerencia
            // (regla 7), igual criterio que en pagos, abonos y traspasos.
            decimal limit = parameters?.AuthorizationLimitCrc ?? 0m;

            if (command.Amount > limit)
            {
                return Rejected(
                    "El monto del pago supera el límite de autorización; solo Gerencia puede registrarlo.");
            }
        }

        bool duplicateReferenceWarning = await DuplicateReferenceGuard.ExistsAsync(
            dbContext, command.CashAccountId, command.Reference, cancellationToken);

        try
        {
            DateTimeOffset now = timeProvider.GetUtcNow();
            DateOnly fiscalPeriodStartDate = command.FiscalPeriodStartDate;
            DateOnly fiscalPeriodEndDate = fiscalPeriodStartDate.Year < DateOnly.MaxValue.Year
                ? fiscalPeriodStartDate.AddYears(1).AddDays(-1)
                : fiscalPeriodStartDate;
            string code = await CatalogPersistence.GenerateSequentialCodeAsync(
                dbContext,
                "finanzas.Seq_PagoRentaCodigo",
                "PIR",
                6,
                cancellationToken);

            IncomeTaxPayment payment = new(
                Guid.NewGuid(),
                code,
                fiscalPeriodStartDate,
                fiscalPeriodEndDate,
                command.CashAccountId,
                command.PaymentMethod,
                command.Amount,
                command.Reference,
                command.Description,
                command.PaymentDate,
                now,
                actorId);

            dbContext.Add(payment);

            await JournalPostingService.PostIncomeTaxPaymentAsync(dbContext, payment, now, actorId, cancellationToken);

            dbContext.Add(new OperationLogEntry(
                Guid.NewGuid(),
                OperationLogEntityType.IncomeTaxPayment,
                payment.Id,
                OperationLogAction.Confirmed,
                reason: null,
                now,
                actorId));

            CatalogOperationResult result = await CatalogPersistence.SaveChangesAsync(
                dbContext,
                "No fue posible registrar el pago de impuesto sobre la renta.",
                cancellationToken);

            return new RegisterIncomeTaxPaymentResult(
                result,
                result.IsSuccess ? payment.Id : null,
                duplicateReferenceWarning);
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

    public async Task<CatalogOperationResult> CancelPaymentAsync(
        Guid id,
        string reason,
        string version,
        Guid actorId,
        CancellationToken cancellationToken = default)
    {
        IncomeTaxPayment? payment = await dbContext.Set<IncomeTaxPayment>()
            .SingleOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);

        if (payment is null)
        {
            return CatalogOperationResult.Missing();
        }

        if (!CatalogPersistence.TrySetOriginalVersion(dbContext, payment, version))
        {
            return CatalogOperationResult.Invalid("El token de concurrencia no es válido.");
        }

        string? closingDateViolation = await ClosingDateGuard.GetViolationMessageAsync(
            dbContext, payment.PaymentDate, timeProvider, cancellationToken);

        if (closingDateViolation is not null)
        {
            dbContext.ChangeTracker.Clear();
            return CatalogOperationResult.Invalid(closingDateViolation);
        }

        try
        {
            DateTimeOffset now = timeProvider.GetUtcNow();
            payment.Cancel(reason, now, actorId);

            await JournalPostingService.ReverseAsync(
                dbContext, JournalEntrySourceType.IncomeTaxPayment, payment.Id, now, actorId, cancellationToken);

            dbContext.Add(new OperationLogEntry(
                Guid.NewGuid(),
                OperationLogEntityType.IncomeTaxPayment,
                payment.Id,
                OperationLogAction.Cancelled,
                reason,
                now,
                actorId));

            return await CatalogPersistence.SaveChangesAsync(
                dbContext,
                "No fue posible anular el pago de impuesto sobre la renta.",
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

    public async Task<CatalogOperationResult> UpdateScaleAsync(
        SaveIncomeTaxScaleCommand command,
        string version,
        Guid actorId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        SystemParameters? parameters = await dbContext.Set<SystemParameters>()
            .SingleOrDefaultAsync(candidate => candidate.Id == SystemParameters.SingletonId, cancellationToken);

        if (parameters is null)
        {
            return CatalogOperationResult.Missing();
        }

        if (!CatalogPersistence.TrySetOriginalVersion(dbContext, parameters, version))
        {
            return CatalogOperationResult.Invalid("El token de concurrencia no es válido.");
        }

        try
        {
            IncomeTaxScale scale = new(
                command.Bracket1UpperLimit,
                command.Bracket2UpperLimit,
                command.Bracket3UpperLimit,
                command.Bracket1Rate,
                command.Bracket2Rate,
                command.Bracket3Rate,
                command.Bracket4Rate,
                command.GrossIncomeThreshold,
                command.GeneralRate);

            DateTimeOffset now = timeProvider.GetUtcNow();
            parameters.UpdateIncomeTaxScale(scale, now, actorId);
            dbContext.Add(new IncomeTaxScaleChange(Guid.NewGuid(), scale, now, actorId));

            return await CatalogPersistence.SaveChangesAsync(
                dbContext,
                "No fue posible actualizar la escala del impuesto sobre la renta.",
                cancellationToken);
        }
        catch (ArgumentException exception)
        {
            dbContext.ChangeTracker.Clear();
            return CatalogOperationResult.Invalid(exception.Message);
        }
    }

    public async Task<IncomeTaxEstimateReportResult?> GetEstimateAsync(
        DateOnly fiscalPeriodStartDate,
        string generatedByUserName,
        CancellationToken cancellationToken = default)
    {
        if (fiscalPeriodStartDate.Day != 1 || fiscalPeriodStartDate.Year >= DateOnly.MaxValue.Year)
        {
            throw new ArgumentException(
                "El período fiscal debe iniciar el primer día de un mes.",
                nameof(fiscalPeriodStartDate));
        }

        SystemParameters? parameters = await dbContext.Set<SystemParameters>()
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == SystemParameters.SingletonId, cancellationToken);

        if (parameters is null)
        {
            return null;
        }

        DateOnly periodTo = fiscalPeriodStartDate.AddYears(1).AddDays(-1);

        IncomeStatementReportResult incomeStatement = await reportService.GetIncomeStatementReportAsync(
            fiscalPeriodStartDate,
            periodTo,
            generatedByUserName,
            cancellationToken);

        decimal withholdings = await GetWithholdingsAsync(fiscalPeriodStartDate, periodTo, cancellationToken);

        List<IncomeTaxEstimatePaymentLine> payments = await dbContext.Set<IncomeTaxPayment>()
            .AsNoTracking()
            .Where(payment => payment.FiscalPeriodStartDate == fiscalPeriodStartDate
                && payment.Status == IncomeTaxPaymentStatus.Active)
            .OrderBy(payment => payment.PaymentDate)
            .ThenBy(payment => payment.Code)
            .Select(payment => new IncomeTaxEstimatePaymentLine(
                payment.Code,
                payment.PaymentDate,
                payment.Reference,
                payment.Amount))
            .ToListAsync(cancellationToken);

        decimal paymentsTotal = payments.Sum(payment => payment.Amount);

        IncomeTaxEstimate estimate = IncomeTaxEstimate.Calculate(
            parameters.GetIncomeTaxScale(),
            incomeStatement.TotalIncome,
            incomeStatement.NetResult,
            withholdings,
            paymentsTotal);

        bool hasData = incomeStatement.IncomeGroups.Count > 0
            || incomeStatement.ExpenseGroups.Count > 0
            || withholdings != 0m
            || payments.Count > 0;

        ReportHeader header = new(
            EstimateReportName,
            $"Período fiscal del {fiscalPeriodStartDate:dd/MM/yyyy} al {periodTo:dd/MM/yyyy}",
            timeProvider.GetUtcNow(),
            generatedByUserName);

        return new IncomeTaxEstimateReportResult(
            header,
            fiscalPeriodStartDate,
            periodTo,
            MapScale(parameters),
            estimate.GrossIncome,
            incomeStatement.TotalExpense,
            estimate.NetIncome,
            estimate.TaxableIncome,
            estimate.Regime,
            estimate.Brackets,
            estimate.EstimatedTax,
            estimate.Withholdings,
            payments,
            estimate.Payments,
            estimate.Balance,
            estimate.AmountDue,
            estimate.AmountInFavor,
            hasData);
    }

    /// <summary>
    /// Retenciones del 2 % acumuladas en el período: débitos menos créditos de la cuenta de sistema
    /// "Retenciones de renta a favor". Incluye las reversiones (abonos anulados y notas de crédito), que
    /// llevan la misma fecha del asiento original, por lo que solo queda lo vigente.
    /// </summary>
    private async Task<decimal> GetWithholdingsAsync(
        DateOnly periodFrom,
        DateOnly periodTo,
        CancellationToken cancellationToken)
    {
        Guid? withholdingAccountId = await dbContext.Set<LedgerAccount>()
            .AsNoTracking()
            .Where(account => account.SystemRole == LedgerAccountSystemRole.WithholdingsReceivable)
            .Select(account => (Guid?)account.Id)
            .SingleOrDefaultAsync(cancellationToken);

        if (withholdingAccountId is not Guid accountId)
        {
            return 0m;
        }

        decimal net = await dbContext.Set<JournalEntry>()
            .AsNoTracking()
            .Where(entry => entry.EntryDate >= periodFrom && entry.EntryDate <= periodTo)
            .SelectMany(entry => entry.Lines)
            .Where(line => line.AccountId == accountId)
            .SumAsync(line => line.Debit - line.Credit, cancellationToken);

        // Un saldo acreedor no es una retención a favor: no puede rebajar el impuesto.
        return net > 0m ? net : 0m;
    }

    private async Task<IReadOnlyList<IncomeTaxPaymentModel>> MapAllAsync(
        IReadOnlyList<IncomeTaxPayment> payments,
        CancellationToken cancellationToken)
    {
        if (payments.Count == 0)
        {
            return [];
        }

        Guid[] accountIds = payments.Select(payment => payment.CashAccountId).Distinct().ToArray();

        IReadOnlyDictionary<Guid, string> accountNames = await dbContext.Set<LedgerAccount>()
            .AsNoTracking()
            .Where(account => accountIds.Contains(account.Id))
            .ToDictionaryAsync(account => account.Id, account => account.Name, cancellationToken);

        return payments
            .Select(payment => new IncomeTaxPaymentModel(
                payment.Id,
                payment.Code,
                payment.FiscalPeriodStartDate,
                payment.FiscalPeriodEndDate,
                payment.CashAccountId,
                accountNames.GetValueOrDefault(payment.CashAccountId, "—"),
                payment.PaymentMethod,
                payment.Amount,
                payment.Reference,
                payment.Description,
                payment.PaymentDate,
                payment.Status,
                payment.CancellationReason,
                payment.CreatedAtUtc,
                CatalogPersistence.EncodeVersion(payment.RowVersion)))
            .ToArray();
    }

    private static IncomeTaxScaleModel MapScale(SystemParameters parameters) => new(
        parameters.IncomeTaxBracket1UpperLimit,
        parameters.IncomeTaxBracket2UpperLimit,
        parameters.IncomeTaxBracket3UpperLimit,
        parameters.IncomeTaxBracket1Rate,
        parameters.IncomeTaxBracket2Rate,
        parameters.IncomeTaxBracket3Rate,
        parameters.IncomeTaxBracket4Rate,
        parameters.IncomeTaxGrossIncomeThreshold,
        parameters.IncomeTaxGeneralRate,
        CatalogPersistence.EncodeVersion(parameters.RowVersion));

    private static RegisterIncomeTaxPaymentResult Rejected(string message) =>
        new(CatalogOperationResult.Invalid(message), null, false);
}
