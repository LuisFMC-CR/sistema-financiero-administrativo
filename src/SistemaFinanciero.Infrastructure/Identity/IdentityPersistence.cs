using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using SistemaFinanciero.Application.Security.Users;
using SistemaFinanciero.Infrastructure.Persistence;

namespace SistemaFinanciero.Infrastructure.Identity;

/// <summary>
/// Ejecuta cambios compuestos de Identity de forma atómica y respeta transacciones externas de prueba.
/// </summary>
/// <remarks>
/// Conexiones: es <c>internal</c>; envuelve cada operación de <c>UserAdministrationService</c> y
/// <c>UserAccountService</c>. Es el equivalente, para Identity, de lo que
/// <c>CatalogPersistence.SaveChangesAsync</c> hace para los catálogos: si la operación falla, revierte
/// (o retrocede a un punto de guardado, cuando una prueba ya abrió su propia transacción) en vez de
/// dejar cambios a medias.
/// </remarks>
internal static class IdentityPersistence
{
    private const string SavepointName = "UserSecurityOperation";

    public static async Task<UserOperationResult> ExecuteAsync(
        FinancialDbContext dbContext,
        Func<Task<UserOperationResult>> operation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(operation);

        IDbContextTransaction? externalTransaction = dbContext.Database.CurrentTransaction;

        if (externalTransaction is not null)
        {
            await externalTransaction.CreateSavepointAsync(SavepointName, cancellationToken);

            try
            {
                UserOperationResult result = await operation();

                if (result.IsSuccess)
                {
                    await externalTransaction.ReleaseSavepointAsync(
                        SavepointName,
                        cancellationToken);
                }
                else
                {
                    await externalTransaction.RollbackToSavepointAsync(
                        SavepointName,
                        cancellationToken);
                    dbContext.ChangeTracker.Clear();
                }

                return result;
            }
            catch
            {
                await externalTransaction.RollbackToSavepointAsync(
                    SavepointName,
                    cancellationToken);
                dbContext.ChangeTracker.Clear();
                throw;
            }
        }

        await using IDbContextTransaction transaction = await dbContext.Database
            .BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        try
        {
            UserOperationResult result = await operation();

            if (result.IsSuccess)
            {
                await transaction.CommitAsync(cancellationToken);
            }
            else
            {
                await transaction.RollbackAsync(cancellationToken);
                dbContext.ChangeTracker.Clear();
            }

            return result;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            dbContext.ChangeTracker.Clear();
            throw;
        }
    }
}
