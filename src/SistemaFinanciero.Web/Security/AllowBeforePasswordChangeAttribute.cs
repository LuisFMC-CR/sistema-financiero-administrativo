namespace SistemaFinanciero.Web.Security;

/// <summary>
/// Identifica una acción autenticada que puede ejecutarse mientras existe la obligación de
/// cambiar una contraseña temporal.
/// </summary>
[AttributeUsage(
    AttributeTargets.Class | AttributeTargets.Method,
    AllowMultiple = false,
    Inherited = true)]
public sealed class AllowBeforePasswordChangeAttribute : Attribute;
