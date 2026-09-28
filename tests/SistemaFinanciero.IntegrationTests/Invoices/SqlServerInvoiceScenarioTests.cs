using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using SistemaFinanciero.Domain.Catalogs;
using SistemaFinanciero.Domain.Currencies;
using SistemaFinanciero.Domain.Invoices;
using SistemaFinanciero.Infrastructure.Identity;
using SistemaFinanciero.Infrastructure.Persistence;

namespace SistemaFinanciero.IntegrationTests.Invoices;

/// <summary>
/// Escenarios reales que se habilitan con SISTEMA_FINANCIERO_RUN_SQL_TESTS=1 y siempre revierten datos.
/// </summary>
public sealed class SqlServerInvoiceScenarioTests : IClassFixture<FinancialWebApplicationFactory>
{
    private const string EnableVariable = "SISTEMA_FINANCIERO_RUN_SQL_TESTS";
    private readonly FinancialWebApplicationFactory factory;

    public SqlServerInvoiceScenarioTests(FinancialWebApplicationFactory factory)
    {
        this.factory = factory;
    }

    [Fact]
    [Trait("Category", "SqlServer")]
    public Task DraftEditingScenario_PersistsReplacedAndRemovedLinesWithoutOrphans()
    {
        return RunInRollbackTransactionAsync(async (context, actorId) =>
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;
            Invoice invoice = await CreateSavedInvoiceAsync(context, actorId, now);
            Guid invoiceId = invoice.Id;
            Guid firstLineId = invoice.Lines.First().Id;
            Guid secondLineId = invoice.Lines.Last().Id;

            Assert.True(invoice.Number > 0);
            Assert.Equal(2, await CountLinesAsync(context, invoiceId));
            Assert.Equal(2, await CountLineTaxesAsync(context, invoiceId));

            // Reemplazo: la línea anterior y su impuesto desaparecen, y aparecen los nuevos.
            Invoice reloaded = await LoadAsync(context, invoiceId);
            InvoiceLine replacement = CreateLine(quantity: 3m, unitPrice: 100m, [Vat(13m)]);
            reloaded.ReplaceLine(firstLineId, replacement, now.AddSeconds(10), actorId);
            await context.SaveChangesAsync();

            reloaded = await LoadAsync(context, invoiceId);
            Assert.Equal(
                new HashSet<Guid> { replacement.Id, secondLineId },
                reloaded.Lines.Select(line => line.Id).ToHashSet());
            Assert.Equal(2, await CountLinesAsync(context, invoiceId));
            Assert.Equal(2, await CountLineTaxesAsync(context, invoiceId));
            Assert.Equal(400m, reloaded.NetAmount);
            Assert.Equal(44m, reloaded.TaxAmount);
            Assert.Equal(444m, reloaded.TotalAmount);

            InvoiceLineTax replacementTax = reloaded.Lines.Single(line => line.Id == replacement.Id).Taxes.Single();
            Assert.Equal(300m, replacementTax.TaxableAmount);
            Assert.Equal(39m, replacementTax.Amount);

            // Eliminación: la línea y su impuesto también se van de la base.
            reloaded.RemoveLine(secondLineId, now.AddSeconds(20), actorId);
            await context.SaveChangesAsync();

            reloaded = await LoadAsync(context, invoiceId);
            Assert.Equal(replacement.Id, Assert.Single(reloaded.Lines).Id);
            Assert.Equal(1, await CountLinesAsync(context, invoiceId));
            Assert.Equal(1, await CountLineTaxesAsync(context, invoiceId));
            Assert.Equal(300m, reloaded.NetAmount);
            Assert.Equal(39m, reloaded.TaxAmount);
            Assert.Equal(339m, reloaded.TotalAmount);
        });
    }

    [Fact]
    [Trait("Category", "SqlServer")]
    public Task LineOrderScenario_KeepsTheOrderOfCaptureAcrossEditsAndReloads()
    {
        return RunInRollbackTransactionAsync(async (context, actorId) =>
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;
            Invoice invoice = await CreateSavedInvoiceAsync(context, actorId, now);
            Guid invoiceId = invoice.Id;

            // La factura trae 2 líneas; se agregan 4 más a una factura ya guardada para tener 6 con
            // identificadores aleatorios, cuyo orden natural en la base casi nunca coincide con el de captura.
            Invoice reloaded = await LoadAsync(context, invoiceId);
            for (int index = 3; index <= 6; index++)
            {
                reloaded.AddLine(
                    CreateLine(1m, 10m, [], $"Línea {index}"),
                    now.AddSeconds(index * 10),
                    actorId);
            }

            await context.SaveChangesAsync();

            reloaded = await LoadAsync(context, invoiceId);
            Assert.Equal([1, 2, 3, 4, 5, 6], reloaded.Lines.Select(line => line.Position));
            Assert.Equal(
                ["Servicio de soporte", "Servicio de soporte", "Línea 3", "Línea 4", "Línea 5", "Línea 6"],
                reloaded.Lines.Select(line => line.Description));

            // Reemplazo en el medio: la línea nueva conserva la posición 3 después de releer.
            Guid thirdId = reloaded.Lines.ElementAt(2).Id;
            reloaded.ReplaceLine(thirdId, CreateLine(1m, 10m, [], "Línea 3 corregida"), now.AddMinutes(2), actorId);
            await context.SaveChangesAsync();

            reloaded = await LoadAsync(context, invoiceId);
            Assert.Equal("Línea 3 corregida", reloaded.Lines.ElementAt(2).Description);
            Assert.Equal([1, 2, 3, 4, 5, 6], reloaded.Lines.Select(line => line.Position));

            // Quitar la primera línea mueve las cinco restantes dentro del índice único.
            reloaded.RemoveLine(reloaded.Lines.First().Id, now.AddMinutes(3), actorId);
            await context.SaveChangesAsync();

            reloaded = await LoadAsync(context, invoiceId);
            Assert.Equal([1, 2, 3, 4, 5], reloaded.Lines.Select(line => line.Position));
            Assert.Equal(
                ["Servicio de soporte", "Línea 3 corregida", "Línea 4", "Línea 5", "Línea 6"],
                reloaded.Lines.Select(line => line.Description));

            // Quitar una del medio y agregar otra: la secuencia sigue consecutiva en la base.
            reloaded.RemoveLine(reloaded.Lines.ElementAt(2).Id, now.AddMinutes(4), actorId);
            reloaded.AddLine(CreateLine(1m, 10m, [], "Línea 7"), now.AddMinutes(5), actorId);
            await context.SaveChangesAsync();

            reloaded = await LoadAsync(context, invoiceId);
            Assert.Equal([1, 2, 3, 4, 5], reloaded.Lines.Select(line => line.Position));
            Assert.Equal(
                ["Servicio de soporte", "Línea 3 corregida", "Línea 5", "Línea 6", "Línea 7"],
                reloaded.Lines.Select(line => line.Description));

            List<int> storedPositions = await context.Set<InvoiceLine>()
                .Where(line => EF.Property<Guid>(line, "InvoiceId") == invoiceId)
                .Select(line => line.Position)
                .OrderBy(position => position)
                .ToListAsync();
            Assert.Equal([1, 2, 3, 4, 5], storedPositions);
        });
    }

    [Fact]
    [Trait("Category", "SqlServer")]
    public Task TaxTypeLink_KeepsTheSnapshotAndProtectsTheCatalogRow()
    {
        return RunInRollbackTransactionAsync(async (context, actorId) =>
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;
            string suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
            TaxType taxType = new(
                Guid.NewGuid(),
                $"VINC-{suffix}",
                "Impuesto vinculado",
                TaxCalculationType.Percentage,
                13m,
                null,
                now,
                actorId);
            context.TaxTypes.Add(taxType);
            await context.SaveChangesAsync();

            Invoice invoice = await CreateSavedInvoiceAsync(context, actorId, now);
            Invoice reloaded = await LoadAsync(context, invoice.Id);
            InvoiceLine linked = CreateLine(
                quantity: 1m,
                unitPrice: 100m,
                [new InvoiceTaxSpecification(
                    Guid.NewGuid(),
                    taxType.Code,
                    taxType.Name,
                    taxType.CalculationType,
                    taxType.Rate,
                    taxType.Id)],
                "Línea con impuesto del catálogo");
            reloaded.AddLine(linked, now.AddSeconds(10), actorId);
            await context.SaveChangesAsync();

            reloaded = await LoadAsync(context, invoice.Id);
            InvoiceLineTax stored = reloaded.Lines.Single(line => line.Id == linked.Id).Taxes.Single();
            Assert.Equal(taxType.Id, stored.TaxTypeId);
            Assert.Equal(13m, stored.Rate);

            // Cambiar y desactivar el tipo del catálogo no altera la fotografía ya guardada.
            TaxType current = await context.TaxTypes.SingleAsync(candidate => candidate.Id == taxType.Id);
            current.UpdateDetails(current.Code, "Nombre nuevo", 1m, null, now.AddMinutes(1), actorId);
            current.Deactivate(now.AddMinutes(2), actorId);
            await context.SaveChangesAsync();

            reloaded = await LoadAsync(context, invoice.Id);
            stored = reloaded.Lines.Single(line => line.Id == linked.Id).Taxes.Single();
            Assert.Equal("Impuesto vinculado", stored.Name);
            Assert.Equal(13m, stored.Rate);
            Assert.Equal(13m, stored.Amount);
            Assert.Equal(taxType.Id, stored.TaxTypeId);

            // La base impide eliminar un tipo que ya se usó en una factura.
            Exception deleteError = await Assert.ThrowsAnyAsync<Exception>(() =>
                context.Database.ExecuteSqlInterpolatedAsync(
                    $"DELETE FROM catalogos.TiposImpuesto WHERE Id = {taxType.Id}"));
            Assert.Contains("FK_FacturaLineaImpuestos_TiposImpuesto", deleteError.ToString());
        });
    }

    [Fact]
    [Trait("Category", "SqlServer")]
    public Task TaxTypeLink_RejectsATaxTypeThatDoesNotExist()
    {
        return RunInRollbackTransactionAsync(async (context, actorId) =>
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;
            Invoice invoice = await CreateSavedInvoiceAsync(context, actorId, now);
            Invoice reloaded = await LoadAsync(context, invoice.Id);
            reloaded.AddLine(
                CreateLine(
                    1m,
                    100m,
                    [new InvoiceTaxSpecification(
                        Guid.NewGuid(),
                        "FANTASMA",
                        "Tipo inexistente",
                        TaxCalculationType.Percentage,
                        13m,
                        Guid.NewGuid())]),
                now.AddSeconds(10),
                actorId);

            DbUpdateException error = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
            Assert.Contains("FK_FacturaLineaImpuestos_TiposImpuesto", error.ToString());
        });
    }

    [Fact]
    [Trait("Category", "SqlServer")]
    public Task HeaderScenario_PersistsTheEditedHeaderAndTheConfirmation()
    {
        return RunInRollbackTransactionAsync(async (context, actorId) =>
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;
            Invoice invoice = await CreateSavedInvoiceAsync(context, actorId, now);
            Guid invoiceId = invoice.Id;
            DateOnly newIssueDate = invoice.IssueDate.AddDays(-3);

            Invoice reloaded = await LoadAsync(context, invoiceId);
            reloaded.UpdateHeader(
                reloaded.CustomerId,
                newIssueDate,
                CurrencyCode.CRC,
                PaymentTerm.Cash,
                dueDate: null,
                now.AddSeconds(10),
                actorId);
            await context.SaveChangesAsync();

            reloaded = await LoadAsync(context, invoiceId);
            Assert.Equal(newIssueDate, reloaded.IssueDate);
            Assert.Equal(PaymentTerm.Cash, reloaded.PaymentTerm);
            Assert.Null(reloaded.DueDate);

            reloaded.Confirm(null, now.AddSeconds(20), actorId);
            await context.SaveChangesAsync();

            reloaded = await LoadAsync(context, invoiceId);
            Assert.Equal(InvoiceStatus.Confirmed, reloaded.Status);
            Assert.Equal(actorId, reloaded.ConfirmedByUserId);
            Assert.Equal(2, reloaded.Lines.Count);
        });
    }

    [Fact]
    [Trait("Category", "SqlServer")]
    public Task Database_RejectsDataThatBreaksTheInvoiceConstraints()
    {
        return RunInRollbackTransactionAsync(async (context, actorId) =>
        {
            Invoice invoice = await CreateSavedInvoiceAsync(context, actorId, DateTimeOffset.UtcNow);
            Guid invoiceId = invoice.Id;

            // La factura es a crédito: pasarla a contado con vencimiento viola CK_Facturas_DueDate.
            Exception dueDateError = await Assert.ThrowsAnyAsync<Exception>(() =>
                context.Database.ExecuteSqlInterpolatedAsync(
                    $"UPDATE finanzas.Facturas SET PaymentTerm = 1 WHERE Id = {invoiceId}"));
            Assert.Contains("CK_Facturas_DueDate", dueDateError.ToString());

            Exception paymentTermError = await Assert.ThrowsAnyAsync<Exception>(() =>
                context.Database.ExecuteSqlInterpolatedAsync(
                    $"UPDATE finanzas.Facturas SET PaymentTerm = 9 WHERE Id = {invoiceId}"));
            // Con el valor 9 también se viola CK_Facturas_DueDate y SQL Server informa una sola.
            Assert.Matches("CK_Facturas_(PaymentTerm|DueDate)", paymentTermError.ToString());

            Exception positionError = await Assert.ThrowsAnyAsync<Exception>(() =>
                context.Database.ExecuteSqlInterpolatedAsync(
                    $"UPDATE finanzas.FacturaLineas SET Position = 0 WHERE InvoiceId = {invoiceId}"));
            Assert.Contains("CK_FacturaLineas_Position", positionError.ToString());

            // Las dos líneas de la factura no pueden compartir posición.
            Exception duplicatePositionError = await Assert.ThrowsAnyAsync<Exception>(() =>
                context.Database.ExecuteSqlInterpolatedAsync(
                    $"UPDATE finanzas.FacturaLineas SET Position = 1 WHERE InvoiceId = {invoiceId}"));
            Assert.Contains("UX_FacturaLineas_InvoiceId_Position", duplicatePositionError.ToString());

            Exception taxableError = await Assert.ThrowsAnyAsync<Exception>(() =>
                context.Database.ExecuteSqlInterpolatedAsync(
                    $"""
                    UPDATE t SET TaxableAmount = -1
                    FROM finanzas.FacturaLineaImpuestos t
                    JOIN finanzas.FacturaLineas l ON l.Id = t.InvoiceLineId
                    WHERE l.InvoiceId = {invoiceId}
                    """));
            Assert.Contains("CK_FacturaLineaImpuestos_RateAmount", taxableError.ToString());
        });
    }

    private static async Task<Invoice> CreateSavedInvoiceAsync(
        FinancialDbContext context,
        Guid actorId,
        DateTimeOffset now)
    {
        string suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        Customer customer = new(
            Guid.NewGuid(),
            $"CLI-{suffix}",
            "Cliente de facturas",
            null,
            null,
            null,
            null,
            now,
            actorId);
        context.Customers.Add(customer);

        DateOnly issueDate = DateOnly.FromDateTime(now.UtcDateTime);
        Invoice invoice = new(
            Guid.NewGuid(),
            customer.Id,
            issueDate,
            CurrencyCode.CRC,
            PaymentTerm.Credit,
            issueDate.AddDays(30),
            now,
            actorId);

        // Primera línea: 100 con IVA 13 % = 13. Segunda: 2 x 50 = 100 con selectivo 5 % = 5.
        invoice.AddLine(CreateLine(quantity: 1m, unitPrice: 100m, [Vat(13m)]), now.AddSeconds(1), actorId);
        invoice.AddLine(CreateLine(quantity: 2m, unitPrice: 50m, [Selective(5m)]), now.AddSeconds(2), actorId);

        context.Invoices.Add(invoice);
        await context.SaveChangesAsync();
        return invoice;
    }

    private static async Task<Invoice> LoadAsync(FinancialDbContext context, Guid invoiceId)
    {
        // Sin seguimiento previo, para comprobar lo que realmente quedó en la base de datos.
        context.ChangeTracker.Clear();

        return await context.Invoices
            .Include(invoice => invoice.Lines)
            .ThenInclude(line => line.Taxes)
            .SingleAsync(invoice => invoice.Id == invoiceId);
    }

    private static Task<int> CountLinesAsync(FinancialDbContext context, Guid invoiceId) =>
        context.Set<InvoiceLine>()
            .CountAsync(line => EF.Property<Guid>(line, "InvoiceId") == invoiceId);

    private static Task<int> CountLineTaxesAsync(FinancialDbContext context, Guid invoiceId) =>
        context.Set<InvoiceLineTax>()
            .CountAsync(tax => context.Set<InvoiceLine>().Any(line =>
                line.Id == EF.Property<Guid>(tax, "InvoiceLineId")
                && EF.Property<Guid>(line, "InvoiceId") == invoiceId));

    private static InvoiceLine CreateLine(
        decimal quantity,
        decimal unitPrice,
        IEnumerable<InvoiceTaxSpecification> taxes,
        string description = "Servicio de soporte") =>
        new(
            Guid.NewGuid(),
            catalogItemId: null,
            description,
            "hora",
            quantity,
            unitPrice,
            discountAmount: 0m,
            taxes);

    private static InvoiceTaxSpecification Vat(decimal rate) =>
        new(Guid.NewGuid(), "IVA", "Impuesto al valor agregado", TaxCalculationType.Percentage, rate);

    private static InvoiceTaxSpecification Selective(decimal rate) =>
        new(Guid.NewGuid(), "SEL", "Impuesto selectivo", TaxCalculationType.Percentage, rate);

    private async Task RunInRollbackTransactionAsync(
        Func<FinancialDbContext, Guid, Task> scenario)
    {
        if (!string.Equals(
            Environment.GetEnvironmentVariable(EnableVariable),
            "1",
            StringComparison.Ordinal))
        {
            return;
        }

        using IServiceScope scope = factory.Services.CreateScope();
        FinancialDbContext context = scope.ServiceProvider.GetRequiredService<FinancialDbContext>();
        await using IDbContextTransaction transaction = await context.Database.BeginTransactionAsync();
        Guid actorId = Guid.NewGuid();
        string email = $"sql-test-{actorId:N}@example.test";

        context.Users.Add(new ApplicationUser
        {
            Id = actorId,
            FullName = "Usuario de integración SQL",
            UserName = email,
            NormalizedUserName = email.ToUpperInvariant(),
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            EmailConfirmed = true,
            IsActive = true,
            SecurityStamp = Guid.NewGuid().ToString("N"),
            ConcurrencyStamp = Guid.NewGuid().ToString("N"),
        });
        await context.SaveChangesAsync();

        try
        {
            await scenario(context, actorId);
        }
        finally
        {
            await transaction.RollbackAsync();
        }
    }
}
