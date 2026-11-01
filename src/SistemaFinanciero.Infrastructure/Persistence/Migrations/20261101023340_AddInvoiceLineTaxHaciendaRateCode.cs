using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SistemaFinanciero.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddInvoiceLineTaxHaciendaRateCode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Tax_HaciendaRateCode",
                schema: "finanzas",
                table: "FacturaLineas",
                type: "nvarchar(2)",
                maxLength: 2,
                nullable: true);

            // Fotografía retroactiva: cada línea toma el código de Hacienda que su tipo de impuesto
            // tiene hoy; las líneas sin tipo de impuesto vinculado (TaxTypeId nulo) caen en "99", sin
            // clasificar.
            migrationBuilder.Sql("""
                UPDATE fl
                    SET fl.Tax_HaciendaRateCode = ti.HaciendaRateCode
                FROM finanzas.FacturaLineas fl
                INNER JOIN catalogos.TiposImpuesto ti ON ti.Id = fl.TaxTypeId
                WHERE fl.Tax_HaciendaRateCode IS NULL;
                """);
            migrationBuilder.Sql("""
                UPDATE finanzas.FacturaLineas
                    SET Tax_HaciendaRateCode = N'99'
                WHERE Tax_HaciendaRateCode IS NULL;
                """);

            migrationBuilder.AlterColumn<string>(
                name: "Tax_HaciendaRateCode",
                schema: "finanzas",
                table: "FacturaLineas",
                type: "nvarchar(2)",
                maxLength: 2,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(2)",
                oldMaxLength: 2,
                oldNullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_FacturaLineas_Tax_HaciendaRateCode",
                schema: "finanzas",
                table: "FacturaLineas",
                sql: "LEN([Tax_HaciendaRateCode]) = 2");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_FacturaLineas_Tax_HaciendaRateCode",
                schema: "finanzas",
                table: "FacturaLineas");

            migrationBuilder.DropColumn(
                name: "Tax_HaciendaRateCode",
                schema: "finanzas",
                table: "FacturaLineas");
        }
    }
}
