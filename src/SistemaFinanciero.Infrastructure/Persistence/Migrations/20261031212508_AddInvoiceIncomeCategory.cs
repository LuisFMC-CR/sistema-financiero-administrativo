using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SistemaFinanciero.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddInvoiceIncomeCategory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "IncomeCategoryId",
                schema: "finanzas",
                table: "Facturas",
                type: "uniqueidentifier",
                nullable: true);

            // Factura de contado: categoría del ingreso generado directamente por la factura.
            migrationBuilder.Sql(@"
UPDATE f
    SET f.IncomeCategoryId = i.CategoryId
FROM finanzas.Facturas f
INNER JOIN finanzas.Ingresos i ON i.InvoiceId = f.Id;");

            // Factura a crédito: categoría del ingreso del primer abono registrado sobre su cuenta por
            // cobrar (regla 2: la categoría ahora vive en la factura, no en el abono).
            migrationBuilder.Sql(@"
UPDATE f
    SET f.IncomeCategoryId = i.CategoryId
FROM finanzas.Facturas f
INNER JOIN finanzas.CuentasPorCobrar r ON r.InvoiceId = f.Id
INNER JOIN finanzas.Abonos ap ON ap.ReceivableId = r.Id
INNER JOIN finanzas.Ingresos i ON i.ReceivablePaymentId = ap.Id
WHERE f.IncomeCategoryId IS NULL;");

            // Facturas sin ningún ingreso todavía (borradores, o a crédito sin abonos): se asigna la
            // categoría de datos iniciales "Otros ingresos de la operación" (ING-005, regla 13) como
            // valor provisional de la migración de desarrollo; Finanzas la corrige manualmente desde
            // Editar encabezado si no corresponde a la factura real.
            migrationBuilder.Sql(@"
UPDATE finanzas.Facturas
    SET IncomeCategoryId = '20000000-0000-0000-0001-000000000005'
WHERE IncomeCategoryId IS NULL;");

            migrationBuilder.AlterColumn<Guid>(
                name: "IncomeCategoryId",
                schema: "finanzas",
                table: "Facturas",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Facturas_IncomeCategoryId",
                schema: "finanzas",
                table: "Facturas",
                column: "IncomeCategoryId");

            migrationBuilder.AddForeignKey(
                name: "FK_Facturas_CategoriasFinancieras",
                schema: "finanzas",
                table: "Facturas",
                column: "IncomeCategoryId",
                principalSchema: "catalogos",
                principalTable: "CategoriasFinancieras",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Facturas_CategoriasFinancieras",
                schema: "finanzas",
                table: "Facturas");

            migrationBuilder.DropIndex(
                name: "IX_Facturas_IncomeCategoryId",
                schema: "finanzas",
                table: "Facturas");

            migrationBuilder.DropColumn(
                name: "IncomeCategoryId",
                schema: "finanzas",
                table: "Facturas");
        }
    }
}
