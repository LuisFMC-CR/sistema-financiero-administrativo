using SistemaFinanciero.Domain.Common;

namespace SistemaFinanciero.Domain.Catalogs;

/// <summary>
/// Clasifica administrativamente ingresos o gastos y puede formar una jerarquía.
/// </summary>
/// <remarks>
/// La naturaleza de la categoría no cambia después de su creación. La validación del tipo del padre
/// y la detección de ciclos requieren consultar la jerarquía completa y corresponden a Application.
/// </remarks>
public sealed class FinancialCategory
{
    /// <summary>Longitud máxima del código visible.</summary>
    public const int CodeMaxLength = 30;

    /// <summary>Longitud máxima del nombre.</summary>
    public const int NameMaxLength = 120;

    /// <summary>Longitud máxima de la descripción.</summary>
    public const int DescriptionMaxLength = 300;

    private FinancialCategory()
    {
    }

    /// <summary>
    /// Crea una categoría financiera activa. Toda categoría apunta a una cuenta contable; que sea del
    /// mismo tipo la valida Application, porque requiere consultar la cuenta.
    /// </summary>
    public FinancialCategory(
        Guid id,
        string code,
        string name,
        FinancialCategoryKind kind,
        Guid? parentId,
        Guid ledgerAccountId,
        string? description,
        DateTimeOffset createdAtUtc,
        Guid createdByUserId)
    {
        Id = DomainRules.RequiredId(id, nameof(id));
        Code = DomainRules.NormalizedCode(code, CodeMaxLength, nameof(code));
        Name = DomainRules.RequiredText(name, NameMaxLength, nameof(name));
        Kind = DomainRules.DefinedEnum(kind, nameof(kind));
        ParentId = ValidateParent(parentId);
        LedgerAccountId = DomainRules.RequiredId(ledgerAccountId, nameof(ledgerAccountId));
        Description = DomainRules.OptionalText(
            description,
            DescriptionMaxLength,
            nameof(description));
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

    /// <summary>Obtiene el nombre de la categoría.</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>Obtiene la naturaleza inmutable de ingreso o gasto.</summary>
    public FinancialCategoryKind Kind { get; private set; }

    /// <summary>Obtiene la categoría superior, si la categoría no es raíz.</summary>
    public Guid? ParentId { get; private set; }

    /// <summary>
    /// Obtiene la cuenta contable de la categoría, del mismo tipo que su naturaleza: una categoría de
    /// ingreso apunta a una cuenta de Ingreso y una de gasto, a una de Gasto.
    /// </summary>
    public Guid LedgerAccountId { get; private set; }

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

    /// <summary>Actualiza código, nombre y descripción sin alterar la naturaleza.</summary>
    public void UpdateDetails(
        string code,
        string name,
        string? description,
        DateTimeOffset updatedAtUtc,
        Guid updatedByUserId)
    {
        string normalizedCode = DomainRules.NormalizedCode(code, CodeMaxLength, nameof(code));
        string normalizedName = DomainRules.RequiredText(name, NameMaxLength, nameof(name));
        string? normalizedDescription = DomainRules.OptionalText(
            description,
            DescriptionMaxLength,
            nameof(description));
        DateTimeOffset normalizedUpdatedAtUtc = ValidateUpdateAudit(updatedAtUtc, updatedByUserId);

        Code = normalizedCode;
        Name = normalizedName;
        Description = normalizedDescription;
        Touch(normalizedUpdatedAtUtc, updatedByUserId);
    }

    /// <summary>
    /// Cambia la categoría superior después de que Application haya validado tipo y ausencia de ciclos.
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

    /// <summary>
    /// Cambia la cuenta contable después de que Application haya validado que existe, está activa y es
    /// del tipo que corresponde a la naturaleza de la categoría.
    /// </summary>
    public void ChangeLedgerAccount(
        Guid ledgerAccountId,
        DateTimeOffset updatedAtUtc,
        Guid updatedByUserId)
    {
        Guid normalizedLedgerAccountId = DomainRules.RequiredId(ledgerAccountId, nameof(ledgerAccountId));

        if (LedgerAccountId == normalizedLedgerAccountId)
        {
            return;
        }

        DateTimeOffset normalizedUpdatedAtUtc = ValidateUpdateAudit(updatedAtUtc, updatedByUserId);
        LedgerAccountId = normalizedLedgerAccountId;
        Touch(normalizedUpdatedAtUtc, updatedByUserId);
    }

    /// <summary>Habilita la categoría para nuevas operaciones.</summary>
    public void Activate(DateTimeOffset updatedAtUtc, Guid updatedByUserId)
    {
        SetActive(true, updatedAtUtc, updatedByUserId);
    }

    /// <summary>Deshabilita la categoría sin eliminar sus referencias históricas.</summary>
    public void Deactivate(DateTimeOffset updatedAtUtc, Guid updatedByUserId)
    {
        SetActive(false, updatedAtUtc, updatedByUserId);
    }

    private Guid? ValidateParent(Guid? parentId)
    {
        Guid? normalizedParentId = DomainRules.OptionalId(parentId, nameof(parentId));

        if (normalizedParentId == Id)
        {
            throw new ArgumentException("Una categoría no puede ser su propio padre.", nameof(parentId));
        }

        return normalizedParentId;
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
