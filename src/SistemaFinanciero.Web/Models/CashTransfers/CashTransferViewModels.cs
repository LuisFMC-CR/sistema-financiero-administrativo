using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Mvc.Rendering;
using SistemaFinanciero.Application.CashTransfers;

namespace SistemaFinanciero.Web.Models.CashTransfers;

/// <summary>
/// Modelo de listado de traspasos. Conserva la consulta completa para repetir los mismos filtros al
/// paginar (paquete v8, bloque 1, punto 5).
/// </summary>
public sealed record CashTransferIndexViewModel(
    SistemaFinanciero.Application.Catalogs.Common.PagedResult<CashTransferModel> Results,
    CashTransferQuery Query,
    IReadOnlyList<SelectListItem> CashAccountOptions);

/// <summary>Modelo de la pantalla de detalle: el traspaso y si el usuario puede anularlo.</summary>
public sealed record CashTransferDetailsViewModel(CashTransferModel CashTransfer, bool CanCancel);

/// <summary>
/// Entrada para registrar un traspaso entre dos cuentas de efectivo propias (paquete v8, bloque 1).
/// </summary>
public sealed class CreateCashTransferViewModel
{
    [Required(ErrorMessage = "La cuenta de origen es obligatoria.")]
    [Display(Name = "Cuenta de origen")]
    public Guid? SourceCashAccountId { get; set; }

    [ValidateNever]
    public IReadOnlyList<SelectListItem> SourceCashAccountOptions { get; set; } = [];

    /// <summary>Saldo informativo de la cuenta de origen según los asientos; no bloquea el registro.</summary>
    [ValidateNever]
    public decimal? SourceCashAccountBalance { get; set; }

    [Required(ErrorMessage = "La cuenta de destino es obligatoria.")]
    [Display(Name = "Cuenta de destino")]
    public Guid? DestinationCashAccountId { get; set; }

    [ValidateNever]
    public IReadOnlyList<SelectListItem> DestinationCashAccountOptions { get; set; } = [];

    [Range(0.01, 999_999_999_999.99, ErrorMessage = "El monto debe ser mayor que cero.")]
    [Display(Name = "Monto")]
    public decimal Amount { get; set; }

    [Required(ErrorMessage = "La fecha es obligatoria.")]
    [DataType(DataType.Date)]
    [Display(Name = "Fecha")]
    public DateOnly TransferDate { get; set; }

    [Required(ErrorMessage = "La referencia es obligatoria.")]
    [StringLength(100, ErrorMessage = "La referencia no puede superar 100 caracteres.")]
    [Display(Name = "Referencia")]
    public string Reference { get; set; } = string.Empty;

    [Required(ErrorMessage = "La descripción es obligatoria.")]
    [StringLength(300, ErrorMessage = "La descripción no puede superar 300 caracteres.")]
    [Display(Name = "Descripción")]
    public string Description { get; set; } = string.Empty;
}

/// <summary>Entrada para anular un traspaso; exige el motivo. Solo Gerencia.</summary>
public sealed class CashTransferCancelViewModel
{
    [ValidateNever]
    public string ReferenceLabel { get; set; } = string.Empty;

    [ValidateNever]
    public decimal Amount { get; set; }

    [Required(ErrorMessage = "El motivo de la anulación es obligatorio.")]
    [StringLength(300, ErrorMessage = "El motivo no puede superar 300 caracteres.")]
    [Display(Name = "Motivo de la anulación")]
    public string Reason { get; set; } = string.Empty;

    [Required(ErrorMessage = "No fue posible validar la versión del registro.")]
    public string Version { get; set; } = string.Empty;
}
