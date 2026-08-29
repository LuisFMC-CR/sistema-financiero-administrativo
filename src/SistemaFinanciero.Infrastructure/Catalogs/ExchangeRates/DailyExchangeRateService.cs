using System.Globalization;
using Microsoft.EntityFrameworkCore;
using SistemaFinanciero.Application.Catalogs.Common;
using SistemaFinanciero.Application.Catalogs.ExchangeRates;
using SistemaFinanciero.Domain.Currencies;
using SistemaFinanciero.Infrastructure.Persistence;

namespace SistemaFinanciero.Infrastructure.Catalogs.ExchangeRates;

/// <summary>
/// Ejecuta los casos de uso de la tasa administrativa diaria CRC por USD.
/// </summary>
internal sealed class DailyExchangeRateService(
    FinancialDbContext dbContext,
    TimeProvider timeProvider) : IDailyExchangeRateService
{
    private static readonly CultureInfo CostaRicanCulture = CultureInfo.GetCultureInfo("es-CR");

    public async Task<PagedResult<DailyExchangeRateModel>> SearchAsync(
        PageQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        IQueryable<DailyExchangeRate> rates = dbContext.Set<DailyExchangeRate>().AsNoTracking();

        if (query.Search is not null)
        {
            string search = query.Search;
            bool hasDate = TryParseDate(search, out DateOnly effectiveDate);

            rates = rates.Where(rate =>
                (hasDate && rate.EffectiveDate == effectiveDate) ||
                rate.Source.Contains(search) ||
                (rate.Notes != null && rate.Notes.Contains(search)));
        }

        PagedResult<DailyExchangeRate> page = await CatalogPersistence.ToPageAsync(
            rates.OrderByDescending(rate => rate.EffectiveDate),
            query.Page,
            query.PageSize,
            cancellationToken);

        return new PagedResult<DailyExchangeRateModel>(
            page.Items.Select(Map).ToArray(),
            page.Page,
            page.PageSize,
            page.TotalCount);
    }

    public async Task<DailyExchangeRateModel?> GetAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        DailyExchangeRate? rate = await dbContext.Set<DailyExchangeRate>()
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);

        return rate is null ? null : Map(rate);
    }

    public async Task<CatalogOperationResult> CreateAsync(
        SaveDailyExchangeRateCommand command,
        Guid actorId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        try
        {
            DailyExchangeRate rate = new(
                Guid.NewGuid(),
                command.EffectiveDate,
                new ExchangeRate(command.CrcPerUsd),
                command.Source,
                command.Notes,
                timeProvider.GetUtcNow(),
                actorId);

            dbContext.Add(rate);

            return await CatalogPersistence.SaveChangesAsync(
                dbContext,
                "Ya existe un tipo de cambio para la fecha indicada.",
                cancellationToken);
        }
        catch (ArgumentException exception)
        {
            dbContext.ChangeTracker.Clear();
            return CatalogOperationResult.Invalid(exception.Message);
        }
    }

    public async Task<CatalogOperationResult> UpdateAsync(
        Guid id,
        SaveDailyExchangeRateCommand command,
        string version,
        Guid actorId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        DailyExchangeRate? rate = await dbContext.Set<DailyExchangeRate>()
            .SingleOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);

        if (rate is null)
        {
            return CatalogOperationResult.Missing();
        }

        if (rate.EffectiveDate != command.EffectiveDate)
        {
            return CatalogOperationResult.Invalid(
                "La fecha de un tipo de cambio existente no puede modificarse.");
        }

        if (!CatalogPersistence.TrySetOriginalVersion(dbContext, rate, version))
        {
            return CatalogOperationResult.Invalid("El token de concurrencia no es válido.");
        }

        try
        {
            rate.Correct(
                new ExchangeRate(command.CrcPerUsd),
                command.Source,
                command.Notes,
                timeProvider.GetUtcNow(),
                actorId);

            return await CatalogPersistence.SaveChangesAsync(
                dbContext,
                "Ya existe un tipo de cambio para la fecha indicada.",
                cancellationToken);
        }
        catch (ArgumentException exception)
        {
            dbContext.ChangeTracker.Clear();
            return CatalogOperationResult.Invalid(exception.Message);
        }
    }

    private static bool TryParseDate(string value, out DateOnly date)
    {
        return DateOnly.TryParse(value, CostaRicanCulture, DateTimeStyles.None, out date) ||
            DateOnly.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
    }

    private static DailyExchangeRateModel Map(DailyExchangeRate rate)
    {
        return new DailyExchangeRateModel(
            rate.Id,
            rate.EffectiveDate,
            rate.Rate.CrcPerUsd,
            rate.Source,
            rate.Notes,
            rate.CreatedAtUtc,
            rate.UpdatedAtUtc,
            CatalogPersistence.EncodeVersion(rate.RowVersion));
    }
}
