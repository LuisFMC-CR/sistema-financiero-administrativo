using SistemaFinanciero.Application.Catalogs.Common;
using SistemaFinanciero.Domain.Adjustments;

namespace SistemaFinanciero.Application.Adjustments;

/// <summary>Proyección de un ajuste de saldo para mostrar en el historial de una cuenta.</summary>
public sealed record BalanceAdjustmentModel(
    Guid Id,
    decimal Amount,
    BalanceAdjustmentDirection Direction,
    string Reason,
    DateTimeOffset CreatedAtUtc,
    string CreatedByUserName);

/// <summary>
/// Casos de uso del ajuste manual de saldo (regla de negocio 4): exclusivo de Gerencia, no genera
/// ingresos ni pagos ni toca efectivo, documento o presupuesto, y un error se corrige con otro ajuste
/// en sentido contrario, nunca editando o anulando el anterior.
/// </summary>
/// <remarks>
/// Conexiones: lo implementa <c>BalanceAdjustmentService</c> (Infrastructure/Adjustments); lo consume
/// <c>BalanceAdjustmentsController</c> (Web, rutas <c>/cuentas-por-cobrar/{id}/ajustar</c> y
/// <c>/cuentas-por-pagar/{id}/ajustar</c>).
/// </remarks>
public interface IBalanceAdjustmentService
{
    /// <summary>Lista los ajustes de una cuenta por cobrar, del más reciente al más antiguo.</summary>
    public Task<IReadOnlyList<BalanceAdjustmentModel>> SearchByReceivableAsync(
        Guid receivableId,
        CancellationToken cancellationToken = default);

    /// <summary>Lista los ajustes de una cuenta por pagar, del más reciente al más antiguo.</summary>
    public Task<IReadOnlyList<BalanceAdjustmentModel>> SearchByPayableAsync(
        Guid payableId,
        CancellationToken cancellationToken = default);

    /// <summary>Ajusta el saldo de una cuenta por cobrar.</summary>
    public Task<CatalogOperationResult> AdjustReceivableAsync(
        Guid receivableId,
        decimal amount,
        BalanceAdjustmentDirection direction,
        string reason,
        string version,
        Guid actorId,
        CancellationToken cancellationToken = default);

    /// <summary>Ajusta el saldo de una cuenta por pagar.</summary>
    public Task<CatalogOperationResult> AdjustPayableAsync(
        Guid payableId,
        decimal amount,
        BalanceAdjustmentDirection direction,
        string reason,
        string version,
        Guid actorId,
        CancellationToken cancellationToken = default);
}
