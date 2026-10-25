using System.Data.Common;
using System.Globalization;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using SistemaFinanciero.Application.Catalogs.Common;

namespace SistemaFinanciero.Infrastructure.Catalogs;

/// <summary>
/// Operaciones técnicas compartidas por los servicios de catálogo.
/// </summary>
/// <remarks>
/// Conexiones: es <c>internal</c>; la usa cada servicio de catálogo en Infrastructure (clientes,
/// proveedores, productos, categorías, cuentas contables, tipos de impuesto, tipos de retención,
/// tasas, parámetros) para paginar (<see cref="ToPageAsync"/>), codificar el <c>RowVersion</c> como
/// texto (<see cref="EncodeVersion"/>), aplicar el token de concurrencia recibido del formulario
/// (<see cref="TrySetOriginalVersion"/>), generar el código automático de un catálogo (regla 12, ver
/// <see cref="GenerateSequentialCodeAsync"/>) y traducir una violación de índice único de SQL Server en
/// un <see cref="CatalogOperationResult.Duplicated"/> en vez de dejar escapar la excepción.
/// </remarks>
internal static class CatalogPersistence
{
    private const int DuplicateKeyError = 2627;
    private const int DuplicateIndexError = 2601;

    public static async Task<PagedResult<T>> ToPageAsync<T>(
        IQueryable<T> query,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        int totalCount = await query.CountAsync(cancellationToken);
        IReadOnlyList<T> items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<T>(items, page, pageSize, totalCount);
    }

    public static string EncodeVersion(byte[] rowVersion)
    {
        ArgumentNullException.ThrowIfNull(rowVersion);
        return Convert.ToBase64String(rowVersion);
    }

    public static bool TrySetOriginalVersion(
        DbContext context,
        object entity,
        string version)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(entity);

        if (string.IsNullOrWhiteSpace(version))
        {
            return false;
        }

        try
        {
            byte[] rowVersion = Convert.FromBase64String(version);

            if (rowVersion.Length != sizeof(long))
            {
                return false;
            }

            context.Entry(entity).Property("RowVersion").OriginalValue = rowVersion;
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    public static bool IsUniqueViolation(DbUpdateException exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        Exception? current = exception;

        while (current is not null)
        {
            if (current is SqlException sqlException &&
                sqlException.Number is DuplicateKeyError or DuplicateIndexError)
            {
                return true;
            }

            current = current.InnerException;
        }

        return false;
    }

    /// <summary>
    /// Calcula el siguiente código secuencial de un catálogo (regla 12), a partir de una secuencia de
    /// SQL Server creada por migración. El incremento de la secuencia es atómico ante concurrencia:
    /// dos transacciones simultáneas nunca reciben el mismo número.
    /// </summary>
    /// <param name="dbContext">Contexto de datos activo.</param>
    /// <param name="sequenceSchemaAndName">Nombre completo de la secuencia, p. ej. <c>catalogos.Seq_ClienteCodigo</c>.</param>
    /// <param name="prefix">Prefijo visible del código, p. ej. <c>CLI</c>.</param>
    /// <param name="digits">Cantidad de dígitos con relleno de ceros a la izquierda.</param>
    /// <param name="cancellationToken">Señal de cancelación.</param>
    public static async Task<string> GenerateSequentialCodeAsync(
        DbContext dbContext,
        string sequenceSchemaAndName,
        string prefix,
        int digits,
        CancellationToken cancellationToken)
    {
        long next = await GetNextSequenceValueAsync(dbContext, sequenceSchemaAndName, cancellationToken);
        return $"{prefix}-{next.ToString(CultureInfo.InvariantCulture).PadLeft(digits, '0')}";
    }

    /// <summary>
    /// Obtiene el siguiente valor crudo de una secuencia de SQL Server, sin prefijo ni relleno. Lo usa
    /// <see cref="GenerateSequentialCodeAsync"/> para los códigos de catálogo y
    /// <c>InvoiceService.ConfirmAsync</c> para el número de factura (RF-34), que no lleva prefijo.
    /// </summary>
    public static async Task<long> GetNextSequenceValueAsync(
        DbContext dbContext,
        string sequenceSchemaAndName,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        // SQL Server no permite "NEXT VALUE FOR" dentro de una subconsulta, que es como EF Core
        // ejecutaría SqlQueryRaw/FromSqlRaw al componerlo. Por eso se ejecuta como un comando ADO.NET
        // directo sobre la misma conexión (y transacción, si hay una activa) que usa el DbContext.
        await dbContext.Database.OpenConnectionAsync(cancellationToken);

        try
        {
            using DbCommand command = dbContext.Database.GetDbConnection().CreateCommand();

            // El nombre de la secuencia siempre viene de una constante literal escrita en el código de
            // cada servicio, nunca de datos externos o de entrada del usuario, así que interpolarlo en
            // el texto del SQL (un identificador, no se puede parametrizar) no es una inyección SQL.
            command.CommandText = $"SELECT NEXT VALUE FOR {sequenceSchemaAndName};";
            command.Transaction = dbContext.Database.CurrentTransaction?.GetDbTransaction();

            object? result = await command.ExecuteScalarAsync(cancellationToken);
            return Convert.ToInt64(result, CultureInfo.InvariantCulture);
        }
        finally
        {
            await dbContext.Database.CloseConnectionAsync();
        }
    }

    /// <summary>
    /// Abre una transacción nueva solo si el contexto no está ya dentro de una. La usan los servicios
    /// que combinan varias escrituras (p. ej. confirmar una factura y registrar su entrada en la
    /// bitácora) en una sola operación atómica, sin romper las pruebas que ya envuelven el escenario
    /// completo en su propia transacción de reversión (<c>RunInRollbackTransactionAsync</c>).
    /// </summary>
    /// <returns>La transacción abierta, para que el llamador la confirme; <c>null</c> si ya había una
    /// activa, en cuyo caso el llamador no debe confirmarla ni revertirla: es responsabilidad de quien
    /// la abrió.</returns>
    public static Task<IDbContextTransaction?> BeginTransactionIfNeededAsync(
        DbContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Database.CurrentTransaction is not null)
        {
            return Task.FromResult<IDbContextTransaction?>(null);
        }

        return BeginAsync(context, cancellationToken);

        static async Task<IDbContextTransaction?> BeginAsync(DbContext context, CancellationToken cancellationToken) =>
            await context.Database.BeginTransactionAsync(cancellationToken);
    }

    public static async Task<CatalogOperationResult> SaveChangesAsync(
        DbContext context,
        string duplicateMessage,
        CancellationToken cancellationToken)
    {
        try
        {
            await context.SaveChangesAsync(cancellationToken);
            return CatalogOperationResult.Succeeded();
        }
        catch (DbUpdateConcurrencyException)
        {
            context.ChangeTracker.Clear();
            return CatalogOperationResult.Concurrent();
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            context.ChangeTracker.Clear();
            return CatalogOperationResult.Duplicated(duplicateMessage);
        }
    }
}
