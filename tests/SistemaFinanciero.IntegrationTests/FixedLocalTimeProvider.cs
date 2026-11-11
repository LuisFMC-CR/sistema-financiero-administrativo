namespace SistemaFinanciero.IntegrationTests;

/// <summary>
/// Reloj fijo para las pruebas (paquete v9, bloque 1): devuelve siempre el mismo instante UTC y
/// convierte a hora local con la zona indicada. Cumple el papel de <c>FakeTimeProvider</c> sin agregar
/// el paquete NuGet <c>Microsoft.Extensions.TimeProvider.Testing</c>.
/// </summary>
public sealed class FixedLocalTimeProvider(DateTimeOffset utcNow, TimeZoneInfo localTimeZone) : TimeProvider
{
    /// <inheritdoc />
    public override DateTimeOffset GetUtcNow() => utcNow;

    /// <inheritdoc />
    public override TimeZoneInfo LocalTimeZone => localTimeZone;

    /// <summary>Zona horaria de Costa Rica (UTC−6, sin horario de verano).</summary>
    public static TimeZoneInfo CostaRicaTimeZone
    {
        get
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById("America/Costa_Rica");
            }
            catch (TimeZoneNotFoundException)
            {
                return TimeZoneInfo.FindSystemTimeZoneById("Central America Standard Time");
            }
        }
    }

    /// <summary>
    /// Crea un reloj que marca la hora local indicada en Costa Rica.
    /// </summary>
    public static FixedLocalTimeProvider AtCostaRicaLocalTime(DateTime localDateTime)
    {
        TimeZoneInfo zone = CostaRicaTimeZone;
        DateTime unspecified = DateTime.SpecifyKind(localDateTime, DateTimeKind.Unspecified);
        DateTimeOffset utc = new(TimeZoneInfo.ConvertTimeToUtc(unspecified, zone), TimeSpan.Zero);
        return new FixedLocalTimeProvider(utc, zone);
    }
}
