using Microsoft.EntityFrameworkCore;
using SistemaFinanciero.Application.Catalogs.Common;
using SistemaFinanciero.Application.Incomes;
using SistemaFinanciero.Domain.Audit;
using SistemaFinanciero.Domain.Catalogs;
using SistemaFinanciero.Domain.Incomes;
using SistemaFinanciero.Domain.Invoices;
using SistemaFinanciero.Infrastructure.Catalogs;
using SistemaFinanciero.Infrastructure.Persistence;

namespace SistemaFinanciero.Infrastructure.Incomes;

/// <summary>Consulta y registra ingresos manuales.</summary>
/// <remarks>
/// Conexiones: implementa <c>IIncomeService</c> (Application); se registra en
/// <c>DependencyInjection.AddInfrastructure</c>; lo consume <c>IncomesController</c> (Web, ruta
/// <c>/ingresos</c>). Los ingresos de factura y de abono los crean <c>InvoiceService</c> y
/// <c>ReceivablePaymentService</c> respectivamente; este servicio solo crea y anula los de origen
/// manual.
/// </remarks>
internal sealed class IncomeService(
    FinancialDbContext dbContext,
    TimeProvider timeProvider) : IIncomeService
{
    public async Task<PagedResult<IncomeModel>> SearchAsync(
        IncomeQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        IQueryable<Income> incomes = dbContext.Set<Income>().AsNoTracking();

        incomes = query.Status switch
        {
            IncomeStatusFilter.Active => incomes.Where(income => income.Status == IncomeStatus.Active),
            IncomeStatusFilter.Cancelled => incomes.Where(income => income.Status == IncomeStatus.Cancelled),
            IncomeStatusFilter.All => incomes,
            _ => throw new ArgumentOutOfRangeException(nameof(query)),
        };

        incomes = query.Origin switch
        {
            IncomeOriginFilter.InvoiceCash => incomes.Where(income => income.Origin == IncomeOrigin.InvoiceCash),
            IncomeOriginFilter.ReceivablePayment => incomes.Where(income => income.Origin == IncomeOrigin.ReceivablePayment),
            IncomeOriginFilter.Manual => incomes.Where(income => income.Origin == IncomeOrigin.Manual),
            IncomeOriginFilter.All => incomes,
            _ => throw new ArgumentOutOfRangeException(nameof(query)),
        };

        if (query.ReferenceOrDescription is not null)
        {
            string search = query.ReferenceOrDescription;
            incomes = incomes.Where(income =>
                (income.Reference != null && income.Reference.Contains(search)) ||
                (income.Description != null && income.Description.Contains(search)));
        }

        if (query.CategoryName is not null)
        {
            string categoryName = query.CategoryName;
            incomes = incomes.Where(income => dbContext.Set<FinancialCategory>().Any(category =>
                category.Id == income.CategoryId && category.Name.Contains(categoryName)));
        }

        if (query.CashAccountName is not null)
        {
            string cashAccountName = query.CashAccountName;
            incomes = incomes.Where(income => dbContext.Set<LedgerAccount>().Any(account =>
                account.Id == income.CashAccountId && account.Name.Contains(cashAccountName)));
        }

        if (query.AmountMin is decimal amountMin)
        {
            incomes = incomes.Where(income => income.Amount >= amountMin);
        }

        if (query.AmountMax is decimal amountMax)
        {
            incomes = incomes.Where(income => income.Amount <= amountMax);
        }

        if (query.IncomeDateFrom is DateOnly incomeDateFrom)
        {
            incomes = incomes.Where(income => income.IncomeDate >= incomeDateFrom);
        }

        if (query.IncomeDateTo is DateOnly incomeDateTo)
        {
            incomes = incomes.Where(income => income.IncomeDate <= incomeDateTo);
        }

        PagedResult<Income> page = await CatalogPersistence.ToPageAsync(
            incomes.OrderByDescending(income => income.CreatedAtUtc),
            query.Page,
            query.PageSize,
            cancellationToken);

        return new PagedResult<IncomeModel>(
            await MapAllAsync(page.Items, cancellationToken),
            page.Page,
            page.PageSize,
            page.TotalCount);
    }

    public async Task<IncomeModel?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        Income? income = await dbContext.Set<Income>()
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);

        if (income is null)
        {
            return null;
        }

        IReadOnlyList<IncomeModel> mapped = await MapAllAsync([income], cancellationToken);
        return mapped[0];
    }

    public async Task<IncomeCreationResult> CreateManualAsync(
        CreateManualIncomeCommand command,
        Guid actorId,
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
            return new IncomeCreationResult(
                CatalogOperationResult.Invalid(
                    "La cuenta de efectivo seleccionada no existe, está inactiva o no es de efectivo."),
                null);
        }

        bool isValidCategory = await dbContext.Set<FinancialCategory>()
            .AsNoTracking()
            .AnyAsync(
                category => category.Id == command.CategoryId
                    && category.IsActive
                    && category.Kind == FinancialCategoryKind.Income,
                cancellationToken);

        if (!isValidCategory)
        {
            return new IncomeCreationResult(
                CatalogOperationResult.Invalid("La categoría seleccionada no existe, está inactiva o no es de ingreso."),
                null);
        }

        try
        {
            DateTimeOffset now = timeProvider.GetUtcNow();
            string code = await CatalogPersistence.GenerateSequentialCodeAsync(
                dbContext,
                "finanzas.Seq_IngresoCodigo",
                "ING",
                3,
                cancellationToken);
            Income income = Income.CreateManual(
                Guid.NewGuid(),
                code,
                command.CategoryId,
                command.CashAccountId,
                command.Amount,
                command.IncomeDate,
                command.PaymentMethod,
                command.Reference,
                command.Description,
                now,
                actorId);

            dbContext.Add(income);
            dbContext.Add(new OperationLogEntry(
                Guid.NewGuid(),
                OperationLogEntityType.Income,
                income.Id,
                OperationLogAction.Confirmed,
                reason: null,
                now,
                actorId));

            CatalogOperationResult result = await CatalogPersistence.SaveChangesAsync(
                dbContext,
                "No fue posible registrar el ingreso.",
                cancellationToken);

            return new IncomeCreationResult(result, result.IsSuccess ? income.Id : null);
        }
        catch (ArgumentException exception)
        {
            dbContext.ChangeTracker.Clear();
            return new IncomeCreationResult(CatalogOperationResult.Invalid(exception.Message), null);
        }
    }

    public async Task<CatalogOperationResult> CancelAsync(
        Guid id,
        string reason,
        string version,
        Guid actorId,
        CancellationToken cancellationToken = default)
    {
        Income? income = await dbContext.Set<Income>()
            .SingleOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);

        if (income is null)
        {
            return CatalogOperationResult.Missing();
        }

        if (income.Origin != IncomeOrigin.Manual)
        {
            dbContext.ChangeTracker.Clear();
            return CatalogOperationResult.Invalid(
                "Este ingreso se generó automáticamente; anúlelo desde la factura o el abono que lo originó.");
        }

        if (!CatalogPersistence.TrySetOriginalVersion(dbContext, income, version))
        {
            return CatalogOperationResult.Invalid("El token de concurrencia no es válido.");
        }

        try
        {
            DateTimeOffset now = timeProvider.GetUtcNow();
            income.Cancel(reason, now, actorId);

            dbContext.Add(new OperationLogEntry(
                Guid.NewGuid(),
                OperationLogEntityType.Income,
                income.Id,
                OperationLogAction.Cancelled,
                reason,
                now,
                actorId));

            return await CatalogPersistence.SaveChangesAsync(
                dbContext,
                "No fue posible anular el ingreso.",
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

    private async Task<IReadOnlyList<IncomeModel>> MapAllAsync(
        IReadOnlyList<Income> incomes,
        CancellationToken cancellationToken)
    {
        if (incomes.Count == 0)
        {
            return [];
        }

        Guid[] invoiceIds = incomes
            .Where(income => income.InvoiceId is not null)
            .Select(income => income.InvoiceId!.Value)
            .Distinct()
            .ToArray();
        IReadOnlyDictionary<Guid, long?> invoiceNumbers = invoiceIds.Length == 0
            ? new Dictionary<Guid, long?>()
            : await dbContext.Set<Invoice>()
                .AsNoTracking()
                .Where(invoice => invoiceIds.Contains(invoice.Id))
                .ToDictionaryAsync(invoice => invoice.Id, invoice => invoice.Number, cancellationToken);

        Guid[] categoryIds = incomes.Select(income => income.CategoryId).Distinct().ToArray();
        IReadOnlyDictionary<Guid, string> categoryNames = await dbContext.Set<FinancialCategory>()
            .AsNoTracking()
            .Where(category => categoryIds.Contains(category.Id))
            .ToDictionaryAsync(category => category.Id, category => category.Name, cancellationToken);

        Guid[] cashAccountIds = incomes.Select(income => income.CashAccountId).Distinct().ToArray();
        IReadOnlyDictionary<Guid, string> cashAccountNames = await dbContext.Set<LedgerAccount>()
            .AsNoTracking()
            .Where(account => cashAccountIds.Contains(account.Id))
            .ToDictionaryAsync(account => account.Id, account => account.Name, cancellationToken);

        return incomes
            .Select(income => new IncomeModel(
                income.Id,
                income.Code,
                income.Origin,
                income.InvoiceId,
                income.InvoiceId is null ? null : invoiceNumbers.GetValueOrDefault(income.InvoiceId.Value),
                income.ReceivablePaymentId,
                income.CategoryId,
                categoryNames.GetValueOrDefault(income.CategoryId, "—"),
                income.CashAccountId,
                cashAccountNames.GetValueOrDefault(income.CashAccountId, "—"),
                income.Amount,
                income.IncomeDate,
                income.PaymentMethod,
                income.Reference,
                income.Description,
                income.Status,
                income.CancellationReason,
                CatalogPersistence.EncodeVersion(income.RowVersion)))
            .ToArray();
    }
}
