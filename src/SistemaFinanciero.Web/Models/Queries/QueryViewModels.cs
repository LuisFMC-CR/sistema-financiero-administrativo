using SistemaFinanciero.Application.Catalogs.Common;
using SistemaFinanciero.Web.Models.Overdue;

namespace SistemaFinanciero.Web.Models.Queries;

/// <summary>
/// Tipo de documento que puede consultarse en la pantalla de Consultas (regla 11); cada uno reutiliza
/// el <c>SearchAsync</c> que ya existe en su propio módulo.
/// </summary>
public enum QueryDocumentType
{
    /// <summary>Facturas.</summary>
    Invoice,

    /// <summary>Gastos.</summary>
    Expense,

    /// <summary>Cuentas por cobrar.</summary>
    Receivable,

    /// <summary>Cuentas por pagar.</summary>
    Payable,

    /// <summary>Ingresos.</summary>
    Income,
}

/// <summary>
/// Fila genérica de resultado de Consultas: cada tipo de documento se traduce a esta misma forma para
/// que la pantalla tenga una sola tabla, y enlaza al <c>Details</c> que ya existe de su propio módulo
/// (regla 11: "detalle con registros relacionados e historial").
/// </summary>
public sealed record QueryResultRow(
    Guid Id,
    string Label,
    DateOnly Date,
    string? SecondaryText,
    decimal Amount,
    string Status,
    string DetailsController);

/// <summary>
/// Filtros combinados de la pantalla de Consultas (regla 11); solo los que apliquen al
/// <see cref="Type"/> elegido afectan la búsqueda.
/// </summary>
public sealed record QueriesIndexViewModel(
    QueryDocumentType Type,
    DateOnly? DateFrom,
    DateOnly? DateTo,
    string? Status,
    string? CustomerName,
    string? SupplierName,
    string? CategoryName,
    string? CashAccountName,
    PagedResult<QueryResultRow> Results,
    OverdueIndicatorViewModel? OverdueIndicator);
