using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SistemaFinanciero.Domain.Catalogs;
using SistemaFinanciero.Domain.Ledger;

namespace SistemaFinanciero.Infrastructure.Persistence.Configurations;

/// <summary>Mapea <see cref="JournalEntryLine"/> a <c>finanzas.AsientoLineas</c>.</summary>
/// <remarks>
/// Conexiones: <c>FinancialDbContext.OnModelCreating</c> la aplica por convención. Declara
/// <c>ValueGeneratedNever()</c> en el <c>Id</c> porque lo asigna el dominio (<c>JournalEntry</c>), no
/// la base de datos. <c>AccountId</c> siempre apunta a una cuenta de detalle (de grupo o de sistema,
/// nunca una protegida sin movimiento propio), restringida para no poder borrarse mientras tenga
/// líneas.
/// </remarks>
internal sealed class JournalEntryLineConfiguration : IEntityTypeConfiguration<JournalEntryLine>
{
    public void Configure(EntityTypeBuilder<JournalEntryLine> builder)
    {
        builder.ToTable(
            "AsientoLineas",
            "finanzas",
            table =>
            {
                table.HasCheckConstraint("CK_AsientoLineas_Position", "[LineNumber] > 0");
                table.HasCheckConstraint(
                    "CK_AsientoLineas_DebitCredit",
                    "[Debit] >= 0 AND [Credit] >= 0 AND (([Debit] > 0 AND [Credit] = 0) OR ([Debit] = 0 AND [Credit] > 0))");
            });

        builder.HasKey(line => line.Id);
        builder.Property(line => line.Id).ValueGeneratedNever();
        builder.Property(line => line.LineNumber).IsRequired();
        builder.Property(line => line.Debit).HasPrecision(18, 2).IsRequired();
        builder.Property(line => line.Credit).HasPrecision(18, 2).IsRequired();

        builder.HasIndex("JournalEntryId").HasDatabaseName("IX_AsientoLineas_JournalEntryId");
        builder.HasIndex("JournalEntryId", nameof(JournalEntryLine.LineNumber))
            .IsUnique()
            .HasDatabaseName("UX_AsientoLineas_JournalEntryId_LineNumber");
        builder.HasIndex(line => line.AccountId)
            .HasDatabaseName("IX_AsientoLineas_AccountId");

        builder.HasOne<LedgerAccount>().WithMany().HasForeignKey(line => line.AccountId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_AsientoLineas_CuentasContables");
    }
}
