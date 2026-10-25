using SistemaFinanciero.Application.Budgets;
using SistemaFinanciero.Domain.Incomes;

namespace SistemaFinanciero.Application.Reports;

/// <summary>
/// Encabezado común de todo reporte (regla de negocio 8): nombre, parámetros usados, fecha y hora de
/// generación y usuario que lo generó.
/// </summary>
public sealed record ReportHeader(
    string ReportName,
    string ParametersDescription,
    DateTimeOffset GeneratedAtUtc,
    string GeneratedByUserName);

/// <summary>Un ingreso vigente dentro del período del reporte de ingresos por período.</summary>
public sealed record IncomeReportLine(
    string Code,
    DateOnly IncomeDate,
    string CategoryName,
    string CashAccountName,
    IncomeOrigin Origin,
    string? Reference,
    decimal Amount);

/// <summary>
/// Resultado del reporte de ingresos por período (regla 8). Excluye siempre los ingresos anulados
/// (regla 4: "lo anulado sale de totales, reportes y presupuesto").
/// </summary>
public sealed record IncomeReportResult(
    ReportHeader Header,
    DateOnly PeriodFrom,
    DateOnly PeriodTo,
    IReadOnlyList<IncomeReportLine> Lines,
    decimal TotalAmount);

/// <summary>El total de los gastos confirmados de una categoría dentro del período del reporte.</summary>
public sealed record ExpenseByCategoryReportLine(
    string CategoryCode,
    string CategoryName,
    int Count,
    decimal NetAmount,
    decimal TaxAmount,
    decimal TotalAmount);

/// <summary>
/// Resultado del reporte de gastos por período y categoría (regla 8). Solo incluye gastos confirmados:
/// nunca borradores ni anulados (regla 4/6). <see cref="CategoryId"/> es nulo cuando el reporte incluye
/// todas las categorías.
/// </summary>
public sealed record ExpenseByCategoryReportResult(
    ReportHeader Header,
    DateOnly PeriodFrom,
    DateOnly PeriodTo,
    Guid? CategoryId,
    IReadOnlyList<ExpenseByCategoryReportLine> Lines,
    decimal GrandTotal);

/// <summary>
/// Resultado del reporte de presupuesto contra lo ejecutado de un mes (regla 8). Reutiliza
/// directamente <see cref="BudgetModel"/> de <c>IBudgetService.SearchByPeriodAsync</c>: una fila por
/// categoría de gasto de último nivel, con o sin presupuesto configurado para ese mes.
/// </summary>
public sealed record BudgetVsExecutedReportResult(
    ReportHeader Header,
    int PeriodYear,
    int PeriodMonth,
    IReadOnlyList<BudgetModel> Lines,
    decimal TotalBudgeted,
    decimal TotalExecuted);

/// <summary>Los totales de ingresos vigentes y gastos confirmados de un mes del comparativo.</summary>
public sealed record MonthlyComparisonLine(
    int PeriodYear,
    int PeriodMonth,
    decimal TotalIncome,
    decimal TotalExpense);

/// <summary>
/// Resultado del comparativo mensual de ingresos y gastos (regla 8): un rango de meses, con una fila
/// por mes incluso sin movimientos (para no romper la serie comparativa). Sin movimientos en ningún
/// mes del rango, <see cref="GrandTotalIncome"/> y <see cref="GrandTotalExpense"/> quedan en 0, que es
/// el caso "sin datos" de este reporte (a diferencia de los demás, aquí <c>Lines</c> no queda vacío).
/// </summary>
public sealed record MonthlyComparisonReportResult(
    ReportHeader Header,
    int PeriodYearFrom,
    int PeriodMonthFrom,
    int PeriodYearTo,
    int PeriodMonthTo,
    IReadOnlyList<MonthlyComparisonLine> Lines,
    decimal GrandTotalIncome,
    decimal GrandTotalExpense);

/// <summary>
/// Un movimiento del historial cronológico del estado de cuenta de un cliente: una factura a crédito
/// (aumenta la deuda) o un abono (la disminuye). <see cref="IsCancelled"/> no se excluye del historial
/// (regla 4: "sigue visible en consultas e historial"), pero no participa en
/// <see cref="CustomerStatementReportResult.CurrentBalance"/>.
/// </summary>
public sealed record CustomerStatementLine(
    DateOnly Date,
    string MovementType,
    string Description,
    decimal Amount,
    bool IsCancelled);

