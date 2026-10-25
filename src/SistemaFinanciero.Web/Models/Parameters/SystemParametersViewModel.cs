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

    /// <summary>Fecha de cierre vigente; solo informativa en este formulario (regla 16).</summary>
    public DateOnly ClosingDate { get; set; }
}

/// <summary>
/// Entrada para cambiar la fecha de cierre (regla de negocio 16). Adelantarla es el cierre mensual,
/// sin motivo; retrocederla es una reapertura y exige motivo.
/// </summary>
public sealed class ChangeClosingDateViewModel
{
    [Required(ErrorMessage = "La nueva fecha de cierre es obligatoria.")]
    [DataType(DataType.Date)]
    [Display(Name = "Nueva fecha de cierre")]
    public DateOnly NewClosingDate { get; set; }

    [StringLength(300, ErrorMessage = "El motivo no puede superar 300 caracteres.")]
    [Display(Name = "Motivo (obligatorio solo si retrocede la fecha)")]
    public string? Reason { get; set; }

    [Required(ErrorMessage = "No fue posible validar la versión del registro.")]
    public string Version { get; set; } = string.Empty;
}

/// <summary>Agrupa los dos formularios independientes de la pantalla de parámetros.</summary>
public sealed class SystemParametersPageViewModel
{
    public SystemParametersViewModel Parameters { get; set; } = new();

    public ChangeClosingDateViewModel ClosingDateForm { get; set; } = new();
}
