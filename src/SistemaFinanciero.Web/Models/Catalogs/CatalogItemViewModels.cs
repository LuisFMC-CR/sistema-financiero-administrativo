using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Mvc.Rendering;
using SistemaFinanciero.Domain.Catalogs;

namespace SistemaFinanciero.Web.Models.Catalogs;

/// <summary>
/// Entrada para crear o editar un producto o servicio. El código no se pide (regla 12); al editar,
/// <see cref="Type"/> se muestra de solo lectura porque el código ya lo codifica (<c>PRD-</c>/<c>SRV-</c>)
/// y queda fijo después de crear.
/// </summary>
/// <remarks>
/// Conexiones: la reciben las acciones de <c>CatalogItemsController</c>, que la convierten en
/// <c>CreateCatalogItemCommand</c> o <c>UpdateCatalogItemCommand</c> (Application) según la acción;
/// <c>IncomeCategoryOptions</c> lo llena <c>CatalogItemsController.PopulateCategoryOptionsAsync</c> con
/// lo que devuelve <c>IFinancialCategoryService.GetActiveOptionsAsync</c>; <c>DefaultTaxTypeOptions</c>
/// (RF-52) lo llena <c>CatalogItemsController.PopulateTaxTypeOptionsAsync</c> con lo que devuelve
/// <c>ITaxTypeService.GetActiveOptionsAsync</c>.
/// </remarks>
public class CatalogItemInputViewModel
{
    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [StringLength(150, ErrorMessage = "El nombre no puede superar 150 caracteres.")]
    [Display(Name = "Nombre")]
    public string Name { get; set; } = string.Empty;

    [EnumDataType(typeof(CatalogItemType), ErrorMessage = "Seleccione un tipo válido.")]
    [Display(Name = "Tipo")]
    public CatalogItemType Type { get; set; } = CatalogItemType.Service;

    [StringLength(500, ErrorMessage = "La descripción no puede superar 500 caracteres.")]
    [Display(Name = "Descripción")]
    public string? Description { get; set; }

    [Required(ErrorMessage = "La unidad de medida es obligatoria.")]
    [StringLength(30, ErrorMessage = "La unidad no puede superar 30 caracteres.")]
    [Display(Name = "Unidad de medida")]
    public string UnitOfMeasure { get; set; } = string.Empty;

    [Range(
        typeof(decimal),
        "0.0001",
        "99999999999999.9999",
        ErrorMessage = "El precio en CRC debe ser mayor que cero.",
        ParseLimitsInInvariantCulture = true,
        ConvertValueInInvariantCulture = true)]
    [Display(Name = "Precio de referencia")]
    public decimal? ReferencePriceCrc { get; set; }

    [Display(Name = "Categoría de ingreso predeterminada")]
    public Guid? DefaultIncomeCategoryId { get; set; }

    [ValidateNever]
    public IReadOnlyList<SelectListItem> IncomeCategoryOptions { get; set; } = [];

    [Display(Name = "Tarifa de IVA predeterminada")]
    public Guid? DefaultTaxTypeId { get; set; }

    [ValidateNever]
    public IReadOnlyList<SelectListItem> DefaultTaxTypeOptions { get; set; } = [];

}

/// <summary>Entrada para editar un producto o servicio con concurrencia optimista. El código se muestra fijo.</summary>
public sealed class CatalogItemEditViewModel : CatalogItemInputViewModel
{
    [Required(ErrorMessage = "No fue posible validar la versión del registro.")]
    public string Version { get; set; } = string.Empty;

    [ValidateNever]
    public string Code { get; set; } = string.Empty;
}
