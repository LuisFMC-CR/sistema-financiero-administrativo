using Microsoft.EntityFrameworkCore;
using SistemaFinanciero.Domain.Parameters;
using SistemaFinanciero.Infrastructure.Persistence;

namespace SistemaFinanciero.Infrastructure.Parameters;

/// <summary>
/// Punto único donde cada servicio impide movimientos en meses pasados cerrados automáticamente o
/// cerrados de nuevo por Gerencia.
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
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        DateOnly today = DateOnly.FromDateTime(timeProvider.GetLocalNow().DateTime);
        SystemParameters? parameters = await dbContext.SystemParameters
            .AsNoTracking()
            .SingleOrDefaultAsync(cancellationToken);

        if (parameters?.FiscalPeriodStartDate is DateOnly fiscalStart
            && parameters.FiscalPeriodEndDate is DateOnly fiscalEnd
            && (operationDate < fiscalStart || operationDate > fiscalEnd))
        {
            return $"No es posible operar con fecha {operationDate:dd/MM/yyyy}: está fuera del "
                + $"período fiscal vigente ({fiscalStart:dd/MM/yyyy}–{fiscalEnd:dd/MM/yyyy}).";
        }

        bool isPastMonth = operationDate.Year < today.Year
            || operationDate.Year == today.Year && operationDate.Month < today.Month;
        if (!isPastMonth)
        {
            return null;
        }

        bool isReopened = await dbContext.AccountingPeriodOverrides
            .AsNoTracking()
            .AnyAsync(period => period.Year == operationDate.Year
                && period.Month == operationDate.Month
                && period.IsOpen,
                cancellationToken);

        if (isReopened)
        {
            return null;
        }

        return $"No es posible operar con fecha {operationDate:dd/MM/yyyy}: el mes ya está cerrado.";
    }
}
