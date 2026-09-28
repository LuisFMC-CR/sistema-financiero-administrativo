using System.Globalization;
using SistemaFinanciero.Application.Catalogs.Common;
using SistemaFinanciero.Domain.Catalogs;
using SistemaFinanciero.Domain.Currencies;
using SistemaFinanciero.Domain.Invoices;

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

    public static string ToDisplayName(this LedgerAccountType type) => type switch
    {
        LedgerAccountType.Asset => "Activo",
        LedgerAccountType.Liability => "Pasivo",
        LedgerAccountType.Equity => "Patrimonio",
        LedgerAccountType.Income => "Ingreso",
        LedgerAccountType.Expense => "Gasto",
        _ => "Desconocido",
    };

    public static string ToDisplayName(this CashAccountKind kind) => kind switch
    {
        CashAccountKind.Cash => "Caja",
        CashAccountKind.Bank => "Cuenta bancaria",
        _ => "Desconocido",
    };

    public static string ToDisplayName(this TaxCalculationType type) => type switch
    {
        TaxCalculationType.Percentage => "Porcentaje",
        TaxCalculationType.FixedAmountPerUnit => "Monto fijo por unidad",
        _ => "Desconocido",
    };

    /// <summary>Muestra la tarifa según su método: como porcentaje o como monto por unidad.</summary>
    public static string ToDisplayRate(this TaxCalculationType type, decimal rate)
    {
        string formattedRate = rate.ToString("0.####", CultureInfo.CurrentCulture);

        return type switch
        {
            TaxCalculationType.Percentage => $"{formattedRate} %",
            TaxCalculationType.FixedAmountPerUnit => $"{formattedRate} por unidad",
            _ => formattedRate,
        };
    }

    public static string ToDisplayName(this CurrencyCode currency) => currency switch
    {
        CurrencyCode.CRC => "CRC - Colón costarricense",
        CurrencyCode.USD => "USD - Dólar estadounidense",
        _ => "Moneda desconocida",
    };
}

