using Microsoft.AspNetCore.Mvc.Rendering;
using SistemaFinanciero.Application.Reports;

namespace SistemaFinanciero.Web.Models.Reports;

/// <summary>Parámetros y resultado, si ya se consultó, del reporte de ingresos por período.</summary>
public sealed record IncomeReportViewModel(
    DateOnly PeriodFrom,
    DateOnly PeriodTo,
    IncomeReportResult? Result);

/// <summary>Parámetros y resultado, si ya se consultó, del reporte de gastos por período y categoría.</summary>
public sealed record ExpenseByCategoryReportViewModel(
    DateOnly PeriodFrom,
    DateOnly PeriodTo,
    Guid? CategoryId,
    IReadOnlyList<SelectListItem> CategoryOptions,
    ExpenseByCategoryReportResult? Result);

/// <summary>Parámetros y resultado, si ya se consultó, del reporte de presupuesto contra lo ejecutado.</summary>
public sealed record BudgetVsExecutedReportViewModel(
    int PeriodYear,
    int PeriodMonth,
    BudgetVsExecutedReportResult? Result);

/// <summary>Parámetros y resultado, si ya se consultó, del comparativo mensual de ingresos y gastos.</summary>
public sealed record MonthlyComparisonReportViewModel(
    int PeriodYearFrom,
    int PeriodMonthFrom,
    int PeriodYearTo,
    int PeriodMonthTo,
    MonthlyComparisonReportResult? Result);

/// <summary>Cliente elegido y resultado, si ya se consultó, del estado de cuenta de cliente.</summary>
public sealed record CustomerStatementReportViewModel(
    Guid? CustomerId,
    IReadOnlyList<SelectListItem> CustomerOptions,
    CustomerStatementReportResult? Result);

/// <summary>Resultado del reporte de cuentas por pagar por proveedor; sin parámetros.</summary>
public sealed record PayableBySupplierReportViewModel(PayableBySupplierReportResult Result);
