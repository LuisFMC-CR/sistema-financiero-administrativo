using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace SistemaFinanciero.Web.Models.Catalogs;

/// <summary>
/// Campos comunes de clientes y proveedores expuestos por la interfaz web. El código no se pide: lo
/// asigna el sistema al crear (regla 12).
/// </summary>
/// <remarks>
/// Conexiones: la heredan <see cref="CustomerInputViewModel"/> y <see cref="SupplierInputViewModel"/>;
/// <c>CustomersController</c> y <c>SuppliersController</c> la convierten en
/// <c>SaveCustomerCommand</c>/<c>SaveSupplierCommand</c> (Application).
/// </remarks>
public abstract class BusinessContactInputViewModel
{
    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [StringLength(150, ErrorMessage = "El nombre no puede superar 150 caracteres.")]
    [Display(Name = "Nombre o razón social")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "La identificación es obligatoria.")]
    [StringLength(50, ErrorMessage = "La identificación no puede superar 50 caracteres.")]
    [Display(Name = "Identificación")]
    public string Identification { get; set; } = string.Empty;

    [StringLength(254, ErrorMessage = "El correo no puede superar 254 caracteres.")]
    [EmailAddress(ErrorMessage = "Ingrese un correo electrónico válido.")]
    [Display(Name = "Correo electrónico")]
    public string? Email { get; set; }

    [StringLength(30, ErrorMessage = "El teléfono no puede superar 30 caracteres.")]
    [Phone(ErrorMessage = "Ingrese un teléfono válido.")]
    [Display(Name = "Teléfono")]
    public string? Phone { get; set; }

    [StringLength(500, ErrorMessage = "La dirección no puede superar 500 caracteres.")]
    [Display(Name = "Dirección")]
    public string? Address { get; set; }
}

/// <summary>Entrada para crear un cliente.</summary>
public class CustomerInputViewModel : BusinessContactInputViewModel;

/// <summary>Entrada para editar un cliente con control de concurrencia. El código se muestra fijo.</summary>
public sealed class CustomerEditViewModel : CustomerInputViewModel
{
    [Required(ErrorMessage = "No fue posible validar la versión del cliente.")]
    public string Version { get; set; } = string.Empty;

    [ValidateNever]
    public string Code { get; set; } = string.Empty;
}

/// <summary>Entrada para crear un proveedor.</summary>
public class SupplierInputViewModel : BusinessContactInputViewModel
{
    [StringLength(200, ErrorMessage = "La actividad económica no puede superar 200 caracteres.")]
    [Display(Name = "Actividad económica")]
    public string? EconomicActivity { get; set; }
}

/// <summary>Entrada para editar un proveedor con control de concurrencia. El código se muestra fijo.</summary>
public sealed class SupplierEditViewModel : SupplierInputViewModel
{
    [Required(ErrorMessage = "No fue posible validar la versión del proveedor.")]
    public string Version { get; set; } = string.Empty;

    [ValidateNever]
    public string Code { get; set; } = string.Empty;
}

