using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SistemaFinanciero.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Código propio por tipo de documento (regla 12 ampliada): abonos y pagos ganan su columna
    /// <c>Code</c> (<c>ABO-000001</c>, <c>PAG-000001</c>), que antes no tenían; ingresos y gastos dejan
    /// de compartir prefijo con sus categorías (<c>ING-001</c>/<c>GAS-001</c> pasan a
    /// <c>REC-000001</c>/<c>GTO-000001</c>). Los documentos ya existentes se numeran en el orden en que
    /// se crearon (<c>CreatedAtUtc</c>); las secuencias de ingresos y gastos no se reinician, porque ya
    /// seguían el conteo correcto y solo cambia el formato de salida (prefijo y relleno) que decide la
    /// aplicación, no la secuencia de SQL Server.
    /// </summary>
    /// <inheritdoc />
    public partial class AddPaymentCodesAndRenameDocumentPrefixes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Code",
                schema: "finanzas",
                table: "Pagos",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Code",
                schema: "finanzas",
                table: "Abonos",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateSequence(
                name: "Seq_PagoCodigo",
                schema: "finanzas",
                startValue: 1L);

            migrationBuilder.CreateSequence(
                name: "Seq_AbonoCodigo",
                schema: "finanzas",
                startValue: 1L);

            // Numera los pagos y abonos ya existentes en el orden en que se crearon, y arranca cada
            // secuencia después del último número asignado (igual criterio que la migración original
            // de códigos de ingresos y gastos, AddExpensesPayablesAndIncomeCode).
            migrationBuilder.Sql(
                """
                UPDATE p
                SET p.Code = 'PAG-' + RIGHT('00000' + CAST(seq.RowNum AS VARCHAR(10)), 6)
                FROM finanzas.Pagos p
                INNER JOIN (
                    SELECT Id, ROW_NUMBER() OVER (ORDER BY CreatedAtUtc) AS RowNum
                    FROM finanzas.Pagos
                ) seq ON seq.Id = p.Id;

                DECLARE @NextPagoSeq BIGINT = (SELECT COUNT(*) FROM finanzas.Pagos) + 1;
                DECLARE @RestartPagoSql NVARCHAR(200) =
                    N'ALTER SEQUENCE finanzas.Seq_PagoCodigo RESTART WITH ' + CAST(@NextPagoSeq AS NVARCHAR(10));
                EXEC sp_executesql @RestartPagoSql;
                """);

            migrationBuilder.Sql(
                """
                UPDATE a
                SET a.Code = 'ABO-' + RIGHT('00000' + CAST(seq.RowNum AS VARCHAR(10)), 6)
                FROM finanzas.Abonos a
                INNER JOIN (
                    SELECT Id, ROW_NUMBER() OVER (ORDER BY CreatedAtUtc) AS RowNum
                    FROM finanzas.Abonos
                ) seq ON seq.Id = a.Id;

                DECLARE @NextAbonoSeq BIGINT = (SELECT COUNT(*) FROM finanzas.Abonos) + 1;
                DECLARE @RestartAbonoSql NVARCHAR(200) =
                    N'ALTER SEQUENCE finanzas.Seq_AbonoCodigo RESTART WITH ' + CAST(@NextAbonoSeq AS NVARCHAR(10));
                EXEC sp_executesql @RestartAbonoSql;
                """);

            // Ingresos y gastos ya tenían código (ING-001, GAS-001); se renumeran al nuevo prefijo y
            // relleno en el mismo orden de creación que ya tenían, sin tocar sus secuencias.
            migrationBuilder.Sql(
                """
                UPDATE i
                SET i.Code = 'REC-' + RIGHT('00000' + CAST(seq.RowNum AS VARCHAR(10)), 6)
                FROM finanzas.Ingresos i
                INNER JOIN (
                    SELECT Id, ROW_NUMBER() OVER (ORDER BY CreatedAtUtc) AS RowNum
                    FROM finanzas.Ingresos
                ) seq ON seq.Id = i.Id;
                """);

            migrationBuilder.Sql(
                """
                UPDATE g
                SET g.Code = 'GTO-' + RIGHT('00000' + CAST(seq.RowNum AS VARCHAR(10)), 6)
                FROM finanzas.Gastos g
                INNER JOIN (
                    SELECT Id, ROW_NUMBER() OVER (ORDER BY CreatedAtUtc) AS RowNum
                    FROM finanzas.Gastos
                ) seq ON seq.Id = g.Id;
                """);

            migrationBuilder.CreateIndex(
                name: "UX_Pagos_Code",
                schema: "finanzas",
                table: "Pagos",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_Abonos_Code",
                schema: "finanzas",
                table: "Abonos",
                column: "Code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_Pagos_Code",
                schema: "finanzas",
                table: "Pagos");

            migrationBuilder.DropIndex(
                name: "UX_Abonos_Code",
                schema: "finanzas",
                table: "Abonos");

            migrationBuilder.DropSequence(
                name: "Seq_PagoCodigo",
                schema: "finanzas");

            migrationBuilder.DropSequence(
                name: "Seq_AbonoCodigo",
                schema: "finanzas");

            migrationBuilder.DropColumn(
                name: "Code",
                schema: "finanzas",
                table: "Pagos");

            migrationBuilder.DropColumn(
                name: "Code",
                schema: "finanzas",
                table: "Abonos");
        }
    }
}
