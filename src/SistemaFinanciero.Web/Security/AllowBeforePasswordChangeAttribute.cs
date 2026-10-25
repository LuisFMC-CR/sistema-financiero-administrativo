namespace SistemaFinanciero.Web.Security;

/// <summary>
/// Identifica una acción autenticada que puede ejecutarse mientras existe la obligación de
/// cambiar una contraseña temporal.
/// </summary>
/// <remarks>
/// Conexiones: <see cref="PasswordChangeRequirementMiddleware"/> la busca en los metadatos del
/// endpoint. Hoy solo la usan <c>AccountController.ChangePassword</c> y <c>Logout</c>: son las únicas
/// acciones que una sesión con contraseña temporal puede ejecutar.
/// </remarks>
[AttributeUsage(
    AttributeTargets.Class | AttributeTargets.Method,
    AllowMultiple = false,
    Inherited = true)]
public sealed class AllowBeforePasswordChangeAttribute : Attribute;
