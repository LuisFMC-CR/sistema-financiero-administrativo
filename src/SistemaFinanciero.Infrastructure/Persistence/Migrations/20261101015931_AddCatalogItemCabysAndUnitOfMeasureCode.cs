using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SistemaFinanciero.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCatalogItemCabysAndUnitOfMeasureCode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Texto existente -> UnitOfMeasureCode (1 Unidad, 2 Hora, 3 Servicios profesionales,
            // 4 Otros servicios), antes de cambiar el tipo de columna, para no depender de que SQL
            // Server convierta implícitamente el texto a entero. El único dato real de desarrollo con
            // texto reconocible es "SRV-0053" ("Servicio técnico"), que se interpreta como Servicios
            // profesionales; cualquier otro valor (incluido "1", un dato de prueba sin significado) cae
            // en Unidad.
            migrationBuilder.Sql("""
                UPDATE catalogos.ProductosServicios
                    SET UnitOfMeasure = N'1'
                WHERE UnitOfMeasure NOT IN (N'1', N'2', N'3', N'4');
                """);
            migrationBuilder.Sql("""
                UPDATE catalogos.ProductosServicios
                    SET UnitOfMeasure = N'3'
                WHERE Code = N'SRV-0053';
                """);

            migrationBuilder.AlterColumn<int>(
                name: "UnitOfMeasure",
                schema: "catalogos",
                table: "ProductosServicios",
                type: "int",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(30)",
                oldMaxLength: 30);

            migrationBuilder.AddColumn<string>(
                name: "CabysCode",
                schema: "catalogos",
                table: "ProductosServicios",
                type: "nvarchar(13)",
                maxLength: 13,
                nullable: true);

            // Dato provisional (13 ceros) para los artículos existentes sin CABYS real; Finanzas lo
            // completa después editando el artículo con el código real.
            migrationBuilder.Sql("""
                UPDATE catalogos.ProductosServicios
                    SET CabysCode = N'0000000000000'
                WHERE CabysCode IS NULL;
                """);

            migrationBuilder.AlterColumn<string>(
                name: "CabysCode",
                schema: "catalogos",
                table: "ProductosServicios",
                type: "nvarchar(13)",
                maxLength: 13,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(13)",
                oldMaxLength: 13,
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CabysCode",
                schema: "finanzas",
                table: "FacturaLineas",
                type: "nvarchar(13)",
                maxLength: 13,
                nullable: true);

            // Fotografía retroactiva: las líneas ya guardadas copian el CABYS (provisional o real) que
            // su artículo del catálogo tiene en este momento de la migración.
            migrationBuilder.Sql("""
                UPDATE fl
                    SET fl.CabysCode = ps.CabysCode
                FROM finanzas.FacturaLineas fl
                INNER JOIN catalogos.ProductosServicios ps ON ps.Id = fl.CatalogItemId
                WHERE fl.CabysCode IS NULL;
                """);

            migrationBuilder.AddCheckConstraint(
                name: "CK_ProductosServicios_CabysCode",
                schema: "catalogos",
                table: "ProductosServicios",
                sql: "LEN([CabysCode]) = 13");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ProductosServicios_UnitOfMeasure",
                schema: "catalogos",
                table: "ProductosServicios",
                sql: "[UnitOfMeasure] IN (1, 2, 3, 4)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_ProductosServicios_CabysCode",
                schema: "catalogos",
                table: "ProductosServicios");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ProductosServicios_UnitOfMeasure",
                schema: "catalogos",
                table: "ProductosServicios");

            migrationBuilder.DropColumn(
                name: "CabysCode",
                schema: "catalogos",
                table: "ProductosServicios");

            migrationBuilder.DropColumn(
                name: "CabysCode",
                schema: "finanzas",
                table: "FacturaLineas");

            migrationBuilder.AlterColumn<string>(
                name: "UnitOfMeasure",
                schema: "catalogos",
                table: "ProductosServicios",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");
        }
    }
}
