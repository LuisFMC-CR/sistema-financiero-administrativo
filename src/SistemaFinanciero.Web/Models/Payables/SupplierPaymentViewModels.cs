using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace SistemaFinanciero.Web.Models.Payables;

/// <summary>
/// Paso 1 del registro de un pago: proveedor, monto total a aplicar, cuenta de efectivo, referencia y
/// fecha. El sistema propone el reparto a partir de aquí (regla 2). Sin categoría ni retención, a
/// diferencia de un abono: un pago no genera ningún ingreso ni documento equivalente.
/// </summary>
public sealed class RegisterSupplierPaymentStartViewModel
{
    [Required(ErrorMessage = "El proveedor es obligatorio.")]
    [Display(Name = "Proveedor")]
    public Guid? SupplierId { get; set; }

    [ValidateNever]
    public IReadOnlyList<SelectListItem> SupplierOptions { get; set; } = [];

    [Range(0.01, 999_999_999_999.99, ErrorMessage = "El monto debe ser mayor que cero.")]
    [Display(Name = "Monto total del pago")]
    public decimal Amount { get; set; }

    [Required(ErrorMessage = "La cuenta de efectivo es obligatoria.")]
    [Display(Name = "Cuenta de efectivo")]
    public Guid? CashAccountId { get; set; }

    [ValidateNever]
    public IReadOnlyList<SelectListItem> CashAccountOptions { get; set; } = [];

    [Required(ErrorMessage = "La referencia es obligatoria.")]
    [StringLength(100, ErrorMessage = "La referencia no puede superar 100 caracteres.")]
    [Display(Name = "Referencia")]
    public string Reference { get; set; } = string.Empty;

    [Required(ErrorMessage = "La fecha del pago es obligatoria.")]
    [DataType(DataType.Date)]
    [Display(Name = "Fecha del pago")]
    public DateOnly PaymentDate { get; set; } = DateOnly.FromDateTime(DateTime.Now);
}

/// <summary>Línea editable del reparto propuesto para una cuenta por pagar.</summary>
public sealed class SupplierPaymentAllocationLineViewModel
{
    public Guid PayableId { get; set; }

    [ValidateNever]
    public string ExpenseLabel { get; set; } = string.Empty;

    [ValidateNever]
    public DateOnly DueDate { get; set; }

    [ValidateNever]
    public decimal Balance { get; set; }

    [Range(0, 999_999_999_999.99, ErrorMessage = "El monto no puede ser negativo.")]
    [Display(Name = "Monto a aplicar")]
    public decimal Amount { get; set; }
}

/// <summary>
/// Paso 2 del registro de un pago: el reparto propuesto (de la más antigua a la más nueva), editable.
/// </summary>
public sealed class RegisterSupplierPaymentAllocateViewModel
{
    public Guid SupplierId { get; set; }

    [ValidateNever]
    public string SupplierName { get; set; } = string.Empty;

    [Required]
    public Guid CashAccountId { get; set; }

    [ValidateNever]
    public string CashAccountLabel { get; set; } = string.Empty;

    [Required]
    public string Reference { get; set; } = string.Empty;

    [Required]
    public DateOnly PaymentDate { get; set; }

    public List<SupplierPaymentAllocationLineViewModel> Allocations { get; set; } = [];
}

/// <summary>Entrada para anular un pago; exige el motivo (regla 4).</summary>
public sealed class SupplierPaymentCancelViewModel
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
