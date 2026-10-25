using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace SistemaFinanciero.Web.Models.Receivables;

/// <summary>
/// Paso 1 del registro de un abono: cliente, monto total a aplicar, cuenta de efectivo, categoría
/// del ingreso, referencia y fecha. El sistema propone el reparto a partir de aquí (regla 2).
/// </summary>
public sealed class RegisterReceivablePaymentStartViewModel
{
    [Required(ErrorMessage = "El cliente es obligatorio.")]
    [Display(Name = "Cliente")]
    public Guid? CustomerId { get; set; }

    [ValidateNever]
    public IReadOnlyList<SelectListItem> CustomerOptions { get; set; } = [];

    [Range(0.01, 999_999_999_999.99, ErrorMessage = "El monto debe ser mayor que cero.")]
    [Display(Name = "Monto total del abono")]
    public decimal Amount { get; set; }

    [Required(ErrorMessage = "La cuenta de efectivo es obligatoria.")]
    [Display(Name = "Cuenta de efectivo")]
    public Guid? CashAccountId { get; set; }

    [ValidateNever]
    public IReadOnlyList<SelectListItem> CashAccountOptions { get; set; } = [];

    [Required(ErrorMessage = "La categoría es obligatoria.")]
    [Display(Name = "Categoría del ingreso")]
    public Guid? CategoryId { get; set; }

    [ValidateNever]
    public IReadOnlyList<SelectListItem> CategoryOptions { get; set; } = [];

    [Required(ErrorMessage = "La referencia es obligatoria.")]
    [StringLength(100, ErrorMessage = "La referencia no puede superar 100 caracteres.")]
    [Display(Name = "Referencia")]
    public string Reference { get; set; } = string.Empty;

    [Required(ErrorMessage = "La fecha del abono es obligatoria.")]
    [DataType(DataType.Date)]
    [Display(Name = "Fecha del abono")]
    public DateOnly PaymentDate { get; set; } = DateOnly.FromDateTime(DateTime.Now);
}

/// <summary>Línea editable del reparto propuesto para una cuenta por cobrar.</summary>
public sealed class ReceivablePaymentAllocationLineViewModel
{
    public Guid ReceivableId { get; set; }

    [ValidateNever]
    public string InvoiceLabel { get; set; } = string.Empty;

    [ValidateNever]
    public DateOnly DueDate { get; set; }

    [ValidateNever]
    public decimal Balance { get; set; }

    [ValidateNever]
    public decimal InvoiceNetAmount { get; set; }

    [Range(0, 999_999_999_999.99, ErrorMessage = "El monto no puede ser negativo.")]
    [Display(Name = "Monto a aplicar")]
    public decimal Amount { get; set; }

    [Display(Name = "Tipo de retención")]
    public Guid? WithholdingTypeId { get; set; }

    [Range(0, 999_999_999_999.99, ErrorMessage = "La base de la retención no puede ser negativa.")]
    [Display(Name = "Base de la retención")]
    public decimal? WithholdingBase { get; set; }
}

/// <summary>
/// Paso 2 del registro de un abono: el reparto propuesto (de la más antigua a la más nueva),
/// editable, con la retención opcional de cada línea.
/// </summary>
public sealed class RegisterReceivablePaymentAllocateViewModel
{
    public Guid CustomerId { get; set; }

    [ValidateNever]
    public string CustomerName { get; set; } = string.Empty;

    [Required]
    public Guid CashAccountId { get; set; }

    [ValidateNever]
    public string CashAccountLabel { get; set; } = string.Empty;

    [Required]
    public Guid CategoryId { get; set; }

    [ValidateNever]
    public string CategoryLabel { get; set; } = string.Empty;

    [Required]
    public string Reference { get; set; } = string.Empty;

    [Required]
    public DateOnly PaymentDate { get; set; }

    public List<ReceivablePaymentAllocationLineViewModel> Allocations { get; set; } = [];

    [ValidateNever]
    public IReadOnlyList<SelectListItem> WithholdingTypeOptions { get; set; } = [];
}

/// <summary>Entrada para anular un abono; exige el motivo (regla 4).</summary>
public sealed class ReceivablePaymentCancelViewModel
{
    [ValidateNever]
    public string InvoiceLabel { get; set; } = string.Empty;

    [ValidateNever]
    public decimal Amount { get; set; }

    [Required(ErrorMessage = "El motivo de la anulación es obligatorio.")]
    [StringLength(300, ErrorMessage = "El motivo no puede superar 300 caracteres.")]
    [Display(Name = "Motivo de la anulación")]
    public string Reason { get; set; } = string.Empty;

    [Required(ErrorMessage = "No fue posible validar la versión del registro.")]
    public string Version { get; set; } = string.Empty;
}
