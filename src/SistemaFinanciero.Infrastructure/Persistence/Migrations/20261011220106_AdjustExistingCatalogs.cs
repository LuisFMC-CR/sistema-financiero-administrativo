using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SistemaFinanciero.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AdjustExistingCatalogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Secuencias para los códigos automáticos (regla 12). Las de categoría arrancan después del
            // catálogo base de la migración SeedBaseChartOfAccounts (ING-001..005, GAS-001..010) para no
            // chocar con esos códigos ya existentes.
            migrationBuilder.CreateSequence(
                name: "Seq_ClienteCodigo",
                schema: "catalogos",
                startValue: 1L);

            migrationBuilder.CreateSequence(
                name: "Seq_ProveedorCodigo",
                schema: "catalogos",
                startValue: 1L);

            migrationBuilder.CreateSequence(
                name: "Seq_ProductoCodigo",
                schema: "catalogos",
                startValue: 1L);

            migrationBuilder.CreateSequence(
                name: "Seq_ServicioCodigo",
                schema: "catalogos",
                startValue: 1L);

            migrationBuilder.CreateSequence(
                name: "Seq_CategoriaIngresoCodigo",
                schema: "catalogos",
                startValue: 6L);

            migrationBuilder.CreateSequence(
                name: "Seq_CategoriaGastoCodigo",
                schema: "catalogos",
                startValue: 11L);

            migrationBuilder.DropIndex(
                name: "UX_Proveedores_Identification",
                schema: "catalogos",
                table: "Proveedores");

            migrationBuilder.DropIndex(
                name: "UX_Clientes_Identification",
                schema: "catalogos",
                table: "Clientes");

            migrationBuilder.AlterColumn<string>(
                name: "Identification",
                schema: "catalogos",
                table: "Proveedores",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EconomicActivity",
                schema: "catalogos",
                table: "Proveedores",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DefaultTaxTypeId",
                schema: "catalogos",
                table: "ProductosServicios",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Identification",
                schema: "catalogos",
                table: "Clientes",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "UX_Proveedores_Identification",
                schema: "catalogos",
                table: "Proveedores",
                column: "Identification",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductosServicios_DefaultTaxTypeId",
                schema: "catalogos",
                table: "ProductosServicios",
                column: "DefaultTaxTypeId");

            migrationBuilder.CreateIndex(
                name: "UX_Clientes_Identification",
                schema: "catalogos",
                table: "Clientes",
                column: "Identification",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_ProductosServicios_TiposImpuesto",
                schema: "catalogos",
                table: "ProductosServicios",
                column: "DefaultTaxTypeId",
                principalSchema: "catalogos",
                principalTable: "TiposImpuesto",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropSequence(
                name: "Seq_ClienteCodigo",
                schema: "catalogos");

            migrationBuilder.DropSequence(
                name: "Seq_ProveedorCodigo",
                schema: "catalogos");

            migrationBuilder.DropSequence(
                name: "Seq_ProductoCodigo",
                schema: "catalogos");

            migrationBuilder.DropSequence(
                name: "Seq_ServicioCodigo",
                schema: "catalogos");

            migrationBuilder.DropSequence(
                name: "Seq_CategoriaIngresoCodigo",
                schema: "catalogos");

            migrationBuilder.DropSequence(
                name: "Seq_CategoriaGastoCodigo",
                schema: "catalogos");

            migrationBuilder.DropForeignKey(
                name: "FK_ProductosServicios_TiposImpuesto",
                schema: "catalogos",
                table: "ProductosServicios");

            migrationBuilder.DropIndex(
                name: "UX_Proveedores_Identification",
                schema: "catalogos",
                table: "Proveedores");

            migrationBuilder.DropIndex(
                name: "IX_ProductosServicios_DefaultTaxTypeId",
                schema: "catalogos",
                table: "ProductosServicios");

            migrationBuilder.DropIndex(
                name: "UX_Clientes_Identification",
                schema: "catalogos",
                table: "Clientes");

            migrationBuilder.DropColumn(
                name: "EconomicActivity",
                schema: "catalogos",
                table: "Proveedores");

            migrationBuilder.DropColumn(
                name: "DefaultTaxTypeId",
                schema: "catalogos",
                table: "ProductosServicios");

            migrationBuilder.AlterColumn<string>(
                name: "Identification",
                schema: "catalogos",
                table: "Proveedores",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "Identification",
                schema: "catalogos",
                table: "Clientes",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50);

            migrationBuilder.CreateIndex(
                name: "UX_Proveedores_Identification",
                schema: "catalogos",
                table: "Proveedores",
                column: "Identification",
                unique: true,
                filter: "[Identification] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_Clientes_Identification",
                schema: "catalogos",
                table: "Clientes",
                column: "Identification",
                unique: true,
                filter: "[Identification] IS NOT NULL");
        }
    }
}
