using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using SistemaFinanciero.Application.Catalogs.Common;
using SistemaFinanciero.Application.Invoices;
using SistemaFinanciero.Domain.Audit;
using SistemaFinanciero.Domain.Catalogs;
using SistemaFinanciero.Domain.Incomes;
using SistemaFinanciero.Domain.Invoices;
using SistemaFinanciero.Domain.Receivables;
using SistemaFinanciero.Infrastructure.Catalogs;
using SistemaFinanciero.Infrastructure.Parameters;
using SistemaFinanciero.Infrastructure.Persistence;

namespace SistemaFinanciero.Infrastructure.Invoices;

/// <summary>
/// Ejecuta el ciclo de vida de la factura administrativa: crear y editar el borrador, agregar,
/// reemplazar y quitar líneas, confirmar y anular.
/// </summary>
/// <remarks>
/// Conexiones: implementa <c>IInvoiceService</c> (Application); se registra en
/// <c>DependencyInjection.AddInfrastructure</c>; lo inyecta <c>InvoicesController</c> (Web, ruta
/// <c>/facturacion/facturas</c>). Lee y escribe <c>FinancialDbContext.Invoices</c> (con sus líneas en
/// cascada) y <c>finanzas.Seq_FacturaNumero</c> al confirmar (RF-34), con el mismo mecanismo que
/// <c>CatalogPersistence.GenerateSequentialCodeAsync</c> usa para los códigos de catálogo.
///
/// Confirmar y anular quedan además en la bitácora de operaciones (<see cref="OperationLogEntry"/>,
/// regla 4/16), dentro de la misma transacción que la mutación del dominio. Confirmar genera la
/// cuenta por cobrar (crédito) o el ingreso (contado, regla 2); anular la anula en cascada, siempre
/// que no tenga abonos vigentes. Todavía no produce el documento PDF de la regla 15; eso corresponde
/// al módulo de Reportes.
/// </remarks>
internal sealed class InvoiceService(
    FinancialDbContext dbContext,
    TimeProvider timeProvider) : IInvoiceService
{
    public async Task<PagedResult<InvoiceSummaryModel>> SearchAsync(
        InvoiceQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        IQueryable<Invoice> invoices = dbContext.Set<Invoice>().AsNoTracking();

        invoices = query.Status switch
        {
            InvoiceStatusFilter.Draft => invoices.Where(invoice => invoice.Status == InvoiceStatus.Draft),
            InvoiceStatusFilter.Confirmed => invoices.Where(invoice => invoice.Status == InvoiceStatus.Confirmed),
            InvoiceStatusFilter.Cancelled => invoices.Where(invoice => invoice.Status == InvoiceStatus.Cancelled),
            InvoiceStatusFilter.All => invoices,
            _ => throw new ArgumentOutOfRangeException(nameof(query)),
        };

        if (query.CreatedByUserId is Guid ownerId)
        {
            invoices = invoices.Where(invoice => invoice.CreatedByUserId == ownerId);
        }

        if (query.Search is not null)
        {
            string search = query.Search;
            bool isNumericSearch = long.TryParse(search, out long numericSearch);

            invoices = invoices.Where(invoice =>
                (isNumericSearch && invoice.Number == numericSearch) ||
                dbContext.Set<Customer>().Any(customer =>
                    customer.Id == invoice.CustomerId && customer.Name.Contains(search)));
        }

        if (query.IssueDateFrom is DateOnly issueDateFrom)
        {
            invoices = invoices.Where(invoice => invoice.IssueDate >= issueDateFrom);
        }

        if (query.IssueDateTo is DateOnly issueDateTo)
        {
            invoices = invoices.Where(invoice => invoice.IssueDate <= issueDateTo);
        }

        PagedResult<Invoice> page = await CatalogPersistence.ToPageAsync(
            invoices.OrderByDescending(invoice => invoice.CreatedAtUtc),
            query.Page,
            query.PageSize,
            cancellationToken);

        IReadOnlyDictionary<Guid, string> customerNames = await GetCustomerNamesAsync(
            page.Items.Select(invoice => invoice.CustomerId),
            cancellationToken);
        IReadOnlyDictionary<Guid, string> userNames = await GetUserNamesAsync(
            page.Items.Select(invoice => invoice.CreatedByUserId),
            cancellationToken);

        return new PagedResult<InvoiceSummaryModel>(
            page.Items.Select(invoice => new InvoiceSummaryModel(
                invoice.Id,
                invoice.Number,
                customerNames.GetValueOrDefault(invoice.CustomerId, "—"),
                invoice.IssueDate,
                invoice.PaymentTerm,
                invoice.DueDate,
                invoice.Status,
                invoice.TotalAmount,
                userNames.GetValueOrDefault(invoice.CreatedByUserId, "—"))).ToArray(),
            page.Page,
            page.PageSize,
            page.TotalCount);
    }

    public async Task<InvoiceModel?> GetAsync(
        Guid id,
        Guid? restrictToOwnerUserId = null,
        CancellationToken cancellationToken = default)
    {
        Invoice? invoice = await dbContext.Set<Invoice>()
            .AsNoTracking()
            .Include(candidate => candidate.Lines)
            .SingleOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);

        if (invoice is null || IsOwnedByAnotherUser(invoice, restrictToOwnerUserId))
        {
            return null;
        }

        IReadOnlyDictionary<Guid, string> customerNames = await GetCustomerNamesAsync(
            [invoice.CustomerId],
            cancellationToken);
        IReadOnlyDictionary<Guid, string> userNames = await GetUserNamesAsync(
            [invoice.CreatedByUserId],
            cancellationToken);

        return MapInvoice(
            invoice,
            customerNames.GetValueOrDefault(invoice.CustomerId, "—"),
            userNames.GetValueOrDefault(invoice.CreatedByUserId, "—"));
    }

    public async Task<InvoiceCreationResult> CreateAsync(
        CreateInvoiceCommand command,
        Guid actorId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        CatalogOperationResult? customerValidation = await ValidateCustomerAsync(
            command.CustomerId,
            cancellationToken);

        if (customerValidation is not null)
        {
            return new InvoiceCreationResult(customerValidation, null);
        }

        string? closingDateViolation = await ClosingDateGuard.GetViolationMessageAsync(
            dbContext, command.IssueDate, cancellationToken);

        if (closingDateViolation is not null)
        {
            return new InvoiceCreationResult(CatalogOperationResult.Invalid(closingDateViolation), null);
        }

        try
        {
            Invoice invoice = new(
                Guid.NewGuid(),
                command.CustomerId,
                command.IssueDate,
                command.PaymentTerm,
                timeProvider.GetUtcNow(),
                actorId);

            dbContext.Add(invoice);

            CatalogOperationResult result = await CatalogPersistence.SaveChangesAsync(
                dbContext,
                "No fue posible crear la factura.",
                cancellationToken);

            return new InvoiceCreationResult(result, result.IsSuccess ? invoice.Id : null);
        }
        catch (ArgumentException exception)
        {
            dbContext.ChangeTracker.Clear();
            return new InvoiceCreationResult(CatalogOperationResult.Invalid(exception.Message), null);
        }
    }

    public async Task<CatalogOperationResult> UpdateHeaderAsync(
        Guid id,
        UpdateInvoiceHeaderCommand command,
        string version,
        Guid actorId,
        Guid? restrictToOwnerUserId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        Invoice? invoice = await LoadTrackedAsync(id, includeLines: false, restrictToOwnerUserId, cancellationToken);

        if (invoice is null)
        {
            return CatalogOperationResult.Missing();
        }

        if (!CatalogPersistence.TrySetOriginalVersion(dbContext, invoice, version))
        {
            return CatalogOperationResult.Invalid("El token de concurrencia no es válido.");
        }

        CatalogOperationResult? customerValidation = await ValidateCustomerAsync(
            command.CustomerId,
            cancellationToken);

        if (customerValidation is not null)
        {
            dbContext.ChangeTracker.Clear();
            return customerValidation;
        }

        try
        {
            invoice.UpdateHeader(
                command.CustomerId,
                command.IssueDate,
                command.PaymentTerm,
                timeProvider.GetUtcNow(),
                actorId);

            return await CatalogPersistence.SaveChangesAsync(
                dbContext,
                "No fue posible actualizar la factura.",
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

    public async Task<CatalogOperationResult> AddLineAsync(
        Guid invoiceId,
        InvoiceLineInputCommand command,
        string version,
        Guid actorId,
        Guid? restrictToOwnerUserId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        Invoice? invoice = await LoadTrackedAsync(invoiceId, includeLines: true, restrictToOwnerUserId, cancellationToken);

        if (invoice is null)
        {
            return CatalogOperationResult.Missing();
        }

        if (!CatalogPersistence.TrySetOriginalVersion(dbContext, invoice, version))
        {
            return CatalogOperationResult.Invalid("El token de concurrencia no es válido.");
        }

        (CatalogOperationResult? taxError, TaxType? taxType) = await LoadActiveTaxTypeAsync(
            command.TaxTypeId,
            cancellationToken);

        if (taxError is not null)
        {
            dbContext.ChangeTracker.Clear();
            return taxError;
        }

        CatalogOperationResult? itemValidation = await ValidateCatalogItemAsync(
            command.CatalogItemId,
            cancellationToken);

        if (itemValidation is not null)
        {
            dbContext.ChangeTracker.Clear();
            return itemValidation;
        }

        try
        {
            InvoiceTaxSpecification tax = new(taxType!.Code, taxType.Name, taxType.Rate, taxType.Id);
            InvoiceLine line = new(
                Guid.NewGuid(),
                command.CatalogItemId,
                command.Description,
                command.UnitOfMeasure,
                command.Quantity,
                command.UnitPrice,
                command.DiscountAmount,
                tax);

            invoice.AddLine(line, timeProvider.GetUtcNow(), actorId);

            return await CatalogPersistence.SaveChangesAsync(
                dbContext,
                "No fue posible agregar la línea.",
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

    public async Task<CatalogOperationResult> ReplaceLineAsync(
        Guid invoiceId,
        Guid lineId,
        InvoiceLineInputCommand command,
        string version,
        Guid actorId,
        Guid? restrictToOwnerUserId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        Invoice? invoice = await LoadTrackedAsync(invoiceId, includeLines: true, restrictToOwnerUserId, cancellationToken);

        if (invoice is null)
        {
            return CatalogOperationResult.Missing();
        }

        if (!CatalogPersistence.TrySetOriginalVersion(dbContext, invoice, version))
        {
            return CatalogOperationResult.Invalid("El token de concurrencia no es válido.");
        }

        (CatalogOperationResult? taxError, TaxType? taxType) = await LoadActiveTaxTypeAsync(
            command.TaxTypeId,
            cancellationToken);

        if (taxError is not null)
        {
            dbContext.ChangeTracker.Clear();
            return taxError;
        }

        CatalogOperationResult? itemValidation = await ValidateCatalogItemAsync(
            command.CatalogItemId,
            cancellationToken);

        if (itemValidation is not null)
        {
            dbContext.ChangeTracker.Clear();
            return itemValidation;
        }

        try
        {
            InvoiceTaxSpecification tax = new(taxType!.Code, taxType.Name, taxType.Rate, taxType.Id);
            InvoiceLine replacement = new(
                Guid.NewGuid(),
                command.CatalogItemId,
                command.Description,
                command.UnitOfMeasure,
                command.Quantity,
                command.UnitPrice,
                command.DiscountAmount,
                tax);

            invoice.ReplaceLine(lineId, replacement, timeProvider.GetUtcNow(), actorId);

            return await CatalogPersistence.SaveChangesAsync(
                dbContext,
                "No fue posible reemplazar la línea.",
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

    public async Task<CatalogOperationResult> RemoveLineAsync(
        Guid invoiceId,
        Guid lineId,
        string version,
        Guid actorId,
        Guid? restrictToOwnerUserId = null,
        CancellationToken cancellationToken = default)
    {
        Invoice? invoice = await LoadTrackedAsync(invoiceId, includeLines: true, restrictToOwnerUserId, cancellationToken);

        if (invoice is null)
        {
            return CatalogOperationResult.Missing();
        }

        if (!CatalogPersistence.TrySetOriginalVersion(dbContext, invoice, version))
        {
            return CatalogOperationResult.Invalid("El token de concurrencia no es válido.");
        }

        try
        {
            invoice.RemoveLine(lineId, timeProvider.GetUtcNow(), actorId);

            return await CatalogPersistence.SaveChangesAsync(
                dbContext,
                "No fue posible quitar la línea.",
                cancellationToken);
        }
        catch (InvalidOperationException exception)
        {
            dbContext.ChangeTracker.Clear();
            return CatalogOperationResult.Invalid(exception.Message);
        }
    }

    public async Task<CatalogOperationResult> ConfirmAsync(
        Guid id,
        ConfirmInvoiceCommand command,
        string version,
        Guid actorId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        Invoice? invoice = await LoadTrackedAsync(id, includeLines: true, restrictToOwnerUserId: null, cancellationToken);

        if (invoice is null)
        {
            return CatalogOperationResult.Missing();
        }

        if (!CatalogPersistence.TrySetOriginalVersion(dbContext, invoice, version))
        {
            return CatalogOperationResult.Invalid("El token de concurrencia no es válido.");
        }

        string? closingDateViolation = await ClosingDateGuard.GetViolationMessageAsync(
            dbContext, invoice.IssueDate, cancellationToken);

        if (closingDateViolation is not null)
        {
            dbContext.ChangeTracker.Clear();
            return CatalogOperationResult.Invalid(closingDateViolation);
        }

        bool isCash = invoice.PaymentTerm == PaymentTerm.Cash;

        if (isCash)
        {
            CatalogOperationResult? cashValidation = await ValidateCashConfirmationAsync(command, cancellationToken);

            if (cashValidation is not null)
            {
                dbContext.ChangeTracker.Clear();
                return cashValidation;
            }
        }

        IDbContextTransaction? transaction = await CatalogPersistence.BeginTransactionIfNeededAsync(
            dbContext,
            cancellationToken);

        try
        {
            long number = await CatalogPersistence.GetNextSequenceValueAsync(
                dbContext,
                "finanzas.Seq_FacturaNumero",
                cancellationToken);

            DateTimeOffset confirmedAtUtc = timeProvider.GetUtcNow();
            invoice.Confirm(confirmedAtUtc, actorId, number);

            if (isCash)
            {
                string incomeCode = await CatalogPersistence.GenerateSequentialCodeAsync(
                    dbContext,
                    "finanzas.Seq_IngresoCodigo",
                    "ING",
                    3,
                    cancellationToken);

                dbContext.Add(Income.FromInvoice(
                    Guid.NewGuid(),
                    incomeCode,
                    invoice.Id,
                    command.CategoryId!.Value,
                    command.CashAccountId!.Value,
                    invoice.TotalAmount,
                    DateOnly.FromDateTime(confirmedAtUtc.UtcDateTime),
                    command.PaymentMethod!.Value,
                    command.Reference!,
                    confirmedAtUtc,
                    actorId));
            }
            else
            {
                dbContext.Add(new Receivable(
                    Guid.NewGuid(),
                    invoice.Id,
                    invoice.CustomerId,
                    invoice.TotalAmount,
                    invoice.DueDate!.Value,
                    confirmedAtUtc,
                    actorId));
            }

            dbContext.Add(new OperationLogEntry(
                Guid.NewGuid(),
                OperationLogEntityType.Invoice,
                invoice.Id,
                OperationLogAction.Confirmed,
                reason: null,
                confirmedAtUtc,
                actorId));

            CatalogOperationResult result = await CatalogPersistence.SaveChangesAsync(
                dbContext,
                "No fue posible confirmar la factura.",
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
        Invoice? invoice = await LoadTrackedAsync(id, includeLines: false, restrictToOwnerUserId: null, cancellationToken);

        if (invoice is null)
        {
            return CatalogOperationResult.Missing();
        }

        if (!CatalogPersistence.TrySetOriginalVersion(dbContext, invoice, version))
        {
            return CatalogOperationResult.Invalid("El token de concurrencia no es válido.");
        }

        string? closingDateViolation = await ClosingDateGuard.GetViolationMessageAsync(
            dbContext, invoice.IssueDate, cancellationToken);

        if (closingDateViolation is not null)
        {
            dbContext.ChangeTracker.Clear();
            return CatalogOperationResult.Invalid(closingDateViolation);
        }

        IDbContextTransaction? transaction = await CatalogPersistence.BeginTransactionIfNeededAsync(
            dbContext,
            cancellationToken);

        try
        {
            DateTimeOffset cancelledAtUtc = timeProvider.GetUtcNow();
            bool isCash = invoice.PaymentTerm == PaymentTerm.Cash;
            invoice.Cancel(reason, cancelledAtUtc, actorId);

            if (isCash)
            {
                Income? income = await dbContext.Set<Income>()
                    .SingleOrDefaultAsync(candidate => candidate.InvoiceId == invoice.Id, cancellationToken);
                income?.Cancel(reason, cancelledAtUtc, actorId);
            }
            else
            {
                Receivable? receivable = await dbContext.Set<Receivable>()
                    .SingleOrDefaultAsync(candidate => candidate.InvoiceId == invoice.Id, cancellationToken);
                receivable?.Cancel(cancelledAtUtc, actorId);
            }

            dbContext.Add(new OperationLogEntry(
                Guid.NewGuid(),
                OperationLogEntityType.Invoice,
                invoice.Id,
                OperationLogAction.Cancelled,
                reason,
                cancelledAtUtc,
                actorId));

            CatalogOperationResult result = await CatalogPersistence.SaveChangesAsync(
                dbContext,
                "No fue posible anular la factura.",
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

    private async Task<CatalogOperationResult?> ValidateCashConfirmationAsync(
        ConfirmInvoiceCommand command,
        CancellationToken cancellationToken)
    {
        if (command.CashAccountId is null ||
            command.PaymentMethod is null ||
            command.CategoryId is null ||
            string.IsNullOrWhiteSpace(command.Reference))
        {
            return CatalogOperationResult.Invalid(
                "Para confirmar una factura de contado debe indicar cuenta de efectivo, medio de pago, "
                + "referencia y categoría.");
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

        bool isValidCategory = await dbContext.Set<FinancialCategory>()
            .AsNoTracking()
            .AnyAsync(
                category => category.Id == command.CategoryId.Value
                    && category.IsActive
                    && category.Kind == FinancialCategoryKind.Income,
                cancellationToken);

        if (!isValidCategory)
        {
            return CatalogOperationResult.Invalid(
                "La categoría seleccionada no existe, está inactiva o no es de ingreso.");
        }

        return null;
    }

    private async Task<Invoice?> LoadTrackedAsync(
        Guid id,
        bool includeLines,
        Guid? restrictToOwnerUserId,
        CancellationToken cancellationToken)
    {
        IQueryable<Invoice> query = dbContext.Set<Invoice>();

        if (includeLines)
        {
            query = query.Include(invoice => invoice.Lines);
        }

        Invoice? invoice = await query.SingleOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);

        return invoice is null || IsOwnedByAnotherUser(invoice, restrictToOwnerUserId) ? null : invoice;
    }

    private static bool IsOwnedByAnotherUser(Invoice invoice, Guid? restrictToOwnerUserId) =>
        restrictToOwnerUserId is Guid ownerId && invoice.CreatedByUserId != ownerId;

    private async Task<CatalogOperationResult?> ValidateCustomerAsync(
        Guid customerId,
        CancellationToken cancellationToken)
    {
        bool isValid = await dbContext.Set<Customer>()
            .AsNoTracking()
            .AnyAsync(customer => customer.Id == customerId && customer.IsActive, cancellationToken);

        return isValid ? null : CatalogOperationResult.Invalid("El cliente seleccionado no existe o está inactivo.");
    }

    private async Task<CatalogOperationResult?> ValidateCatalogItemAsync(
        Guid? catalogItemId,
        CancellationToken cancellationToken)
    {
        if (catalogItemId is null)
        {
            return null;
        }

        bool isValid = await dbContext.Set<CatalogItem>()
            .AsNoTracking()
            .AnyAsync(item => item.Id == catalogItemId.Value && item.IsActive, cancellationToken);

        return isValid
            ? null
            : CatalogOperationResult.Invalid("El producto o servicio seleccionado no existe o está inactivo.");
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

    private async Task<IReadOnlyDictionary<Guid, string>> GetCustomerNamesAsync(
        IEnumerable<Guid> customerIds,
        CancellationToken cancellationToken)
    {
        Guid[] ids = customerIds.Distinct().ToArray();

        if (ids.Length == 0)
        {
            return new Dictionary<Guid, string>();
        }

        return await dbContext.Set<Customer>()
            .AsNoTracking()
            .Where(customer => ids.Contains(customer.Id))
            .ToDictionaryAsync(customer => customer.Id, customer => customer.Name, cancellationToken);
    }

    private async Task<IReadOnlyDictionary<Guid, string>> GetUserNamesAsync(
        IEnumerable<Guid> userIds,
        CancellationToken cancellationToken)
    {
        Guid[] ids = userIds.Distinct().ToArray();

        if (ids.Length == 0)
        {
            return new Dictionary<Guid, string>();
        }

        return await dbContext.Users
            .AsNoTracking()
            .Where(user => ids.Contains(user.Id))
            .ToDictionaryAsync(user => user.Id, user => user.FullName, cancellationToken);
    }

    private static InvoiceModel MapInvoice(Invoice invoice, string customerName, string createdByUserName)
    {
        return new InvoiceModel(
            invoice.Id,
            invoice.Number,
            invoice.CustomerId,
            customerName,
            invoice.IssueDate,
            invoice.PaymentTerm,
            invoice.DueDate,
            invoice.Status,
            invoice.GrossAmount,
            invoice.DiscountAmount,
            invoice.NetAmount,
            invoice.TaxAmount,
            invoice.TotalAmount,
            invoice.ConfirmedAtUtc,
            invoice.CancellationReason,
            invoice.CancelledAtUtc,
            invoice.CreatedByUserId,
            createdByUserName,
            invoice.CreatedAtUtc,
            invoice.UpdatedAtUtc,
            CatalogPersistence.EncodeVersion(invoice.RowVersion),
            invoice.Lines.Select(MapLine).ToArray());
    }

    private static InvoiceLineModel MapLine(InvoiceLine line)
    {
        return new InvoiceLineModel(
            line.Id,
            line.Position,
            line.CatalogItemId,
            line.Description,
            line.UnitOfMeasure,
            line.Quantity,
            line.UnitPrice,
            line.GrossAmount,
            line.DiscountAmount,
            line.NetAmount,
            line.Tax.Code,
            line.Tax.Name,
            line.Tax.Rate,
            line.Tax.TaxableAmount,
            line.Tax.Amount,
            line.TotalAmount);
    }
}
