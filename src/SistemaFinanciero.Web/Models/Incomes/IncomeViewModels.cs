using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Mvc.Rendering;
using SistemaFinanciero.Application.Incomes;
using SistemaFinanciero.Domain.Common;

namespace SistemaFinanciero.Web.Models.Incomes;

/// <summary>
/// Modelo de listado de ingresos. Conserva la consulta completa para repetir los mismos filtros, uno
/// por columna, al paginar.
/// </summary>
public sealed record IncomeIndexViewModel(
    SistemaFinanciero.Application.Catalogs.Common.PagedResult<IncomeModel> Results,
    IncomeQuery Query);

/// <summary>Modelo de la pantalla de detalle: el ingreso y si el usuario puede anularlo.</summary>
public sealed record IncomeDetailsViewModel(IncomeModel Income, bool CanCancel);

/// <summary>Entrada para registrar un ingreso manual (regla 2).</summary>
public sealed class CreateManualIncomeViewModel
{
    [Required(ErrorMessage = "La categoría es obligatoria.")]
    [Display(Name = "Categoría")]
    public Guid? CategoryId { get; set; }

    [ValidateNever]
    public IReadOnlyList<SelectListItem> CategoryOptions { get; set; } = [];

    [Required(ErrorMessage = "La cuenta de efectivo es obligatoria.")]
    [Display(Name = "Cuenta de efectivo")]
    public Guid? CashAccountId { get; set; }

    [ValidateNever]
    public IReadOnlyList<SelectListItem> CashAccountOptions { get; set; } = [];

    [Range(0.01, 999_999_999_999.99, ErrorMessage = "El monto debe ser mayor que cero.")]
    [Display(Name = "Monto")]
    public decimal Amount { get; set; }

    [Required(ErrorMessage = "La fecha es obligatoria.")]
    [DataType(DataType.Date)]
    [Display(Name = "Fecha")]
    public DateOnly IncomeDate { get; set; } = DateOnly.FromDateTime(DateTime.Now);

    [Display(Name = "Medio de pago")]
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.BankTransfer;

    [StringLength(100, ErrorMessage = "La referencia no puede superar 100 caracteres.")]
    [Display(Name = "Referencia")]
    public string? Reference { get; set; }

    [Required(ErrorMessage = "La descripción es obligatoria: explique de qué se trata el ingreso.")]
    [StringLength(300, ErrorMessage = "La descripción no puede superar 300 caracteres.")]
    [Display(Name = "Descripción")]
    public string Description { get; set; } = string.Empty;
}

/// <summary>Entrada para anular un ingreso manual; exige el motivo (regla 4).</summary>
public sealed class IncomeCancelViewModel
{
    [ValidateNever]
    public string Description { get; set; } = string.Empty;

    [ValidateNever]
    public decimal Amount { get; set; }

    [Required(ErrorMessage = "El motivo de la anulación es obligatorio.")]
    [StringLength(300, ErrorMessage = "El motivo no puede superar 300 caracteres.")]
    [Display(Name = "Motivo de la anulación")]
    public string Reason { get; set; } = string.Empty;

    [Required(ErrorMessage = "No fue posible validar la versión del registro.")]
    public string Version { get; set; } = string.Empty;
}
