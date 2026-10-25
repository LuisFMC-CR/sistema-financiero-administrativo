using SistemaFinanciero.Application.Catalogs.Common;
using SistemaFinanciero.Domain.Common;
using SistemaFinanciero.Domain.Expenses;
using SistemaFinanciero.Domain.Invoices;

namespace SistemaFinanciero.Application.Expenses;

/// <summary>Estado por el que se filtra el listado de gastos.</summary>
public enum ExpenseStatusFilter
{
    /// <summary>Incluye borradores, confirmados y anulados.</summary>
    All,

    /// <summary>Solo borradores.</summary>
    Draft,

    /// <summary>Solo confirmados.</summary>
    Confirmed,

    /// <summary>Solo anulados.</summary>
    Cancelled,
}

/// <summary>
/// Define los filtros, uno por columna del listado, y la paginación de los gastos. Admite restringir
/// el resultado a los gastos de un usuario (Asistente "ve solo los suyos", igual que con facturas).
/// </summary>
public sealed record ExpenseQuery
{
    public ExpenseQuery(
        string? code = null,
        string? supplierName = null,
        string? categoryName = null,
        DateOnly? issueDateFrom = null,
        DateOnly? issueDateTo = null,
        decimal? amountMin = null,
        decimal? amountMax = null,
        ExpenseStatusFilter status = ExpenseStatusFilter.All,
        Guid? createdByUserId = null,
        int page = 1,
        int pageSize = 20)
    {
        Code = string.IsNullOrWhiteSpace(code) ? null : code.Trim();
        SupplierName = string.IsNullOrWhiteSpace(supplierName) ? null : supplierName.Trim();
        CategoryName = string.IsNullOrWhiteSpace(categoryName) ? null : categoryName.Trim();
        IssueDateFrom = issueDateFrom;
        IssueDateTo = issueDateTo;
        AmountMin = amountMin;
        AmountMax = amountMax;
        Status = status;
        CreatedByUserId = createdByUserId;
        Page = Math.Max(1, page);
        PageSize = Math.Clamp(pageSize, 1, 100);
    }

    /// <summary>Texto opcional buscado en el código del gasto.</summary>
    public string? Code { get; }

    /// <summary>Texto opcional buscado en el nombre del proveedor.</summary>
    public string? SupplierName { get; }

    /// <summary>Texto opcional buscado en el nombre de la categoría.</summary>
    public string? CategoryName { get; }

    /// <summary>Fecha de emisión mínima opcional.</summary>
    public DateOnly? IssueDateFrom { get; }

    /// <summary>Fecha de emisión máxima opcional.</summary>
    public DateOnly? IssueDateTo { get; }

    /// <summary>Total mínimo opcional.</summary>
    public decimal? AmountMin { get; }

    /// <summary>Total máximo opcional.</summary>
    public decimal? AmountMax { get; }

    /// <summary>Estado que debe incluirse.</summary>
    public ExpenseStatusFilter Status { get; }

    /// <summary>
    /// Cuando no es nulo, limita el resultado a los gastos creados por este usuario (Asistente).
    /// </summary>
    public Guid? CreatedByUserId { get; }

    /// <summary>Número de página basado en uno.</summary>
    public int Page { get; }

    /// <summary>Cantidad máxima de registros por página.</summary>
    public int PageSize { get; }
}

/// <summary>
/// Datos para crear o editar el encabezado de un gasto; es lo único que tiene, porque un gasto no
/// tiene líneas (regla 2). Mientras es borrador, todo es editable.
/// </summary>
public sealed record SaveExpenseCommand(
    Guid SupplierId,
    Guid CategoryId,
    string Description,
    DateOnly IssueDate,
    PaymentTerm PaymentTerm,
    decimal NetAmount,
    Guid TaxTypeId,
    PaymentMethod PaymentMethod);

/// <summary>
/// Datos que solo se piden al confirmar un gasto de contado (regla 7): la cuenta de efectivo de
/// origen y la referencia. El medio de pago no se pide: ya quedó fijo desde el borrador. A crédito se
/// ignoran: confirmar solo crea la cuenta por pagar (regla 2).
/// </summary>
public sealed record ConfirmExpenseCommand(Guid? CashAccountId, string? Reference);

/// <summary>Resultado de crear un gasto: la operación y el identificador del borrador nuevo.</summary>
public sealed record ExpenseCreationResult(CatalogOperationResult Operation, Guid? ExpenseId);

