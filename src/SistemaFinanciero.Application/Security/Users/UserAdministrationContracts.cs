using SistemaFinanciero.Application.Catalogs.Common;

namespace SistemaFinanciero.Application.Security.Users;

/// <summary>Estados lógicos disponibles al consultar usuarios internos.</summary>
public enum UserStatusFilter
{
    /// <summary>Incluye únicamente cuentas habilitadas para iniciar sesión.</summary>
    Active,

    /// <summary>Incluye únicamente cuentas dadas de baja lógica.</summary>
    Inactive,

    /// <summary>Incluye cuentas activas e inactivas.</summary>
    All,
}

/// <summary>
/// Consulta normalizada de usuarios con búsqueda, filtros y paginación del lado del servidor.
/// </summary>
public sealed record UserQuery
{
    /// <summary>Inicializa una consulta y limita la página a un máximo de cien filas.</summary>
    public UserQuery(
        string? search = null,
        UserStatusFilter status = UserStatusFilter.Active,
        string? role = null,
        int page = 1,
        int pageSize = 20)
    {
        Search = string.IsNullOrWhiteSpace(search) ? null : search.Trim();
        Status = status;
        Role = string.IsNullOrWhiteSpace(role) ? null : role.Trim();
        Page = Math.Max(1, page);
        PageSize = Math.Clamp(pageSize, 1, 100);
    }

    /// <summary>Texto buscado en nombre o correo.</summary>
    public string? Search { get; }

    /// <summary>Estado lógico incluido.</summary>
    public UserStatusFilter Status { get; }

    /// <summary>Rol exacto incluido, o <see langword="null"/> para todos.</summary>
    public string? Role { get; }

    /// <summary>Número de página basado en uno.</summary>
    public int Page { get; }

    /// <summary>Cantidad máxima de filas por página.</summary>
    public int PageSize { get; }
}

/// <summary>Datos seguros que se muestran al administrar una cuenta interna.</summary>
public sealed record UserModel(
    Guid Id,
    string FullName,
    string Email,
    string Role,
    bool IsActive,
    bool MustChangePassword,
    bool IsLockedOut,
    DateTimeOffset? LockoutEndUtc,
    string Version);

/// <summary>Datos requeridos para crear una cuenta interna.</summary>
public sealed record CreateUserCommand(
    string FullName,
    string Email,
    string Role,
    string TemporaryPassword);

/// <summary>Datos administrativos editables de una cuenta existente.</summary>
public sealed record UpdateUserCommand(
    string FullName,
    string Email,
    string Role);

/// <summary>Datos requeridos para restablecer una contraseña ajena.</summary>
public sealed record ResetUserPasswordCommand(string TemporaryPassword);

/// <summary>Clasifica el resultado esperado de una operación administrativa de seguridad.</summary>
public enum UserOperationStatus
{
    /// <summary>La operación finalizó correctamente.</summary>
    Success,

    /// <summary>La cuenta solicitada no existe.</summary>
    NotFound,

    /// <summary>Los datos incumplen una regla funcional o de Identity.</summary>
    Invalid,

    /// <summary>El correo ya pertenece a otra cuenta.</summary>
    Duplicate,

    /// <summary>La versión enviada es anterior a la almacenada.</summary>
    ConcurrencyConflict,

    /// <summary>Una regla de protección administrativa impide la operación.</summary>
    ProtectedAction,
}

/// <summary>Resultado controlado que evita exponer excepciones o detalles internos de Identity.</summary>
public sealed record UserOperationResult(UserOperationStatus Status, string? Message = null)
{
    /// <summary>Indica si la operación fue confirmada.</summary>
    public bool IsSuccess => Status == UserOperationStatus.Success;

    /// <summary>Crea un resultado exitoso.</summary>
    public static UserOperationResult Succeeded() => new(UserOperationStatus.Success);

    /// <summary>Crea un resultado para una cuenta inexistente.</summary>
    public static UserOperationResult Missing() => new(
        UserOperationStatus.NotFound,
        "La cuenta solicitada ya no existe.");

    /// <summary>Crea un resultado de validación funcional.</summary>
    public static UserOperationResult Invalid(string message) => new(
        UserOperationStatus.Invalid,
        message);

    /// <summary>Crea un resultado de correo duplicado.</summary>
    public static UserOperationResult Duplicated() => new(
        UserOperationStatus.Duplicate,
        "Ya existe una cuenta con el mismo correo electrónico.");

    /// <summary>Crea un resultado de concurrencia optimista.</summary>
    public static UserOperationResult Concurrent() => new(
        UserOperationStatus.ConcurrencyConflict,
        "Otro administrador modificó esta cuenta. Revise los datos actuales e intente nuevamente.");

    /// <summary>Crea un resultado bloqueado por una regla de protección administrativa.</summary>
    public static UserOperationResult Protected(string message) => new(
        UserOperationStatus.ProtectedAction,
        message);
}

/// <summary>Casos de uso administrativos disponibles únicamente para el rol Administrador.</summary>
public interface IUserAdministrationService
{
    /// <summary>Busca y pagina cuentas sin exponer hashes, tokens ni sellos internos.</summary>
    public Task<PagedResult<UserModel>> SearchAsync(
        UserQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>Obtiene una cuenta por su identificador.</summary>
    public Task<UserModel?> GetAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    /// <summary>Crea una cuenta activa con un único rol y contraseña temporal.</summary>
    public Task<UserOperationResult> CreateAsync(
        CreateUserCommand command,
        Guid actorId,
        CancellationToken cancellationToken = default);

    /// <summary>Actualiza nombre, correo y rol aplicando concurrencia optimista.</summary>
    public Task<UserOperationResult> UpdateAsync(
        Guid id,
        UpdateUserCommand command,
        string version,
        Guid actorId,
        CancellationToken cancellationToken = default);

    /// <summary>Activa o desactiva una cuenta sin eliminarla.</summary>
    public Task<UserOperationResult> SetActiveAsync(
        Guid id,
        bool isActive,
        string version,
        Guid actorId,
        CancellationToken cancellationToken = default);

    /// <summary>Asigna una contraseña temporal e invalida las sesiones anteriores.</summary>
    public Task<UserOperationResult> ResetPasswordAsync(
        Guid id,
        ResetUserPasswordCommand command,
        string version,
        Guid actorId,
        CancellationToken cancellationToken = default);

    /// <summary>Elimina un bloqueo temporal sin cambiar la contraseña.</summary>
    public Task<UserOperationResult> UnlockAsync(
        Guid id,
        string version,
        Guid actorId,
        CancellationToken cancellationToken = default);
}
