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
/// <remarks>
/// Conexiones: implementa <c>ISystemParametersService</c> (Application); se registra en
/// <c>DependencyInjection.AddInfrastructure</c>; lo inyecta <c>SystemParametersController</c> (Web, ruta
/// <c>/parametros</c>). Lee y escribe <c>FinancialDbContext.SystemParameters</c> (tabla
/// <c>finanzas.ParametrosSistema</c>) y agrega filas a los historiales de parámetros, período fiscal
/// y datos de empresa cuando corresponde.
/// </remarks>
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

    public async Task<CatalogOperationResult> UpdateFiscalPeriodAsync(
        DateOnly startDate,
        DateOnly endDate,
        string? reason,
        string version,
        Guid actorId,
        CancellationToken cancellationToken = default)
    {
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

        DateOnly? previousStartDate = parameters.FiscalPeriodStartDate;
        DateOnly? previousEndDate = parameters.FiscalPeriodEndDate;

        if (previousStartDate == startDate && previousEndDate == endDate)
        {
            return CatalogOperationResult.Succeeded();
        }

        try
        {
            DateTimeOffset now = timeProvider.GetUtcNow();
            parameters.UpdateFiscalPeriod(startDate, endDate, now, actorId);
            dbContext.Add(new FiscalPeriodChange(
                previousStartDate,
                previousEndDate,
                startDate,
                endDate,
                reason,
                now,
                actorId));

            return await CatalogPersistence.SaveChangesAsync(
                dbContext,
                "El período fiscal se actualizó correctamente.",
                cancellationToken);
        }
        catch (ArgumentException exception)
        {
            dbContext.ChangeTracker.Clear();
            return CatalogOperationResult.Invalid(exception.Message);
        }
    }

    public async Task<IReadOnlyList<ReopenedAccountingPeriodModel>> GetReopenedAccountingPeriodsAsync(
        CancellationToken cancellationToken = default)
    {
        var periods = await (
            from period in dbContext.AccountingPeriodOverrides.AsNoTracking()
            join user in dbContext.Users.AsNoTracking()
                on period.UpdatedByUserId equals user.Id
            where period.IsOpen
            orderby period.Year descending, period.Month descending
            select new
            {
                period.Year,
                period.Month,
                period.Reason,
                period.UpdatedAtUtc,
                UserName = user.FullName,
                period.RowVersion,
            }).ToListAsync(cancellationToken);

        return periods
            .Select(period => new ReopenedAccountingPeriodModel(
                period.Year,
                period.Month,
                period.Reason!,
                period.UpdatedAtUtc,
                period.UserName,
                CatalogPersistence.EncodeVersion(period.RowVersion)))
            .ToArray();
    }

    public async Task<CatalogOperationResult> ReopenAccountingPeriodAsync(
        int year,
        int month,
        string reason,
        Guid actorId,
        CancellationToken cancellationToken = default)
    {
        DateOnly today = DateOnly.FromDateTime(timeProvider.GetLocalNow().DateTime);
        if (year > today.Year || year == today.Year && month >= today.Month)
        {
            return CatalogOperationResult.Invalid("Solo se puede reabrir un mes que ya cerró automáticamente.");
        }

        AccountingPeriodOverride? period = await dbContext.AccountingPeriodOverrides
            .SingleOrDefaultAsync(
                candidate => candidate.Year == year && candidate.Month == month,
                cancellationToken);

        bool wasOpen = period?.IsOpen ?? false;
        DateTimeOffset now = timeProvider.GetUtcNow();

        try
        {
            if (period is null)
            {
                period = new AccountingPeriodOverride(year, month, reason, now, actorId);
                dbContext.Add(period);
            }
            else
            {
                if (period.IsOpen)
                {
                    return CatalogOperationResult.Duplicated("El período ya está abierto.");
                }

                period.Reopen(reason, now, actorId);
            }

            dbContext.Add(new AccountingPeriodChange(
                year, month, wasOpen, isOpen: true, reason, now, actorId));

            return await CatalogPersistence.SaveChangesAsync(
                dbContext,
                "El período ya está abierto.",
                cancellationToken);
        }
        catch (ArgumentException exception)
        {
            dbContext.ChangeTracker.Clear();
            return CatalogOperationResult.Invalid(exception.Message);
        }
    }

    public async Task<CatalogOperationResult> CloseAccountingPeriodAsync(
        int year,
        int month,
        string version,
        Guid actorId,
        CancellationToken cancellationToken = default)
    {
        DateOnly today = DateOnly.FromDateTime(timeProvider.GetLocalNow().DateTime);
        if (year > today.Year || year == today.Year && month >= today.Month)
        {
            return CatalogOperationResult.Invalid(
                "Solo se puede cerrar manualmente un mes que ya cerró automáticamente.");
        }

        AccountingPeriodOverride? period = await dbContext.AccountingPeriodOverrides
            .SingleOrDefaultAsync(
                candidate => candidate.Year == year && candidate.Month == month,
                cancellationToken);
        if (period is null || !period.IsOpen)
        {
            return CatalogOperationResult.Missing();
        }

        if (!CatalogPersistence.TrySetOriginalVersion(dbContext, period, version))
        {
            return CatalogOperationResult.Invalid("El token de concurrencia no es válido.");
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        try
        {
            period.Close(now, actorId);
            dbContext.Add(new AccountingPeriodChange(
                year, month, wasOpen: true, isOpen: false, reason: null, now, actorId));

            return await CatalogPersistence.SaveChangesAsync(
                dbContext,
                "El período ya está cerrado.",
                cancellationToken);
        }
        catch (ArgumentException exception)
        {
            dbContext.ChangeTracker.Clear();
            return CatalogOperationResult.Invalid(exception.Message);
        }
    }

    public async Task<CatalogOperationResult> UpdateCompanyAsync(
        SaveCompanyDataCommand command,
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

        try
        {
            DateTimeOffset now = timeProvider.GetUtcNow();
            parameters.UpdateCompany(
                command.Name,
                command.LegalId,
                command.Address,
                command.Phone,
                command.Email,
                command.ActivityCode,
                command.TradeName,
                command.ProvinceCode,
                command.CantonCode,
                command.DistrictCode,
                command.BranchCode,
                command.TerminalCode,
                now,
                actorId);

            dbContext.Add(new CompanyDataChange(
                Guid.NewGuid(),
                parameters.CompanyName!,
                parameters.CompanyLegalId!,
                parameters.CompanyAddress,
                parameters.CompanyPhone,
                parameters.CompanyEmail,
                parameters.CompanyActivityCode,
                parameters.CompanyTradeName,
                parameters.CompanyProvinceCode,
                parameters.CompanyCantonCode,
                parameters.CompanyDistrictCode,
                parameters.CompanyBranchCode,
                parameters.CompanyTerminalCode,
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
            parameters.ClosingDate,
            parameters.CompanyName,
            parameters.CompanyLegalId,
            parameters.CompanyAddress,
            parameters.CompanyPhone,
            parameters.CompanyEmail,
            parameters.CompanyActivityCode,
            parameters.CompanyTradeName,
            parameters.CompanyProvinceCode,
            parameters.CompanyCantonCode,
            parameters.CompanyDistrictCode,
            parameters.CompanyBranchCode,
            parameters.CompanyTerminalCode,
            parameters.CreatedAtUtc,
            parameters.UpdatedAtUtc,
            updatedByUserName,
            CatalogPersistence.EncodeVersion(parameters.RowVersion),
            parameters.FiscalPeriodStartDate,
            parameters.FiscalPeriodEndDate,
            new SistemaFinanciero.Application.IncomeTax.IncomeTaxScaleModel(
                parameters.IncomeTaxBracket1UpperLimit,
                parameters.IncomeTaxBracket2UpperLimit,
                parameters.IncomeTaxBracket3UpperLimit,
                parameters.IncomeTaxBracket1Rate,
                parameters.IncomeTaxBracket2Rate,
                parameters.IncomeTaxBracket3Rate,
                parameters.IncomeTaxBracket4Rate,
                parameters.IncomeTaxGrossIncomeThreshold,
                parameters.IncomeTaxGeneralRate,
                CatalogPersistence.EncodeVersion(parameters.RowVersion)));
    }
}
