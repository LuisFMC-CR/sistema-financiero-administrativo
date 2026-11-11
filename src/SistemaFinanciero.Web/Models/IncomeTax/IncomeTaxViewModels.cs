using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Mvc.Rendering;
using SistemaFinanciero.Application.Catalogs.Common;
using SistemaFinanciero.Application.IncomeTax;
using SistemaFinanciero.Domain.Common;
using SistemaFinanciero.Domain.IncomeTax;

namespace SistemaFinanciero.Web.Models.IncomeTax;

/// <summary>Nombres en español para los enums del impuesto sobre la renta (RF-99).</summary>
public static class IncomeTaxDisplayExtensions
{
    public static string ToDisplayName(this IncomeTaxPaymentStatus status) => status switch
    {
        IncomeTaxPaymentStatus.Active => "Vigente",
        IncomeTaxPaymentStatus.Cancelled => "Anulado",
        _ => status.ToString(),
    };

    public static string ToDisplayName(this IncomeTaxPaymentStatusFilter status) => status switch
    {
        IncomeTaxPaymentStatusFilter.All => "Todos",
        IncomeTaxPaymentStatusFilter.Active => "Vigente",
        IncomeTaxPaymentStatusFilter.Cancelled => "Anulado",
        _ => status.ToString(),
    };

    public static string ToDisplayName(this IncomeTaxRegime regime) => regime switch
    {
        IncomeTaxRegime.Reduced => "Tramos para pequeñas empresas",
        IncomeTaxRegime.General => "Tarifa general",
        _ => regime.ToString(),
    };

    /// <summary>Período fiscal como texto: "01/01/2026 – 31/12/2026".</summary>
    public static string ToFiscalPeriodLabel(this DateOnly startDate) =>
        $"{startDate:dd/MM/yyyy} – {startDate.AddYears(1).AddDays(-1):dd/MM/yyyy}";
}

/// <summary>Listado de pagos de renta: conserva la consulta para repetir los filtros al paginar.</summary>
public sealed record IncomeTaxPaymentIndexViewModel(
    PagedResult<IncomeTaxPaymentModel> Results,
    IncomeTaxPaymentQuery Query,
    IReadOnlyList<SelectListItem> CashAccountOptions,
    IReadOnlyList<SelectListItem> FiscalPeriodOptions);

/// <summary>Detalle de un pago de renta y si el usuario puede anularlo.</summary>
public sealed record IncomeTaxPaymentDetailsViewModel(IncomeTaxPaymentModel Payment, bool CanCancel);

/// <summary>Entrada para registrar un pago de impuesto sobre la renta hecho a Hacienda (RF-99).</summary>
public sealed class CreateIncomeTaxPaymentViewModel
{
    [Required(ErrorMessage = "El período fiscal es obligatorio.")]
    [Display(Name = "Período fiscal al que se aplica")]
    public DateOnly? FiscalPeriodStartDate { get; set; }

    [ValidateNever]
    public IReadOnlyList<SelectListItem> FiscalPeriodOptions { get; set; } = [];

    [Required(ErrorMessage = "La cuenta de efectivo es obligatoria.")]
    [Display(Name = "Cuenta de efectivo de origen")]
    public Guid? CashAccountId { get; set; }

    [ValidateNever]
    public IReadOnlyList<SelectListItem> CashAccountOptions { get; set; } = [];

    [Display(Name = "Medio de pago")]
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.BankTransfer;

    [Range(0.01, 999_999_999_999.99, ErrorMessage = "El monto debe ser mayor que cero.")]
    [Display(Name = "Monto")]
    public decimal Amount { get; set; }

    [Required(ErrorMessage = "La fecha es obligatoria.")]
    [DataType(DataType.Date)]
    [Display(Name = "Fecha del pago")]
    public DateOnly PaymentDate { get; set; }

    [Required(ErrorMessage = "La referencia es obligatoria.")]
    [StringLength(100, ErrorMessage = "La referencia no puede superar 100 caracteres.")]
    [Display(Name = "Referencia")]
    public string Reference { get; set; } = string.Empty;

    [StringLength(300, ErrorMessage = "La descripción no puede superar 300 caracteres.")]
    [Display(Name = "Descripción")]
    public string? Description { get; set; }
}

