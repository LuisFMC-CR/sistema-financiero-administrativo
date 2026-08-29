using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SistemaFinanciero.Domain.Currencies;
using SistemaFinanciero.Infrastructure.Identity;

namespace SistemaFinanciero.Infrastructure.Persistence.Configurations;

internal sealed class DailyExchangeRateConfiguration : IEntityTypeConfiguration<DailyExchangeRate>
{
    public void Configure(EntityTypeBuilder<DailyExchangeRate> builder)
    {
        builder.ToTable(
            "TiposCambioDiarios",
            "finanzas",
            table => table.HasCheckConstraint(
                "CK_TiposCambioDiarios_CrcPerUsd",
                "[CrcPerUsd] > 0"));

        builder.HasKey(rate => rate.Id);
        builder.Property(rate => rate.EffectiveDate)
            .HasColumnType("date")
            .IsRequired();
        builder.Property(rate => rate.Rate)
            .HasConversion(
                exchangeRate => exchangeRate.CrcPerUsd,
                value => new ExchangeRate(value))
            .HasColumnName("CrcPerUsd")
            .HasPrecision(18, 6)
            .IsRequired();
        builder.Property(rate => rate.Source)
            .HasMaxLength(DailyExchangeRate.SourceMaxLength)
            .IsRequired();
        builder.Property(rate => rate.Notes)
            .HasMaxLength(DailyExchangeRate.NotesMaxLength);
        builder.Property(rate => rate.CreatedAtUtc).IsRequired();
        builder.Property(rate => rate.UpdatedAtUtc).IsRequired();
        builder.Property(rate => rate.RowVersion).IsRowVersion();

        builder.HasIndex(rate => rate.EffectiveDate)
            .IsUnique()
            .HasDatabaseName("UX_TiposCambioDiarios_EffectiveDate");

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(rate => rate.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_TiposCambioDiarios_Usuarios_CreatedBy");
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(rate => rate.UpdatedByUserId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_TiposCambioDiarios_Usuarios_UpdatedBy");
    }
}
