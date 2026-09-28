using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SistemaFinanciero.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddInvoiceLinePosition : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Position",
                schema: "finanzas",
                table: "FacturaLineas",
                type: "int",
                nullable: false,
                defaultValue: 0);

            // Las líneas existentes reciben posiciones consecutivas por factura antes de crear la
            // restricción y el índice único. Sin datos previos esta instrucción no modifica nada.
            migrationBuilder.Sql(
                """
                UPDATE line
                SET line.[Position] = numbered.[Position]
                FROM [finanzas].[FacturaLineas] AS line
                INNER JOIN (
                    SELECT [Id], ROW_NUMBER() OVER (PARTITION BY [InvoiceId] ORDER BY [Id]) AS [Position]
                    FROM [finanzas].[FacturaLineas]
                ) AS numbered ON numbered.[Id] = line.[Id];
                """);

            migrationBuilder.CreateIndex(
                name: "UX_FacturaLineas_InvoiceId_Position",
                schema: "finanzas",
                table: "FacturaLineas",
                columns: new[] { "InvoiceId", "Position" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_FacturaLineas_Position",
                schema: "finanzas",
                table: "FacturaLineas",
                sql: "[Position] > 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_FacturaLineas_InvoiceId_Position",
                schema: "finanzas",
                table: "FacturaLineas");

            migrationBuilder.DropCheckConstraint(
                name: "CK_FacturaLineas_Position",
                schema: "finanzas",
                table: "FacturaLineas");

            migrationBuilder.DropColumn(
                name: "Position",
                schema: "finanzas",
                table: "FacturaLineas");
        }
    }
}
