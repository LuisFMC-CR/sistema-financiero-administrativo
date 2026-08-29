using System.ComponentModel.DataAnnotations;

namespace SistemaFinanciero.Web.Models.Catalogs;

/// <summary>
/// Campos comunes de clientes y proveedores expuestos por la interfaz web.
/// </summary>
public abstract class BusinessContactInputViewModel
{
    [Required(ErrorMessage = "El código es obligatorio.")]
    [StringLength(30, ErrorMessage = "El código no puede superar 30 caracteres.")]
    [Display(Name = "Código")]
    public string Code { get; set; } = string.Empty;

    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [StringLength(150, ErrorMessage = "El nombre no puede superar 150 caracteres.")]
    [Display(Name = "Nombre o razón social")]
    public string Name { get; set; } = string.Empty;

    [StringLength(50, ErrorMessage = "La identificación no puede superar 50 caracteres.")]
    [Display(Name = "Identificación")]
    public string? Identification { get; set; }

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

/// <summary>Entrada para editar un cliente con control de concurrencia.</summary>
public sealed class CustomerEditViewModel : CustomerInputViewModel
{
    [Required(ErrorMessage = "No fue posible validar la versión del cliente.")]
    public string Version { get; set; } = string.Empty;
}

/// <summary>Entrada para crear un proveedor.</summary>
public class SupplierInputViewModel : BusinessContactInputViewModel;

/// <summary>Entrada para editar un proveedor con control de concurrencia.</summary>
public sealed class SupplierEditViewModel : SupplierInputViewModel
{
    [Required(ErrorMessage = "No fue posible validar la versión del proveedor.")]
    public string Version { get; set; } = string.Empty;
}

