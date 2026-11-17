using System.ComponentModel.DataAnnotations;
using SistemaFinanciero.Domain.Catalogs;

namespace SistemaFinanciero.Web.Models.Catalogs;

/// <summary>
/// Valida junto al campo que la unidad de medida sea una de las vigentes, Unidad u Hora (ADR-0061).
/// </summary>
/// <remarks>
/// Repite en el formulario la regla de <c>CatalogItem</c> para mostrar el mensaje junto al campo; el
/// dominio la sigue aplicando aunque se manipule la solicitud.
/// </remarks>
[AttributeUsage(AttributeTargets.Property)]
public sealed class SelectableUnitOfMeasureAttribute : ValidationAttribute
{
    public SelectableUnitOfMeasureAttribute()
        : base("Seleccione la unidad de medida Unidad u Hora.")
    {
    }

    public override bool IsValid(object? value) =>
        value is UnitOfMeasureCode code && code.IsSelectable();
}
