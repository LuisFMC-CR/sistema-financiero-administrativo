using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SistemaFinanciero.Domain.Catalogs;
using SistemaFinanciero.Domain.Currencies;
using SistemaFinanciero.Domain.Invoices;
using SistemaFinanciero.Infrastructure.Identity;

namespace SistemaFinanciero.Infrastructure.Persistence;

/// <summary>
/// Contexto único de persistencia para identidad y operaciones financieras.
/// </summary>
public sealed class FinancialDbContext(
    DbContextOptions<FinancialDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options)
{
    /// <summary>Clientes disponibles para facturación administrativa.</summary>
    public DbSet<Customer> Customers => Set<Customer>();

    /// <summary>Proveedores disponibles para gastos y cuentas por pagar.</summary>
    public DbSet<Supplier> Suppliers => Set<Supplier>();

    /// <summary>Catálogo unificado de productos y servicios.</summary>
    public DbSet<CatalogItem> CatalogItems => Set<CatalogItem>();

    /// <summary>Categorías jerárquicas de ingreso y gasto.</summary>
    public DbSet<FinancialCategory> FinancialCategories => Set<FinancialCategory>();

    /// <summary>Cajas y cuentas bancarias con moneda fija.</summary>
    public DbSet<FinancialAccount> FinancialAccounts => Set<FinancialAccount>();

    /// <summary>Tipos de cambio administrativos por fecha.</summary>
    public DbSet<DailyExchangeRate> DailyExchangeRates => Set<DailyExchangeRate>();

    /// <summary>Facturas administrativas y sus detalles históricos.</summary>
    public DbSet<Invoice> Invoices => Set<Invoice>();

    /// <summary>Bitácora inmutable de acciones administrativas de seguridad.</summary>
    public DbSet<SecurityAuditEvent> SecurityAuditEvents => Set<SecurityAuditEvent>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        ArgumentNullException.ThrowIfNull(builder);

        ConfigureIdentitySchema(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(FinancialDbContext).Assembly);
    }

    /// <inheritdoc />
    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        EnsureSecurityAuditIsAppendOnly();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    /// <inheritdoc />
    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        EnsureSecurityAuditIsAppendOnly();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private static void ConfigureIdentitySchema(ModelBuilder builder)
    {
        builder.Entity<ApplicationUser>(entity =>
        {
            entity.ToTable(
                "Usuarios",
                "seguridad",
                table =>
                {
                    table.HasCheckConstraint(
                        "CK_Usuarios_FullName_NoVacio",
                        "LEN(LTRIM(RTRIM([FullName]))) > 0");
                    table.HasCheckConstraint(
                        "CK_Usuarios_Email_UserName",
                        "[NormalizedEmail] = [NormalizedUserName]");
                });
            entity.Property(user => user.FullName)
                .HasMaxLength(150)
                .IsRequired();
            entity.Property(user => user.IsActive)
                .HasDefaultValue(true);
            entity.Property(user => user.MustChangePassword)
                .HasDefaultValue(false);
            entity.Property(user => user.Email)
                .IsRequired();
            entity.Property(user => user.NormalizedEmail)
                .IsRequired();
            entity.Property(user => user.UserName)
                .IsRequired();
            entity.Property(user => user.NormalizedUserName)
                .IsRequired();
            entity.HasIndex(user => user.NormalizedEmail)
                .HasDatabaseName("EmailIndex")
                .IsUnique();
            entity.HasIndex(user => new { user.IsActive, user.FullName })
                .HasDatabaseName("IX_Usuarios_IsActive_FullName");
        });

        builder.Entity<IdentityRole<Guid>>()
            .ToTable("Roles", "seguridad");
        builder.Entity<IdentityUserRole<Guid>>(entity =>
        {
            entity.ToTable("UsuariosRoles", "seguridad");
            entity.HasIndex(userRole => userRole.UserId)
                .IsUnique()
                .HasDatabaseName("UX_UsuariosRoles_UserId");
        });
        builder.Entity<IdentityUserClaim<Guid>>()
            .ToTable("UsuariosClaims", "seguridad");
        builder.Entity<IdentityUserLogin<Guid>>()
            .ToTable("UsuariosLogins", "seguridad");
        builder.Entity<IdentityRoleClaim<Guid>>()
            .ToTable("RolesClaims", "seguridad");
        builder.Entity<IdentityUserToken<Guid>>()
            .ToTable("UsuariosTokens", "seguridad");
    }

    private void EnsureSecurityAuditIsAppendOnly()
    {
        bool containsMutation = ChangeTracker.Entries<SecurityAuditEvent>()
            .Any(entry => entry.State is EntityState.Modified or EntityState.Deleted);

        if (containsMutation)
        {
            throw new InvalidOperationException(
                "La bitácora de seguridad es inmutable y solo admite nuevos eventos.");
        }
    }
}
