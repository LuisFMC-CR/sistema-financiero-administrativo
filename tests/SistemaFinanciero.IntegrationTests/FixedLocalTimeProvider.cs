namespace SistemaFinanciero.IntegrationTests;

/// <summary>
/// Reloj fijo para las pruebas (paquete v9, bloques 1, 8 y 9): devuelve el instante UTC indicado y
/// convierte a hora local con la zona indicada; la prueba puede moverlo con <see cref="SetLocalTime"/>.
/// Cumple el papel de <c>FakeTimeProvider</c> sin agregar el paquete NuGet
/// <c>Microsoft.Extensions.TimeProvider.Testing</c>.
/// </summary>
public sealed class FixedLocalTimeProvider(DateTimeOffset utcNow, TimeZoneInfo localTimeZone) : TimeProvider
{
    private DateTimeOffset current = utcNow;

    /// <inheritdoc />
    public override DateTimeOffset GetUtcNow() => current;

    /// <summary>Mueve el reloj a la hora local indicada (en la zona del reloj).</summary>
    public void SetLocalTime(DateTime localDateTime)
    {
        DateTime unspecified = DateTime.SpecifyKind(localDateTime, DateTimeKind.Unspecified);
        current = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(unspecified, localTimeZone), TimeSpan.Zero);
    }

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
