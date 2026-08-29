using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SistemaFinanciero.Application.Catalogs.Common;

namespace SistemaFinanciero.Infrastructure.Catalogs;

/// <summary>
/// Operaciones técnicas compartidas por los servicios de catálogo.
/// </summary>
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
