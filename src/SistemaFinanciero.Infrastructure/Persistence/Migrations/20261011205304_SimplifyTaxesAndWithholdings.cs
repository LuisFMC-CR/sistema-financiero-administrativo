using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SistemaFinanciero.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SimplifyTaxesAndWithholdings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FacturaLineaImpuestos",
                schema: "finanzas");

            migrationBuilder.DropCheckConstraint(
                name: "CK_TiposImpuesto_CalculationType",
                schema: "catalogos",
                table: "TiposImpuesto");

            migrationBuilder.DropCheckConstraint(
                name: "CK_TiposImpuesto_Rate",
                schema: "catalogos",
                table: "TiposImpuesto");

            migrationBuilder.DropColumn(
                name: "CalculationType",
                schema: "catalogos",
                table: "TiposImpuesto");

            migrationBuilder.AddColumn<Guid>(
                name: "TaxTypeId",
                schema: "finanzas",
                table: "FacturaLineas",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Tax_Amount",
                schema: "finanzas",
                table: "FacturaLineas",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "Tax_Code",
                schema: "finanzas",
                table: "FacturaLineas",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Tax_Name",
                schema: "finanzas",
                table: "FacturaLineas",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "Tax_Rate",
                schema: "finanzas",
                table: "FacturaLineas",
                type: "decimal(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "Tax_TaxableAmount",
                schema: "finanzas",
                table: "FacturaLineas",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddCheckConstraint(
                name: "CK_TiposImpuesto_Rate",
                schema: "catalogos",
                table: "TiposImpuesto",
                sql: "[Rate] >= 0 AND [Rate] <= 100");

            migrationBuilder.CreateIndex(
                name: "IX_FacturaLineas_TaxTypeId",
                schema: "finanzas",
                table: "FacturaLineas",
                column: "TaxTypeId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_FacturaLineas_Tax_RateAmount",
                schema: "finanzas",
                table: "FacturaLineas",
                sql: "[Tax_Rate] >= 0 AND [Tax_TaxableAmount] >= 0 AND [Tax_Amount] >= 0");

            migrationBuilder.AddForeignKey(
                name: "FK_FacturaLineas_TiposImpuesto",
                schema: "finanzas",
                table: "FacturaLineas",
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
                name: "FK_FacturaLineas_TiposImpuesto",
                schema: "finanzas",
                table: "FacturaLineas");

            migrationBuilder.DropCheckConstraint(
                name: "CK_TiposImpuesto_Rate",
                schema: "catalogos",
                table: "TiposImpuesto");

            migrationBuilder.DropIndex(
                name: "IX_FacturaLineas_TaxTypeId",
                schema: "finanzas",
                table: "FacturaLineas");

            migrationBuilder.DropCheckConstraint(
                name: "CK_FacturaLineas_Tax_RateAmount",
                schema: "finanzas",
                table: "FacturaLineas");

            migrationBuilder.DropColumn(
                name: "TaxTypeId",
                schema: "finanzas",
                table: "FacturaLineas");

            migrationBuilder.DropColumn(
                name: "Tax_Amount",
                schema: "finanzas",
                table: "FacturaLineas");

            migrationBuilder.DropColumn(
                name: "Tax_Code",
                schema: "finanzas",
                table: "FacturaLineas");

            migrationBuilder.DropColumn(
                name: "Tax_Name",
                schema: "finanzas",
                table: "FacturaLineas");

            migrationBuilder.DropColumn(
                name: "Tax_Rate",
                schema: "finanzas",
                table: "FacturaLineas");

            migrationBuilder.DropColumn(
                name: "Tax_TaxableAmount",
                schema: "finanzas",
                table: "FacturaLineas");

            migrationBuilder.AddColumn<int>(
                name: "CalculationType",
                schema: "catalogos",
                table: "TiposImpuesto",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "FacturaLineaImpuestos",
                schema: "finanzas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CalculationType = table.Column<int>(type: "int", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    InvoiceLineId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Rate = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    TaxTypeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TaxableAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FacturaLineaImpuestos", x => x.Id);
                    table.CheckConstraint("CK_FacturaLineaImpuestos_RateAmount", "[Rate] >= 0 AND [TaxableAmount] >= 0 AND [Amount] >= 0");
                    table.CheckConstraint("CK_FacturaLineaImpuestos_Type", "[CalculationType] IN (1, 2)");
                    table.ForeignKey(
                        name: "FK_FacturaLineaImpuestos_FacturaLineas",
                        column: x => x.InvoiceLineId,
                        principalSchema: "finanzas",
                        principalTable: "FacturaLineas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FacturaLineaImpuestos_TiposImpuesto",
                        column: x => x.TaxTypeId,
                        principalSchema: "catalogos",
                        principalTable: "TiposImpuesto",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_TiposImpuesto_CalculationType",
                schema: "catalogos",
                table: "TiposImpuesto",
                sql: "[CalculationType] IN (1, 2)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_TiposImpuesto_Rate",
                schema: "catalogos",
                table: "TiposImpuesto",
                sql: "[Rate] >= 0 AND ([CalculationType] <> 1 OR [Rate] <= 100)");

            migrationBuilder.CreateIndex(
                name: "IX_FacturaLineaImpuestos_TaxTypeId",
                schema: "finanzas",
                table: "FacturaLineaImpuestos",
                column: "TaxTypeId");

            migrationBuilder.CreateIndex(
                name: "UX_FacturaLineaImpuestos_Line_Code",
                schema: "finanzas",
                table: "FacturaLineaImpuestos",
                columns: new[] { "InvoiceLineId", "Code" },
                unique: true);
        }
    }
}