/// <summary>Entrada para anular un pago de renta; exige el motivo. Solo Gerencia.</summary>
public sealed class IncomeTaxPaymentCancelViewModel
{
    [ValidateNever]
    public string CodeLabel { get; set; } = string.Empty;

    [ValidateNever]
    public decimal Amount { get; set; }

    [Required(ErrorMessage = "El motivo de la anulación es obligatorio.")]
    [StringLength(300, ErrorMessage = "El motivo no puede superar 300 caracteres.")]
    [Display(Name = "Motivo de la anulación")]
    public string Reason { get; set; } = string.Empty;

    [Required(ErrorMessage = "No fue posible validar la versión del registro.")]
    public string Version { get; set; } = string.Empty;
}

/// <summary>Entrada de la escala del impuesto sobre la renta (sección de Parámetros, solo Gerencia).</summary>
public sealed class IncomeTaxScaleViewModel
{
    [Range(0.01, 999_999_999_999.99, ErrorMessage = "El límite debe ser mayor que cero.")]
    [Display(Name = "Límite del primer tramo")]
    public decimal Bracket1UpperLimit { get; set; }

    [Range(0.01, 999_999_999_999.99, ErrorMessage = "El límite debe ser mayor que cero.")]
    [Display(Name = "Límite del segundo tramo")]
    public decimal Bracket2UpperLimit { get; set; }

    [Range(0.01, 999_999_999_999.99, ErrorMessage = "El límite debe ser mayor que cero.")]
    [Display(Name = "Límite del tercer tramo")]
    public decimal Bracket3UpperLimit { get; set; }

    [Range(0, 100, ErrorMessage = "El porcentaje debe estar entre 0 y 100.")]
    [Display(Name = "Porcentaje del primer tramo")]
    public decimal Bracket1Rate { get; set; }

    [Range(0, 100, ErrorMessage = "El porcentaje debe estar entre 0 y 100.")]
    [Display(Name = "Porcentaje del segundo tramo")]
    public decimal Bracket2Rate { get; set; }

    [Range(0, 100, ErrorMessage = "El porcentaje debe estar entre 0 y 100.")]
    [Display(Name = "Porcentaje del tercer tramo")]
    public decimal Bracket3Rate { get; set; }

    [Range(0, 100, ErrorMessage = "El porcentaje debe estar entre 0 y 100.")]
    [Display(Name = "Porcentaje sobre el exceso del tercer tramo")]
    public decimal Bracket4Rate { get; set; }

    [Range(0.01, 999_999_999_999.99, ErrorMessage = "El umbral debe ser mayor que cero.")]
    [Display(Name = "Renta bruta máxima para aplicar los tramos")]
    public decimal GrossIncomeThreshold { get; set; }

    [Range(0, 100, ErrorMessage = "El porcentaje debe estar entre 0 y 100.")]
    [Display(Name = "Tarifa general")]
    public decimal GeneralRate { get; set; }

    [Required(ErrorMessage = "No fue posible validar la versión del registro.")]
    public string Version { get; set; } = string.Empty;

    public static IncomeTaxScaleViewModel From(IncomeTaxScaleModel scale) => new()
    {
        Bracket1UpperLimit = scale.Bracket1UpperLimit,
        Bracket2UpperLimit = scale.Bracket2UpperLimit,
        Bracket3UpperLimit = scale.Bracket3UpperLimit,
        Bracket1Rate = scale.Bracket1Rate,
        Bracket2Rate = scale.Bracket2Rate,
        Bracket3Rate = scale.Bracket3Rate,
        Bracket4Rate = scale.Bracket4Rate,
        GrossIncomeThreshold = scale.GrossIncomeThreshold,
        GeneralRate = scale.GeneralRate,
        Version = scale.Version,
    };

    public SaveIncomeTaxScaleCommand ToCommand() => new(
        Bracket1UpperLimit,
        Bracket2UpperLimit,
        Bracket3UpperLimit,
        Bracket1Rate,
        Bracket2Rate,
        Bracket3Rate,
        Bracket4Rate,
        GrossIncomeThreshold,
        GeneralRate);
}
