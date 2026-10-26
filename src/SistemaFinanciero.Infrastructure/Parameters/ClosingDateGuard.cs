using Microsoft.EntityFrameworkCore;
using SistemaFinanciero.Domain.Parameters;
using SistemaFinanciero.Infrastructure.Persistence;

namespace SistemaFinanciero.Infrastructure.Parameters;

/// <summary>
/// Punto único donde cada servicio comprueba la fecha de cierre (regla de negocio 16) antes de
/// registrar, confirmar, anular o ajustar. Sin parámetros configurados, no hay fecha de cierre que
/// aplicar: todo está abierto, igual criterio que el límite de autorización (regla 7).
/// </summary>
/// <remarks>
/// Conexiones: la usan <c>InvoiceService</c>, <c>ExpenseService</c>, <c>ReceivablePaymentService</c>,
/// <c>SupplierPaymentService</c>, <c>IncomeService</c> y <c>BalanceAdjustmentService</c>
/// (Infrastructure), cada uno con la fecha de negocio que le corresponde a su operación.
/// </remarks>
internal static class ClosingDateGuard
{
    public static async Task<string?> GetViolationMessageAsync(
        FinancialDbContext dbContext,
        DateOnly operationDate,
        CancellationToken cancellationToken)
    {
        SystemParameters? parameters = await dbContext.Set<SystemParameters>()
            .AsNoTracking()
            .SingleOrDefaultAsync(cancellationToken);

        if (parameters is null || parameters.IsOpen(operationDate))
        {
            return null;
        }

        return $"No es posible operar con fecha {operationDate:dd/MM/yyyy}: el período ya está "
            + $"cerrado (fecha de cierre {parameters.ClosingDate:dd/MM/yyyy}).";
    }
}
