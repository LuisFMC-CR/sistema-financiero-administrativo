using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SistemaFinanciero.Domain.Catalogs;
using SistemaFinanciero.Infrastructure.Identity;

namespace SistemaFinanciero.Infrastructure.Persistence.Configurations;

/// <summary>Mapea <see cref="LedgerAccount"/> a la tabla <c>catalogos.CuentasContables</c>.</summary>
/// <remarks>
/// Conexiones: <c>FinancialDbContext.OnModelCreating</c> la aplica por convención. La usa
/// <c>LedgerAccountService</c>; su restricción <c>CHECK</c> de efectivo y su clave foránea
/// autorreferente (cuenta superior) son la contraparte en base de datos de las reglas que ya valida el
/// dominio.
/// </remarks>
internal sealed class LedgerAccountConfiguration : IEntityTypeConfiguration<LedgerAccount>
{
    public void Configure(EntityTypeBuilder<LedgerAccount> builder)
    {
        builder.ToTable(
            "CuentasContables",
            "catalogos",
            table =>
            {
                table.HasCheckConstraint(
                    "CK_CuentasContables_Codigo_NoVacio",
                    "LEN(LTRIM(RTRIM([Code]))) > 0");
                table.HasCheckConstraint(
                    "CK_CuentasContables_Type",
                    "[Type] IN (1, 4, 5)");
                table.HasCheckConstraint(
                    "CK_CuentasContables_CashKind",
                    "[CashKind] IS NULL OR [CashKind] IN (1, 2)");
                table.HasCheckConstraint(
                    "CK_CuentasContables_Cash",
                    "[CashKind] IS NULL OR [Type] = 1");
                table.HasCheckConstraint(
                    "CK_CuentasContables_Parent",
                    "[ParentId] IS NULL OR [ParentId] <> [Id]");
            });

        builder.HasKey(account => account.Id);
        builder.Property(account => account.Code)
            .HasMaxLength(LedgerAccount.CodeMaxLength)
            .IsRequired();
        builder.Property(account => account.Name)
            .HasMaxLength(LedgerAccount.NameMaxLength)
            .IsRequired();
        builder.Property(account => account.Type).IsRequired();
        builder.Property(account => account.CashKind);
        builder.Property(account => account.Reference)
            .HasMaxLength(LedgerAccount.ReferenceMaxLength);
        builder.Property(account => account.Description)
            .HasMaxLength(LedgerAccount.DescriptionMaxLength);
        builder.Property(account => account.IsActive)
            .HasDefaultValue(true);
        builder.Property(account => account.IsProtected)
            .HasDefaultValue(false);
        builder.Property(account => account.CreatedAtUtc).IsRequired();
        builder.Property(account => account.UpdatedAtUtc).IsRequired();
        builder.Property(account => account.RowVersion).IsRowVersion();

        builder.HasIndex(account => account.Code)
            .IsUnique()
            .HasDatabaseName("UX_CuentasContables_Code");
        builder.HasIndex(account => new { account.IsActive, account.Type, account.Name })
            .HasDatabaseName("IX_CuentasContables_IsActive_Type_Name");

        builder.HasOne<LedgerAccount>()
            .WithMany()
            .HasForeignKey(account => account.ParentId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_CuentasContables_Parent");
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(account => account.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_CuentasContables_Usuarios_CreatedBy");
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(account => account.UpdatedByUserId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_CuentasContables_Usuarios_UpdatedBy");
    }
}
