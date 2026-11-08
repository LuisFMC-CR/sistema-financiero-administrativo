using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SistemaFinanciero.Domain.Catalogs;
using SistemaFinanciero.Domain.CreditNotes;
using SistemaFinanciero.Domain.Invoices;
using SistemaFinanciero.Infrastructure.Identity;

namespace SistemaFinanciero.Infrastructure.Persistence.Configurations;

internal sealed class CreditNoteConfiguration : IEntityTypeConfiguration<CreditNote>
{
    public void Configure(EntityTypeBuilder<CreditNote> builder)
    {
        builder.ToTable(
            "NotasCredito",
            "finanzas",
            table =>
            {
                table.HasCheckConstraint("CK_NotasCredito_Status", "[Status] = 1");
                table.HasCheckConstraint(
                    "CK_NotasCredito_Totals",
                    "[TotalAmount] = [AppliedToBalance] + [RefundedAmount] + [WithholdingReversedAmount]");
                table.HasCheckConstraint(
                    "CK_NotasCredito_Refund",
                    "([RefundedAmount] = 0 AND [CashAccountId] IS NULL AND [RefundReference] IS NULL)"
                    + " OR ([RefundedAmount] > 0 AND [CashAccountId] IS NOT NULL AND [RefundReference] IS NOT NULL)");
                table.HasCheckConstraint(
                    "CK_NotasCredito_NonNegativeAmounts",
                    "[NetAmount] >= 0 AND [TaxAmount] >= 0 AND [TotalAmount] > 0"
                    + " AND [AppliedToBalance] >= 0 AND [RefundedAmount] >= 0 AND [WithholdingReversedAmount] >= 0");
                table.HasCheckConstraint(
                    "CK_NotasCredito_ElectronicCreditNoteKey",
                    "[ElectronicCreditNoteKey] IS NULL OR LEN([ElectronicCreditNoteKey]) = 50");
                table.HasCheckConstraint(
                    "CK_NotasCredito_ElectronicCreditNoteNumber",
                    "[ElectronicCreditNoteNumber] IS NULL OR LEN([ElectronicCreditNoteNumber]) = 20");
            });

        builder.HasKey(note => note.Id);
        builder.Property(note => note.Code).HasMaxLength(CreditNote.CodeMaxLength).IsRequired();
        builder.Property(note => note.IssueDate).HasColumnType("date").IsRequired();
        builder.Property(note => note.Reason).HasMaxLength(CreditNote.ReasonMaxLength).IsRequired();
        builder.Property(note => note.NetAmount).HasPrecision(18, 2).IsRequired();
        builder.Property(note => note.TaxAmount).HasPrecision(18, 2).IsRequired();
        builder.Property(note => note.TotalAmount).HasPrecision(18, 2).IsRequired();
        builder.Property(note => note.AppliedToBalance).HasPrecision(18, 2).IsRequired();
        builder.Property(note => note.RefundedAmount).HasPrecision(18, 2).IsRequired();
        builder.Property(note => note.WithholdingReversedAmount).HasPrecision(18, 2).IsRequired();
        builder.Property(note => note.RefundReference).HasMaxLength(CreditNote.RefundReferenceMaxLength);
        builder.Property(note => note.ElectronicCreditNoteKey).HasMaxLength(CreditNote.ElectronicCreditNoteKeyLength);
        builder.Property(note => note.ElectronicCreditNoteNumber).HasMaxLength(CreditNote.ElectronicCreditNoteNumberLength);
        builder.Property(note => note.Status).HasConversion<int>().IsRequired();
        builder.Property(note => note.CreatedAtUtc).IsRequired();
        builder.Property(note => note.UpdatedAtUtc).IsRequired();
        builder.Property(note => note.RowVersion).IsRowVersion();

        builder.HasIndex(note => note.Code)
            .IsUnique()
            .HasDatabaseName("UX_NotasCredito_Code");
        builder.HasIndex(note => note.InvoiceId)
            .IsUnique()
            .HasDatabaseName("UX_NotasCredito_InvoiceId");
        builder.HasIndex(note => new { note.Status, note.IssueDate })
            .HasDatabaseName("IX_NotasCredito_Status_IssueDate");
        builder.HasIndex(note => note.ElectronicCreditNoteKey)
            .IsUnique()
            .HasFilter("[ElectronicCreditNoteKey] IS NOT NULL")
            .HasDatabaseName("UX_NotasCredito_ElectronicCreditNoteKey");

        builder.HasMany(note => note.Lines)
            .WithOne()
            .HasForeignKey(line => line.CreditNoteId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_NotaCreditoLineas_NotasCredito");
        builder.HasOne<Invoice>().WithMany().HasForeignKey(note => note.InvoiceId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_NotasCredito_Facturas");
        builder.HasOne<LedgerAccount>().WithMany().HasForeignKey(note => note.CashAccountId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_NotasCredito_CuentasContables_CashAccount");
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(note => note.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_NotasCredito_Usuarios_CreatedBy");
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(note => note.UpdatedByUserId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_NotasCredito_Usuarios_UpdatedBy");
    }
}
