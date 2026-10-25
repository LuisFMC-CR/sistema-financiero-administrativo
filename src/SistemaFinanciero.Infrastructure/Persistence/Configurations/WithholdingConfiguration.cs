using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SistemaFinanciero.Domain.Catalogs;
using SistemaFinanciero.Domain.Receivables;
using SistemaFinanciero.Infrastructure.Identity;

namespace SistemaFinanciero.Infrastructure.Persistence.Configurations;

/// <summary>Configura la persistencia de las retenciones.</summary>
/// <remarks>
/// Conexiones: <c>FinancialDbContext.OnModelCreating</c> la aplica por convención. Es 0..1 por
/// <see cref="ReceivablePayment"/> (FK única por <see cref="Withholding.ReceivablePaymentId"/>), sin
/// más filas ni cambios posteriores: es una fotografía inmutable del tipo elegido al momento del
/// abono (regla 3).
/// </remarks>
internal sealed class WithholdingConfiguration : IEntityTypeConfiguration<Withholding>
{
    public void Configure(EntityTypeBuilder<Withholding> builder)
    {
        builder.ToTable(
            "Retenciones",
            "finanzas",
            table =>
            {
                table.HasCheckConstraint("CK_Retenciones_Rate", "[Rate] >= 0 AND [Rate] <= 100");
                table.HasCheckConstraint("CK_Retenciones_Amounts", "[Base] >= 0 AND [Amount] >= 0");
            });

        builder.HasKey(withholding => withholding.Id);
        builder.Property(withholding => withholding.Code).HasMaxLength(Withholding.CodeMaxLength).IsRequired();
        builder.Property(withholding => withholding.Name).HasMaxLength(Withholding.NameMaxLength).IsRequired();
        builder.Property(withholding => withholding.Rate).HasPrecision(18, 4).IsRequired();
        builder.Property(withholding => withholding.Base).HasPrecision(18, 2).IsRequired();
        builder.Property(withholding => withholding.Amount).HasPrecision(18, 2).IsRequired();
        builder.Property(withholding => withholding.CreatedAtUtc).IsRequired();

        builder.HasIndex(withholding => withholding.ReceivablePaymentId)
            .IsUnique()
            .HasDatabaseName("UX_Retenciones_ReceivablePaymentId");

        builder.HasOne<ReceivablePayment>().WithMany().HasForeignKey(withholding => withholding.ReceivablePaymentId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_Retenciones_Abonos");
        builder.HasOne<WithholdingType>().WithMany().HasForeignKey(withholding => withholding.WithholdingTypeId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_Retenciones_TiposRetencion");
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(withholding => withholding.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_Retenciones_Usuarios_CreatedBy");
    }
}
