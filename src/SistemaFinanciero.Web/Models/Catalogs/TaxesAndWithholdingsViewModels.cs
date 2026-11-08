using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using SistemaFinanciero.Application.Catalogs.Common;
using SistemaFinanciero.Application.Catalogs.TaxTypes;
using SistemaFinanciero.Application.Catalogs.WithholdingTypes;

namespace SistemaFinanciero.Web.Models.Catalogs;

/// <summary>
/// Modelo de la pantalla única "Impuestos y retenciones" (bloque 1 del paquete v7, módulo Gestionar
/// Impuestos y Retenciones de la Tabla 4): dos secciones independientes, cada una con su propio
/// filtro, búsqueda y paginación.
/// </summary>
public sealed record TaxesAndWithholdingsIndexViewModel(
    PagedResult<TaxTypeModel> TaxTypes,
    CatalogQuery TaxQuery,
    PagedResult<WithholdingTypeModel> WithholdingTypes,
    CatalogQuery WithholdingQuery,
    bool CanManage,
    bool CanAdd);

/// <summary>
/// Entrada reducida para editar una tarifa de IVA desde la pantalla única: solo el porcentaje, la
/// descripción y el estado se editan después de crearla (bloque 1 del paquete v7, punto 6); código,
/// nombre y código de Hacienda se muestran de solo lectura y el servidor los conserva tal como están.
/// </summary>
public sealed class TaxTypeRateEditViewModel
{
    [ValidateNever]
    public string Code { get; set; } = string.Empty;

    [ValidateNever]
    public string Name { get; set; } = string.Empty;

    [ValidateNever]
    public string? HaciendaRateCode { get; set; }

    [ValidateNever]
    public bool IsSystemRole { get; set; }

    [Range(
        typeof(decimal),
        "0",
        "100",
        ErrorMessage = "La tarifa debe estar entre 0 y 100 %.",
        ParseLimitsInInvariantCulture = true,
        ConvertValueInInvariantCulture = true)]
    [Display(Name = "Tarifa")]
    public decimal Rate { get; set; }

    [StringLength(300, ErrorMessage = "La descripción no puede superar 300 caracteres.")]
    [Display(Name = "Descripción")]
    public string? Description { get; set; }

    [Required(ErrorMessage = "No fue posible validar la versión del registro.")]
    public string Version { get; set; } = string.Empty;
}

/// <summary>
/// Entrada reducida para editar un tipo de retención desde la pantalla única: porcentaje, base mínima,
/// descripción y estado; código y nombre se muestran de solo lectura (bloque 1 del paquete v7, punto 6).
/// </summary>
public sealed class WithholdingTypeRateEditViewModel
{
    [ValidateNever]
    public string Code { get; set; } = string.Empty;

    [ValidateNever]
    public string Name { get; set; } = string.Empty;

    [ValidateNever]
    public bool IsSystemType { get; set; }

    [Range(
        typeof(decimal),
        "0",
        "100",
        ErrorMessage = "La tarifa debe estar entre 0 y 100 %.",
        ParseLimitsInInvariantCulture = true,
        ConvertValueInInvariantCulture = true)]
    [Display(Name = "Tarifa")]
    public decimal Rate { get; set; }

    [Range(
        typeof(decimal),
        "0",
        "99999999999999.99",
        ErrorMessage = "La base mínima no puede ser negativa.",
        ParseLimitsInInvariantCulture = true,
        ConvertValueInInvariantCulture = true)]
    [Display(Name = "Base mínima")]
    public decimal? MinimumBase { get; set; }

    [StringLength(300, ErrorMessage = "La descripción no puede superar 300 caracteres.")]
    [Display(Name = "Descripción")]
    public string? Description { get; set; }

    [Required(ErrorMessage = "No fue posible validar la versión del registro.")]
    public string Version { get; set; } = string.Empty;
}
