namespace SistemaFinanciero.Web.Models;

/// <summary>Datos mostrados en la página genérica de error.</summary>
/// <remarks>Conexiones: la llena <c>HomeController.Error</c> y la muestra <c>Views/Shared/Error.cshtml</c>.</remarks>
public class ErrorViewModel
{
    public string? RequestId { get; set; }

    public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);
}
