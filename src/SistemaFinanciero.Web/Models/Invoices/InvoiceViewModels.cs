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
/// <remarks>
/// El artículo del catálogo es obligatorio: la descripción y la unidad de medida siempre se toman de
/// él, así que ya no se capturan aquí. <see cref="UnitPrice"/> y <see cref="TaxTypeId"/> se muestran
/// siempre (para que la persona vea qué se va a facturar), pero el script de la vista los bloquea
/// cuando el artículo elegido ya tiene su propio precio de referencia o su tarifa de IVA
/// predeterminada; solo quedan editables cuando el artículo no los tiene.
/// </remarks>
public sealed class InvoiceLineInputViewModel
{
    [Required(ErrorMessage = "El producto o servicio es obligatorio.")]
    [Display(Name = "Producto o servicio")]
    public Guid? CatalogItemId { get; set; }

    [ValidateNever]
    public IReadOnlyList<InvoiceLineCatalogItemOptionViewModel> CatalogItemOptions { get; set; } = [];

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
    public decimal? UnitPrice { get; set; }

    [Range(
        typeof(decimal),
        "0",
        "99999999999999.99",
        ErrorMessage = "El descuento no puede ser negativo.",
        ParseLimitsInInvariantCulture = true,
        ConvertValueInInvariantCulture = true)]
    [Display(Name = "Descuento")]
    public decimal DiscountAmount { get; set; }

    [Display(Name = "Tarifa de IVA")]
    public Guid? TaxTypeId { get; set; }

    [ValidateNever]
    public IReadOnlyList<InvoiceLineTaxTypeOptionViewModel> TaxTypeOptions { get; set; } = [];

    /// <summary>Token de concurrencia de la factura (no de la línea, que no lo tiene propio).</summary>
    [Required(ErrorMessage = "No fue posible validar la versión del registro.")]
    public string Version { get; set; } = string.Empty;
}

/// <summary>
/// Opción del selector de producto o servicio en una línea de factura. Lleva <see cref="Name"/>,
/// <see cref="UnitOfMeasure"/>, <see cref="ReferencePriceCrc"/>, <see cref="DefaultTaxTypeId"/> y
/// <see cref="DefaultTaxRate"/> como atributos <c>data-*</c> de la opción para que el script de la
/// vista muestre la descripción, la unidad, el precio y la tarifa sin otra consulta al servidor, y
/// bloquee el precio y la tarifa cuando el artículo ya los tiene definidos.
/// </summary>
public sealed record InvoiceLineCatalogItemOptionViewModel(
    Guid Id,
    string Label,
    string Name,
    string UnitOfMeasure,
    decimal? ReferencePriceCrc,
    Guid? DefaultTaxTypeId,
    decimal? DefaultTaxRate);

/// <summary>
/// Opción del selector de tarifa de IVA de una línea de factura. Lleva <see cref="Rate"/> como
/// atributo <c>data-rate</c> de la opción para que el script de la vista calcule el IVA de la línea
/// en tiempo real sin otra consulta al servidor.
/// </summary>
public sealed record InvoiceLineTaxTypeOptionViewModel(Guid Id, string Label, decimal Rate);

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
