using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Mvc.Rendering;
using SistemaFinanciero.Application.Invoices;
using SistemaFinanciero.Domain.Common;
using SistemaFinanciero.Domain.Invoices;

namespace SistemaFinanciero.Web.Models.Invoices;

/// <summary>Modelo de listado de facturas, con su propio filtro de estado (Borrador/Confirmada/Anulada).</summary>
public sealed record InvoiceIndexViewModel(
    SistemaFinanciero.Application.Catalogs.Common.PagedResult<InvoiceSummaryModel> Results,
    string? Search,
    InvoiceStatusFilter Status);

/// <summary>Datos del formulario de filtros del listado de facturas.</summary>
public sealed record InvoiceFilterViewModel(string? Search, InvoiceStatusFilter Status);

/// <summary>Datos necesarios para conservar los filtros al navegar entre páginas del listado.</summary>
public sealed record InvoicePaginationViewModel(
    int CurrentPage,
    int TotalPages,
    bool HasPreviousPage,
    bool HasNextPage,
    string? Search,
    InvoiceStatusFilter Status);

/// <summary>Entrada para crear el encabezado de un borrador de factura.</summary>
public class InvoiceCreateViewModel
{
    [Required(ErrorMessage = "El cliente es obligatorio.")]
    [Display(Name = "Cliente")]
    public Guid? CustomerId { get; set; }

    [ValidateNever]
    public IReadOnlyList<SelectListItem> CustomerOptions { get; set; } = [];

    [Required(ErrorMessage = "La fecha de emisión es obligatoria.")]
    [DataType(DataType.Date)]
    [Display(Name = "Fecha de emisión")]
    public DateOnly IssueDate { get; set; } = DateOnly.FromDateTime(DateTime.Now);

    [EnumDataType(typeof(PaymentTerm), ErrorMessage = "Seleccione un plazo de pago válido.")]
    [Display(Name = "Plazo de pago")]
    public PaymentTerm PaymentTerm { get; set; } = PaymentTerm.Cash;
}

/// <summary>
/// Entrada para editar el encabezado de un borrador existente. Hereda los mismos campos de
/// <see cref="InvoiceCreateViewModel"/> y agrega el control de concurrencia.
/// </summary>
public sealed class InvoiceHeaderEditViewModel : InvoiceCreateViewModel
{
    [Required(ErrorMessage = "No fue posible validar la versión del registro.")]
    public string Version { get; set; } = string.Empty;
}

/// <summary>
/// Entrada para agregar o reemplazar una línea de factura. La misma forma sirve para las dos
/// operaciones porque los datos que se capturan son idénticos (solo cambia a cuál línea se aplican).
/// </summary>
public sealed class InvoiceLineInputViewModel
{
    [Display(Name = "Producto o servicio")]
    public Guid? CatalogItemId { get; set; }

    [ValidateNever]
    public IReadOnlyList<InvoiceLineCatalogItemOptionViewModel> CatalogItemOptions { get; set; } = [];

    [Required(ErrorMessage = "La descripción es obligatoria.")]
    [StringLength(300, ErrorMessage = "La descripción no puede superar 300 caracteres.")]
    [Display(Name = "Descripción")]
    public string Description { get; set; } = string.Empty;

    [Required(ErrorMessage = "La unidad de medida es obligatoria.")]
    [StringLength(30, ErrorMessage = "La unidad no puede superar 30 caracteres.")]
    [Display(Name = "Unidad de medida")]
    public string UnitOfMeasure { get; set; } = string.Empty;

    [Range(
        typeof(decimal),
        "0.0001",
        "99999999999999.9999",
        ErrorMessage = "La cantidad debe ser mayor que cero.",
        ParseLimitsInInvariantCulture = true,
        ConvertValueInInvariantCulture = true)]
    [Display(Name = "Cantidad")]
    public decimal Quantity { get; set; } = 1m;

