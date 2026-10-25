using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SistemaFinanciero.Domain.Catalogs;
using SistemaFinanciero.Domain.Invoices;
using SistemaFinanciero.Infrastructure.Identity;

namespace SistemaFinanciero.Infrastructure.Persistence.Configurations;

/// <summary>Configura la persistencia histórica de las facturas administrativas.</summary>
/// <remarks>
/// Conexiones: <c>FinancialDbContext.OnModelCreating</c> la aplica por convención. Ningún servicio la
/// usa todavía: no existe un <c>IInvoiceService</c>. Solo las pruebas de integración
/// (<c>SqlServerInvoiceScenarioTests</c>) guardan facturas directamente contra
/// <c>FinancialDbContext.Invoices</c>. <c>Number</c> ya no es identity: un borrador no tiene número;
/// quien confirme (hoy la prueba, luego <c>IInvoiceService</c>) debe obtenerlo de la secuencia SQL
/// <c>finanzas.Seq_FacturaNumero</c> (migración <c>AddInvoiceNumberSequence</c>) y pasarlo a
/// <c>Invoice.Confirm</c> (RF-34). <c>DueDate</c> tampoco lo asigna quien llama: el dominio lo calcula
/// como <c>IssueDate</c> más los días fijos del plazo (regla 14), y <c>CK_Facturas_DueDate</c> repite
/// esa misma fórmula con <c>DATEADD</c> para que la base rechace cualquier valor que no coincida.
/// </remarks>
internal sealed class InvoiceConfiguration : IEntityTypeConfiguration<Invoice>
{
    public void Configure(EntityTypeBuilder<Invoice> builder)
    {
        builder.ToTable(
            "Facturas",
            "finanzas",
            table =>
            {
                table.HasCheckConstraint("CK_Facturas_Status", "[Status] IN (1, 2, 3)");
                table.HasCheckConstraint("CK_Facturas_PaymentTerm", "[PaymentTerm] IN (1, 2, 3, 4)");
                table.HasCheckConstraint(
                    "CK_Facturas_DueDate",
                    "([PaymentTerm] = 1 AND [DueDate] IS NULL)"
                    + " OR ([PaymentTerm] = 2 AND [DueDate] = DATEADD(DAY, 30, [IssueDate]))"
                    + " OR ([PaymentTerm] = 3 AND [DueDate] = DATEADD(DAY, 60, [IssueDate]))"
                    + " OR ([PaymentTerm] = 4 AND [DueDate] = DATEADD(DAY, 90, [IssueDate]))");
                table.HasCheckConstraint("CK_Facturas_Totals", "[GrossAmount] >= 0 AND [DiscountAmount] >= 0 AND [NetAmount] >= 0 AND [TaxAmount] >= 0 AND [TotalAmount] >= 0");
            });

        builder.HasKey(invoice => invoice.Id);
        builder.Property(invoice => invoice.Number);
        builder.Property(invoice => invoice.IssueDate).HasColumnType("date").IsRequired();
        builder.Property(invoice => invoice.PaymentTerm).IsRequired();
        builder.Property(invoice => invoice.DueDate).HasColumnType("date");
        builder.Property(invoice => invoice.Status).IsRequired();
        builder.Property(invoice => invoice.GrossAmount).HasPrecision(18, 2).IsRequired();
        builder.Property(invoice => invoice.DiscountAmount).HasPrecision(18, 2).IsRequired();
        builder.Property(invoice => invoice.NetAmount).HasPrecision(18, 2).IsRequired();
        builder.Property(invoice => invoice.TaxAmount).HasPrecision(18, 2).IsRequired();
        builder.Property(invoice => invoice.TotalAmount).HasPrecision(18, 2).IsRequired();
        builder.Property(invoice => invoice.CancellationReason).HasMaxLength(300);
        builder.Property(invoice => invoice.CreatedAtUtc).IsRequired();
        builder.Property(invoice => invoice.UpdatedAtUtc).IsRequired();
        builder.Property(invoice => invoice.RowVersion).IsRowVersion();

        builder.HasIndex(invoice => invoice.Number)
            .IsUnique()
            .HasFilter("[Number] IS NOT NULL")
            .HasDatabaseName("UX_Facturas_Number");
        builder.HasIndex(invoice => new { invoice.Status, invoice.IssueDate })
            .HasDatabaseName("IX_Facturas_Status_IssueDate");
        builder.HasIndex(invoice => new { invoice.CustomerId, invoice.Status })
            .HasDatabaseName("IX_Facturas_CustomerId_Status");

        builder.HasMany(invoice => invoice.Lines)
            .WithOne()
            .HasForeignKey("InvoiceId")
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_FacturaLineas_Facturas");
        builder.HasOne<Customer>().WithMany().HasForeignKey(invoice => invoice.CustomerId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_Facturas_Clientes");
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(invoice => invoice.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_Facturas_Usuarios_CreatedBy");
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(invoice => invoice.UpdatedByUserId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_Facturas_Usuarios_UpdatedBy");
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(invoice => invoice.ConfirmedByUserId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_Facturas_Usuarios_ConfirmedBy");
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(invoice => invoice.CancelledByUserId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_Facturas_Usuarios_CancelledBy");
    }
}
