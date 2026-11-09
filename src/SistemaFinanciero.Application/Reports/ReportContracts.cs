using SistemaFinanciero.Application.Budgets;
using SistemaFinanciero.Domain.Incomes;
using SistemaFinanciero.Domain.Ledger;

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
    string AccountName,
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
    string AccountCode,
    string AccountName,
    int Count,
    decimal NetAmount,
    decimal TaxAmount,
    decimal TotalAmount);

/// <summary>
/// Resultado del reporte de gastos por período y categoría (regla 8). Solo incluye gastos confirmados:
/// nunca borradores ni anulados (regla 4/6). <see cref="AccountId"/> es nulo cuando el reporte incluye
/// todas las categorías.
/// </summary>
public sealed record ExpenseByCategoryReportResult(
    ReportHeader Header,
    DateOnly PeriodFrom,
    DateOnly PeriodTo,
    Guid? AccountId,
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

/// <summary>
/// El saldo de un documento (cuenta por cobrar o por pagar) repartido en los rangos de antigüedad
/// estándar (al día, 1-30, 31-60, 61-90 y más de 90 días de vencido), a la fecha del reporte.
/// </summary>
public sealed record AgingAmounts(
    decimal Current,
    decimal Overdue1To30,
    decimal Overdue31To60,
    decimal Overdue61To90,
    decimal OverdueOver90)
{
    /// <summary>Instancia en cero, usada para iniciar la acumulación por grupo.</summary>
    public static readonly AgingAmounts Zero = new(0m, 0m, 0m, 0m, 0m);

    /// <summary>Suma el saldo de un documento al rango de antigüedad que le corresponde según sus días de vencido.</summary>
    public AgingAmounts Add(int daysPastDue, decimal balance) => daysPastDue switch
    {
        <= 0 => this with { Current = Current + balance },
        <= 30 => this with { Overdue1To30 = Overdue1To30 + balance },
        <= 60 => this with { Overdue31To60 = Overdue31To60 + balance },
        <= 90 => this with { Overdue61To90 = Overdue61To90 + balance },
        _ => this with { OverdueOver90 = OverdueOver90 + balance },
    };

    /// <summary>Suma los rangos de antigüedad de otra instancia (para el gran total del reporte).</summary>
    public static AgingAmounts operator +(AgingAmounts left, AgingAmounts right) => new(
        left.Current + right.Current,
        left.Overdue1To30 + right.Overdue1To30,
        left.Overdue31To60 + right.Overdue31To60,
        left.Overdue61To90 + right.Overdue61To90,
        left.OverdueOver90 + right.OverdueOver90);
}

/// <summary>Una cuenta por pagar individual, dentro del desglose de un proveedor.</summary>
public sealed record PayableDetailLine(
    string ExpenseCode,
    DateOnly IssueDate,
    DateOnly DueDate,
    decimal Balance,
    AgingAmounts Aging);

/// <summary>
/// Las cuentas por pagar vigentes de un proveedor, resumidas para el reporte, con el desglose de cada
/// una (<see cref="Details"/>, ordenado por vencimiento, el más antiguo primero).
/// </summary>
public sealed record PayableBySupplierReportLine(
    Guid SupplierId,
    string SupplierName,
    int PayableCount,
    decimal TotalBalance,
    DateOnly EarliestDueDate,
    AgingAmounts Aging,
    IReadOnlyList<PayableDetailLine> Details);

/// <summary>
/// Resultado del reporte de cuentas por pagar por proveedor (regla 8): una fotografía del momento de
/// todas las cuentas por pagar vigentes (Pendiente o Parcialmente pagada), agrupadas por proveedor, con
/// el saldo de cada proveedor repartido por antigüedad (paquete v7).
/// </summary>
public sealed record PayableBySupplierReportResult(
    ReportHeader Header,
    IReadOnlyList<PayableBySupplierReportLine> Lines,
    decimal GrandTotalBalance,
    AgingAmounts GrandTotalAging);

/// <summary>Una cuenta por cobrar individual, dentro del desglose de un cliente.</summary>
public sealed record ReceivableDetailLine(
    long? InvoiceNumber,
    DateOnly IssueDate,
    DateOnly DueDate,
    decimal Balance,
    AgingAmounts Aging);

/// <summary>
/// Las cuentas por cobrar vigentes de un cliente, resumidas para el reporte, con el desglose de cada
/// una (<see cref="Details"/>, ordenado por vencimiento, el más antiguo primero).
/// </summary>
public sealed record ReceivableByCustomerReportLine(
    Guid CustomerId,
    string CustomerName,
    int ReceivableCount,
    decimal TotalBalance,
    DateOnly EarliestDueDate,
    AgingAmounts Aging,
    IReadOnlyList<ReceivableDetailLine> Details);

/// <summary>
/// Resultado del reporte de cuentas por cobrar por cliente con antigüedad de saldos (RF-98, paquete
/// v7): una fotografía del momento de todas las cuentas por cobrar vigentes (Pendiente o Parcialmente
/// pagada), agrupadas por cliente, con el saldo de cada cliente repartido por antigüedad. Distinto del
/// estado de cuenta de cliente: ese muestra el historial de un solo cliente; este muestra el saldo de
/// todos a la vez.
/// </summary>
public sealed record ReceivableByCustomerReportResult(
    ReportHeader Header,
    IReadOnlyList<ReceivableByCustomerReportLine> Lines,
    decimal GrandTotalBalance,
    AgingAmounts GrandTotalAging);

/// <summary>Una línea (débito o crédito) de un asiento, dentro del libro diario.</summary>
public sealed record JournalBookLine(string AccountCode, string AccountName, decimal Debit, decimal Credit);

/// <summary>Un asiento completo, con sus líneas, dentro del libro diario.</summary>
public sealed record JournalBookEntry(
    long Number,
    DateOnly EntryDate,
    string Description,
    JournalEntrySourceType SourceType,
    IReadOnlyList<JournalBookLine> Lines);

/// <summary>
/// Resultado del libro diario (RF-80, bloque 5/6 del paquete de mejoras finales): todos los asientos
/// de un rango de fechas, con sus líneas; los totales de débito y crédito del reporte coinciden porque
/// cada asiento ya balancea por separado (regla 1).
/// </summary>
public sealed record JournalBookReportResult(
    ReportHeader Header,
    DateOnly PeriodFrom,
    DateOnly PeriodTo,
    IReadOnlyList<JournalBookEntry> Entries,
    decimal TotalDebit,
    decimal TotalCredit);

/// <summary>Un movimiento del libro mayor de una cuenta, con su saldo acumulado después de aplicarse.</summary>
public sealed record LedgerBookMovement(
    DateOnly EntryDate,
    long JournalEntryNumber,
    string Description,
    decimal Debit,
    decimal Credit,
    decimal RunningBalance);

/// <summary>
/// Resultado del libro mayor de una cuenta (RF-81): el saldo anterior al período (movimientos previos
/// a <see cref="PeriodFrom"/>), los movimientos del período con su asiento y el saldo acumulado, según
/// la naturaleza de la cuenta (deudora para Activo y Gasto, acreedora para Pasivo, Patrimonio e
/// Ingreso).
/// </summary>
public sealed record LedgerBookReportResult(
    ReportHeader Header,
    Guid AccountId,
    string AccountCode,
    string AccountName,
    DateOnly PeriodFrom,
    DateOnly PeriodTo,
    decimal OpeningBalance,
    IReadOnlyList<LedgerBookMovement> Movements,
    decimal ClosingBalance);

/// <summary>Una cuenta de detalle con movimiento, dentro de la balanza de comprobación.</summary>
public sealed record TrialBalanceAccountLine(
    string AccountCode,
    string AccountName,
    decimal TotalDebit,
    decimal TotalCredit,
    decimal DebitBalance,
    decimal CreditBalance);

/// <summary>El subtotal de un grupo de cuentas (la cuenta superior inmediata) en la balanza de comprobación.</summary>
public sealed record TrialBalanceGroupLine(
    string GroupCode,
    string GroupName,
    IReadOnlyList<TrialBalanceAccountLine> Accounts,
    decimal TotalDebit,
    decimal TotalCredit,
    decimal DebitBalance,
    decimal CreditBalance);

/// <summary>
/// Resultado de la balanza de comprobación a una fecha de corte (RF-82): solo cuentas de detalle con
/// movimiento hasta esa fecha, con subtotales por grupo; los totales de débito y crédito, y los de
/// saldo deudor y acreedor, coinciden siempre (propiedad de la partida doble).
/// </summary>
public sealed record TrialBalanceReportResult(
    ReportHeader Header,
    DateOnly CutoffDate,
    IReadOnlyList<TrialBalanceGroupLine> Groups,
    decimal GrandTotalDebit,
    decimal GrandTotalCredit,
    decimal GrandTotalDebitBalance,
    decimal GrandTotalCreditBalance);

/// <summary>Una cuenta de ingreso o de gasto con movimiento, dentro del estado de resultados.</summary>
public sealed record IncomeStatementAccountLine(string AccountCode, string AccountName, decimal Amount);

/// <summary>El subtotal de un grupo de cuentas de ingreso o de gasto, dentro del estado de resultados.</summary>
public sealed record IncomeStatementGroupLine(
    string GroupCode,
    string GroupName,
    IReadOnlyList<IncomeStatementAccountLine> Accounts,
    decimal Subtotal);

/// <summary>
/// Resultado del estado de resultados de un rango de fechas (RF-83): ingresos y gastos por cuenta,
/// agrupados por su cuenta superior inmediata, y la utilidad o pérdida del período. Sale de los
/// asientos (cuentas tipo Ingreso y Gasto), por lo que no incluye IVA: el IVA de las ventas y de las
/// compras se contabiliza en cuentas de Pasivo y de Activo, nunca de Ingreso ni de Gasto.
/// </summary>
public sealed record IncomeStatementReportResult(
    ReportHeader Header,
    DateOnly PeriodFrom,
    DateOnly PeriodTo,
    IReadOnlyList<IncomeStatementGroupLine> IncomeGroups,
    decimal TotalIncome,
    IReadOnlyList<IncomeStatementGroupLine> ExpenseGroups,
    decimal TotalExpense,
    decimal NetResult);

/// <summary>
/// Una categoría de gasto de último nivel dentro del presupuesto anual a la fecha (RF-87): acumulado
/// desde el inicio del período fiscal hasta el mes indicado, sin IVA, más el presupuesto de sus doce
/// meses como referencia. Es solo una vista: el presupuesto sigue registrándose por mes y categoría.
/// </summary>
public sealed record AnnualBudgetReportLine(
    string AccountCode,
    string AccountName,
    decimal AccumulatedBudgeted,
    decimal AccumulatedExecuted,
    decimal AnnualBudgeted,
    decimal AvailableAmount,
    decimal VarianceAmount,
    decimal? VariancePercent)
{
    /// <summary>Semáforo de ejecución (RF-92) sobre el acumulado a la fecha.</summary>
    public BudgetExecutionStatus ExecutionStatus =>
        BudgetExecutionStatusCalculator.Compute(AccumulatedBudgeted, AccumulatedExecuted)!.Value;
}

/// <summary>Resultado del presupuesto anual a la fecha (RF-87).</summary>
public sealed record AnnualBudgetReportResult(
    ReportHeader Header,
    int PeriodYear,
    int UpToMonth,
    IReadOnlyList<AnnualBudgetReportLine> Lines,
    decimal TotalAccumulatedBudgeted,
    decimal TotalAccumulatedExecuted,
    decimal TotalAnnualBudgeted);

/// <summary>Las entradas y salidas de efectivo de un mes, dentro del flujo de efectivo mensual.</summary>
/// <summary>
/// <see cref="TransfersReceived"/> y <see cref="TransfersSent"/> solo se calculan cuando el reporte
/// está filtrado a una cuenta de efectivo (bloque 3 del paquete v8, punto 1): sin filtro, un traspaso
/// se compensa a sí mismo (entra a una cuenta propia y sale de otra propia) y se excluye por completo.
/// <see cref="VatPayments"/> es el pago de liquidaciones de IVA a Hacienda del mes, siempre como
/// salida, limitado a la cuenta filtrada cuando hay filtro. <see cref="TotalInflow"/> y
/// <see cref="TotalOutflow"/> ya incluyen estos tres montos.
/// </summary>
public sealed record CashFlowReportLine(
    int PeriodYear,
    int PeriodMonth,
    decimal TotalInflow,
    decimal TotalOutflow,
    decimal TransfersReceived,
    decimal TransfersSent,
    decimal VatPayments,
    decimal Net);

/// <summary>
/// Resultado del flujo de efectivo mensual (RF-86, bloque 7 del paquete de mejoras finales, punto 5;
/// traspasos y pago de liquidaciones de IVA, bloque 3 del paquete v8): entradas (ingresos vigentes de
/// facturas de contado, abonos e ingresos manuales, y traspasos recibidos cuando se filtra por cuenta)
/// contra salidas (pagos vigentes, gastos de contado confirmados, pago de liquidaciones de IVA, y
/// traspasos enviados cuando se filtra por cuenta), con IVA, por mes, con el neto.
/// <see cref="CashAccountId"/> es nulo cuando el reporte incluye todas las cuentas de efectivo.
/// </summary>
public sealed record CashFlowReportResult(
    ReportHeader Header,
    DateOnly PeriodFrom,
    DateOnly PeriodTo,
    Guid? CashAccountId,
    IReadOnlyList<CashFlowReportLine> Lines,
    decimal GrandTotalInflow,
    decimal GrandTotalOutflow,
    decimal GrandTotalNet);

/// <summary>El IVA cobrado y pagado de una tarifa, dentro del reporte de IVA del período.</summary>
public sealed record VatPeriodReportLine(
    string TaxCode,
    string TaxName,
    decimal SalesBase,
    decimal VatCollected,
    decimal PurchasesBase,
    decimal VatPaid)
{
    /// <summary>Obtiene la diferencia entre el IVA cobrado y el IVA pagado de esta tarifa.</summary>
    public decimal Difference => VatCollected - VatPaid;
}

/// <summary>
/// Resultado del IVA del período (RF-84, bloque 7 del paquete de mejoras finales, punto 6): por
/// tarifa, la base y el IVA de facturas confirmadas (cobrado) y de gastos confirmados (pagado);
/// excluye siempre borradores y anulados. Apoyo para la declaración; no sustituye la declaración ante
/// Hacienda.
/// </summary>
/// <summary>
/// La liquidación de IVA vigente del mes que resume un <see cref="VatPeriodReportResult"/> (bloque 3
/// del paquete v8, punto 2), cuando <see cref="VatPeriodReportResult.PeriodFrom"/> y
/// <see cref="VatPeriodReportResult.PeriodTo"/> cubren exactamente ese mes calendario.
/// </summary>
public sealed record VatPeriodSettlementSummary(
    string Code,
    decimal PaymentAmount,
    decimal CreditCarriedForward);

/// <summary>
/// <see cref="Settlement"/> es nulo salvo que el rango cubra exactamente un mes calendario con una
/// liquidación de IVA vigente; nunca descuenta ni altera <see cref="TotalVatCollected"/> ni
/// <see cref="TotalVatPaid"/>, que siempre vienen de facturas y gastos, nunca de los asientos de
/// liquidación.
/// </summary>
public sealed record VatPeriodReportResult(
    ReportHeader Header,
    DateOnly PeriodFrom,
    DateOnly PeriodTo,
    IReadOnlyList<VatPeriodReportLine> Lines,
    decimal TotalVatCollected,
    decimal TotalVatPaid,
    VatPeriodSettlementSummary? Settlement);

/// <summary>Una retención aplicada por un cliente al abonar una factura.</summary>
public sealed record WithholdingByCustomerLine(
    string CustomerName,
    string ReceivablePaymentCode,
    long? InvoiceNumber,
    DateOnly PaymentDate,
    string WithholdingTypeName,
    decimal Base,
    decimal Amount);

/// <summary>
/// Resultado de las retenciones aplicadas por clientes (RF-85, bloque 7 del paquete de mejoras
/// finales, punto 7): solo de abonos vigentes, con el total.
/// </summary>
public sealed record WithholdingByCustomerReportResult(
    ReportHeader Header,
    DateOnly PeriodFrom,
    DateOnly PeriodTo,
    IReadOnlyList<WithholdingByCustomerLine> Lines,
    decimal TotalAmount);

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
    /// por categoría; <paramref name="accountId"/> limita el resultado a una sola categoría cuando se
    /// indica.
    /// </summary>
    public Task<ExpenseByCategoryReportResult> GetExpenseByCategoryReportAsync(
        DateOnly periodFrom,
        DateOnly periodTo,
        Guid? accountId,
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

    /// <summary>
    /// Cuentas por pagar vigentes de todos los proveedores, agrupadas por proveedor, con antigüedad de
    /// saldos a la fecha de generación (paquete v7).
    /// </summary>
    public Task<PayableBySupplierReportResult> GetPayableBySupplierReportAsync(
        string generatedByUserName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Cuentas por cobrar vigentes de todos los clientes, agrupadas por cliente, con antigüedad de
    /// saldos a la fecha de generación (RF-98, paquete v7).
    /// </summary>
    public Task<ReceivableByCustomerReportResult> GetReceivableByCustomerReportAsync(
        string generatedByUserName,
        CancellationToken cancellationToken = default);

    /// <summary>Libro diario: todos los asientos entre <paramref name="periodFrom"/> y <paramref name="periodTo"/>.</summary>
    public Task<JournalBookReportResult> GetJournalBookReportAsync(
        DateOnly periodFrom,
        DateOnly periodTo,
        string generatedByUserName,
        CancellationToken cancellationToken = default);

    /// <summary>Libro mayor de <paramref name="accountId"/> entre <paramref name="periodFrom"/> y <paramref name="periodTo"/>.</summary>
    public Task<LedgerBookReportResult> GetLedgerBookReportAsync(
        Guid accountId,
        DateOnly periodFrom,
        DateOnly periodTo,
        string generatedByUserName,
        CancellationToken cancellationToken = default);

    /// <summary>Balanza de comprobación a la fecha de corte <paramref name="cutoffDate"/>.</summary>
    public Task<TrialBalanceReportResult> GetTrialBalanceReportAsync(
        DateOnly cutoffDate,
        string generatedByUserName,
        CancellationToken cancellationToken = default);

    /// <summary>Estado de resultados entre <paramref name="periodFrom"/> y <paramref name="periodTo"/>.</summary>
    public Task<IncomeStatementReportResult> GetIncomeStatementReportAsync(
        DateOnly periodFrom,
        DateOnly periodTo,
        string generatedByUserName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Presupuesto anual a la fecha (RF-87): acumulado hasta el mes fiscal ordinal
    /// <paramref name="upToMonth"/> del período cuyo año inicial es <paramref name="periodYear"/>.
    /// </summary>
    public Task<AnnualBudgetReportResult> GetAnnualBudgetReportAsync(
        int periodYear,
        int upToMonth,
        string generatedByUserName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Flujo de efectivo mensual (RF-86) entre <paramref name="periodFrom"/> y
    /// <paramref name="periodTo"/>; <paramref name="cashAccountId"/> limita el resultado a una sola
    /// cuenta de efectivo cuando se indica.
    /// </summary>
    public Task<CashFlowReportResult> GetCashFlowReportAsync(
        DateOnly periodFrom,
        DateOnly periodTo,
        Guid? cashAccountId,
        string generatedByUserName,
        CancellationToken cancellationToken = default);

    /// <summary>IVA del período (RF-84) entre <paramref name="periodFrom"/> y <paramref name="periodTo"/>, por tarifa.</summary>
    public Task<VatPeriodReportResult> GetVatPeriodReportAsync(
        DateOnly periodFrom,
        DateOnly periodTo,
        string generatedByUserName,
        CancellationToken cancellationToken = default);

    /// <summary>Retenciones aplicadas por clientes (RF-85) entre <paramref name="periodFrom"/> y <paramref name="periodTo"/>.</summary>
    public Task<WithholdingByCustomerReportResult> GetWithholdingByCustomerReportAsync(
        DateOnly periodFrom,
        DateOnly periodTo,
        string generatedByUserName,
        CancellationToken cancellationToken = default);
}