    [Range(
        typeof(decimal),
        "0",
        "99999999999999.9999",
        ErrorMessage = "El precio unitario no puede ser negativo.",
        ParseLimitsInInvariantCulture = true,
        ConvertValueInInvariantCulture = true)]
    [Display(Name = "Precio unitario (sin IVA)")]
    public decimal UnitPrice { get; set; }

    [Range(
        typeof(decimal),
        "0",
        "99999999999999.99",
        ErrorMessage = "El descuento no puede ser negativo.",
        ParseLimitsInInvariantCulture = true,
        ConvertValueInInvariantCulture = true)]
    [Display(Name = "Descuento")]
    public decimal DiscountAmount { get; set; }

    [Required(ErrorMessage = "La tarifa de IVA es obligatoria.")]
    [Display(Name = "Tarifa de IVA")]
    public Guid? TaxTypeId { get; set; }

    [ValidateNever]
    public IReadOnlyList<SelectListItem> TaxTypeOptions { get; set; } = [];

    /// <summary>Token de concurrencia de la factura (no de la línea, que no lo tiene propio).</summary>
    [Required(ErrorMessage = "No fue posible validar la versión del registro.")]
    public string Version { get; set; } = string.Empty;
}

/// <summary>
/// Opción del selector de producto o servicio en una línea de factura. Lleva
/// <see cref="DefaultTaxTypeId"/> como atributo <c>data-*</c> de la opción para que el script de la
/// vista proponga esa tarifa de IVA al elegir el artículo, sin otra consulta al servidor.
/// </summary>
public sealed record InvoiceLineCatalogItemOptionViewModel(Guid Id, string Label, Guid? DefaultTaxTypeId);

/// <summary>
/// Entrada para confirmar una factura. De contado exige además la cuenta de efectivo de destino, el
/// medio de pago, la referencia y la categoría del ingreso que se generará (regla 10); a crédito solo
/// necesita la versión, porque confirmar únicamente crea la cuenta por cobrar (regla 2).
/// </summary>
public sealed class InvoiceConfirmViewModel
{
    [ValidateNever]
    public bool IsCash { get; set; }

    [ValidateNever]
    public decimal TotalAmount { get; set; }

    [Display(Name = "Cuenta de efectivo")]
    public Guid? CashAccountId { get; set; }

    [ValidateNever]
    public IReadOnlyList<SelectListItem> CashAccountOptions { get; set; } = [];

    [Display(Name = "Medio de pago")]
    public PaymentMethod? PaymentMethod { get; set; }

    [StringLength(100, ErrorMessage = "La referencia no puede superar 100 caracteres.")]
    [Display(Name = "Referencia")]
    public string? Reference { get; set; }

    [Display(Name = "Categoría del ingreso")]
    public Guid? CategoryId { get; set; }

    [ValidateNever]
    public IReadOnlyList<SelectListItem> CategoryOptions { get; set; } = [];

    [Required(ErrorMessage = "No fue posible validar la versión del registro.")]
    public string Version { get; set; } = string.Empty;
}

/// <summary>Entrada para anular una factura confirmada; exige el motivo (regla 4).</summary>
public sealed class InvoiceCancelViewModel
{
    [Required(ErrorMessage = "El motivo de la anulación es obligatorio.")]
    [StringLength(300, ErrorMessage = "El motivo no puede superar 300 caracteres.")]
    [Display(Name = "Motivo de la anulación")]
    public string Reason { get; set; } = string.Empty;

    [Required(ErrorMessage = "No fue posible validar la versión del registro.")]
    public string Version { get; set; } = string.Empty;
}

/// <summary>
/// Modelo de la pantalla de detalle: la factura completa, si el usuario puede editarla (borrador propio
/// o de cualquiera si es Finanzas) y si puede confirmarla o anularla.
/// </summary>
public sealed record InvoiceDetailsViewModel(
    InvoiceModel Invoice,
    bool CanEditDraft,
    bool CanConfirm,
    bool CanCancel);
