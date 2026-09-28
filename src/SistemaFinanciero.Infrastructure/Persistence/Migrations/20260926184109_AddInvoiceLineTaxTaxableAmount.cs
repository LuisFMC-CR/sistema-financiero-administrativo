using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SistemaFinanciero.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddInvoiceLineTaxTaxableAmount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_FacturaLineaImpuestos_RateAmount",
                schema: "finanzas",
                table: "FacturaLineaImpuestos");

            migrationBuilder.AddColumn<decimal>(
                name: "TaxableAmount",
                schema: "finanzas",
                table: "FacturaLineaImpuestos",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddCheckConstraint(
                name: "CK_FacturaLineaImpuestos_RateAmount",
                schema: "finanzas",
                table: "FacturaLineaImpuestos",
                sql: "[Rate] >= 0 AND [TaxableAmount] >= 0 AND [Amount] >= 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_FacturaLineaImpuestos_RateAmount",
                schema: "finanzas",
                table: "FacturaLineaImpuestos");

            migrationBuilder.DropColumn(
                name: "TaxableAmount",
                schema: "finanzas",
                table: "FacturaLineaImpuestos");

            migrationBuilder.AddCheckConstraint(
                name: "CK_FacturaLineaImpuestos_RateAmount",
                schema: "finanzas",
                table: "FacturaLineaImpuestos",
                sql: "[Rate] >= 0 AND [Amount] >= 0");
        }
    }
}
