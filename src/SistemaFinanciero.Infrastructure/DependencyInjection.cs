using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SistemaFinanciero.Application.Catalogs.Categories;
using SistemaFinanciero.Application.Catalogs.Customers;
using SistemaFinanciero.Application.Catalogs.ExchangeRates;
using SistemaFinanciero.Application.Catalogs.Items;
using SistemaFinanciero.Application.Catalogs.LedgerAccounts;
using SistemaFinanciero.Application.Catalogs.Suppliers;
using SistemaFinanciero.Application.Catalogs.TaxTypes;
using SistemaFinanciero.Application.Catalogs.WithholdingTypes;
using SistemaFinanciero.Application.Parameters;
using SistemaFinanciero.Application.Security.Users;
using SistemaFinanciero.Infrastructure.Catalogs.Categories;
using SistemaFinanciero.Infrastructure.Catalogs.Customers;
using SistemaFinanciero.Infrastructure.Catalogs.ExchangeRates;
using SistemaFinanciero.Infrastructure.Catalogs.Items;
using SistemaFinanciero.Infrastructure.Catalogs.LedgerAccounts;
using SistemaFinanciero.Infrastructure.Catalogs.Suppliers;
using SistemaFinanciero.Infrastructure.Catalogs.TaxTypes;
using SistemaFinanciero.Infrastructure.Catalogs.WithholdingTypes;
using SistemaFinanciero.Infrastructure.Identity;
using SistemaFinanciero.Infrastructure.Parameters;
using SistemaFinanciero.Infrastructure.Persistence;

namespace SistemaFinanciero.Infrastructure;

/// <summary>
/// Registra la persistencia y los servicios de identidad de la aplicación.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Agrega SQL Server, ASP.NET Core Identity y sus opciones de seguridad.
    /// </summary>
    /// <param name="services">Colección de servicios de la aplicación.</param>
    /// <param name="configuration">Configuración del proceso.</param>
    /// <returns>La misma colección para permitir encadenamiento.</returns>
    /// <exception cref="InvalidOperationException">
    /// Se produce cuando no existe la cadena <c>DefaultConnection</c>.
    /// </exception>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        string? connectionString = configuration.GetConnectionString("DefaultConnection");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "No se configuró la cadena de conexión DefaultConnection.");
        }

        services.AddDbContext<FinancialDbContext>(options =>
            options.UseSqlServer(
                connectionString,
                sqlOptions => sqlOptions.MigrationsAssembly(
                    typeof(FinancialDbContext).Assembly.FullName)));

        services
            .AddIdentity<ApplicationUser, IdentityRole<Guid>>(options =>
            {
                options.Password.RequiredLength = 12;
                options.Password.RequiredUniqueChars = 4;
                options.Password.RequireUppercase = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireDigit = true;
                options.Password.RequireNonAlphanumeric = true;

                options.Lockout.AllowedForNewUsers = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);

                options.SignIn.RequireConfirmedAccount = false;
                options.SignIn.RequireConfirmedEmail = false;
                options.User.RequireUniqueEmail = true;
            })
            .AddEntityFrameworkStores<FinancialDbContext>()
            .AddClaimsPrincipalFactory<ApplicationUserClaimsPrincipalFactory>()
            .AddErrorDescriber<SpanishIdentityErrorDescriber>()
            .AddDefaultTokenProviders();

        services.ConfigureApplicationCookie(options =>
        {
            options.Cookie.Name = "__Host-SistemaFinanciero.Auth";
            options.Cookie.HttpOnly = true;
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            options.Cookie.SameSite = SameSiteMode.Strict;
            options.Cookie.Path = "/";
            options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
            options.SlidingExpiration = true;
            options.LoginPath = "/cuenta/iniciar-sesion";
            options.AccessDeniedPath = "/cuenta/acceso-denegado";
        });

        services.Configure<SecurityStampValidatorOptions>(options =>
            options.ValidationInterval = TimeSpan.Zero);

        services.AddScoped<IdentityBootstrapper>();
        services.AddScoped<IUserAdministrationService, UserAdministrationService>();
        services.AddScoped<IUserAccountService, UserAccountService>();
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<ICustomerCatalogService, CustomerCatalogService>();
        services.AddScoped<ISupplierCatalogService, SupplierCatalogService>();
        services.AddScoped<ICatalogItemService, CatalogItemService>();
        services.AddScoped<IFinancialCategoryService, FinancialCategoryService>();
        services.AddScoped<IDailyExchangeRateService, DailyExchangeRateService>();
        services.AddScoped<ITaxTypeService, TaxTypeService>();
        services.AddScoped<ILedgerAccountService, LedgerAccountService>();
        services.AddScoped<IWithholdingTypeService, WithholdingTypeService>();
        services.AddScoped<ISystemParametersService, SystemParametersService>();

        return services;
    }
}
