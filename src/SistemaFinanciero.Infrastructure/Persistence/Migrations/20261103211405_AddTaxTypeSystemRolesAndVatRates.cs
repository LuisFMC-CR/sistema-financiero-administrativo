using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SistemaFinanciero.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Agrega <c>TiposImpuesto.SystemRole</c> (bloque 1 del paquete v7), para que las reglas del
    /// sistema identifiquen las tarifas reservadas por su rol y no por código ni nombre (editables).
    /// Corrige el código de Hacienda mal asignado del Exento (01 "Tarifa 0 %" → 10 "Tarifa Exenta") en
    /// el catálogo y en la fotografía de las líneas de factura ya confirmadas, siembra IVA 2 % y No
    /// sujeto, y asigna el rol de sistema a las cuatro tarifas reservadas.
    /// </summary>
    /// <remarks>
    /// El código de Hacienda de "No sujeto" queda en blanco (bloque 1, punto 2 del documento): el
    /// catálogo "Código de tarifa de IVA" del Anexo 4.4 no tiene una entrada para ella; queda pendiente
    /// de validar contra el documento oficial (ADR-0046).
    /// </remarks>
    /// <inheritdoc />
    public partial class AddTaxTypeSystemRolesAndVatRates : Migration
    {
        private const string SystemUserId = "00000000-0000-0000-0000-000000000001";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_TiposImpuesto_HaciendaRateCode",
                schema: "catalogos",
                table: "TiposImpuesto");

            migrationBuilder.DropCheckConstraint(
                name: "CK_FacturaLineas_Tax_HaciendaRateCode",
                schema: "finanzas",
                table: "FacturaLineas");

            migrationBuilder.AlterColumn<string>(
                name: "HaciendaRateCode",
                schema: "catalogos",
                table: "TiposImpuesto",
                type: "nvarchar(2)",
                maxLength: 2,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(2)",
                oldMaxLength: 2);

            migrationBuilder.AddColumn<int>(
                name: "SystemRole",
                schema: "catalogos",
                table: "TiposImpuesto",
                type: "int",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Tax_HaciendaRateCode",
                schema: "finanzas",
                table: "FacturaLineas",
                type: "nvarchar(2)",
                maxLength: 2,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(2)",
                oldMaxLength: 2);

            migrationBuilder.CreateIndex(
                name: "UX_TiposImpuesto_SystemRole",
                schema: "catalogos",
                table: "TiposImpuesto",
                column: "SystemRole",
                unique: true,
                filter: "[SystemRole] IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_TiposImpuesto_HaciendaRateCode",
                schema: "catalogos",
                table: "TiposImpuesto",
                sql: "[HaciendaRateCode] IS NULL OR LEN([HaciendaRateCode]) = 2");

            migrationBuilder.AddCheckConstraint(
                name: "CK_TiposImpuesto_SystemRole",
                schema: "catalogos",
                table: "TiposImpuesto",
                sql: "[SystemRole] IS NULL OR [SystemRole] IN (1, 2, 3, 4)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_FacturaLineas_Tax_HaciendaRateCode",
                schema: "finanzas",
                table: "FacturaLineas",
                sql: "[Tax_HaciendaRateCode] IS NULL OR LEN([Tax_HaciendaRateCode]) = 2");

            // Corrige el código mal asignado del Exento: "01" es la tasa cero (fuera de alcance,
            // regla 3), no el Exento. No es un cambio de tarifa, solo de su código de Hacienda.
            // Restringido a la tarifa activa: alguna base de prueba puede tener filas sueltas e
            // inactivas con el mismo código que no son el Exento real (regla de baja lógica: no se
            // eliminan, pero tampoco deben confundirse con la tarifa de sistema).
            migrationBuilder.Sql(
                "UPDATE [catalogos].[TiposImpuesto] SET [HaciendaRateCode] = '10' " +
                "WHERE [HaciendaRateCode] = '01' AND [IsActive] = 1;");
            migrationBuilder.Sql(
                "UPDATE [finanzas].[FacturaLineas] SET [Tax_HaciendaRateCode] = '10' " +
                "WHERE [Tax_HaciendaRateCode] = '01';");

            // Backfill del rol de sistema de las dos tarifas existentes, identificadas por tarifa y
            // código de Hacienda (nunca por código ni nombre, que son editables) y restringido a la
            // fila activa, para no chocar con filas inactivas sueltas que compartan el mismo valor.
            migrationBuilder.Sql(
                "UPDATE [catalogos].[TiposImpuesto] SET [SystemRole] = 1 " +
                "WHERE [Rate] = 13 AND [HaciendaRateCode] = '08' AND [IsActive] = 1 AND [SystemRole] IS NULL;");
            migrationBuilder.Sql(
                "UPDATE [catalogos].[TiposImpuesto] SET [SystemRole] = 2 " +
                "WHERE [Rate] = 0 AND [HaciendaRateCode] = '10' AND [IsActive] = 1 AND [SystemRole] IS NULL;");

            // Siembra IVA 2 % y No sujeto (regla 13, RF-54), idempotente por código.
            migrationBuilder.Sql($"""
                IF NOT EXISTS (SELECT 1 FROM [catalogos].[TiposImpuesto] WHERE [Code] = 'IVA-2')
                BEGIN
                    INSERT INTO [catalogos].[TiposImpuesto]
                        ([Id], [Code], [Name], [Rate], [HaciendaRateCode], [Description],
                         [IsActive], [SystemRole], [CreatedAtUtc], [CreatedByUserId],
                         [UpdatedAtUtc], [UpdatedByUserId])
                    VALUES
                        (NEWID(), 'IVA-2', N'IVA tarifa reducida 2 %', 2, '03', NULL,
                         1, 3, CAST(SYSUTCDATETIME() AS datetimeoffset), '{SystemUserId}',
                         CAST(SYSUTCDATETIME() AS datetimeoffset), '{SystemUserId}');
                END;
                """);
            migrationBuilder.Sql($"""
                IF NOT EXISTS (SELECT 1 FROM [catalogos].[TiposImpuesto] WHERE [Code] = 'NO-SUJETO')
                BEGIN
                    INSERT INTO [catalogos].[TiposImpuesto]
                        ([Id], [Code], [Name], [Rate], [HaciendaRateCode], [Description],
                         [IsActive], [SystemRole], [CreatedAtUtc], [CreatedByUserId],
                         [UpdatedAtUtc], [UpdatedByUserId])
                    VALUES
                        (NEWID(), 'NO-SUJETO', N'No sujeto', 0, NULL, NULL,
                         1, 4, CAST(SYSUTCDATETIME() AS datetimeoffset), '{SystemUserId}',
                         CAST(SYSUTCDATETIME() AS datetimeoffset), '{SystemUserId}');
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM [catalogos].[TiposImpuesto] WHERE [Code] IN ('IVA-2', 'NO-SUJETO');");
            migrationBuilder.Sql(
                "UPDATE [catalogos].[TiposImpuesto] SET [HaciendaRateCode] = '01' " +
                "WHERE [Rate] = 0 AND [HaciendaRateCode] = '10' AND [SystemRole] = 2;");
            migrationBuilder.Sql(
                "UPDATE [finanzas].[FacturaLineas] SET [Tax_HaciendaRateCode] = '01' " +
                "WHERE [Tax_HaciendaRateCode] = '10';");

            migrationBuilder.DropIndex(
                name: "UX_TiposImpuesto_SystemRole",
                schema: "catalogos",
                table: "TiposImpuesto");

            migrationBuilder.DropCheckConstraint(
                name: "CK_TiposImpuesto_HaciendaRateCode",
                schema: "catalogos",
                table: "TiposImpuesto");

            migrationBuilder.DropCheckConstraint(
                name: "CK_TiposImpuesto_SystemRole",
                schema: "catalogos",
                table: "TiposImpuesto");

            migrationBuilder.DropCheckConstraint(
                name: "CK_FacturaLineas_Tax_HaciendaRateCode",
                schema: "finanzas",
                table: "FacturaLineas");

            migrationBuilder.DropColumn(
                name: "SystemRole",
                schema: "catalogos",
                table: "TiposImpuesto");

            migrationBuilder.AlterColumn<string>(
                name: "HaciendaRateCode",
                schema: "catalogos",
                table: "TiposImpuesto",
                type: "nvarchar(2)",
                maxLength: 2,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(2)",
                oldMaxLength: 2,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Tax_HaciendaRateCode",
                schema: "finanzas",
                table: "FacturaLineas",
                type: "nvarchar(2)",
                maxLength: 2,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(2)",
                oldMaxLength: 2,
                oldNullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_TiposImpuesto_HaciendaRateCode",
                schema: "catalogos",
                table: "TiposImpuesto",
                sql: "LEN([HaciendaRateCode]) = 2");

            migrationBuilder.AddCheckConstraint(
                name: "CK_FacturaLineas_Tax_HaciendaRateCode",
                schema: "finanzas",
                table: "FacturaLineas",
                sql: "LEN([Tax_HaciendaRateCode]) = 2");
        }
    }
}
