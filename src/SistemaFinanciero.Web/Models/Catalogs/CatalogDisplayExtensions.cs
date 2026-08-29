using SistemaFinanciero.Application.Catalogs.Common;
using SistemaFinanciero.Domain.Catalogs;
using SistemaFinanciero.Domain.Currencies;

namespace SistemaFinanciero.Web.Models.Catalogs;

/// <summary>Centraliza las etiquetas en español utilizadas en los catálogos MVC.</summary>
public static class CatalogDisplayExtensions
{
    public static string ToDisplayName(this CatalogStatusFilter status) => status switch
    {
        CatalogStatusFilter.Active => "Activos",
        CatalogStatusFilter.Inactive => "Inactivos",
        CatalogStatusFilter.All => "Todos",
        _ => "Desconocido",
    };

    public static string ToDisplayName(this CatalogItemType type) => type switch
    {
        CatalogItemType.Product => "Producto",
        CatalogItemType.Service => "Servicio",
        _ => "Desconocido",
    };

    public static string ToDisplayName(this FinancialCategoryKind kind) => kind switch
    {
        FinancialCategoryKind.Income => "Ingreso",
        FinancialCategoryKind.Expense => "Gasto",
        _ => "Desconocida",
    };

    public static string ToDisplayName(this FinancialAccountType type) => type switch
    {
        FinancialAccountType.Cash => "Caja",
        FinancialAccountType.Bank => "Cuenta bancaria",
        _ => "Desconocido",
    };

    public static string ToDisplayName(this CurrencyCode currency) => currency switch
    {
        CurrencyCode.CRC => "CRC - Colón costarricense",
        CurrencyCode.USD => "USD - Dólar estadounidense",
        _ => "Moneda desconocida",
    };
}

