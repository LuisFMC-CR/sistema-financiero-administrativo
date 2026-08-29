namespace SistemaFinanciero.Application.Catalogs.Common;

/// <summary>
/// Clasifica el resultado esperado de una modificación de catálogo sin exponer detalles técnicos.
/// </summary>
public enum CatalogOperationStatus
{
    /// <summary>La operación se confirmó.</summary>
    Success,

    /// <summary>El registro solicitado no existe.</summary>
    NotFound,

    /// <summary>La entrada incumple una regla de negocio.</summary>
    Invalid,

    /// <summary>Un código, identificación, fecha u otro valor único ya está registrado.</summary>
    Duplicate,

    /// <summary>Otro usuario modificó el registro desde que fue consultado.</summary>
    ConcurrencyConflict,

    /// <summary>Otra entidad activa impide el cambio solicitado.</summary>
    DependencyConflict,
}

/// <summary>
/// Resultado de una creación, edición o cambio de estado en un catálogo.
/// </summary>
public sealed record CatalogOperationResult(
    CatalogOperationStatus Status,
    string? Message = null)
{
    /// <summary>Indica si la modificación fue confirmada.</summary>
    public bool IsSuccess => Status == CatalogOperationStatus.Success;

    /// <summary>Crea un resultado exitoso.</summary>
    public static CatalogOperationResult Succeeded() => new(CatalogOperationStatus.Success);

    /// <summary>Crea un resultado para un registro inexistente.</summary>
    public static CatalogOperationResult Missing() => new(
        CatalogOperationStatus.NotFound,
        "El registro solicitado ya no existe.");

    /// <summary>Crea un resultado de validación funcional.</summary>
    public static CatalogOperationResult Invalid(string message) => new(
        CatalogOperationStatus.Invalid,
        message);

    /// <summary>Crea un resultado de unicidad.</summary>
    public static CatalogOperationResult Duplicated(string message) => new(
        CatalogOperationStatus.Duplicate,
        message);

    /// <summary>Crea un resultado de concurrencia optimista.</summary>
    public static CatalogOperationResult Concurrent() => new(
        CatalogOperationStatus.ConcurrencyConflict,
        "Otro usuario modificó este registro. Revise los datos actuales e intente nuevamente.");

    /// <summary>Crea un resultado bloqueado por una dependencia.</summary>
    public static CatalogOperationResult Dependency(string message) => new(
        CatalogOperationStatus.DependencyConflict,
        message);
}
