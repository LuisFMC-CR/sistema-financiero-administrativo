using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SistemaFinanciero.Domain.Payables;
using SistemaFinanciero.Domain.Receivables;
using SistemaFinanciero.Domain.VatSettlements;
using SistemaFinanciero.Infrastructure.Persistence;

namespace SistemaFinanciero.IntegrationTests;

/// <summary>
/// Aislamiento de las pruebas SQL frente a los datos que ya tiene la base de desarrollo (paquete v9,
/// bloque 9). Todo cambio ocurre dentro de la transacción de la prueba, que siempre se revierte.
/// </summary>
public static class SqlTestIsolation
{
    /// <summary>
    /// Anula, solo dentro de la transacción de la prueba, las liquidaciones de IVA vigentes que ya
    /// existen en la base. Sin esto, una liquidación real (por ejemplo, la de 11/2026 registrada al
    /// usar la aplicación) hace que el servicio rechace cualquier liquidación de un mes fabricado
    /// anterior ("no se puede liquidar un período igual o anterior") y arrastra su saldo a favor.
    /// </summary>
    public static async Task CancelExistingVatSettlementsAsync(IServiceProvider services, Guid actorId)
    {
        FinancialDbContext context = services.GetRequiredService<FinancialDbContext>();
        List<VatSettlement> active = await context.Set<VatSettlement>()
            .Where(settlement => settlement.Status == VatSettlementStatus.Active)
            .ToListAsync();

        foreach (VatSettlement settlement in active)
        {
            settlement.Cancel("Aislamiento de la prueba (se revierte)", DateTimeOffset.UtcNow, actorId);
        }

        await context.SaveChangesAsync();
    }

    /// <summary>
    /// Marca como anuladas, solo dentro de la transacción de la prueba, las cuentas por cobrar y por
    /// pagar abiertas que ya existen en la base. Los reportes de antigüedad listan todas las cuentas
    /// pendientes del sistema; sin esto, los datos reales de la base de desarrollo aparecen junto a
    /// los de la prueba.
    /// </summary>
    public static async Task HideExistingOpenReceivablesAndPayablesAsync(IServiceProvider services)
    {
        FinancialDbContext context = services.GetRequiredService<FinancialDbContext>();
        await context.Set<Receivable>()
            .Where(receivable => receivable.Status == ReceivableStatus.Pending
                || receivable.Status == ReceivableStatus.PartiallyPaid)
            .ExecuteUpdateAsync(update => update.SetProperty(receivable => receivable.Status, ReceivableStatus.Cancelled));
        await context.Set<Payable>()
            .Where(payable => payable.Status == PayableStatus.Pending
                || payable.Status == PayableStatus.PartiallyPaid)
            .ExecuteUpdateAsync(update => update.SetProperty(payable => payable.Status, PayableStatus.Cancelled));
    }
}
