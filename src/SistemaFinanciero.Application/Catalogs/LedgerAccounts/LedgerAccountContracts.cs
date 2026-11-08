using SistemaFinanciero.Application.Catalogs.Common;
using SistemaFinanciero.Domain.Catalogs;

namespace SistemaFinanciero.Application.Catalogs.LedgerAccounts;

/// <summary>
/// Datos requeridos para crear una cuenta contable de movimiento. El código lo calcula el servicio
/// como el siguiente hijo libre de <see cref="ParentId"/> (regla 12); el tipo se hereda de la cuenta
/// superior. Una cuenta de efectivo indica su subtipo.
/// </summary>
/// <remarks>
/// Conexiones: lo recibe <c>ILedgerAccountService.CreateAsync</c>, implementado por
/// <c>LedgerAccountService</c> (Infrastructure/Catalogs/LedgerAccounts), consumido por
/// <c>LedgerAccountsController</c> (Web, ruta <c>/catalogos/cuentas-contables</c>). El usuario agrega
/// cuentas de movimiento dentro de los grupos permitidos por <see cref="LedgerAccountGroups"/>: por
/// eso <see cref="ParentId"/> es obligatorio y no hay un parámetro de tipo ni de código.
/// </remarks>
public sealed record CreateLedgerAccountCommand(
    string Name,
    Guid ParentId,
    CashAccountKind? CashKind,
    string? Reference,
    string? Description);

/// <summary>
/// Datos editables de una cuenta de movimiento existente; código, tipo, subtipo de efectivo, cuenta
/// superior y naturaleza (movimiento/acumuladora) se omiten porque son inmutables (regla 12).
/// </summary>
public sealed record UpdateLedgerAccountCommand(
    string Name,
    string? Reference,
    string? Description);

/// <summary>
/// Filtros por columna del catálogo contable, más la paginación. Los filtros son opcionales: si no se
/// envían, el listado devuelve el catálogo completo.
/// </summary>
public sealed record LedgerAccountQuery
{
    /// <summary>Inicializa los filtros normalizando los textos y limitando el tamaño de página.</summary>
    public LedgerAccountQuery(
        string? code = null,
        string? name = null,
        LedgerAccountType? type = null,
        bool? isMovement = null,
        bool? isActive = null,
        int page = 1,
        int pageSize = 500)
    {
        Code = string.IsNullOrWhiteSpace(code) ? null : code.Trim();
        Name = string.IsNullOrWhiteSpace(name) ? null : name.Trim();
        Type = type;
        IsMovement = isMovement;
        IsActive = isActive;
        Page = Math.Max(1, page);
        PageSize = Math.Clamp(pageSize, 1, 500);
    }

    /// <summary>Texto opcional buscado en el código de la cuenta.</summary>
    public string? Code { get; }

    /// <summary>Texto opcional buscado en el nombre o la descripción de la cuenta.</summary>
    public string? Name { get; }

    /// <summary>Tipo de cuenta opcional (Activo, Pasivo, Patrimonio, Ingreso o Gasto).</summary>
    public LedgerAccountType? Type { get; }

    /// <summary>
    /// Cuando se indica, filtra por naturaleza: <c>true</c> solo cuentas de movimiento y
    /// <c>false</c> solo cuentas acumuladoras o de título.
    /// </summary>
    public bool? IsMovement { get; }

    /// <summary>Estado opcional: activa o inactiva/bloqueada.</summary>
    public bool? IsActive { get; }

    /// <summary>Número de página basado en uno.</summary>
    public int Page { get; }

    /// <summary>Cantidad máxima de registros por página.</summary>
    public int PageSize { get; }
}

