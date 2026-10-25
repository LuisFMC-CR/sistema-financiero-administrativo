using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SistemaFinanciero.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddInvoiceNumberSequence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Secuencia del consecutivo de facturas (RF-34, regla 12). Se arranca en 1 porque no hay
            // facturas existentes en la base; al confirmar, quien llame a Invoice.Confirm obtiene el
            // siguiente valor (ver SqlServerInvoiceScenarioTests.GetNextInvoiceNumberAsync).
            migrationBuilder.CreateSequence(
                name: "Seq_FacturaNumero",
                schema: "finanzas",
                startValue: 1L);

            migrationBuilder.DropIndex(
                name: "UX_Facturas_Number",
                schema: "finanzas",
                table: "Facturas");

            // SQL Server no permite quitar la propiedad IDENTITY con ALTER COLUMN: hay que soltar la
            // columna y volver a crearla. No hay facturas todavía, así que no se pierden datos.
            migrationBuilder.DropColumn(
                name: "Number",
                schema: "finanzas",
                table: "Facturas");

            migrationBuilder.AddColumn<long>(
                name: "Number",
                schema: "finanzas",
                table: "Facturas",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "UX_Facturas_Number",
                schema: "finanzas",
                table: "Facturas",
                column: "Number",
                unique: true,
                filter: "[Number] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropSequence(
                name: "Seq_FacturaNumero",
                schema: "finanzas");

            migrationBuilder.DropIndex(
                name: "UX_Facturas_Number",
                schema: "finanzas",
                table: "Facturas");

            migrationBuilder.DropColumn(
                name: "Number",
                schema: "finanzas",
                table: "Facturas");

            migrationBuilder.AddColumn<long>(
                    name: "Number",
                    schema: "finanzas",
                    table: "Facturas",
                    type: "bigint",
                    nullable: false,
                    defaultValue: 0L)
                .Annotation("SqlServer:Identity", "1, 1");

            migrationBuilder.CreateIndex(
                name: "UX_Facturas_Number",
                schema: "finanzas",
                table: "Facturas",
                column: "Number",
                unique: true);
        }
    }
}
