using SistemaFinanciero.Domain.Common;

namespace SistemaFinanciero.Domain.Parameters;

/// <summary>
/// Registro inmutable de un cambio en los datos de la empresa (RF-55): conserva la fotografía
/// completa de los cinco campos en el momento del cambio, quién lo hizo y cuándo. Es una fotografía,
/// no un par anterior/nuevo por campo como <see cref="SystemParameterChange"/>: al ser una tabla de
/// solo anexo, la fila anterior en el historial ya conserva el valor previo.
/// </summary>
/// <remarks>
/// Conexiones: la crea <c>SystemParametersService.UpdateCompanyAsync</c> (Infrastructure); se guarda
/// en <c>finanzas.ParametrosEmpresaHistorial</c> vía <c>CompanyDataChangeConfiguration</c>.
/// <c>FinancialDbContext.EnsureAppendOnlyLogsAreNotMutated</c> impide modificarla o borrarla. No tiene
/// pantalla propia de consulta todavía.
/// </remarks>
public sealed class CompanyDataChange
{
    private CompanyDataChange()
    {
    }

    /// <summary>Crea un registro de cambio. No admite modificaciones posteriores.</summary>
    public CompanyDataChange(
        Guid id,
        string companyName,
        string companyLegalId,
        string? companyAddress,
        string? companyPhone,
        string? companyEmail,
        DateTimeOffset changedAtUtc,
        Guid changedByUserId)
    {
        Id = DomainRules.RequiredId(id, nameof(id));
        CompanyName = DomainRules.RequiredText(companyName, SystemParameters.CompanyNameMaxLength, nameof(companyName));
        CompanyLegalId = DomainRules.RequiredText(
            companyLegalId, SystemParameters.CompanyLegalIdMaxLength, nameof(companyLegalId));
        CompanyAddress = DomainRules.OptionalText(
            companyAddress, SystemParameters.CompanyAddressMaxLength, nameof(companyAddress));
        CompanyPhone = DomainRules.OptionalText(
            companyPhone, SystemParameters.CompanyPhoneMaxLength, nameof(companyPhone));
        CompanyEmail = DomainRules.OptionalText(
            companyEmail, SystemParameters.CompanyEmailMaxLength, nameof(companyEmail));
        ChangedAtUtc = DomainRules.RequiredUtcTimestamp(changedAtUtc, nameof(changedAtUtc));
        ChangedByUserId = DomainRules.RequiredId(changedByUserId, nameof(changedByUserId));
    }

    /// <summary>Obtiene el identificador del registro.</summary>
    public Guid Id { get; private set; }

    /// <summary>Obtiene el nombre de la empresa vigente desde este cambio.</summary>
    public string CompanyName { get; private set; } = string.Empty;

    /// <summary>Obtiene la cédula jurídica vigente desde este cambio.</summary>
    public string CompanyLegalId { get; private set; } = string.Empty;

    /// <summary>Obtiene la dirección vigente desde este cambio.</summary>
    public string? CompanyAddress { get; private set; }

    /// <summary>Obtiene el teléfono vigente desde este cambio.</summary>
    public string? CompanyPhone { get; private set; }

    /// <summary>Obtiene el correo vigente desde este cambio.</summary>
    public string? CompanyEmail { get; private set; }

    /// <summary>Obtiene el instante UTC del cambio.</summary>
    public DateTimeOffset ChangedAtUtc { get; private set; }

    /// <summary>Obtiene el usuario de Gerencia que realizó el cambio.</summary>
    public Guid ChangedByUserId { get; private set; }
}