/// <summary>Proyección administrativa de una cuenta contable del catálogo.</summary>
public sealed record LedgerAccountModel(
    Guid Id,
    string Code,
    string Name,
    LedgerAccountType Type,
    Guid? ParentId,
    string? ParentCode,
    string? ParentName,
    CashAccountKind? CashKind,
    string? Reference,
    string? Description,
    bool IsActive,
    bool IsProtected,
    bool IsMovement,
    LedgerAccountSystemRole? SystemRole,
    decimal Balance,
    bool HasTransactions,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    string Version)
{
    /// <summary>Indica si es una cuenta de efectivo.</summary>
    public bool IsCash => CashKind is not null;

    /// <summary>Nivel jerárquico (raíz = 1, grupo = 2, cuenta de detalle = 3).</summary>
    public int Level => Code.Count(character => character == '.') + 1;

    /// <summary>Etiqueta de la naturaleza contable para la columna Nivel / Jerarquía.</summary>
    public string NatureLabel => IsMovement
        ? "Movimiento"
        : "Acumuladora / título";

    /// <summary>Indica si el usuario puede modificar la cuenta con su perfil actual.</summary>
    public bool CanEdit => IsMovement && !IsProtected;

    /// <summary>
    /// Indica si se debe mostrar la acción para solicitar la desactivación; el servicio valida los
    /// movimientos históricos antes de permitirla.
    /// </summary>
    public bool CanDeactivate => CanEdit && IsActive;
}

/// <summary>Opción activa para selectores de cuenta contable.</summary>
public sealed record LedgerAccountOption(Guid Id, string Code, string Name);

/// <summary>Opción activa para elegir una caja o un banco en ingresos y pagos.</summary>
public sealed record CashAccountOption(
    Guid Id,
    string Code,
    string Name,
    CashAccountKind CashKind);

/// <summary>Casos de uso del catálogo de cuentas contables jerárquicas.</summary>
public interface ILedgerAccountService
{
    /// <summary>Lista el catálogo contable con filtros por columna y saldos calculados del libro mayor.</summary>
    public Task<PagedResult<LedgerAccountModel>> SearchAsync(
        LedgerAccountQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>Obtiene una cuenta contable por identificador con su saldo actual.</summary>
    public Task<LedgerAccountModel?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lista las cuentas activas que pueden ser cuenta superior de otra del tipo indicado: del mismo
    /// tipo, no de efectivo y pertenecientes a los grupos permitidos por
    /// <see cref="LedgerAccountGroups.DetailParentCodes"/>.
    /// </summary>
    public Task<IReadOnlyList<LedgerAccountOption>> GetParentOptionsAsync(
        LedgerAccountType type,
        Guid? excludedAccountId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lista las cuentas de movimiento activas del tipo indicado, por ejemplo para las categorías de
    /// ingreso/gasto que usan facturas, gastos, presupuestos y productos.
    /// </summary>
    public Task<IReadOnlyList<LedgerAccountOption>> GetActiveOptionsAsync(
        LedgerAccountType type,
        CancellationToken cancellationToken = default);

    /// <summary>Lista las cajas y bancos activos.</summary>
    public Task<IReadOnlyList<CashAccountOption>> GetActiveCashAccountsAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lista todas las cuentas de movimiento (de cualquier tipo y estado, incluidas las de sistema),
    /// para elegir la cuenta del reporte de libro mayor: a diferencia de
    /// <see cref="GetActiveOptionsAsync"/>, no filtra por tipo ni excluye cuentas inactivas o de
    /// sistema, porque un reporte histórico puede necesitar cualquier cuenta que haya tenido
    /// movimiento.
    /// </summary>
    public Task<IReadOnlyList<LedgerAccountOption>> GetDetailAccountOptionsAsync(
        CancellationToken cancellationToken = default);

    /// <summary>Crea una cuenta contable de movimiento dentro de un grupo permitido.</summary>
    public Task<CatalogOperationResult> CreateAsync(
        CreateLedgerAccountCommand command,
        Guid actorId,
        CancellationToken cancellationToken = default);

    /// <summary>Actualiza una cuenta de movimiento; las acumuladoras no se modifican.</summary>
    public Task<CatalogOperationResult> UpdateAsync(
        Guid id,
        UpdateLedgerAccountCommand command,
        string version,
        Guid actorId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Activa o desactiva una cuenta de movimiento sin eliminar sus referencias históricas. Las
    /// cuentas con hijas activas y las cuentas protegidas mantienen sus restricciones.
    /// </summary>
    public Task<CatalogOperationResult> SetActiveAsync(
        Guid id,
        bool isActive,
        string version,
        Guid actorId,
        CancellationToken cancellationToken = default);
}
