using Microsoft.EntityFrameworkCore;
using SistemaFinanciero.Domain.CashTransfers;
using SistemaFinanciero.Domain.Incomes;
using SistemaFinanciero.Domain.IncomeTax;
using SistemaFinanciero.Domain.Payables;
using SistemaFinanciero.Domain.Receivables;
using SistemaFinanciero.Infrastructure.Persistence;

namespace SistemaFinanciero.Infrastructure.Common;

/// <summary>
/// Comprueba si una referencia ya se usó antes en la misma cuenta de efectivo (regla de negocio 10):
/// advierte, sin bloquear, en un ingreso, un abono, un pago o un traspaso.
/// </summary>
/// <remarks>
/// Conexiones: la usan <c>ReceivablePaymentService</c>, <c>SupplierPaymentService</c>,
/// <c>IncomeService</c>, <c>InvoiceService</c> (factura de contado, que genera un ingreso igual que
/// un abono), <c>CashTransferService</c> e <c>IncomeTaxService</c>; busca en las cinco tablas de
/// movimientos de efectivo con referencia (incluidos los pagos de renta, RF-99), sin
/// importar su estado, porque la regla solo exige advertir, no distinguir vigentes de anulados. En
/// <c>Traspasos</c> compara contra el origen y el destino, porque la referencia puede repetirse en
/// cualquiera de las dos cuentas.
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

        bool existsInIncomes = await dbContext.Set<Income>()
            .AsNoTracking()
            .AnyAsync(
                income => income.CashAccountId == cashAccountId && income.Reference == reference,
                cancellationToken);

        if (existsInIncomes)
        {
            return true;
        }

        bool existsInCashTransfers = await dbContext.Set<CashTransfer>()
            .AsNoTracking()
            .AnyAsync(
                transfer => (transfer.SourceCashAccountId == cashAccountId || transfer.DestinationCashAccountId == cashAccountId)
                    && transfer.Reference == reference,
                cancellationToken);

        if (existsInCashTransfers)
        {
            return true;
        }

        return await dbContext.Set<IncomeTaxPayment>()
            .AsNoTracking()
            .AnyAsync(
                payment => payment.CashAccountId == cashAccountId && payment.Reference == reference,
                cancellationToken);
    }
}
