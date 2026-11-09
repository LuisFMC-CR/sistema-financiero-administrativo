using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Mvc.Rendering;
using SistemaFinanciero.Application.VatSettlements;

namespace SistemaFinanciero.Web.Models.VatSettlements;

/// <summary>
/// Modelo de listado de liquidaciones de IVA. Conserva la consulta completa para repetir los mismos
/// filtros al paginar (paquete v8, bloque 2).
/// </summary>
public sealed record VatSettlementIndexViewModel(
    SistemaFinanciero.Application.Catalogs.Common.PagedResult<VatSettlementModel> Results,
    VatSettlementQuery Query);

/// <summary>Modelo de la pantalla de detalle: la liquidación y si el usuario puede anularla.</summary>
public sealed record VatSettlementDetailsViewModel(VatSettlementModel VatSettlement, bool CanCancel);

/// <summary>
/// Entrada para elegir el período y, una vez calculada la vista previa, registrar la liquidación
/// (paquete v8, bloque 2, punto 7).
/// </summary>
public sealed class RegisterVatSettlementViewModel
{
    [Required(ErrorMessage = "El año es obligatorio.")]
    [Range(2000, 2200, ErrorMessage = "El año no es válido.")]
    [Display(Name = "Año")]
    public int? PeriodYear { get; set; }

    [Required(ErrorMessage = "El mes es obligatorio.")]
    [Range(1, 12, ErrorMessage = "El mes debe estar entre 1 y 12.")]
    [Display(Name = "Mes")]
    public int? PeriodMonth { get; set; }

    [ValidateNever]
    public VatSettlementPreview? Preview { get; set; }

    [Required(ErrorMessage = "La fecha de la liquidación es obligatoria.")]
    [DataType(DataType.Date)]
    [Display(Name = "Fecha de la liquidación")]
    public DateOnly? SettlementDate { get; set; }

    [Display(Name = "Cuenta de efectivo")]
    public Guid? CashAccountId { get; set; }

    [ValidateNever]
    public IReadOnlyList<SelectListItem> CashAccountOptions { get; set; } = [];

    [StringLength(100, ErrorMessage = "La referencia no puede superar 100 caracteres.")]
    [Display(Name = "Referencia del comprobante de pago")]
    public string? Reference { get; set; }
}

/// <summary>Entrada para anular una liquidación; exige el motivo. Solo Gerencia.</summary>
public sealed class VatSettlementCancelViewModel
{
    [ValidateNever]
    public string CodeLabel { get; set; } = string.Empty;

    [ValidateNever]
    public decimal PaymentAmount { get; set; }

    [Required(ErrorMessage = "El motivo de la anulación es obligatorio.")]
    [StringLength(300, ErrorMessage = "El motivo no puede superar 300 caracteres.")]
    [Display(Name = "Motivo de la anulación")]
    public string Reason { get; set; } = string.Empty;

    [Required(ErrorMessage = "No fue posible validar la versión del registro.")]
    public string Version { get; set; } = string.Empty;
}
