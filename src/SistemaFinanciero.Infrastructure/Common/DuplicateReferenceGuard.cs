using Microsoft.EntityFrameworkCore;
using SistemaFinanciero.Domain.Incomes;
using SistemaFinanciero.Domain.Payables;
using SistemaFinanciero.Domain.Receivables;
using SistemaFinanciero.Infrastructure.Persistence;

namespace SistemaFinanciero.Infrastructure.Common;

/// <summary>
/// Comprueba si una referencia ya se usó antes en la misma cuenta de efectivo (regla de negocio 10):
/// advierte, sin bloquear, en un ingreso, un abono o un pago.
/// </summary>
/// <remarks>
/// Conexiones: la usan <c>ReceivablePaymentService</c>, <c>SupplierPaymentService</c>,
/// <c>IncomeService</c> e <c>InvoiceService</c> (factura de contado, que genera un ingreso igual que
/// un abono); busca en las tres tablas de movimientos de efectivo, sin importar su estado, porque la
/// regla solo exige advertir, no distinguir vigentes de anulados.
/// </remarks>
internal static class DuplicateReferenceGuard
{
    public static async Task<bool> ExistsAsync(
        FinancialDbContext dbContext,
        Guid cashAccountId,
        string reference,
        CancellationToken cancellationToken)
    {
        bool existsInReceivablePayments = await dbContext.Set<ReceivablePayment>()
            .AsNoTracking()
            .AnyAsync(
                payment => payment.CashAccountId == cashAccountId && payment.Reference == reference,
                cancellationToken);

        if (existsInReceivablePayments)
        {
            return true;
        }

        bool existsInSupplierPayments = await dbContext.Set<SupplierPayment>()
            .AsNoTracking()
            .AnyAsync(
                payment => payment.CashAccountId == cashAccountId && payment.Reference == reference,
                cancellationToken);

        if (existsInSupplierPayments)
        {
            return true;
        }

        return await dbContext.Set<Income>()
            .AsNoTracking()
            .AnyAsync(
                income => income.CashAccountId == cashAccountId && income.Reference == reference,
                cancellationToken);
    }
}