/// <summary>
/// Resultado del estado de cuenta de un cliente (regla 8): su historial cronológico de facturas a
/// crédito y abonos, y el saldo actual. No es un libro contable (fuera del alcance del sistema): no
/// calcula un saldo que corre línea por línea, solo el saldo actual, igual que la cuenta por cobrar ya
/// lo mantiene.
/// </summary>
public sealed record CustomerStatementReportResult(
    ReportHeader Header,
    Guid CustomerId,
    string CustomerName,
    IReadOnlyList<CustomerStatementLine> Lines,
    decimal CurrentBalance);

/// <summary>Las cuentas por pagar vigentes de un proveedor, resumidas para el reporte.</summary>
public sealed record PayableBySupplierReportLine(
    Guid SupplierId,
    string SupplierName,
    int PayableCount,
    decimal TotalBalance,
    DateOnly EarliestDueDate);

/// <summary>
/// Resultado del reporte de cuentas por pagar por proveedor (regla 8): una fotografía del momento de
/// todas las cuentas por pagar vigentes (Pendiente o Parcialmente pagada), agrupadas por proveedor.
/// </summary>
public sealed record PayableBySupplierReportResult(
    ReportHeader Header,
    IReadOnlyList<PayableBySupplierReportLine> Lines,
    decimal GrandTotalBalance);

/// <summary>
/// Casos de uso de los reportes financieros (regla de negocio 8), en pantalla, PDF y Excel. Cada
/// reporte agrega sus propios datos consultando las tablas directamente, igual criterio que
/// <c>BudgetService</c>, porque no son operaciones de un solo módulo sino una vista de varios a la vez.
/// </summary>
/// <remarks>
/// Conexiones: lo implementa <c>ReportService</c> (Infrastructure/Reports); lo consume
/// <c>ReportsController</c> (Web, ruta <c>/reportes</c>), que construye el nombre de cada reporte, la
/// descripción de los parámetros usados y el usuario (del claim <c>SystemClaimTypes.FullName</c>) y
/// los pasa para que queden en el <see cref="ReportHeader"/> de cada resultado.
/// </remarks>
public interface IReportService
{
    /// <summary>Ingresos vigentes emitidos entre <paramref name="periodFrom"/> y <paramref name="periodTo"/>.</summary>
    public Task<IncomeReportResult> GetIncomeReportAsync(
        DateOnly periodFrom,
        DateOnly periodTo,
        string generatedByUserName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gastos confirmados entre <paramref name="periodFrom"/> y <paramref name="periodTo"/>, agrupados
    /// por categoría; <paramref name="categoryId"/> limita el resultado a una sola categoría cuando se
    /// indica.
    /// </summary>
    public Task<ExpenseByCategoryReportResult> GetExpenseByCategoryReportAsync(
        DateOnly periodFrom,
        DateOnly periodTo,
        Guid? categoryId,
        string generatedByUserName,
        CancellationToken cancellationToken = default);

    /// <summary>Presupuesto contra lo ejecutado del mes <paramref name="periodYear"/>/<paramref name="periodMonth"/>.</summary>
    public Task<BudgetVsExecutedReportResult> GetBudgetVsExecutedReportAsync(
        int periodYear,
        int periodMonth,
        string generatedByUserName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Comparativo mensual de ingresos y gastos entre el mes <paramref name="periodYearFrom"/>/
    /// <paramref name="periodMonthFrom"/> y el mes <paramref name="periodYearTo"/>/
    /// <paramref name="periodMonthTo"/>, ambos inclusive.
    /// </summary>
    public Task<MonthlyComparisonReportResult> GetMonthlyComparisonReportAsync(
        int periodYearFrom,
        int periodMonthFrom,
        int periodYearTo,
        int periodMonthTo,
        string generatedByUserName,
        CancellationToken cancellationToken = default);

    /// <summary>Estado de cuenta de un cliente: su historial y su saldo actual.</summary>
    public Task<CustomerStatementReportResult> GetCustomerStatementReportAsync(
        Guid customerId,
        string generatedByUserName,
        CancellationToken cancellationToken = default);

    /// <summary>Cuentas por pagar vigentes de todos los proveedores, agrupadas por proveedor.</summary>
    public Task<PayableBySupplierReportResult> GetPayableBySupplierReportAsync(
        string generatedByUserName,
        CancellationToken cancellationToken = default);
}
