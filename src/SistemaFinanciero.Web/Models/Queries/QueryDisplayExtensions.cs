namespace SistemaFinanciero.Web.Models.Queries;

/// <summary>Centraliza las etiquetas en español utilizadas en la pantalla de Consultas.</summary>
public static class QueryDisplayExtensions
{
    public static string ToDisplayName(this QueryDocumentType type) => type switch
    {
        QueryDocumentType.Invoice => "Facturas",
        QueryDocumentType.Expense => "Gastos",
        QueryDocumentType.Receivable => "Cuentas por cobrar",
        QueryDocumentType.Payable => "Cuentas por pagar",
        QueryDocumentType.Income => "Ingresos",
        _ => "Desconocido",
    };
}
