using SistemaFinanciero.Domain.Common;
using SistemaFinanciero.Domain.Currencies;

namespace SistemaFinanciero.Domain.Catalogs;

/// <summary>
/// Cuenta del catálogo contable, con jerarquía opcional. Las cajas y los bancos son cuentas de Activo
/// «de efectivo», con moneda fija.
/// </summary>
/// <remarks>
/// Es solo clasificación, no partida doble. El tipo, el subtipo de efectivo y la moneda son
/// inmutables, y el saldo no se almacena: se calculará desde los movimientos vigentes. La validación del
/// tipo de la cuenta superior y de los ciclos requiere consultar la jerarquía y corresponde a
/// Application.
/// </remarks>
public sealed class LedgerAccount
{
    /// <summary>Longitud máxima del código visible.</summary>
    public const int CodeMaxLength = 30;

    /// <summary>Longitud máxima del nombre.</summary>
    public const int NameMaxLength = 120;

    /// <summary>Longitud máxima de la referencia bancaria o administrativa.</summary>
    public const int ReferenceMaxLength = 100;

    /// <summary>Longitud máxima de la descripción.</summary>
    public const int DescriptionMaxLength = 300;

    private LedgerAccount()
    {
    }

    /// <summary>
    /// Crea una cuenta contable activa. Una cuenta de efectivo indica su subtipo y su moneda, y es de
    /// tipo Activo; las demás no tienen ni subtipo ni moneda.
    /// </summary>
    public LedgerAccount(
        Guid id,
        string code,
        string name,
        LedgerAccountType type,
        Guid? parentId,
        CashAccountKind? cashKind,
        CurrencyCode? currency,
        string? reference,
        string? description,
        DateTimeOffset createdAtUtc,
        Guid createdByUserId)
    {
        Id = DomainRules.RequiredId(id, nameof(id));
        Code = DomainRules.NormalizedCode(code, CodeMaxLength, nameof(code));
        Name = DomainRules.RequiredText(name, NameMaxLength, nameof(name));
        Type = DomainRules.DefinedEnum(type, nameof(type));
        ParentId = ValidateParent(parentId);
        (CashKind, Currency) = ValidateCash(Type, cashKind, currency);
        Reference = DomainRules.OptionalText(reference, ReferenceMaxLength, nameof(reference));
        Description = DomainRules.OptionalText(description, DescriptionMaxLength, nameof(description));
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

    /// <summary>Obtiene el tipo inmutable: Activo, Pasivo, Patrimonio, Ingreso o Gasto.</summary>
    public LedgerAccountType Type { get; private set; }

    /// <summary>Obtiene la cuenta superior, si la cuenta no es raíz.</summary>
    public Guid? ParentId { get; private set; }

    /// <summary>
    /// Obtiene el subtipo inmutable Caja o Banco cuando la cuenta es de efectivo; nulo en las demás.
    /// </summary>
    public CashAccountKind? CashKind { get; private set; }

    /// <summary>Obtiene la moneda inmutable de una cuenta de efectivo; nula en las demás.</summary>
    public CurrencyCode? Currency { get; private set; }

    /// <summary>Indica si es una cuenta de efectivo, que puede recibir o entregar dinero.</summary>
    public bool IsCash => CashKind is not null;

    /// <summary>Obtiene la referencia bancaria o administrativa opcional.</summary>
    public string? Reference { get; private set; }

    /// <summary>Obtiene la descripción opcional.</summary>
    public string? Description { get; private set; }

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

    /// <summary>Actualiza código, nombre, referencia y descripción sin alterar tipo, subtipo ni moneda.</summary>
    public void UpdateDetails(
        string code,
        string name,
        string? reference,
        string? description,
        DateTimeOffset updatedAtUtc,
        Guid updatedByUserId)
    {
        string normalizedCode = DomainRules.NormalizedCode(code, CodeMaxLength, nameof(code));
        string normalizedName = DomainRules.RequiredText(name, NameMaxLength, nameof(name));
        string? normalizedReference = DomainRules.OptionalText(
            reference,
            ReferenceMaxLength,
            nameof(reference));
        string? normalizedDescription = DomainRules.OptionalText(
            description,
            DescriptionMaxLength,
            nameof(description));
        DateTimeOffset normalizedUpdatedAtUtc = ValidateUpdateAudit(updatedAtUtc, updatedByUserId);

        Code = normalizedCode;
        Name = normalizedName;
        Reference = normalizedReference;
        Description = normalizedDescription;
        Touch(normalizedUpdatedAtUtc, updatedByUserId);
    }

    /// <summary>
    /// Cambia la cuenta superior después de que Application haya validado tipo y ausencia de ciclos.
    /// </summary>
    public void ChangeParent(
        Guid? parentId,
        DateTimeOffset updatedAtUtc,
        Guid updatedByUserId)
    {
        Guid? normalizedParentId = ValidateParent(parentId);

        if (ParentId == normalizedParentId)
        {
            return;
        }

        DateTimeOffset normalizedUpdatedAtUtc = ValidateUpdateAudit(updatedAtUtc, updatedByUserId);
        ParentId = normalizedParentId;
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

    private Guid? ValidateParent(Guid? parentId)
    {
        Guid? normalizedParentId = DomainRules.OptionalId(parentId, nameof(parentId));

        if (normalizedParentId == Id)
        {
            throw new ArgumentException("Una cuenta no puede ser su propia cuenta superior.", nameof(parentId));
        }

        return normalizedParentId;
    }

    private static (CashAccountKind? CashKind, CurrencyCode? Currency) ValidateCash(
        LedgerAccountType type,
        CashAccountKind? cashKind,
        CurrencyCode? currency)
    {
        if (cashKind is null)
        {
            return currency is null
                ? (null, null)
                : throw new ArgumentException(
                    "Una cuenta que no es de efectivo no tiene moneda.",
                    nameof(currency));
        }

        CashAccountKind validKind = DomainRules.DefinedEnum(cashKind.Value, nameof(cashKind));

        if (type != LedgerAccountType.Asset)
        {
            throw new ArgumentException(
                "Solo una cuenta de tipo Activo puede ser de efectivo.",
                nameof(cashKind));
        }

        if (currency is null)
        {
            throw new ArgumentException(
                "Una cuenta de efectivo requiere una moneda.",
                nameof(currency));
        }

        return (validKind, DomainRules.DefinedEnum(currency.Value, nameof(currency)));
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
