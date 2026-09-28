using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SistemaFinanciero.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddInvoiceLineTaxTaxTypeId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "TaxTypeId",
                schema: "finanzas",
                table: "FacturaLineaImpuestos",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_FacturaLineaImpuestos_TaxTypeId",
                schema: "finanzas",
                table: "FacturaLineaImpuestos",
                column: "TaxTypeId");

            migrationBuilder.AddForeignKey(
                name: "FK_FacturaLineaImpuestos_TiposImpuesto",
                schema: "finanzas",
                table: "FacturaLineaImpuestos",
                column: "TaxTypeId",
                principalSchema: "catalogos",
                principalTable: "TiposImpuesto",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FacturaLineaImpuestos_TiposImpuesto",
                schema: "finanzas",
                table: "FacturaLineaImpuestos");

            migrationBuilder.DropIndex(
                name: "IX_FacturaLineaImpuestos_TaxTypeId",
                schema: "finanzas",
                table: "FacturaLineaImpuestos");

            migrationBuilder.DropColumn(
                name: "TaxTypeId",
                schema: "finanzas",
                table: "FacturaLineaImpuestos");
        }
    }
}
