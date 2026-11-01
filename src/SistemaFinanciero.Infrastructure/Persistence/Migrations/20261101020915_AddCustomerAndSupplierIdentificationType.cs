using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SistemaFinanciero.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomerAndSupplierIdentificationType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "IdentificationType",
                schema: "catalogos",
                table: "Proveedores",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "IdentificationType",
                schema: "catalogos",
                table: "Clientes",
                type: "int",
                nullable: true);

            // Migración de datos existentes: tipo deducido de la longitud del número ya guardado
            // (regla pedida por el bloque 3). 9 dígitos -> Física (1), 10 u 11-12 -> Jurídica (2) o
            // DIMEX (3); cualquier otra longitud (dato de prueba sin un tipo real que le corresponda,
            // ej. identificaciones de 8 dígitos) cae en Jurídica (2) como caso dudoso, porque los
            // nombres de esos registros de desarrollo son de empresas, no de personas.
            migrationBuilder.Sql("""
                UPDATE catalogos.Clientes
                    SET IdentificationType = CASE LEN(Identification)
                        WHEN 9 THEN 1
                        WHEN 11 THEN 3
                        WHEN 12 THEN 3
                        ELSE 2
                    END;
                """);
            migrationBuilder.Sql("""
                UPDATE catalogos.Proveedores
                    SET IdentificationType = CASE LEN(Identification)
                        WHEN 9 THEN 1
                        WHEN 11 THEN 3
                        WHEN 12 THEN 3
                        ELSE 2
                    END;
                """);

            migrationBuilder.AlterColumn<int>(
                name: "IdentificationType",
                schema: "catalogos",
                table: "Proveedores",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "IdentificationType",
                schema: "catalogos",
                table: "Clientes",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Proveedores_IdentificationType",
                schema: "catalogos",
                table: "Proveedores",
                sql: "[IdentificationType] IN (1, 2, 3, 4, 5)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Clientes_IdentificationType",
                schema: "catalogos",
                table: "Clientes",
                sql: "[IdentificationType] IN (1, 2, 3, 4, 5)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Proveedores_IdentificationType",
                schema: "catalogos",
                table: "Proveedores");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Clientes_IdentificationType",
                schema: "catalogos",
                table: "Clientes");

            migrationBuilder.DropColumn(
                name: "IdentificationType",
                schema: "catalogos",
                table: "Proveedores");

            migrationBuilder.DropColumn(
                name: "IdentificationType",
                schema: "catalogos",
                table: "Clientes");
        }
    }
}
