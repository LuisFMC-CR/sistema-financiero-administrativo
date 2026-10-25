using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using SistemaFinanciero.Application.Catalogs.Common;
using SistemaFinanciero.Application.Expenses;
using SistemaFinanciero.Domain.Audit;
using SistemaFinanciero.Domain.Budgets;
using SistemaFinanciero.Domain.Catalogs;
using SistemaFinanciero.Domain.Expenses;
using SistemaFinanciero.Domain.Invoices;
using SistemaFinanciero.Domain.Parameters;
using SistemaFinanciero.Domain.Payables;
using SistemaFinanciero.Infrastructure.Catalogs;
using SistemaFinanciero.Infrastructure.Persistence;

namespace SistemaFinanciero.Infrastructure.Expenses;

/// <summary>
/// Ejecuta el ciclo de vida del gasto: crear y editar el encabezado del borrador, confirmar y anular.
/// Es el espejo, del lado de gastos, de <c>InvoiceService</c>.
/// </summary>
/// <remarks>
/// Conexiones: implementa <c>IExpenseService</c> (Application); se registra en
/// <c>DependencyInjection.AddInfrastructure</c>. Genera el código del gasto (regla 12,
/// <c>finanzas.Seq_GastoCodigo</c>) al crear, con el mismo mecanismo que
/// <c>CatalogPersistence.GenerateSequentialCodeAsync</c> usa para los códigos de catálogo. Confirmar y
/// anular quedan en la bitácora de operaciones (<see cref="OperationLogEntry"/>, regla 4/16), dentro
/// de la misma transacción que la mutación del dominio; solo se registra el gasto, no la cuenta por
/// pagar que genera (igual criterio que <c>Receivable</c> del lado de ingresos, que tampoco tiene
/// entrada propia). Confirmar de contado sobre el límite de autorización se rechaza salvo que quien
/// confirma sea Gerencia (regla 7); todavía no valida la fecha de cierre, que queda para el paso 6 del
/// roadmap.
/// </remarks>
internal sealed class ExpenseService(
    FinancialDbContext dbContext,
    TimeProvider timeProvider) : IExpenseService
{
    public async Task<PagedResult<ExpenseModel>> SearchAsync(
        ExpenseQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        IQueryable<Expense> expenses = dbContext.Set<Expense>().AsNoTracking();

        expenses = query.Status switch
        {
            ExpenseStatusFilter.Draft => expenses.Where(expense => expense.Status == ExpenseStatus.Draft),
            ExpenseStatusFilter.Confirmed => expenses.Where(expense => expense.Status == ExpenseStatus.Confirmed),
            ExpenseStatusFilter.Cancelled => expenses.Where(expense => expense.Status == ExpenseStatus.Cancelled),
            ExpenseStatusFilter.All => expenses,
            _ => throw new ArgumentOutOfRangeException(nameof(query)),
        };

        if (query.CreatedByUserId is Guid ownerId)
        {
            expenses = expenses.Where(expense => expense.CreatedByUserId == ownerId);
        }

        if (query.Code is not null)
        {
            string code = query.Code;
            expenses = expenses.Where(expense => expense.Code.Contains(code));
        }

        if (query.SupplierName is not null)
        {
            string supplierName = query.SupplierName;
            expenses = expenses.Where(expense => dbContext.Set<Supplier>().Any(supplier =>
                supplier.Id == expense.SupplierId && supplier.Name.Contains(supplierName)));
        }

        if (query.CategoryName is not null)
        {
            string categoryName = query.CategoryName;
            expenses = expenses.Where(expense => dbContext.Set<FinancialCategory>().Any(category =>
                category.Id == expense.CategoryId && category.Name.Contains(categoryName)));
        }

        if (query.IssueDateFrom is DateOnly issueDateFrom)
        {
            expenses = expenses.Where(expense => expense.IssueDate >= issueDateFrom);
        }

        if (query.IssueDateTo is DateOnly issueDateTo)
        {
            expenses = expenses.Where(expense => expense.IssueDate <= issueDateTo);
        }

        if (query.AmountMin is decimal amountMin)
        {
            expenses = expenses.Where(expense => expense.TotalAmount >= amountMin);
        }

        if (query.AmountMax is decimal amountMax)
        {
            expenses = expenses.Where(expense => expense.TotalAmount <= amountMax);
        }

        PagedResult<Expense> page = await CatalogPersistence.ToPageAsync(
            expenses.OrderByDescending(expense => expense.CreatedAtUtc),
            query.Page,
            query.PageSize,
            cancellationToken);

        return new PagedResult<ExpenseModel>(
            await MapAllAsync(page.Items, cancellationToken),
            page.Page,
            page.PageSize,
            page.TotalCount);
    }

    public async Task<ExpenseModel?> GetAsync(
        Guid id,
        Guid? restrictToOwnerUserId = null,
        CancellationToken cancellationToken = default)
    {
        Expense? expense = await dbContext.Set<Expense>()
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);

        if (expense is null || IsOwnedByAnotherUser(expense, restrictToOwnerUserId))
        {
            return null;
        }

        IReadOnlyList<ExpenseModel> mapped = await MapAllAsync([expense], cancellationToken);
        return mapped[0];
    }

    public async Task<ExpenseCreationResult> CreateAsync(
        SaveExpenseCommand command,
        Guid actorId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        CatalogOperationResult? validation = await ValidateHeaderReferencesAsync(command, cancellationToken);

        if (validation is not null)
        {
            return new ExpenseCreationResult(validation, null);
        }

        (CatalogOperationResult? taxError, TaxType? taxType) = await LoadActiveTaxTypeAsync(
            command.TaxTypeId,
            cancellationToken);

        if (taxError is not null)
        {
            return new ExpenseCreationResult(taxError, null);
        }

        try
        {
            string code = await CatalogPersistence.GenerateSequentialCodeAsync(
                dbContext,
                "finanzas.Seq_GastoCodigo",
                "GAS",
                3,
                cancellationToken);

            Expense expense = new(
                Guid.NewGuid(),
                code,
                command.SupplierId,
                command.CategoryId,
                command.Description,
                command.IssueDate,
                command.PaymentTerm,
                command.NetAmount,
                taxType!.Id,
                taxType.Code,
                taxType.Name,
                taxType.Rate,
                command.PaymentMethod,
                timeProvider.GetUtcNow(),
                actorId);

            dbContext.Add(expense);

            CatalogOperationResult result = await CatalogPersistence.SaveChangesAsync(
                dbContext,
                "No fue posible crear el gasto.",
                cancellationToken);

            return new ExpenseCreationResult(result, result.IsSuccess ? expense.Id : null);
        }
        catch (ArgumentException exception)
        {
            dbContext.ChangeTracker.Clear();
            return new ExpenseCreationResult(CatalogOperationResult.Invalid(exception.Message), null);
        }
    }

    public async Task<CatalogOperationResult> UpdateHeaderAsync(
        Guid id,
        SaveExpenseCommand command,
        string version,
        Guid actorId,
        Guid? restrictToOwnerUserId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        Expense? expense = await dbContext.Set<Expense>()
            .SingleOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);

        if (expense is null || IsOwnedByAnotherUser(expense, restrictToOwnerUserId))
        {
            return CatalogOperationResult.Missing();
        }

        if (!CatalogPersistence.TrySetOriginalVersion(dbContext, expense, version))
        {
            return CatalogOperationResult.Invalid("El token de concurrencia no es válido.");
        }

        CatalogOperationResult? validation = await ValidateHeaderReferencesAsync(command, cancellationToken);

        if (validation is not null)
        {
            dbContext.ChangeTracker.Clear();
            return validation;
        }

        (CatalogOperationResult? taxError, TaxType? taxType) = await LoadActiveTaxTypeAsync(
            command.TaxTypeId,
            cancellationToken);

        if (taxError is not null)
        {
            dbContext.ChangeTracker.Clear();
            return taxError;
        }

        try
        {
            expense.UpdateHeader(
                command.SupplierId,
                command.CategoryId,
                command.Description,
                command.IssueDate,
                command.PaymentTerm,
                command.NetAmount,
                taxType!.Id,
                taxType.Code,
                taxType.Name,
                taxType.Rate,
                command.PaymentMethod,
                timeProvider.GetUtcNow(),
                actorId);

            return await CatalogPersistence.SaveChangesAsync(
                dbContext,
                "No fue posible actualizar el gasto.",
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

    public async Task<CatalogOperationResult> ConfirmAsync(
        Guid id,
        ConfirmExpenseCommand command,
        string version,
        Guid actorId,
        bool actorIsManagement,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        Expense? expense = await dbContext.Set<Expense>()
            .SingleOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);

        if (expense is null)
        {
            return CatalogOperationResult.Missing();
        }

        if (!CatalogPersistence.TrySetOriginalVersion(dbContext, expense, version))
        {
            return CatalogOperationResult.Invalid("El token de concurrencia no es válido.");
        }

        bool isCash = expense.PaymentTerm == PaymentTerm.Cash;

        if (isCash)
        {
            CatalogOperationResult? cashValidation = await ValidateCashConfirmationAsync(
                command,
                expense.TotalAmount,
                actorIsManagement,
                cancellationToken);

            if (cashValidation is not null)
            {
                dbContext.ChangeTracker.Clear();
                return cashValidation;
            }
        }
        else if (command.CashAccountId is not null || command.Reference is not null)
        {
            dbContext.ChangeTracker.Clear();
            return CatalogOperationResult.Invalid(
                "Un gasto a crédito no indica cuenta de efectivo ni referencia al confirmarse.");
        }

        IDbContextTransaction? transaction = await CatalogPersistence.BeginTransactionIfNeededAsync(
            dbContext,
            cancellationToken);

        try
        {
            DateTimeOffset confirmedAtUtc = timeProvider.GetUtcNow();
            expense.Confirm(confirmedAtUtc, actorId, command.CashAccountId, command.Reference);

            if (!isCash)
            {
                dbContext.Add(new Payable(
                    Guid.NewGuid(),
                    expense.Id,
                    expense.SupplierId,
                    expense.TotalAmount,
                    expense.DueDate!.Value,
                    confirmedAtUtc,
                    actorId));
            }

            dbContext.Add(new OperationLogEntry(
                Guid.NewGuid(),
                OperationLogEntityType.Expense,
                expense.Id,
                OperationLogAction.Confirmed,
                reason: null,
                confirmedAtUtc,
                actorId));

            CatalogOperationResult result = await CatalogPersistence.SaveChangesAsync(
                dbContext,
                "No fue posible confirmar el gasto.",
                cancellationToken);

            if (result.IsSuccess && transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken);
            }

            return result;
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
        finally
        {
            if (transaction is not null)
            {
                await transaction.DisposeAsync();
            }
        }
    }

    public async Task<CatalogOperationResult> CancelAsync(
        Guid id,
        string reason,
        string version,
        Guid actorId,
        CancellationToken cancellationToken = default)
    {
        Expense? expense = await dbContext.Set<Expense>()
            .SingleOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);

        if (expense is null)
        {
            return CatalogOperationResult.Missing();
        }

        if (!CatalogPersistence.TrySetOriginalVersion(dbContext, expense, version))
        {
            return CatalogOperationResult.Invalid("El token de concurrencia no es válido.");
        }

        IDbContextTransaction? transaction = await CatalogPersistence.BeginTransactionIfNeededAsync(
            dbContext,
            cancellationToken);

        try
        {
            DateTimeOffset cancelledAtUtc = timeProvider.GetUtcNow();
            bool isCash = expense.PaymentTerm == PaymentTerm.Cash;
            expense.Cancel(reason, cancelledAtUtc, actorId);

            if (!isCash)
            {
                Payable? payable = await dbContext.Set<Payable>()
                    .SingleOrDefaultAsync(candidate => candidate.ExpenseId == expense.Id, cancellationToken);
                payable?.Cancel(cancelledAtUtc, actorId);
            }

            dbContext.Add(new OperationLogEntry(
                Guid.NewGuid(),
                OperationLogEntityType.Expense,
                expense.Id,
                OperationLogAction.Cancelled,
                reason,
                cancelledAtUtc,
                actorId));

            CatalogOperationResult result = await CatalogPersistence.SaveChangesAsync(
                dbContext,
                "No fue posible anular el gasto.",
                cancellationToken);

            if (result.IsSuccess && transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken);
            }

            return result;
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
        finally
        {
            if (transaction is not null)
            {
                await transaction.DisposeAsync();
            }
        }
    }

    public async Task<ExpenseBudgetCheck> CheckBudgetAsync(
        Guid categoryId,
        DateOnly issueDate,
        decimal totalAmount,
        CancellationToken cancellationToken = default)
    {
        Budget? budget = await dbContext.Set<Budget>()
            .AsNoTracking()
            .SingleOrDefaultAsync(
                candidate => candidate.CategoryId == categoryId
                    && candidate.PeriodYear == issueDate.Year
                    && candidate.PeriodMonth == issueDate.Month,
                cancellationToken);

        if (budget is null)
        {
            return new ExpenseBudgetCheck(false, null, 0m, false);
        }

        DateOnly periodStart = new(issueDate.Year, issueDate.Month, 1);
        DateOnly periodEnd = periodStart.AddMonths(1).AddDays(-1);

        decimal executedAmount = await dbContext.Set<Expense>()
            .AsNoTracking()
            .Where(expense => expense.CategoryId == categoryId
                && expense.Status == ExpenseStatus.Confirmed
                && expense.IssueDate >= periodStart
                && expense.IssueDate <= periodEnd)
            .Select(expense => expense.TotalAmount)
            .SumAsync(cancellationToken);

        decimal availableAmount = Math.Max(0m, budget.Amount - executedAmount);

        return new ExpenseBudgetCheck(true, budget.Amount, availableAmount, totalAmount > availableAmount);
    }

    private static bool IsOwnedByAnotherUser(Expense expense, Guid? restrictToOwnerUserId) =>
        restrictToOwnerUserId is Guid ownerId && expense.CreatedByUserId != ownerId;

    private async Task<CatalogOperationResult?> ValidateCashConfirmationAsync(
        ConfirmExpenseCommand command,
        decimal totalAmount,
        bool actorIsManagement,
        CancellationToken cancellationToken)
    {
        if (command.CashAccountId is null || string.IsNullOrWhiteSpace(command.Reference))
        {
            return CatalogOperationResult.Invalid(
                "Para confirmar un gasto de contado debe indicar cuenta de efectivo y referencia.");
        }

        bool isValidCashAccount = await dbContext.Set<LedgerAccount>()
            .AsNoTracking()
            .AnyAsync(
                account => account.Id == command.CashAccountId.Value && account.IsActive && account.CashKind != null,
                cancellationToken);

        if (!isValidCashAccount)
        {
            return CatalogOperationResult.Invalid(
                "La cuenta de efectivo seleccionada no existe, está inactiva o no es de efectivo.");
        }

        if (!actorIsManagement)
        {
            SystemParameters? parameters = await dbContext.Set<SystemParameters>()
                .AsNoTracking()
                .SingleOrDefaultAsync(cancellationToken);

            // Sin parámetros configurados no hay límite que aplicar de forma segura: se exige Gerencia
            // hasta que Gerencia los configure (regla 7), igual criterio que en abonos y pagos.
            decimal limit = parameters?.AuthorizationLimitCrc ?? 0m;

            if (totalAmount > limit)
            {
                return CatalogOperationResult.Invalid(
                    "El total del gasto supera el límite de autorización; cambie el plazo a crédito o "
                    + "pida a Gerencia que lo confirme de contado.");
            }
        }

        return null;
    }

    private async Task<CatalogOperationResult?> ValidateHeaderReferencesAsync(
        SaveExpenseCommand command,
        CancellationToken cancellationToken)
    {
        bool isValidSupplier = await dbContext.Set<Supplier>()
            .AsNoTracking()
            .AnyAsync(supplier => supplier.Id == command.SupplierId && supplier.IsActive, cancellationToken);

        if (!isValidSupplier)
        {
            return CatalogOperationResult.Invalid("El proveedor seleccionado no existe o está inactivo.");
        }

        bool isValidCategory = await dbContext.Set<FinancialCategory>()
            .AsNoTracking()
            .AnyAsync(
                category => category.Id == command.CategoryId
                    && category.IsActive
                    && category.Kind == FinancialCategoryKind.Expense,
                cancellationToken);

        if (!isValidCategory)
        {
            return CatalogOperationResult.Invalid("La categoría seleccionada no existe, está inactiva o no es de gasto.");
        }

        return null;
    }

    private async Task<(CatalogOperationResult? Error, TaxType? TaxType)> LoadActiveTaxTypeAsync(
        Guid taxTypeId,
        CancellationToken cancellationToken)
    {
        TaxType? taxType = await dbContext.Set<TaxType>()
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == taxTypeId, cancellationToken);

        if (taxType is null || !taxType.IsActive)
        {
            return (CatalogOperationResult.Invalid("La tarifa de IVA seleccionada no existe o está inactiva."), null);
        }

        return (null, taxType);
    }

    private async Task<IReadOnlyList<ExpenseModel>> MapAllAsync(
        IReadOnlyList<Expense> expenses,
        CancellationToken cancellationToken)
    {
        if (expenses.Count == 0)
        {
            return [];
        }

        Guid[] supplierIds = expenses.Select(expense => expense.SupplierId).Distinct().ToArray();
        IReadOnlyDictionary<Guid, string> supplierNames = await dbContext.Set<Supplier>()
            .AsNoTracking()
            .Where(supplier => supplierIds.Contains(supplier.Id))
            .ToDictionaryAsync(supplier => supplier.Id, supplier => supplier.Name, cancellationToken);

        Guid[] categoryIds = expenses.Select(expense => expense.CategoryId).Distinct().ToArray();
        IReadOnlyDictionary<Guid, string> categoryNames = await dbContext.Set<FinancialCategory>()
            .AsNoTracking()
            .Where(category => categoryIds.Contains(category.Id))
            .ToDictionaryAsync(category => category.Id, category => category.Name, cancellationToken);

        Guid[] cashAccountIds = expenses
            .Where(expense => expense.CashAccountId is not null)
            .Select(expense => expense.CashAccountId!.Value)
            .Distinct()
            .ToArray();
        IReadOnlyDictionary<Guid, string> cashAccountNames = cashAccountIds.Length == 0
            ? new Dictionary<Guid, string>()
            : await dbContext.Set<LedgerAccount>()
                .AsNoTracking()
                .Where(account => cashAccountIds.Contains(account.Id))
                .ToDictionaryAsync(account => account.Id, account => account.Name, cancellationToken);

        Guid[] userIds = expenses.Select(expense => expense.CreatedByUserId).Distinct().ToArray();
        IReadOnlyDictionary<Guid, string> userNames = await dbContext.Users
            .AsNoTracking()
            .Where(user => userIds.Contains(user.Id))
            .ToDictionaryAsync(user => user.Id, user => user.FullName, cancellationToken);

        return expenses
            .Select(expense => new ExpenseModel(
                expense.Id,
                expense.Code,
                expense.SupplierId,
                supplierNames.GetValueOrDefault(expense.SupplierId, "—"),
                expense.CategoryId,
                categoryNames.GetValueOrDefault(expense.CategoryId, "—"),
                expense.Description,
                expense.IssueDate,
                expense.PaymentTerm,
                expense.DueDate,
                expense.NetAmount,
                expense.TaxTypeId,
                expense.TaxCode,
                expense.TaxName,
                expense.TaxRate,
                expense.TaxAmount,
                expense.TotalAmount,
                expense.PaymentMethod,
                expense.CashAccountId,
                expense.CashAccountId is null ? null : cashAccountNames.GetValueOrDefault(expense.CashAccountId.Value, "—"),
                expense.Reference,
                expense.Status,
                expense.ConfirmedAtUtc,
                expense.CancellationReason,
                expense.CancelledAtUtc,
                expense.CreatedByUserId,
                userNames.GetValueOrDefault(expense.CreatedByUserId, "—"),
                expense.CreatedAtUtc,
                expense.UpdatedAtUtc,
                CatalogPersistence.EncodeVersion(expense.RowVersion)))
            .ToArray();
    }
}
