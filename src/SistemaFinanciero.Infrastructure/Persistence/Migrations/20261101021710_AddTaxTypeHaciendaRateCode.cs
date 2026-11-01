using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SistemaFinanciero.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTaxTypeHaciendaRateCode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "HaciendaRateCode",
                schema: "catalogos",
                table: "TiposImpuesto",
                type: "nvarchar(2)",
                maxLength: 2,
                nullable: true);

            // Migración de datos existentes: código deducido de la tarifa (08 tarifa general 13 %,
            // confirmado; 01 Exento, pendiente de validar contra el Anexo 4.4 de Hacienda, igual que
            // ReferenceTaxTypes). Cualquier otra tarifa (ninguna hoy) cae en 99, sin clasificar, hasta
            // que Finanzas la corrija manualmente.
            migrationBuilder.Sql("""
                UPDATE catalogos.TiposImpuesto
                    SET HaciendaRateCode = CASE
                        WHEN Rate = 13 THEN N'08'
                        WHEN Rate = 0 THEN N'01'
                        ELSE N'99'
                    END;
                """);

            migrationBuilder.AlterColumn<string>(
                name: "HaciendaRateCode",
                schema: "catalogos",
                table: "TiposImpuesto",
                type: "nvarchar(2)",
                maxLength: 2,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(2)",
                oldMaxLength: 2,
                oldNullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_TiposImpuesto_HaciendaRateCode",
                schema: "catalogos",
                table: "TiposImpuesto",
                sql: "LEN([HaciendaRateCode]) = 2");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_TiposImpuesto_HaciendaRateCode",
                schema: "catalogos",
                table: "TiposImpuesto");

            migrationBuilder.DropColumn(
                name: "HaciendaRateCode",
                schema: "catalogos",
                table: "TiposImpuesto");
        }
    }
}
