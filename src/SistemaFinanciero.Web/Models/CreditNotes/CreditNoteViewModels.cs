using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Mvc.Rendering;
using SistemaFinanciero.Application.Catalogs.Common;
using SistemaFinanciero.Application.CreditNotes;

namespace SistemaFinanciero.Web.Models.CreditNotes;

public sealed record CreditNoteIndexViewModel(
    PagedResult<CreditNoteInvoiceOption> EligibleInvoices,
    PagedResult<CreditNoteListItem> CreditNotes,
    string? Search);

public sealed class CreditNoteRegisterViewModel
{
    [ValidateNever]
    public CreditNoteInvoiceOption Invoice { get; set; } = null!;

    [Required(ErrorMessage = "La fecha de la nota de crédito es obligatoria.")]
    [DataType(DataType.Date)]
    [Display(Name = "Fecha")]
    public DateOnly IssueDate { get; set; }

    [Required(ErrorMessage = "El motivo de la nota de crédito es obligatorio.")]
    [StringLength(300, ErrorMessage = "El motivo no puede superar 300 caracteres.")]
    [Display(Name = "Motivo")]
    public string Reason { get; set; } = string.Empty;

    [Display(Name = "Cuenta de efectivo para la devolución")]
    public Guid? CashAccountId { get; set; }

    [StringLength(100, ErrorMessage = "La referencia no puede superar 100 caracteres.")]
    [Display(Name = "Referencia de la devolución")]
    public string? RefundReference { get; set; }

    [Required(ErrorMessage = "No fue posible validar la versión de la factura.")]
    public string InvoiceVersion { get; set; } = string.Empty;

    [ValidateNever]
    public IReadOnlyList<SelectListItem> CashAccountOptions { get; set; } = [];
}
