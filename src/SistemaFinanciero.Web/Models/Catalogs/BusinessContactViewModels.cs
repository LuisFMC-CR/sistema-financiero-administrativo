using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using SistemaFinanciero.Domain.Catalogs;

namespace SistemaFinanciero.Web.Models.Catalogs;

/// <summary>
/// Campos comunes de clientes y proveedores expuestos por la interfaz web. El código no se pide: lo
/// asigna el sistema al crear (regla 12).
/// </summary>
/// <remarks>
/// Conexiones: la heredan <see cref="CustomerInputViewModel"/> y <see cref="SupplierInputViewModel"/>;
/// <c>CustomersController</c> y <c>SuppliersController</c> la convierten en
/// <c>SaveCustomerCommand</c>/<c>SaveSupplierCommand</c> (Application). La validación de la longitud de
/// <see cref="Identification"/> según <see cref="IdentificationType"/> (bloque 3 del paquete de mejoras
/// finales) se repite aquí junto al campo para que el error se muestre antes de llegar al dominio;
/// <c>IdentificationTypeRules</c> (Domain) es la validación autoritativa.
/// </remarks>
public abstract class BusinessContactInputViewModel : IValidatableObject
{
    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [StringLength(150, ErrorMessage = "El nombre no puede superar 150 caracteres.")]
    [Display(Name = "Nombre o razón social")]
    public string Name { get; set; } = string.Empty;

    [Display(Name = "Tipo de identificación")]
    public IdentificationType IdentificationType { get; set; } = IdentificationType.Individual;

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

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.IsNullOrWhiteSpace(Identification))
        {
            yield break;
        }

        bool isValid = IdentificationType switch
        {
            IdentificationType.Individual => Identification.Length == 9 && Identification.All(char.IsAsciiDigit),
            IdentificationType.LegalEntity => Identification.Length == 10 && Identification.All(char.IsAsciiDigit),
            IdentificationType.Dimex => Identification.Length is 11 or 12 && Identification.All(char.IsAsciiDigit),
            IdentificationType.Nite => Identification.Length == 10 && Identification.All(char.IsAsciiDigit),
            IdentificationType.ForeignNonResident => true,
            _ => false,
        };

        if (!isValid)
        {
            string message = IdentificationType switch
            {
                IdentificationType.Individual => "La identificación física debe tener 9 dígitos.",
                IdentificationType.LegalEntity => "La identificación jurídica debe tener 10 dígitos.",
                IdentificationType.Dimex => "El DIMEX debe tener 11 o 12 dígitos.",
                IdentificationType.Nite => "El NITE debe tener 10 dígitos.",
                _ => "La identificación no corresponde al tipo seleccionado.",
            };
            yield return new ValidationResult(message, [nameof(Identification)]);
        }
    }
}

/// <summary>Entrada para crear un cliente.</summary>
public class CustomerInputViewModel : BusinessContactInputViewModel
{
    [Range(
        typeof(decimal),
        "0",
        "99999999999999.99",
        ErrorMessage = "El límite de crédito no puede ser negativo.",
        ParseLimitsInInvariantCulture = true,
        ConvertValueInInvariantCulture = true)]
    [Display(Name = "Límite de crédito")]
    public decimal? CreditLimit { get; set; }

    [Display(Name = "Condición de IVA")]
    public VatCondition VatCondition { get; set; } = VatCondition.Normal;

    [Display(Name = "Retiene 2 % (sector público)")]
    public bool AppliesStateWithholding { get; set; }
}

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

