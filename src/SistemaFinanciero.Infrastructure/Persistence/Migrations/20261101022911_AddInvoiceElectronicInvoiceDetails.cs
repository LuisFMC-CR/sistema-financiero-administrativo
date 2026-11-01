using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SistemaFinanciero.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddInvoiceElectronicInvoiceDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ElectronicInvoiceKey",
                schema: "finanzas",
                table: "Facturas",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ElectronicInvoiceNumber",
                schema: "finanzas",
                table: "Facturas",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "UX_Facturas_ElectronicInvoiceKey",
                schema: "finanzas",
                table: "Facturas",
                column: "ElectronicInvoiceKey",
                unique: true,
                filter: "[ElectronicInvoiceKey] IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Facturas_ElectronicInvoiceKey",
                schema: "finanzas",
                table: "Facturas",
                sql: "[ElectronicInvoiceKey] IS NULL OR LEN([ElectronicInvoiceKey]) = 50");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Facturas_ElectronicInvoiceNumber",
                schema: "finanzas",
                table: "Facturas",
                sql: "[ElectronicInvoiceNumber] IS NULL OR LEN([ElectronicInvoiceNumber]) = 20");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_Facturas_ElectronicInvoiceKey",
                schema: "finanzas",
                table: "Facturas");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Facturas_ElectronicInvoiceKey",
                schema: "finanzas",
                table: "Facturas");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Facturas_ElectronicInvoiceNumber",
                schema: "finanzas",
                table: "Facturas");

            migrationBuilder.DropColumn(
                name: "ElectronicInvoiceKey",
                schema: "finanzas",
                table: "Facturas");

            migrationBuilder.DropColumn(
                name: "ElectronicInvoiceNumber",
                schema: "finanzas",
                table: "Facturas");
        }
    }
}
