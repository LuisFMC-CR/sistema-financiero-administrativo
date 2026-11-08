using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SistemaFinanciero.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Desactiva por baja lógica la tarifa de sistema "No sujeto" (<c>TaxTypeSystemRole.NotSubject</c>)
    /// a pedido del usuario: la empresa no factura hoy a clientes bajo ese régimen (CCSS,
    /// municipalidades). No se elimina (regla de catálogos con baja lógica): si en el futuro se
    /// factura a un cliente con condición de IVA "No sujeto", el sistema bloqueará la línea hasta que
    /// Gerencia la reactive desde "Impuestos y retenciones".
    /// </summary>
    /// <inheritdoc />
    public partial class DeactivateNotSubjectVatRate : Migration
    {
        private const string SystemUserId = "00000000-0000-0000-0000-000000000001";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql($"""
                UPDATE [catalogos].[TiposImpuesto]
                SET [IsActive] = 0,
                    [UpdatedAtUtc] = CAST(SYSUTCDATETIME() AS datetimeoffset),
                    [UpdatedByUserId] = '{SystemUserId}'
                WHERE [Code] = 'NO-SUJETO' AND [IsActive] = 1;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql($"""
                UPDATE [catalogos].[TiposImpuesto]
                SET [IsActive] = 1,
                    [UpdatedAtUtc] = CAST(SYSUTCDATETIME() AS datetimeoffset),
                    [UpdatedByUserId] = '{SystemUserId}'
                WHERE [Code] = 'NO-SUJETO' AND [IsActive] = 0;
                """);
        }
    }
}
