using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SistemaFinanciero.Infrastructure.Identity;

namespace SistemaFinanciero.Infrastructure.Persistence.Configurations;

internal sealed class SecurityAuditEventConfiguration
    : IEntityTypeConfiguration<SecurityAuditEvent>
{
    public void Configure(EntityTypeBuilder<SecurityAuditEvent> builder)
    {
        builder.ToTable(
            "EventosSeguridad",
            "seguridad",
            table =>
            {
                table.HasCheckConstraint(
                    "CK_EventosSeguridad_Action",
                    "[Action] BETWEEN 1 AND 9");
                table.HasCheckConstraint(
                    "CK_EventosSeguridad_Role",
                    "[Role] IS NULL OR [Role] IN (N'Administrador', N'Gerencia', N'Finanzas', N'Asistente')");
            });

        builder.HasKey(auditEvent => auditEvent.Id);
        builder.Property(auditEvent => auditEvent.Action)
            .HasConversion<int>()
            .IsRequired();
        builder.Property(auditEvent => auditEvent.OccurredAtUtc).IsRequired();
        builder.Property(auditEvent => auditEvent.Role).HasMaxLength(32);

        builder.HasIndex(auditEvent => auditEvent.OccurredAtUtc)
            .HasDatabaseName("IX_EventosSeguridad_OccurredAtUtc");
        builder.HasIndex(auditEvent => new
        {
            auditEvent.TargetUserId,
            auditEvent.OccurredAtUtc,
        })
            .HasDatabaseName("IX_EventosSeguridad_TargetUserId_OccurredAtUtc");
        builder.HasIndex(auditEvent => new
        {
            auditEvent.ActorUserId,
            auditEvent.OccurredAtUtc,
        })
            .HasDatabaseName("IX_EventosSeguridad_ActorUserId_OccurredAtUtc");

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(auditEvent => auditEvent.ActorUserId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_EventosSeguridad_Usuarios_Actor");
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(auditEvent => auditEvent.TargetUserId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_EventosSeguridad_Usuarios_Target");
    }
}
