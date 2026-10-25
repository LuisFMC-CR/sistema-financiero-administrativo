using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Mvc.Rendering;
using SistemaFinanciero.Application.Expenses;
using SistemaFinanciero.Domain.Common;
using SistemaFinanciero.Domain.Invoices;

namespace SistemaFinanciero.Web.Models.Expenses;

/// <summary>
/// Modelo de listado de gastos. Conserva la consulta completa para repetir los mismos filtros, uno
/// por columna, al paginar.
/// </summary>
public sealed record ExpenseIndexViewModel(
    SistemaFinanciero.Application.Catalogs.Common.PagedResult<ExpenseModel> Results,
    ExpenseQuery Query);

/// <summary>Entrada para crear el encabezado de un borrador de gasto; es lo único que tiene (regla 2).</summary>
public class ExpenseCreateViewModel
{
    [Required(ErrorMessage = "El proveedor es obligatorio.")]
    [Display(Name = "Proveedor")]
    public Guid? SupplierId { get; set; }

    [ValidateNever]
    public IReadOnlyList<SelectListItem> SupplierOptions { get; set; } = [];

    [Required(ErrorMessage = "La categoría es obligatoria.")]
    [Display(Name = "Categoría")]
    public Guid? CategoryId { get; set; }

    [ValidateNever]
    public IReadOnlyList<SelectListItem> CategoryOptions { get; set; } = [];

    [Required(ErrorMessage = "La descripción es obligatoria.")]
    [StringLength(300, ErrorMessage = "La descripción no puede superar 300 caracteres.")]
    [Display(Name = "Descripción")]
    public string Description { get; set; } = string.Empty;

    [Required(ErrorMessage = "La fecha del gasto es obligatoria.")]
    [DataType(DataType.Date)]
    [Display(Name = "Fecha")]
    public DateOnly IssueDate { get; set; } = DateOnly.FromDateTime(DateTime.Now);

    [EnumDataType(typeof(PaymentTerm), ErrorMessage = "Seleccione un plazo de pago válido.")]
    [Display(Name = "Plazo de pago")]
    public PaymentTerm PaymentTerm { get; set; } = PaymentTerm.Cash;

    [Range(0.01, 999_999_999_999.99, ErrorMessage = "El monto neto debe ser mayor que cero.")]
    [Display(Name = "Monto neto (sin IVA)")]
    public decimal NetAmount { get; set; }

    [Required(ErrorMessage = "La tarifa de IVA es obligatoria.")]
    [Display(Name = "Tarifa de IVA")]
    public Guid? TaxTypeId { get; set; }

    [ValidateNever]
    public IReadOnlyList<SelectListItem> TaxTypeOptions { get; set; } = [];

    [EnumDataType(typeof(PaymentMethod), ErrorMessage = "Seleccione un medio de pago válido.")]
    [Display(Name = "Medio de pago")]
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Cash;
}

/// <summary>
/// Entrada para editar el encabezado de un borrador existente. Hereda los mismos campos de
/// <see cref="ExpenseCreateViewModel"/> y agrega el control de concurrencia.
/// </summary>
public sealed class ExpenseHeaderEditViewModel : ExpenseCreateViewModel
{
    [Required(ErrorMessage = "No fue posible validar la versión del registro.")]
    public string Version { get; set; } = string.Empty;
}

/// <summary>
/// Entrada para confirmar un gasto. De contado exige además la cuenta de efectivo de origen y la
/// referencia (regla 7); el medio de pago no se pide, porque ya quedó fijo desde el borrador. A
/// crédito solo necesita la versión, porque confirmar únicamente crea la cuenta por pagar (regla 2).
/// </summary>
public sealed class ExpenseConfirmViewModel
{
    [ValidateNever]
    public bool IsCash { get; set; }

    [ValidateNever]
    public decimal TotalAmount { get; set; }

    [Display(Name = "Cuenta de efectivo")]
    public Guid? CashAccountId { get; set; }

    [ValidateNever]
    public IReadOnlyList<SelectListItem> CashAccountOptions { get; set; } = [];

    [StringLength(100, ErrorMessage = "La referencia no puede superar 100 caracteres.")]
    [Display(Name = "Referencia")]
    public string? Reference { get; set; }

    [Required(ErrorMessage = "No fue posible validar la versión del registro.")]
    public string Version { get; set; } = string.Empty;

    /// <summary>
    /// Resultado de <c>IExpenseService.CheckBudgetAsync</c> (regla 6): solo informativo, nunca
    /// bloquea el envío de este formulario.
    /// </summary>
    [ValidateNever]
    public bool HasBudget { get; set; }

    [ValidateNever]
    public decimal? BudgetedAmount { get; set; }

    [ValidateNever]
    public decimal AvailableAmountBeforeThisExpense { get; set; }

    [ValidateNever]
    public bool ExceedsAvailableBudget { get; set; }
}

/// <summary>Entrada para anular un gasto confirmado; exige el motivo (regla 4).</summary>
public sealed class ExpenseCancelViewModel
{
    [Required(ErrorMessage = "El motivo de la anulación es obligatorio.")]
    [StringLength(300, ErrorMessage = "El motivo no puede superar 300 caracteres.")]
    [Display(Name = "Motivo de la anulación")]
    public string Reason { get; set; } = string.Empty;

    [Required(ErrorMessage = "No fue posible validar la versión del registro.")]
    public string Version { get; set; } = string.Empty;
}

/// <summary>
/// Modelo de la pantalla de detalle: el gasto completo, si el usuario puede editarlo (borrador propio
/// o de cualquiera si es Finanzas o Gerencia) y si puede confirmarlo o anularlo.
/// </summary>
public sealed record ExpenseDetailsViewModel(
    ExpenseModel Expense,
    bool CanEditDraft,
    bool CanConfirm,
    bool CanCancel);
