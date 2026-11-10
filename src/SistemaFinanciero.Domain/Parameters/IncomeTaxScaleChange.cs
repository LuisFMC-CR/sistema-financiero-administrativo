using SistemaFinanciero.Domain.Common;
using SistemaFinanciero.Domain.IncomeTax;

namespace SistemaFinanciero.Domain.Parameters;

/// <summary>
/// Registro inmutable de un cambio en la escala del impuesto sobre la renta (RF-99): fotografía de los
/// nueve valores vigentes después del cambio, quién lo hizo y cuándo. Igual criterio que
/// <see cref="CompanyDataChange"/>: la fila anterior del historial conserva los valores previos.
/// </summary>
/// <remarks>
/// Conexiones: la crea <c>IncomeTaxService.UpdateScaleAsync</c> (Infrastructure); se guarda en
/// <c>finanzas.EscalaRentaHistorial</c>. <c>FinancialDbContext.EnsureAppendOnlyLogsAreNotMutated</c>
/// impide modificarla o borrarla.
/// </remarks>
public sealed class IncomeTaxScaleChange
{
    private IncomeTaxScaleChange()
    {
    }

    /// <summary>Crea un registro de cambio con la escala nueva.</summary>
    public IncomeTaxScaleChange(Guid id, IncomeTaxScale scale, DateTimeOffset changedAtUtc, Guid changedByUserId)
    {
        ArgumentNullException.ThrowIfNull(scale);

        Id = DomainRules.RequiredId(id, nameof(id));
        Bracket1UpperLimit = scale.Bracket1UpperLimit;
        Bracket2UpperLimit = scale.Bracket2UpperLimit;
        Bracket3UpperLimit = scale.Bracket3UpperLimit;
        Bracket1Rate = scale.Bracket1Rate;
        Bracket2Rate = scale.Bracket2Rate;
        Bracket3Rate = scale.Bracket3Rate;
        Bracket4Rate = scale.Bracket4Rate;
        GrossIncomeThreshold = scale.GrossIncomeThreshold;
        GeneralRate = scale.GeneralRate;
        ChangedAtUtc = DomainRules.RequiredUtcTimestamp(changedAtUtc, nameof(changedAtUtc));
        ChangedByUserId = DomainRules.RequiredId(changedByUserId, nameof(changedByUserId));
    }

    /// <summary>Identificador del registro.</summary>
    public Guid Id { get; private set; }

    /// <summary>Límite del primer tramo.</summary>
    public decimal Bracket1UpperLimit { get; private set; }

    /// <summary>Límite del segundo tramo.</summary>
    public decimal Bracket2UpperLimit { get; private set; }

    /// <summary>Límite del tercer tramo.</summary>
    public decimal Bracket3UpperLimit { get; private set; }

    /// <summary>Porcentaje del primer tramo.</summary>
    public decimal Bracket1Rate { get; private set; }

    /// <summary>Porcentaje del segundo tramo.</summary>
    public decimal Bracket2Rate { get; private set; }

    /// <summary>Porcentaje del tercer tramo.</summary>
    public decimal Bracket3Rate { get; private set; }

    /// <summary>Porcentaje sobre el exceso del tercer límite.</summary>
    public decimal Bracket4Rate { get; private set; }

    /// <summary>Umbral de renta bruta para la tarifa general.</summary>
    public decimal GrossIncomeThreshold { get; private set; }

    /// <summary>Tarifa general.</summary>
    public decimal GeneralRate { get; private set; }

    /// <summary>Instante UTC del cambio.</summary>
    public DateTimeOffset ChangedAtUtc { get; private set; }

    /// <summary>Usuario de Gerencia que hizo el cambio.</summary>
    public Guid ChangedByUserId { get; private set; }
}
