using SistemaFinanciero.Domain.Common;
using SistemaFinanciero.Domain.Currencies;

namespace SistemaFinanciero.Domain.Catalogs;

/// <summary>
/// Representa una caja o cuenta bancaria utilizada para recibir y entregar dinero.
/// </summary>
/// <remarks>
/// El tipo y la moneda son inmutables. El saldo no se almacena en el catálogo: deberá obtenerse de
/// los movimientos confirmados para evitar inconsistencias.
/// </remarks>
public sealed class FinancialAccount
{
    /// <summary>Longitud máxima del código visible.</summary>
    public const int CodeMaxLength = 30;

    /// <summary>Longitud máxima del nombre.</summary>
    public const int NameMaxLength = 120;

    /// <summary>Longitud máxima de la referencia bancaria o administrativa.</summary>
    public const int ReferenceMaxLength = 100;

    private FinancialAccount()
    {
    }

    /// <summary>Crea una cuenta financiera activa con tipo y moneda fijos.</summary>
    public FinancialAccount(
        Guid id,
        string code,
        string name,
        FinancialAccountType type,
        CurrencyCode currency,
        string? reference,
        DateTimeOffset createdAtUtc,
        Guid createdByUserId)
    {
        Id = DomainRules.RequiredId(id, nameof(id));
        Code = DomainRules.NormalizedCode(code, CodeMaxLength, nameof(code));
        Name = DomainRules.RequiredText(name, NameMaxLength, nameof(name));
        Type = DomainRules.DefinedEnum(type, nameof(type));
        Currency = DomainRules.DefinedEnum(currency, nameof(currency));
        Reference = DomainRules.OptionalText(reference, ReferenceMaxLength, nameof(reference));
        CreatedAtUtc = DomainRules.RequiredUtcTimestamp(createdAtUtc, nameof(createdAtUtc));
        CreatedByUserId = DomainRules.RequiredId(createdByUserId, nameof(createdByUserId));
        UpdatedAtUtc = CreatedAtUtc;
        UpdatedByUserId = CreatedByUserId;
        IsActive = true;
    }

    /// <summary>Obtiene el identificador técnico estable.</summary>
    public Guid Id { get; private set; }

    /// <summary>Obtiene el código visible, almacenado en mayúsculas.</summary>
    public string Code { get; private set; } = string.Empty;

    /// <summary>Obtiene el nombre de la cuenta.</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>Obtiene el tipo inmutable de caja o banco.</summary>
    public FinancialAccountType Type { get; private set; }

    /// <summary>Obtiene la moneda inmutable de la cuenta.</summary>
    public CurrencyCode Currency { get; private set; }

    /// <summary>Obtiene la referencia bancaria o administrativa opcional.</summary>
    public string? Reference { get; private set; }

    /// <summary>Indica si puede seleccionarse en operaciones nuevas.</summary>
    public bool IsActive { get; private set; }

    /// <summary>Obtiene el instante UTC de creación.</summary>
    public DateTimeOffset CreatedAtUtc { get; private set; }

    /// <summary>Obtiene el usuario que creó el registro.</summary>
    public Guid CreatedByUserId { get; private set; }

    /// <summary>Obtiene el instante UTC del último cambio.</summary>
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    /// <summary>Obtiene el usuario que realizó el último cambio.</summary>
    public Guid UpdatedByUserId { get; private set; }

    /// <summary>Obtiene el token de concurrencia administrado por la persistencia.</summary>
    public byte[] RowVersion { get; private set; } = [];

    /// <summary>Actualiza código, nombre y referencia sin alterar tipo o moneda.</summary>
    public void UpdateDetails(
        string code,
        string name,
        string? reference,
        DateTimeOffset updatedAtUtc,
        Guid updatedByUserId)
    {
        string normalizedCode = DomainRules.NormalizedCode(code, CodeMaxLength, nameof(code));
        string normalizedName = DomainRules.RequiredText(name, NameMaxLength, nameof(name));
        string? normalizedReference = DomainRules.OptionalText(
            reference,
            ReferenceMaxLength,
            nameof(reference));
        DateTimeOffset normalizedUpdatedAtUtc = ValidateUpdateAudit(updatedAtUtc, updatedByUserId);

        Code = normalizedCode;
        Name = normalizedName;
        Reference = normalizedReference;
        Touch(normalizedUpdatedAtUtc, updatedByUserId);
    }

    /// <summary>Habilita la cuenta para nuevas operaciones.</summary>
    public void Activate(DateTimeOffset updatedAtUtc, Guid updatedByUserId)
    {
        SetActive(true, updatedAtUtc, updatedByUserId);
    }

    /// <summary>Deshabilita la cuenta sin eliminar sus referencias históricas.</summary>
    public void Deactivate(DateTimeOffset updatedAtUtc, Guid updatedByUserId)
    {
        SetActive(false, updatedAtUtc, updatedByUserId);
    }

    private void SetActive(bool isActive, DateTimeOffset updatedAtUtc, Guid updatedByUserId)
    {
        if (IsActive == isActive)
        {
            return;
        }

        DateTimeOffset normalizedUpdatedAtUtc = ValidateUpdateAudit(updatedAtUtc, updatedByUserId);
        IsActive = isActive;
        Touch(normalizedUpdatedAtUtc, updatedByUserId);
    }

    private DateTimeOffset ValidateUpdateAudit(
        DateTimeOffset updatedAtUtc,
        Guid updatedByUserId)
    {
        DomainRules.RequiredId(updatedByUserId, nameof(updatedByUserId));
        return DomainRules.RequiredUtcTimestamp(
            updatedAtUtc,
            nameof(updatedAtUtc),
            UpdatedAtUtc);
    }

    private void Touch(DateTimeOffset updatedAtUtc, Guid updatedByUserId)
    {
        UpdatedAtUtc = updatedAtUtc;
        UpdatedByUserId = updatedByUserId;
    }
}
