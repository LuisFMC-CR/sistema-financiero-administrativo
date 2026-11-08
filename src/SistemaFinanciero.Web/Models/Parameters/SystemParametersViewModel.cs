using System.ComponentModel.DataAnnotations;

namespace SistemaFinanciero.Web.Models.Parameters;

/// <summary>
/// Entrada única para configurar o editar los parámetros del sistema. <see cref="Version"/> vacío
/// indica que todavía no existen y el envío los crea; con valor, lo edita con control de concurrencia.
/// </summary>
public sealed class SystemParametersViewModel
{
    [Range(
        typeof(decimal),
        "0",
        "999999999999.99",
        ErrorMessage = "El límite no puede ser negativo.",
        ParseLimitsInInvariantCulture = true,
        ConvertValueInInvariantCulture = true)]
    [Display(Name = "Límite de autorización (CRC)")]
    public decimal AuthorizationLimitCrc { get; set; }

    [Range(1, 3650, ErrorMessage = "Los días de alerta deben estar entre 1 y 3650.")]
    [Display(Name = "Días de alerta de vencimiento")]
    public int OverdueAlertDays { get; set; } = 7;

    public string Version { get; set; } = string.Empty;

    public DateTimeOffset? UpdatedAtUtc { get; set; }

    public string? UpdatedByUserName { get; set; }

    public DateOnly? FiscalPeriodStartDate { get; set; }

    public DateOnly? FiscalPeriodEndDate { get; set; }
}

public sealed class FiscalPeriodViewModel
{
    [Required(ErrorMessage = "La fecha de inicio del período fiscal es obligatoria.")]
    [DataType(DataType.Date)]
    [Display(Name = "Inicio del período fiscal")]
    public DateOnly? StartDate { get; set; }

    [Required(ErrorMessage = "La fecha de cierre del período fiscal es obligatoria.")]
    [DataType(DataType.Date)]
    [Display(Name = "Cierre del período fiscal")]
    public DateOnly? EndDate { get; set; }

    [StringLength(300, ErrorMessage = "El motivo no puede superar 300 caracteres.")]
    [Display(Name = "Motivo del cambio (opcional)")]
    public string? Reason { get; set; }

    [Required(ErrorMessage = "No fue posible validar la versión del registro.")]
    public string Version { get; set; } = string.Empty;
}

public sealed class ReopenAccountingPeriodViewModel
{
    [Range(1, 9998, ErrorMessage = "El año del período no es válido.")]
    [Display(Name = "Año")]
    public int Year { get; set; }

    [Range(1, 12, ErrorMessage = "El mes debe estar entre 1 y 12.")]
    [Display(Name = "Mes")]
    public int Month { get; set; }

    [Required(ErrorMessage = "El motivo de reapertura es obligatorio.")]
    [StringLength(300, ErrorMessage = "El motivo no puede superar 300 caracteres.")]
    [Display(Name = "Motivo de reapertura")]
    public string Reason { get; set; } = string.Empty;
}

public sealed class CloseAccountingPeriodViewModel
{
    [Range(1, 9998)]
    public int Year { get; set; }

    [Range(1, 12)]
    public int Month { get; set; }

    [Required]
    public string Version { get; set; } = string.Empty;
}

/// <summary>
/// Entrada para actualizar los datos de la empresa (RF-55): nombre y cédula jurídica obligatorios;
/// dirección, teléfono y correo opcionales. Alimentan el encabezado de la factura interna (regla 15).
/// </summary>
public sealed class CompanyDataViewModel
{
    [Required(ErrorMessage = "El nombre de la empresa es obligatorio.")]
    [StringLength(150, ErrorMessage = "El nombre no puede superar 150 caracteres.")]
    [Display(Name = "Nombre de la empresa")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "La cédula jurídica es obligatoria.")]
    [StringLength(50, ErrorMessage = "La cédula jurídica no puede superar 50 caracteres.")]
    [Display(Name = "Cédula jurídica")]
    public string LegalId { get; set; } = string.Empty;

    [StringLength(300, ErrorMessage = "La dirección no puede superar 300 caracteres.")]
    [Display(Name = "Otras señas")]
    public string? Address { get; set; }

    [StringLength(30, ErrorMessage = "El teléfono no puede superar 30 caracteres.")]
    [Display(Name = "Teléfono")]
    public string? Phone { get; set; }

    [StringLength(150, ErrorMessage = "El correo no puede superar 150 caracteres.")]
    [EmailAddress(ErrorMessage = "El correo no tiene un formato válido.")]
    [Display(Name = "Correo")]
    public string? Email { get; set; }

    [StringLength(10, ErrorMessage = "El código de actividad económica no puede superar 10 caracteres.")]
    [Display(Name = "Código de actividad económica")]
    public string? ActivityCode { get; set; }

    [StringLength(100, ErrorMessage = "El nombre comercial no puede superar 100 caracteres.")]
    [Display(Name = "Nombre comercial")]
    public string? TradeName { get; set; }

    [Display(Name = "Provincia")]
    public string? ProvinceCode { get; set; }

    [StringLength(2, ErrorMessage = "El código de cantón debe tener exactamente 2 dígitos.")]
    [RegularExpression(@"^\d{2}$", ErrorMessage = "El código de cantón debe tener exactamente 2 dígitos.")]
    [Display(Name = "Cantón")]
    public string? CantonCode { get; set; }

    [StringLength(2, ErrorMessage = "El código de distrito debe tener exactamente 2 dígitos.")]
    [RegularExpression(@"^\d{2}$", ErrorMessage = "El código de distrito debe tener exactamente 2 dígitos.")]
    [Display(Name = "Distrito")]
    public string? DistrictCode { get; set; }

    [StringLength(3, ErrorMessage = "El código de sucursal debe tener exactamente 3 dígitos.")]
    [RegularExpression(@"^\d{3}$", ErrorMessage = "El código de sucursal debe tener exactamente 3 dígitos.")]
    [Display(Name = "Sucursal")]
    public string? BranchCode { get; set; } = "001";

    [StringLength(5, ErrorMessage = "El código de terminal debe tener exactamente 5 dígitos.")]
    [RegularExpression(@"^\d{5}$", ErrorMessage = "El código de terminal debe tener exactamente 5 dígitos.")]
    [Display(Name = "Terminal")]
    public string? TerminalCode { get; set; } = "00001";

    [Required(ErrorMessage = "No fue posible validar la versión del registro.")]
    public string Version { get; set; } = string.Empty;
}

/// <summary>Agrupa los formularios independientes de la pantalla de parámetros.</summary>
public sealed class SystemParametersPageViewModel
{
    public SistemaFinanciero.Application.Parameters.SystemParametersModel? CurrentConfiguration { get; set; }

    public SystemParametersViewModel Parameters { get; set; } = new();

    public FiscalPeriodViewModel FiscalPeriodForm { get; set; } = new();

    public ReopenAccountingPeriodViewModel ReopenAccountingPeriodForm { get; set; } = new();

    public IReadOnlyList<SistemaFinanciero.Application.Parameters.ReopenedAccountingPeriodModel>
        ReopenedAccountingPeriods { get; set; } = [];

    public CompanyDataViewModel CompanyDataForm { get; set; } = new();
}
