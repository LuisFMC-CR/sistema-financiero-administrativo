using SistemaFinanciero.Application.Catalogs.Common;
using SistemaFinanciero.Domain.Catalogs;

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

    public static string ToDisplayName(this UnitOfMeasureCode unitOfMeasure) => unitOfMeasure.ToLabel();

    public static string ToDisplayName(this LedgerAccountSystemRole role) => role switch
    {
        LedgerAccountSystemRole.ReceivablesFromCustomers => "Cuentas por cobrar a clientes",
        LedgerAccountSystemRole.VatCreditable => "IVA acreditable",
        LedgerAccountSystemRole.WithholdingsReceivable => "Retenciones de renta a favor",
        LedgerAccountSystemRole.PayablesToSuppliers => "Cuentas por pagar a proveedores",
        LedgerAccountSystemRole.VatPayable => "IVA por pagar",
        LedgerAccountSystemRole.ReceivableAdjustments => "Ajustes a cuentas por cobrar",
        LedgerAccountSystemRole.PayableAdjustmentsInFavor => "Ajustes a favor en cuentas por pagar",
        LedgerAccountSystemRole.SalesReturnsAndAllowances => "Devoluciones y descuentos sobre ventas",
        _ => "Desconocido",
    };

    public static string ToDisplayName(this IdentificationType type) => type switch
    {
        IdentificationType.Individual => "Física",
        IdentificationType.LegalEntity => "Jurídica",
        IdentificationType.Dimex => "DIMEX",
        IdentificationType.Nite => "NITE",
        IdentificationType.ForeignNonResident => "Extranjero no domiciliado",
        _ => "Desconocido",
    };

    public static string ToDisplayName(this VatCondition condition) => condition switch
    {
        VatCondition.Normal => "Normal",
        VatCondition.StateHigherEducationInstitution => "Institución estatal de educación superior",
        VatCondition.NotSubject => "No sujeto",
        _ => "Desconocido",
    };
}