/// <summary>
/// Resultado de comprobar el presupuesto de la categoría y el mes de un gasto, antes de confirmarlo
/// (regla 6). Es solo informativo: nunca bloquea la confirmación.
/// </summary>
/// <remarks>
/// <see cref="AvailableAmountBeforeThisExpense"/> y <see cref="BudgetedAmount"/> son irrelevantes
/// cuando <see cref="HasBudget"/> es falso: no hay presupuesto configurado para esa categoría y ese
/// mes, y el gasto puede confirmarse igual (regla 6: "si no hay presupuesto, se indica y se permite
/// continuar").
/// </remarks>
public sealed record ExpenseBudgetCheck(
    bool HasBudget,
    decimal? BudgetedAmount,
    decimal AvailableAmountBeforeThisExpense,
    bool ExceedsAvailable);

/// <summary>Proyección completa de un gasto para el detalle y la edición.</summary>
public sealed record ExpenseModel(
    Guid Id,
    string Code,
    Guid SupplierId,
    string SupplierName,
    Guid CategoryId,
    string CategoryName,
    string Description,
    DateOnly IssueDate,
    PaymentTerm PaymentTerm,
    DateOnly? DueDate,
    decimal NetAmount,
    Guid? TaxTypeId,
    string TaxCode,
    string TaxName,
    decimal TaxRate,
    decimal TaxAmount,
    decimal TotalAmount,
    PaymentMethod PaymentMethod,
    Guid? CashAccountId,
    string? CashAccountName,
    string? Reference,
    ExpenseStatus Status,
    DateTimeOffset? ConfirmedAtUtc,
    string? CancellationReason,
    DateTimeOffset? CancelledAtUtc,
    Guid CreatedByUserId,
    string CreatedByUserName,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    string Version);

/// <summary>
/// Casos de uso del ciclo de vida del gasto: crear y editar el encabezado del borrador, confirmar y
/// anular. Es el espejo, del lado de gastos, de <c>IInvoiceService</c>; a diferencia de este, no tiene
/// líneas que agregar, reemplazar o quitar.
/// </summary>
/// <remarks>
/// Conexiones: lo implementa <c>ExpenseService</c> (Infrastructure/Expenses). Confirmar de crédito
/// genera la cuenta por pagar (<c>Payable</c>); de contado guarda la cuenta de efectivo y la
/// referencia en el propio gasto, sin generar ningún otro documento (regla 2/7). Anular lo anula en
/// cascada, siempre que la cuenta por pagar no tenga pagos vigentes. Confirmar de contado sobre el
/// límite de autorización se rechaza salvo que quien confirma sea Gerencia (<c>actorIsManagement</c>
/// de <see cref="ConfirmAsync"/>, regla 7). El parámetro <c>restrictToOwnerUserId</c> que reciben la
/// lectura y la edición de borrador aplica la regla de perfiles «Asistente ve solo los suyos», igual
/// criterio que <c>IInvoiceService</c>; confirmar y anular no lo reciben porque son exclusivos de
/// Finanzas y Gerencia respectivamente.
/// </remarks>
public interface IExpenseService
{
    /// <summary>Busca gastos con filtros y paginación.</summary>
    public Task<PagedResult<ExpenseModel>> SearchAsync(
        ExpenseQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>Obtiene un gasto por identificador.</summary>
    public Task<ExpenseModel?> GetAsync(
        Guid id,
        Guid? restrictToOwnerUserId = null,
        CancellationToken cancellationToken = default);

    /// <summary>Crea un borrador de gasto. Genera su código automático (regla 12, <c>GAS-001</c>).</summary>
    public Task<ExpenseCreationResult> CreateAsync(
        SaveExpenseCommand command,
        Guid actorId,
        CancellationToken cancellationToken = default);

    /// <summary>Actualiza el encabezado de un borrador.</summary>
    public Task<CatalogOperationResult> UpdateHeaderAsync(
        Guid id,
        SaveExpenseCommand command,
        string version,
        Guid actorId,
        Guid? restrictToOwnerUserId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Confirma el gasto. De contado, rechaza si el total supera el límite de autorización y
    /// <paramref name="actorIsManagement"/> es falso (regla 7); a crédito genera su cuenta por pagar.
    /// </summary>
    public Task<CatalogOperationResult> ConfirmAsync(
        Guid id,
        ConfirmExpenseCommand command,
        string version,
        Guid actorId,
        bool actorIsManagement,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Anula un gasto confirmado con motivo; si es a crédito, anula también su cuenta por pagar,
    /// siempre que no tenga pagos vigentes. Solo Gerencia.
    /// </summary>
    public Task<CatalogOperationResult> CancelAsync(
        Guid id,
        string reason,
        string version,
        Guid actorId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Comprueba el presupuesto de una categoría y un mes antes de confirmar un gasto (regla 6); no
    /// bloquea nada, solo informa para mostrar una alerta en la pantalla de confirmación.
    /// </summary>
    public Task<ExpenseBudgetCheck> CheckBudgetAsync(
        Guid categoryId,
        DateOnly issueDate,
        decimal totalAmount,
        CancellationToken cancellationToken = default);
}
