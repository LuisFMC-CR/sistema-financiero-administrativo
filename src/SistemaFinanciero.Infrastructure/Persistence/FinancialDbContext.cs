using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SistemaFinanciero.Domain.Adjustments;
using SistemaFinanciero.Domain.Audit;
using SistemaFinanciero.Domain.Budgets;
using SistemaFinanciero.Domain.Catalogs;
using SistemaFinanciero.Domain.Expenses;
using SistemaFinanciero.Domain.Incomes;
using SistemaFinanciero.Domain.Invoices;
using SistemaFinanciero.Domain.Parameters;
using SistemaFinanciero.Domain.Payables;
using SistemaFinanciero.Domain.Receivables;
using SistemaFinanciero.Infrastructure.Identity;

namespace SistemaFinanciero.Infrastructure.Persistence;

/// <summary>
/// Contexto único de persistencia para identidad y operaciones financieras.
/// </summary>
/// <remarks>
/// Conexiones: es el único <c>DbContext</c> del sistema; todo servicio de Infrastructure lo recibe por
/// inyección. <c>OnModelCreating</c> aplica cada <c>IEntityTypeConfiguration&lt;T&gt;</c> del ensamblado
/// (carpeta <c>Persistence/Configurations</c>) y, además, configura a mano las tablas propias de
/// Identity (<c>ConfigureIdentitySchema</c>). <c>SaveChanges</c>/<c>SaveChangesAsync</c> están
/// sobrescritos para que ninguna bitácora de solo inserción (<c>SecurityAuditEvent</c>,
/// <c>SystemParameterChange</c>, <c>OperationLogEntry</c>) pueda modificarse o borrarse por accidente.
/// </remarks>
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

    /// <summary>Catálogo de cuentas contables, incluidas las cajas y los bancos.</summary>
    public DbSet<LedgerAccount> LedgerAccounts => Set<LedgerAccount>();

    /// <summary>Tipos de impuesto configurables para las líneas de factura.</summary>
    public DbSet<TaxType> TaxTypes => Set<TaxType>();

    /// <summary>Tipos de retención que un cliente aplica al pagar una factura.</summary>
    public DbSet<WithholdingType> WithholdingTypes => Set<WithholdingType>();

    /// <summary>Facturas administrativas y sus detalles históricos.</summary>
    public DbSet<Invoice> Invoices => Set<Invoice>();

    /// <summary>Cuentas por cobrar generadas por las facturas a crédito.</summary>
    public DbSet<Receivable> Receivables => Set<Receivable>();

    /// <summary>Ingresos automáticos y manuales.</summary>
    public DbSet<Income> Incomes => Set<Income>();

    /// <summary>Abonos aplicados a las cuentas por cobrar, incluido el reparto a varias facturas.</summary>
    public DbSet<ReceivablePayment> ReceivablePayments => Set<ReceivablePayment>();

    /// <summary>Retenciones registradas dentro de un abono.</summary>
    public DbSet<Withholding> Withholdings => Set<Withholding>();

    /// <summary>Gastos: compras a proveedores respaldadas por comprobante electrónico.</summary>
    public DbSet<Expense> Expenses => Set<Expense>();

    /// <summary>Cuentas por pagar generadas por los gastos a crédito.</summary>
    public DbSet<Payable> Payables => Set<Payable>();

    /// <summary>Pagos aplicados a las cuentas por pagar, incluido el reparto a varias cuentas.</summary>
    public DbSet<SupplierPayment> SupplierPayments => Set<SupplierPayment>();

    /// <summary>Bitácora inmutable de acciones administrativas de seguridad.</summary>
    public DbSet<SecurityAuditEvent> SecurityAuditEvents => Set<SecurityAuditEvent>();

    /// <summary>Fila única de parámetros financieros del sistema.</summary>
    public DbSet<SystemParameters> SystemParameters => Set<SystemParameters>();

    /// <summary>Bitácora inmutable de cambios en los parámetros del sistema.</summary>
    public DbSet<SystemParameterChange> SystemParameterChanges => Set<SystemParameterChange>();

    /// <summary>Bitácora inmutable de cambios en la fecha de cierre.</summary>
    public DbSet<ClosingDateChange> ClosingDateChanges => Set<ClosingDateChange>();

    /// <summary>Bitácora inmutable de confirmaciones y anulaciones de documentos financieros.</summary>
    public DbSet<OperationLogEntry> OperationLogEntries => Set<OperationLogEntry>();

    /// <summary>Presupuesto mensual por categoría de gasto de último nivel.</summary>
    public DbSet<Budget> Budgets => Set<Budget>();

    /// <summary>Bitácora inmutable de cambios en el presupuesto mensual.</summary>
    public DbSet<BudgetChange> BudgetChanges => Set<BudgetChange>();

    /// <summary>Bitácora inmutable de ajustes manuales de saldo de cuentas por cobrar y por pagar.</summary>
    public DbSet<BalanceAdjustment> BalanceAdjustments => Set<BalanceAdjustment>();

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
        EnsureAppendOnlyLogsAreNotMutated();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    /// <inheritdoc />
    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        EnsureAppendOnlyLogsAreNotMutated();
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

    private void EnsureAppendOnlyLogsAreNotMutated()
    {
        bool securityAuditMutated = ChangeTracker.Entries<SecurityAuditEvent>()
            .Any(entry => entry.State is EntityState.Modified or EntityState.Deleted);

        if (securityAuditMutated)
        {
            throw new InvalidOperationException(
                "La bitácora de seguridad es inmutable y solo admite nuevos eventos.");
        }

        bool parameterHistoryMutated = ChangeTracker.Entries<SystemParameterChange>()
            .Any(entry => entry.State is EntityState.Modified or EntityState.Deleted);

        if (parameterHistoryMutated)
        {
            throw new InvalidOperationException(
                "El historial de parámetros es inmutable y solo admite nuevos registros.");
        }

        bool operationLogMutated = ChangeTracker.Entries<OperationLogEntry>()
            .Any(entry => entry.State is EntityState.Modified or EntityState.Deleted);

        if (operationLogMutated)
        {
            throw new InvalidOperationException(
                "La bitácora de operaciones es inmutable y solo admite nuevos registros.");
        }

        bool budgetHistoryMutated = ChangeTracker.Entries<BudgetChange>()
            .Any(entry => entry.State is EntityState.Modified or EntityState.Deleted);

        if (budgetHistoryMutated)
        {
            throw new InvalidOperationException(
                "El historial del presupuesto es inmutable y solo admite nuevos registros.");
        }

        bool balanceAdjustmentsMutated = ChangeTracker.Entries<BalanceAdjustment>()
            .Any(entry => entry.State is EntityState.Modified or EntityState.Deleted);

        if (balanceAdjustmentsMutated)
        {
            throw new InvalidOperationException(
                "Los ajustes de saldo son inmutables y solo admiten nuevos registros.");
        }

        bool closingDateHistoryMutated = ChangeTracker.Entries<ClosingDateChange>()
            .Any(entry => entry.State is EntityState.Modified or EntityState.Deleted);

        if (closingDateHistoryMutated)
        {
            throw new InvalidOperationException(
                "El historial de la fecha de cierre es inmutable y solo admite nuevos registros.");
        }
    }
}
