using System.Collections.ObjectModel;

namespace SistemaFinanciero.Application.Security;

/// <summary>
/// Roles internos aprobados para la primera versión del sistema.
/// </summary>
public static class SystemRoles
{
    /// <summary>
    /// Gestiona usuarios y configuración técnica.
    /// </summary>
    public const string Administrator = "Administrador";

    /// <summary>
    /// Consulta reportes y autoriza anulaciones.
    /// </summary>
    public const string Management = "Gerencia";

    /// <summary>
    /// Confirma movimientos y administra la operación financiera.
    /// </summary>
    public const string Finance = "Finanzas";

    /// <summary>
    /// Prepara borradores y mantiene datos permitidos.
    /// </summary>
    public const string Assistant = "Asistente";

    /// <summary>
    /// Obtiene la colección inmutable de roles admitidos.
    /// </summary>
    public static ReadOnlyCollection<string> All { get; } = Array.AsReadOnly(
    [
        Administrator,
        Management,
        Finance,
        Assistant,
    ]);

    /// <summary>Indica si un nombre corresponde exactamente a uno de los roles aprobados.</summary>
    public static bool IsDefined(string? roleName)
    {
        return roleName is not null && All.Contains(roleName, StringComparer.Ordinal);
    }
}
