using System.ComponentModel.DataAnnotations;

namespace SistemaFinanciero.Web.Models.Catalogs;

/// <summary>
/// Entrada protegida por concurrencia para activar o desactivar un registro.
/// </summary>
/// <remarks>
/// Conexiones: la recibe la acción <c>SetActive</c> de cada controlador de catálogo; el formulario que
/// la envía vive en cada <c>Index.cshtml</c> (el botón «Activar»/«Desactivar» de cada fila).
/// </remarks>
public sealed class CatalogStatusViewModel
{
    /// <summary>Estado que se desea establecer.</summary>
    public bool IsActive { get; set; }

    /// <summary>Versión leída por el usuario antes de solicitar el cambio.</summary>
    [Required(ErrorMessage = "No fue posible validar la versión del registro.")]
    public string Version { get; set; } = string.Empty;
}

