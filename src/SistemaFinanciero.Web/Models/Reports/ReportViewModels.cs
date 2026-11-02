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

/// <summary>Parámetros y resultado, si ya se consultó, del libro diario.</summary>
public sealed record JournalBookReportViewModel(
    DateOnly PeriodFrom,
    DateOnly PeriodTo,
    JournalBookReportResult? Result);

/// <summary>Cuenta y parámetros elegidos, y resultado, si ya se consultó, del libro mayor.</summary>
public sealed record LedgerBookReportViewModel(
    Guid? AccountId,
    DateOnly PeriodFrom,
    DateOnly PeriodTo,
    IReadOnlyList<SelectListItem> AccountOptions,
    LedgerBookReportResult? Result);

/// <summary>Parámetros y resultado, si ya se consultó, de la balanza de comprobación.</summary>
public sealed record TrialBalanceReportViewModel(
    DateOnly CutoffDate,
    TrialBalanceReportResult? Result);

/// <summary>Parámetros y resultado, si ya se consultó, del estado de resultados.</summary>
public sealed record IncomeStatementReportViewModel(
    DateOnly PeriodFrom,
    DateOnly PeriodTo,
    IncomeStatementReportResult? Result);

/// <summary>Parámetros y resultado del presupuesto anual a la fecha.</summary>
public sealed record AnnualBudgetReportViewModel(
    int PeriodYear,
    int UpToMonth,
    AnnualBudgetReportResult? Result);

/// <summary>Parámetros, cuentas de efectivo disponibles y resultado del flujo de efectivo mensual.</summary>
public sealed record CashFlowReportViewModel(
    DateOnly PeriodFrom,
    DateOnly PeriodTo,
    Guid? CashAccountId,
    IReadOnlyList<SelectListItem> CashAccountOptions,
    CashFlowReportResult? Result);

/// <summary>Parámetros y resultado del IVA del período.</summary>
public sealed record VatPeriodReportViewModel(
    DateOnly PeriodFrom,
    DateOnly PeriodTo,
    VatPeriodReportResult? Result);

/// <summary>Parámetros y resultado de las retenciones aplicadas por clientes.</summary>
public sealed record WithholdingByCustomerReportViewModel(
    DateOnly PeriodFrom,
    DateOnly PeriodTo,
    WithholdingByCustomerReportResult? Result);
