using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SistemaFinanciero.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Agrega <c>TiposRetencion.MinimumBase</c> e <c>IsSystemType</c>, y siembra (idempotente) el tipo
    /// "Retención 2 % sector público" con la base mínima de un salario base de 2026 (bloque 1 del
    /// paquete v7, RF-95).
    /// </summary>
    /// <inheritdoc />
    public partial class AddWithholdingTypeMinimumBaseAndStateWithholding : Migration
    {
        private const string SystemUserId = "00000000-0000-0000-0000-000000000001";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsSystemType",
                schema: "catalogos",
                table: "TiposRetencion",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "MinimumBase",
                schema: "catalogos",
                table: "TiposRetencion",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "UX_TiposRetencion_IsSystemType",
                schema: "catalogos",
                table: "TiposRetencion",
                column: "IsSystemType",
                unique: true,
                filter: "[IsSystemType] = 1");

            migrationBuilder.AddCheckConstraint(
                name: "CK_TiposRetencion_MinimumBase",
                schema: "catalogos",
                table: "TiposRetencion",
                sql: "[MinimumBase] IS NULL OR [MinimumBase] >= 0");

            // Siembra la retención del 2 % del sector público (regla 3, RF-95), idempotente por código.
            migrationBuilder.Sql($"""
                IF NOT EXISTS (SELECT 1 FROM [catalogos].[TiposRetencion] WHERE [Code] = 'RET-2-SECTOR-PUBLICO')
                BEGIN
                    INSERT INTO [catalogos].[TiposRetencion]
                        ([Id], [Code], [Name], [Rate], [MinimumBase], [Description],
                         [IsActive], [IsSystemType], [CreatedAtUtc], [CreatedByUserId],
                         [UpdatedAtUtc], [UpdatedByUserId])
                    VALUES
                        (NEWID(), 'RET-2-SECTOR-PUBLICO', N'Retención 2 % sector público', 2, 462200, NULL,
                         1, 1, CAST(SYSUTCDATETIME() AS datetimeoffset), '{SystemUserId}',
                         CAST(SYSUTCDATETIME() AS datetimeoffset), '{SystemUserId}');
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM [catalogos].[TiposRetencion] WHERE [Code] = 'RET-2-SECTOR-PUBLICO';");

            migrationBuilder.DropIndex(
                name: "UX_TiposRetencion_IsSystemType",
                schema: "catalogos",
                table: "TiposRetencion");

            migrationBuilder.DropCheckConstraint(
                name: "CK_TiposRetencion_MinimumBase",
                schema: "catalogos",
                table: "TiposRetencion");

            migrationBuilder.DropColumn(
                name: "IsSystemType",
                schema: "catalogos",
                table: "TiposRetencion");

            migrationBuilder.DropColumn(
                name: "MinimumBase",
                schema: "catalogos",
                table: "TiposRetencion");
        }
    }
}
