using Microsoft.EntityFrameworkCore;
using SistemaFinanciero.Application.Catalogs.Common;
using SistemaFinanciero.Application.Parameters;
using SistemaFinanciero.Domain.Parameters;
using SistemaFinanciero.Infrastructure.Catalogs;
using SistemaFinanciero.Infrastructure.Persistence;

namespace SistemaFinanciero.Infrastructure.Parameters;

/// <summary>
/// Ejecuta los casos de uso de los parámetros únicos del sistema y conserva su historial de cambios.
/// </summary>
internal sealed class SystemParametersService(
    FinancialDbContext dbContext,
    TimeProvider timeProvider) : ISystemParametersService
{
    public async Task<SystemParametersModel?> GetAsync(CancellationToken cancellationToken = default)
    {
        SystemParameters? parameters = await dbContext.Set<SystemParameters>()
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == SystemParameters.SingletonId, cancellationToken);

        if (parameters is null)
        {
            return null;
        }

        string updatedByUserName = await dbContext.Users
            .AsNoTracking()
            .Where(user => user.Id == parameters.UpdatedByUserId)
            .Select(user => user.FullName)
            .SingleOrDefaultAsync(cancellationToken) ?? string.Empty;

        return Map(parameters, updatedByUserName);
    }

    public async Task<CatalogOperationResult> CreateAsync(
        SaveSystemParametersCommand command,
        Guid actorId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        bool alreadyExists = await dbContext.Set<SystemParameters>()
            .AsNoTracking()
            .AnyAsync(candidate => candidate.Id == SystemParameters.SingletonId, cancellationToken);

        if (alreadyExists)
        {
            return CatalogOperationResult.Duplicated(
                "Los parámetros del sistema ya están configurados; edítelos en su lugar.");
        }

        try
        {
            DateTimeOffset now = timeProvider.GetUtcNow();
            SystemParameters parameters = new(
                command.AuthorizationLimitCrc,
                command.OverdueAlertDays,
                now,
                actorId);

            dbContext.Add(parameters);
            dbContext.Add(new SystemParameterChange(
                Guid.NewGuid(),
                previousAuthorizationLimitCrc: null,
                parameters.AuthorizationLimitCrc,
                previousOverdueAlertDays: null,
                parameters.OverdueAlertDays,
                now,
                actorId));

            return await CatalogPersistence.SaveChangesAsync(
                dbContext,
                "Los parámetros del sistema ya están configurados.",
                cancellationToken);
        }
        catch (ArgumentException exception)
        {
            dbContext.ChangeTracker.Clear();
            return CatalogOperationResult.Invalid(exception.Message);
        }
    }

    public async Task<CatalogOperationResult> UpdateAsync(
        SaveSystemParametersCommand command,
        string version,
        Guid actorId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        SystemParameters? parameters = await dbContext.Set<SystemParameters>()
            .SingleOrDefaultAsync(candidate => candidate.Id == SystemParameters.SingletonId, cancellationToken);

        if (parameters is null)
        {
            return CatalogOperationResult.Missing();
        }

        if (!CatalogPersistence.TrySetOriginalVersion(dbContext, parameters, version))
        {
            return CatalogOperationResult.Invalid("El token de concurrencia no es válido.");
        }

        decimal previousLimit = parameters.AuthorizationLimitCrc;
        int previousDays = parameters.OverdueAlertDays;

        try
        {
            DateTimeOffset now = timeProvider.GetUtcNow();
            parameters.UpdateValues(command.AuthorizationLimitCrc, command.OverdueAlertDays, now, actorId);

            dbContext.Add(new SystemParameterChange(
                Guid.NewGuid(),
                previousLimit,
                parameters.AuthorizationLimitCrc,
                previousDays,
                parameters.OverdueAlertDays,
                now,
                actorId));

            return await CatalogPersistence.SaveChangesAsync(
                dbContext,
                "Los parámetros del sistema ya están configurados.",
                cancellationToken);
        }
        catch (ArgumentException exception)
        {
            dbContext.ChangeTracker.Clear();
            return CatalogOperationResult.Invalid(exception.Message);
        }
    }

    private static SystemParametersModel Map(SystemParameters parameters, string updatedByUserName)
    {
        return new SystemParametersModel(
            parameters.AuthorizationLimitCrc,
            parameters.OverdueAlertDays,
            parameters.CreatedAtUtc,
            parameters.UpdatedAtUtc,
            updatedByUserName,
            CatalogPersistence.EncodeVersion(parameters.RowVersion));
    }
}
