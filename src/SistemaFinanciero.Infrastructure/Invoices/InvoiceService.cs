using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using SistemaFinanciero.Application.Catalogs.Common;
using SistemaFinanciero.Application.Invoices;
using SistemaFinanciero.Domain.Audit;
using SistemaFinanciero.Domain.Catalogs;
using SistemaFinanciero.Domain.Common;
using SistemaFinanciero.Domain.Incomes;
using SistemaFinanciero.Domain.Invoices;
using SistemaFinanciero.Domain.Parameters;
using SistemaFinanciero.Domain.Receivables;
using SistemaFinanciero.Domain.Ledger;
using SistemaFinanciero.Infrastructure.Catalogs;
using SistemaFinanciero.Infrastructure.Common;
using SistemaFinanciero.Infrastructure.Ledger;
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
                (invoice.ElectronicInvoiceKey != null && invoice.ElectronicInvoiceKey.Contains(search)) ||
                dbContext.Set<Customer>().Any(customer =>
                    customer.Id == invoice.CustomerId && customer.Name.Contains(search)));
        }

        if (query.Number is long number)
        {
            invoices = invoices.Where(invoice => invoice.Number == number);
        }

        if (query.CustomerName is string customerName)
        {
            invoices = invoices.Where(invoice =>
                dbContext.Set<Customer>().Any(customer =>
                    customer.Id == invoice.CustomerId && customer.Name.Contains(customerName)));
        }

        if (query.IssueDateFrom is DateOnly issueDateFrom)
        {
            invoices = invoices.Where(invoice => invoice.IssueDate >= issueDateFrom);
        }

        if (query.IssueDate is DateOnly issueDate)
        {
            invoices = invoices.Where(invoice => invoice.IssueDate == issueDate);
        }

        if (query.IssueDateTo is DateOnly issueDateTo)
        {
            invoices = invoices.Where(invoice => invoice.IssueDate <= issueDateTo);
        }

        if (query.DueDate is DateOnly dueDate)
        {
            invoices = invoices.Where(invoice => invoice.DueDate == dueDate);
        }

        if (query.PaymentTerm is PaymentTerm paymentTerm)
        {
            invoices = invoices.Where(invoice => invoice.PaymentTerm == paymentTerm);
        }

        if (query.TotalAmount is decimal totalAmount)
        {
            invoices = invoices.Where(invoice => invoice.TotalAmount == totalAmount);
        }

        if (query.CreatedByUserName is string createdByUserName)
        {
            invoices = invoices.Where(invoice =>
                dbContext.Users.Any(user =>
                    user.Id == invoice.CreatedByUserId && user.FullName.Contains(createdByUserName)));
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

        CustomerContact? customer = await GetCustomerContactAsync(invoice.CustomerId, cancellationToken);
        string accountName = await GetAccountNameAsync(invoice.IncomeAccountId, cancellationToken);
        IReadOnlyDictionary<Guid, string> userNames = await GetUserNamesAsync(
            [invoice.CreatedByUserId],
            cancellationToken);
        IReadOnlyDictionary<Guid, CatalogItemInfo> catalogItemInfo = await GetCatalogItemInfoAsync(
            invoice.Lines
                .Where(line => line.CatalogItemId.HasValue)
                .Select(line => line.CatalogItemId!.Value),
            cancellationToken);
        PaymentMethod? paymentMethod = await dbContext.Set<Income>()
            .AsNoTracking()
            .Where(income => income.InvoiceId == invoice.Id)
            .Select(income => income.PaymentMethod)
            .SingleOrDefaultAsync(cancellationToken);

        return MapInvoice(
            invoice,
            customer?.Name ?? "—",
            customer?.IdentificationType ?? IdentificationType.ForeignNonResident,
            customer?.Identification ?? "—",
            customer?.Email,
            customer?.Phone,
            customer?.VatCondition ?? VatCondition.Normal,
            customer?.ActivityCode,
            accountName,
            userNames.GetValueOrDefault(invoice.CreatedByUserId, "—"),
            catalogItemInfo,
            paymentMethod);
    }

    private async Task<string> GetAccountNameAsync(Guid accountId, CancellationToken cancellationToken)
    {
        return await dbContext.Set<LedgerAccount>()
            .AsNoTracking()
            .Where(account => account.Id == accountId)
            .Select(account => account.Name)
            .SingleOrDefaultAsync(cancellationToken) ?? "—";
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

        CatalogOperationResult? categoryValidation = await ValidateIncomeAccountAsync(
            command.IncomeAccountId,
            cancellationToken);

        if (categoryValidation is not null)
        {
            return new InvoiceCreationResult(categoryValidation, null);
        }

        string? closingDateViolation = await ClosingDateGuard.GetViolationMessageAsync(
            dbContext, command.IssueDate, timeProvider, cancellationToken);

        if (closingDateViolation is not null)
        {
            return new InvoiceCreationResult(CatalogOperationResult.Invalid(closingDateViolation), null);
        }

        try
        {
            Invoice invoice = new(
                Guid.NewGuid(),
                command.CustomerId,
                command.IncomeAccountId,
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

        // Con líneas siempre: si el cliente cambia, hay que recalcular la tarifa de cada una según
        // la nueva condición de IVA (RF-93, bloque 1 del paquete v7).
        Invoice? invoice = await LoadTrackedAsync(id, includeLines: true, restrictToOwnerUserId, cancellationToken);

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

        CatalogOperationResult? categoryValidation = await ValidateIncomeAccountAsync(
            command.IncomeAccountId,
            cancellationToken);

        if (categoryValidation is not null)
        {
            dbContext.ChangeTracker.Clear();
            return categoryValidation;
        }

        bool customerChanged = command.CustomerId != invoice.CustomerId;

        try
        {
            invoice.UpdateHeader(
                command.CustomerId,
                command.IncomeAccountId,
                command.IssueDate,
                command.PaymentTerm,
                timeProvider.GetUtcNow(),
                actorId);

            int recalculatedLineCount = customerChanged
                ? await RecalculateLineTaxesForNewCustomerAsync(invoice, actorId, cancellationToken)
                : 0;

            CatalogOperationResult saveResult = await CatalogPersistence.SaveChangesAsync(
                dbContext,
                "No fue posible actualizar la factura.",
                cancellationToken);

            if (saveResult.IsSuccess && recalculatedLineCount > 0)
            {
                return new CatalogOperationResult(
                    CatalogOperationStatus.Success,
                    recalculatedLineCount == 1
                        ? "Se recalculó el IVA de 1 línea según la nueva condición del cliente."
                        : $"Se recalculó el IVA de {recalculatedLineCount} líneas según la nueva condición del cliente.");
            }

            return saveResult;
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
    /// Vuelve a resolver la tarifa de cada línea existente según la nueva condición de IVA del
    /// cliente (RF-93, bloque 1 del paquete v7) y reemplaza solo las que cambian; conserva la
    /// posición y los demás datos de cada línea. Devuelve cuántas líneas cambiaron, para informarlo.
    /// </summary>
    private async Task<int> RecalculateLineTaxesForNewCustomerAsync(
        Invoice invoice,
        Guid actorId,
        CancellationToken cancellationToken)
    {
        int changedCount = 0;

        foreach (InvoiceLine line in invoice.Lines.ToArray())
        {
            Guid? defaultTaxTypeId = line.CatalogItemId is Guid catalogItemId
                ? await dbContext.Set<CatalogItem>()
                    .AsNoTracking()
                    .Where(item => item.Id == catalogItemId)
                    .Select(item => item.DefaultTaxTypeId)
                    .SingleOrDefaultAsync(cancellationToken)
                : null;

            (CatalogOperationResult? error, Guid newTaxTypeId) = await ResolveLineTaxTypeIdAsync(
                invoice.CustomerId,
                defaultTaxTypeId,
                commandTaxTypeId: null,
                cancellationToken);

            if (error is not null || newTaxTypeId == line.Tax.TaxTypeId)
            {
                continue;
            }

            (CatalogOperationResult? taxError, TaxType? taxType) = await LoadActiveTaxTypeAsync(
                newTaxTypeId,
                cancellationToken);

            if (taxError is not null)
            {
                continue;
            }

            InvoiceTaxSpecification tax = new(taxType!.Code, taxType.Name, taxType.Rate, taxType.HaciendaRateCode, taxType.Id);
            InvoiceLine replacement = new(
                Guid.NewGuid(),
                line.CatalogItemId,
                line.CabysCode,
                line.Description,
                line.UnitOfMeasure,
                line.Quantity,
                line.UnitPrice,
                line.DiscountAmount,
                tax);

            invoice.ReplaceLine(line.Id, replacement, timeProvider.GetUtcNow(), actorId);
            changedCount++;
        }

        return changedCount;
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

        (CatalogOperationResult? lineError, CatalogItem? catalogItem, decimal unitPrice, Guid taxTypeId) =
            await ResolveLineInputAsync(invoice.CustomerId, command, cancellationToken);

        if (lineError is not null)
        {
            dbContext.ChangeTracker.Clear();
            return lineError;
        }

        (CatalogOperationResult? taxError, TaxType? taxType) = await LoadActiveTaxTypeAsync(
            taxTypeId,
            cancellationToken);

        if (taxError is not null)
        {
            dbContext.ChangeTracker.Clear();
            return taxError;
        }

        try
        {
            InvoiceTaxSpecification tax = new(taxType!.Code, taxType.Name, taxType.Rate, taxType.HaciendaRateCode, taxType.Id);
            InvoiceLine line = new(
                Guid.NewGuid(),
                catalogItem!.Id,
                catalogItem.CabysCode,
                catalogItem.Name,
                catalogItem.UnitOfMeasure.ToLabel(),
                command.Quantity,
                unitPrice,
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

        (CatalogOperationResult? lineError, CatalogItem? catalogItem, decimal unitPrice, Guid taxTypeId) =
            await ResolveLineInputAsync(invoice.CustomerId, command, cancellationToken);

        if (lineError is not null)
        {
            dbContext.ChangeTracker.Clear();
            return lineError;
        }

        (CatalogOperationResult? taxError, TaxType? taxType) = await LoadActiveTaxTypeAsync(
            taxTypeId,
            cancellationToken);

        if (taxError is not null)
        {
            dbContext.ChangeTracker.Clear();
            return taxError;
        }

        try
        {
            InvoiceTaxSpecification tax = new(taxType!.Code, taxType.Name, taxType.Rate, taxType.HaciendaRateCode, taxType.Id);
            InvoiceLine replacement = new(
                Guid.NewGuid(),
                catalogItem!.Id,
                catalogItem.CabysCode,
                catalogItem.Name,
                catalogItem.UnitOfMeasure.ToLabel(),
                command.Quantity,
                unitPrice,
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
            dbContext, invoice.IssueDate, timeProvider, cancellationToken);

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

        SystemParameters? parameters = await dbContext.SystemParameters.AsNoTracking()
            .SingleOrDefaultAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(parameters?.CompanyLegalId)
            || string.IsNullOrWhiteSpace(parameters.CompanyBranchCode)
            || string.IsNullOrWhiteSpace(parameters.CompanyTerminalCode))
        {
            dbContext.ChangeTracker.Clear();
            return CatalogOperationResult.Invalid(
                "Configure la cédula jurídica, la sucursal y la terminal de la empresa en Parámetros "
                + "antes de confirmar facturas.");
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

            long claveSequential = await CatalogPersistence.GetNextSequenceValueAsync(
                dbContext,
                "finanzas.Seq_FacturaClaveConsecutivo",
                cancellationToken);

            string consecutivo = ElectronicDocumentKeyBuilder.BuildConsecutivo(
                ElectronicDocumentKeyBuilder.InvoiceDocumentTypeCode,
                parameters.CompanyBranchCode,
                parameters.CompanyTerminalCode,
                claveSequential);

            string issuerId = ElectronicDocumentKeyBuilder.PadIssuerId(parameters.CompanyLegalId);
            string electronicInvoiceKey = ElectronicDocumentKeyBuilder.BuildKey(
                invoice.IssueDate, issuerId, consecutivo, ElectronicDocumentKeyBuilder.GenerateSecurityCode());

            DateTimeOffset confirmedAtUtc = timeProvider.GetUtcNow();
            invoice.Confirm(confirmedAtUtc, actorId, number, electronicInvoiceKey, consecutivo);

            if (isCash)
            {
                string incomeCode = await CatalogPersistence.GenerateSequentialCodeAsync(
                    dbContext,
                    "finanzas.Seq_IngresoCodigo",
                    "REC",
                    6,
                    cancellationToken);

                dbContext.Add(Income.FromInvoice(
                    Guid.NewGuid(),
                    incomeCode,
                    invoice.Id,
                    invoice.IncomeAccountId,
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

            await JournalPostingService.PostInvoiceConfirmationAsync(
                dbContext,
                invoice,
                isCash ? command.CashAccountId : null,
                confirmedAtUtc,
                actorId,
                cancellationToken);

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

    public Task<bool> HasDuplicateCashReferenceAsync(
        Guid cashAccountId,
        string reference,
        CancellationToken cancellationToken = default) =>
        DuplicateReferenceGuard.ExistsAsync(dbContext, cashAccountId, reference, cancellationToken);

    public async Task<CustomerCreditLimitCheck> CheckCustomerCreditLimitAsync(
        Guid customerId,
        decimal invoiceTotalAmount,
        CancellationToken cancellationToken = default)
    {
        decimal? creditLimit = await dbContext.Set<Customer>()
            .AsNoTracking()
            .Where(customer => customer.Id == customerId)
            .Select(customer => customer.CreditLimit)
            .SingleOrDefaultAsync(cancellationToken);

        decimal outstandingBalance = await dbContext.Set<Receivable>()
            .AsNoTracking()
            .Where(receivable => receivable.CustomerId == customerId
                && (receivable.Status == ReceivableStatus.Pending
                    || receivable.Status == ReceivableStatus.PartiallyPaid))
            .Select(receivable => receivable.Balance)
            .SumAsync(cancellationToken);

        bool exceedsLimit = creditLimit is decimal limit
            && outstandingBalance + invoiceTotalAmount > limit;

        return new CustomerCreditLimitCheck(
            creditLimit is not null,
            creditLimit,
            outstandingBalance,
            exceedsLimit);
    }

    private async Task<CatalogOperationResult?> ValidateCashConfirmationAsync(
        ConfirmInvoiceCommand command,
        CancellationToken cancellationToken)
    {
        if (command.CashAccountId is null ||
            command.PaymentMethod is null ||
            string.IsNullOrWhiteSpace(command.Reference))
        {
            return CatalogOperationResult.Invalid(
                "Para confirmar una factura de contado debe indicar cuenta de efectivo, medio de pago "
                + "y referencia.");
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

    /// <summary>
    /// Valida la cuenta de ingreso de la venta: pertenece al encabezado de la factura, no a cada cobro,
    /// así que se valida aquí en vez de al confirmar o al registrar cada abono.
    /// </summary>
    private async Task<CatalogOperationResult?> ValidateIncomeAccountAsync(
        Guid incomeAccountId,
        CancellationToken cancellationToken)
    {
        bool isValid = await dbContext.Set<LedgerAccount>()
            .AsNoTracking()
            .AnyAsync(
                account => account.Id == incomeAccountId
                    && account.IsActive
                    && account.Type == LedgerAccountType.Income
                    && account.IsMovement,
                cancellationToken);

        return isValid
            ? null
            : CatalogOperationResult.Invalid(
                "La cuenta de ingreso seleccionada no existe, está inactiva, no es de ingreso o no es de movimiento.");
    }

    /// <summary>
    /// Carga el artículo del catálogo de la línea y resuelve el precio unitario y la tarifa de IVA: si
    /// el artículo tiene su propio precio de referencia o su tarifa predeterminada, esos valores se
    /// usan siempre; si no, se exige el valor correspondiente de <paramref name="command"/> (ADR-0043).
    /// La tarifa final siempre pasa por <see cref="ResolveLineTaxTypeIdAsync"/>, que la ajusta o la
    /// valida según la condición de IVA de <paramref name="customerId"/> (bloque 1 del paquete v7).
    /// </summary>
    private async Task<(CatalogOperationResult? Error, CatalogItem? CatalogItem, decimal UnitPrice, Guid TaxTypeId)>
        ResolveLineInputAsync(
            Guid customerId,
            InvoiceLineInputCommand command,
            CancellationToken cancellationToken)
    {
        CatalogItem? catalogItem = await dbContext.Set<CatalogItem>()
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == command.CatalogItemId, cancellationToken);

        if (catalogItem is null || !catalogItem.IsActive)
        {
            return (
                CatalogOperationResult.Invalid("El producto o servicio seleccionado no existe o está inactivo."),
                null,
                default,
                default);
        }

        decimal? unitPrice = catalogItem.ReferencePriceCrc ?? command.UnitPrice;

        if (unitPrice is null)
        {
            return (
                CatalogOperationResult.Invalid(
                    "El artículo no tiene un precio de referencia: indique el precio unitario de la línea."),
                null,
                default,
                default);
        }

        (CatalogOperationResult? taxTypeError, Guid taxTypeId) = await ResolveLineTaxTypeIdAsync(
            customerId,
            catalogItem.DefaultTaxTypeId,
            command.TaxTypeId,
            cancellationToken);

        if (taxTypeError is not null)
        {
            return (taxTypeError, null, default, default);
        }

        return (null, catalogItem, unitPrice.Value, taxTypeId);
    }

    /// <summary>
    /// Resuelve la tarifa de IVA final de una línea según la condición de IVA del cliente (RF-93,
    /// bloque 1 del paquete v7): en condición Normal usa la tarifa del artículo o del formulario (o
    /// IVA 13 % si no se indicó ninguna), siempre que no sea una tarifa reservada para otra condición;
    /// en las otras dos condiciones ignora el artículo y el formulario y fuerza la tarifa de sistema
    /// que corresponde.
    /// </summary>
    private async Task<(CatalogOperationResult? Error, Guid TaxTypeId)> ResolveLineTaxTypeIdAsync(
        Guid customerId,
        Guid? defaultTaxTypeId,
        Guid? commandTaxTypeId,
        CancellationToken cancellationToken)
    {
        VatCondition vatCondition = await dbContext.Set<Customer>()
            .AsNoTracking()
            .Where(customer => customer.Id == customerId)
            .Select(customer => customer.VatCondition)
            .SingleAsync(cancellationToken);

        if (vatCondition != VatCondition.Normal)
        {
            TaxTypeSystemRole requiredRole = vatCondition == VatCondition.StateHigherEducationInstitution
                ? TaxTypeSystemRole.Reduced2
                : TaxTypeSystemRole.NotSubject;

            Guid forcedTaxTypeId = await dbContext.Set<TaxType>()
                .AsNoTracking()
                .Where(taxType => taxType.SystemRole == requiredRole)
                .Select(taxType => taxType.Id)
                .SingleAsync(cancellationToken);

            return (null, forcedTaxTypeId);
        }

        Guid? taxTypeId = defaultTaxTypeId ?? commandTaxTypeId;

        if (taxTypeId is null)
        {
            Guid general13Id = await dbContext.Set<TaxType>()
                .AsNoTracking()
                .Where(taxType => taxType.SystemRole == TaxTypeSystemRole.General13)
                .Select(taxType => taxType.Id)
                .SingleAsync(cancellationToken);

            return (null, general13Id);
        }

        bool isReservedForAnotherCondition = await dbContext.Set<TaxType>()
            .AsNoTracking()
            .AnyAsync(
                taxType => taxType.Id == taxTypeId.Value &&
                    (taxType.SystemRole == TaxTypeSystemRole.Reduced2
                        || taxType.SystemRole == TaxTypeSystemRole.NotSubject),
                cancellationToken);

        if (isReservedForAnotherCondition)
        {
            return (
                CatalogOperationResult.Invalid(
                    "La tarifa seleccionada no corresponde a la condición de IVA del cliente."),
                default);
        }

        return (null, taxTypeId.Value);
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

    private readonly record struct CustomerContact(
        string Name,
        IdentificationType IdentificationType,
        string Identification,
        string? Email,
        string? Phone,
        VatCondition VatCondition,
        string? ActivityCode);

    private async Task<CustomerContact?> GetCustomerContactAsync(
        Guid customerId,
        CancellationToken cancellationToken)
    {
        CustomerContact[] matches = await dbContext.Set<Customer>()
            .AsNoTracking()
            .Where(customer => customer.Id == customerId)
            .Select(customer => new CustomerContact(
                customer.Name,
                customer.IdentificationType,
                customer.Identification,
                customer.Email,
                customer.Phone,
                customer.VatCondition,
                customer.ActivityCode))
            .Take(1)
            .ToArrayAsync(cancellationToken);

        return matches.Length > 0 ? matches[0] : null;
    }

    private readonly record struct CatalogItemInfo(string Code, CatalogItemType Type);

    private async Task<IReadOnlyDictionary<Guid, CatalogItemInfo>> GetCatalogItemInfoAsync(
        IEnumerable<Guid> catalogItemIds,
        CancellationToken cancellationToken)
    {
        Guid[] ids = catalogItemIds.Distinct().ToArray();

        if (ids.Length == 0)
        {
            return new Dictionary<Guid, CatalogItemInfo>();
        }

        return await dbContext.Set<CatalogItem>()
            .AsNoTracking()
            .Where(item => ids.Contains(item.Id))
            .ToDictionaryAsync(
                item => item.Id,
                item => new CatalogItemInfo(item.Code, item.Type),
                cancellationToken);
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

    private static InvoiceModel MapInvoice(
        Invoice invoice,
        string customerName,
        IdentificationType customerIdentificationType,
        string customerIdentification,
        string? customerEmail,
        string? customerPhone,
        VatCondition customerVatCondition,
        string? customerActivityCode,
        string accountName,
        string createdByUserName,
        IReadOnlyDictionary<Guid, CatalogItemInfo> catalogItemInfo,
        PaymentMethod? paymentMethod = null)
    {
        return new InvoiceModel(
            invoice.Id,
            invoice.Number,
            invoice.CustomerId,
            customerName,
            customerIdentificationType,
            customerIdentification,
            customerEmail,
            customerPhone,
            customerVatCondition,
            customerActivityCode,
            invoice.IncomeAccountId,
            accountName,
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
            invoice.ElectronicInvoiceKey,
            invoice.ElectronicInvoiceNumber,
            invoice.CancellationReason,
            invoice.CancelledAtUtc,
            invoice.CreatedByUserId,
            createdByUserName,
            invoice.CreatedAtUtc,
            invoice.UpdatedAtUtc,
            CatalogPersistence.EncodeVersion(invoice.RowVersion),
            invoice.Lines.Select(line => MapLine(line, catalogItemInfo)).ToArray(),
            paymentMethod);
    }

    private static InvoiceLineModel MapLine(InvoiceLine line, IReadOnlyDictionary<Guid, CatalogItemInfo> catalogItemInfo)
    {
        CatalogItemInfo? info = line.CatalogItemId is Guid catalogItemId && catalogItemInfo.TryGetValue(catalogItemId, out CatalogItemInfo found)
            ? found
            : null;

        return new InvoiceLineModel(
            line.Id,
            line.Position,
            line.CatalogItemId,
            info?.Code,
            info?.Type,
            line.CabysCode,
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
            line.Tax.HaciendaRateCode,
            line.Tax.TaxableAmount,
            line.Tax.Amount,
            line.TotalAmount);
    }
}
