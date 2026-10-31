using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SistemaFinanciero.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Siembra las tarifas de IVA de referencia (13 % y Exento, regla 13 y <c>ReferenceTaxTypes</c>) por
    /// migración, en vez de depender solo del botón manual "Cargar tarifas de referencia". Idempotente:
    /// no inserta un código que ya exista, sin importar quién lo haya creado.
    /// </summary>
    /// <remarks>
    /// Usa el mismo usuario técnico "Sistema" que <c>SeedBaseChartOfAccounts</c> (ya existe en
    /// <c>seguridad.Usuarios</c> para entonces, porque esa migración corre antes). <c>Down</c> solo
    /// retira las filas que esta migración creó (identificadas por ese usuario como autor), nunca las
    /// que alguien haya cargado manualmente con el mismo código.
    /// </remarks>
    public partial class SeedReferenceTaxTypes : Migration
    {
        private const string SystemUserId = "00000000-0000-0000-0000-000000000001";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql($"""
                IF NOT EXISTS (SELECT 1 FROM [catalogos].[TiposImpuesto] WHERE [Code] = N'IVA-13')
                INSERT INTO [catalogos].[TiposImpuesto]
                    ([Id], [Code], [Name], [Rate], [Description], [IsActive],
                     [CreatedAtUtc], [CreatedByUserId], [UpdatedAtUtc], [UpdatedByUserId])
                VALUES
                    (NEWID(), N'IVA-13', N'IVA tarifa general 13 %', 13.0000, NULL, 1,
                     SYSUTCDATETIME(), '{SystemUserId}', SYSUTCDATETIME(), '{SystemUserId}');
                """);

            migrationBuilder.Sql($"""
                IF NOT EXISTS (SELECT 1 FROM [catalogos].[TiposImpuesto] WHERE [Code] = N'IVA-EXENTO')
                INSERT INTO [catalogos].[TiposImpuesto]
                    ([Id], [Code], [Name], [Rate], [Description], [IsActive],
                     [CreatedAtUtc], [CreatedByUserId], [UpdatedAtUtc], [UpdatedByUserId])
                VALUES
                    (NEWID(), N'IVA-EXENTO', N'IVA exento', 0.0000, NULL, 1,
                     SYSUTCDATETIME(), '{SystemUserId}', SYSUTCDATETIME(), '{SystemUserId}');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "DELETE FROM [catalogos].[TiposImpuesto] " +
                "WHERE [CreatedByUserId] = '" + SystemUserId + "' AND [Code] IN (N'IVA-13', N'IVA-EXENTO');");
        }
    }
}
