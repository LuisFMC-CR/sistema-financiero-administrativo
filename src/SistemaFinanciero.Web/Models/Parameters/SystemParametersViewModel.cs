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
}
