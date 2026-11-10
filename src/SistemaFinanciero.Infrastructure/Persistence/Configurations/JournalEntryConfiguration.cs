using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SistemaFinanciero.Domain.Ledger;
using SistemaFinanciero.Infrastructure.Identity;

namespace SistemaFinanciero.Infrastructure.Persistence.Configurations;

/// <summary>Mapea <see cref="JournalEntry"/> a <c>finanzas.Asientos</c>.</summary>
/// <remarks>
/// Conexiones: <c>FinancialDbContext.OnModelCreating</c> la aplica por convención; el carácter de solo
/// inserción de la tabla lo hace cumplir <c>FinancialDbContext.EnsureAppendOnlyLogsAreNotMutated</c>,
/// no esta clase. <c>JournalPostingService</c> (Infrastructure/Ledger) es el único que inserta filas.
/// </remarks>
internal sealed class JournalEntryConfiguration : IEntityTypeConfiguration<JournalEntry>
{
    public void Configure(EntityTypeBuilder<JournalEntry> builder)
    {
        builder.ToTable(
            "Asientos",
            "finanzas",
            table =>
            {
                table.HasCheckConstraint("CK_Asientos_SourceType", "[SourceType] BETWEEN 1 AND 10");
                table.HasCheckConstraint("CK_Asientos_Number", "[Number] > 0");
            });

        builder.HasKey(entry => entry.Id);
        builder.Property(entry => entry.Id).ValueGeneratedNever();
        builder.Property(entry => entry.Number).IsRequired();
        builder.Property(entry => entry.EntryDate).HasColumnType("date").IsRequired();
        builder.Property(entry => entry.Description).HasMaxLength(JournalEntry.DescriptionMaxLength).IsRequired();
        builder.Property(entry => entry.SourceType).HasConversion<int>().IsRequired();
        builder.Property(entry => entry.CreatedAtUtc).IsRequired();

        builder.HasIndex(entry => entry.Number)
            .IsUnique()
            .HasDatabaseName("UX_Asientos_Number");
        builder.HasIndex(entry => new { entry.SourceType, entry.SourceId })
            .HasDatabaseName("IX_Asientos_SourceType_SourceId");
        builder.HasIndex(entry => entry.EntryDate)
            .HasDatabaseName("IX_Asientos_EntryDate");
        builder.HasIndex(entry => entry.ReversesEntryId)
            .IsUnique()
            .HasFilter("[ReversesEntryId] IS NOT NULL")
            .HasDatabaseName("UX_Asientos_ReversesEntryId");

        builder.HasMany(entry => entry.Lines)
            .WithOne()
            .HasForeignKey("JournalEntryId")
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_AsientoLineas_Asientos");

        builder.HasOne<JournalEntry>().WithMany().HasForeignKey(entry => entry.ReversesEntryId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_Asientos_Asientos_ReversesEntryId");
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(entry => entry.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_Asientos_Usuarios_CreatedBy");
    }
}
