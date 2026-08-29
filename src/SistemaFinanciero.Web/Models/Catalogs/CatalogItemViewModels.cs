using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Mvc.Rendering;
using SistemaFinanciero.Domain.Catalogs;

namespace SistemaFinanciero.Web.Models.Catalogs;

/// <summary>Entrada para crear o editar un producto o servicio.</summary>
public class CatalogItemInputViewModel
{
    [Required(ErrorMessage = "El código es obligatorio.")]
    [StringLength(30, ErrorMessage = "El código no puede superar 30 caracteres.")]
    [Display(Name = "Código")]
    public string Code { get; set; } = string.Empty;

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

    [Range(typeof(decimal), "0.0001", "99999999999999.9999", ErrorMessage = "El precio en CRC debe ser mayor que cero.")]
    [Display(Name = "Precio de referencia CRC")]
    public decimal? ReferencePriceCrc { get; set; }

    [Range(typeof(decimal), "0.0001", "99999999999999.9999", ErrorMessage = "El precio en USD debe ser mayor que cero.")]
    [Display(Name = "Precio de referencia USD")]
    public decimal? ReferencePriceUsd { get; set; }

    [Display(Name = "Categoría de ingreso predeterminada")]
    public Guid? DefaultIncomeCategoryId { get; set; }

    [ValidateNever]
    public IReadOnlyList<SelectListItem> IncomeCategoryOptions { get; set; } = [];

}

/// <summary>Entrada para editar un producto o servicio con concurrencia optimista.</summary>
public sealed class CatalogItemEditViewModel : CatalogItemInputViewModel
{
    [Required(ErrorMessage = "No fue posible validar la versión del registro.")]
    public string Version { get; set; } = string.Empty;
}
