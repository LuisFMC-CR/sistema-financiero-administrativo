using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SistemaFinanciero.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FiscalPeriodAndMonthlyAccountingPeriods : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "FiscalPeriodEndDate",
                schema: "finanzas",
                table: "ParametrosSistema",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "FiscalPeriodStartDate",
                schema: "finanzas",
                table: "ParametrosSistema",
                type: "date",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PeriodoFiscalHistorial",
                schema: "finanzas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PreviousStartDate = table.Column<DateOnly>(type: "date", nullable: true),
                    PreviousEndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    NewStartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    NewEndDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    ChangedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ChangedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PeriodoFiscalHistorial", x => x.Id);
                    table.CheckConstraint("CK_PeriodoFiscalHistorial_Range", "([PreviousStartDate] IS NULL AND [PreviousEndDate] IS NULL OR [PreviousStartDate] IS NOT NULL AND DAY([PreviousStartDate]) = 1 AND [PreviousEndDate] = DATEADD(day, -1, DATEADD(year, 1, [PreviousStartDate]))) AND DAY([NewStartDate]) = 1 AND [NewEndDate] = DATEADD(day, -1, DATEADD(year, 1, [NewStartDate]))");
                    table.ForeignKey(
                        name: "FK_PeriodoFiscalHistorial_Usuarios_ChangedBy",
                        column: x => x.ChangedByUserId,
                        principalSchema: "seguridad",
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PeriodosContablesHistorial",
                schema: "finanzas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Year = table.Column<int>(type: "int", nullable: false),
                    Month = table.Column<int>(type: "int", nullable: false),
                    WasOpen = table.Column<bool>(type: "bit", nullable: false),
                    IsOpen = table.Column<bool>(type: "bit", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    ChangedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ChangedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PeriodosContablesHistorial", x => x.Id);
                    table.CheckConstraint("CK_PeriodosContablesHistorial_Month", "[Month] BETWEEN 1 AND 12");
                    table.CheckConstraint("CK_PeriodosContablesHistorial_StateChanged", "[WasOpen] <> [IsOpen]");
                    table.ForeignKey(
                        name: "FK_PeriodosContablesHistorial_Usuarios_ChangedBy",
                        column: x => x.ChangedByUserId,
                        principalSchema: "seguridad",
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PeriodosContablesReabiertos",
                schema: "finanzas",
                columns: table => new
                {
                    Year = table.Column<int>(type: "int", nullable: false),
                    Month = table.Column<int>(type: "int", nullable: false),
                    IsOpen = table.Column<bool>(type: "bit", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PeriodosContablesReabiertos", x => new { x.Year, x.Month });
                    table.CheckConstraint("CK_PeriodosContablesReabiertos_Month", "[Month] BETWEEN 1 AND 12 AND [Year] BETWEEN 1 AND 9998");
                    table.ForeignKey(
                        name: "FK_PeriodosContablesReabiertos_Usuarios_CreatedBy",
                        column: x => x.CreatedByUserId,
                        principalSchema: "seguridad",
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PeriodosContablesReabiertos_Usuarios_UpdatedBy",
                        column: x => x.UpdatedByUserId,
                        principalSchema: "seguridad",
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_ParametrosSistema_FiscalPeriod",
                schema: "finanzas",
                table: "ParametrosSistema",
                sql: "([FiscalPeriodStartDate] IS NULL AND [FiscalPeriodEndDate] IS NULL) OR ([FiscalPeriodStartDate] IS NOT NULL AND DAY([FiscalPeriodStartDate]) = 1 AND [FiscalPeriodEndDate] = DATEADD(day, -1, DATEADD(year, 1, [FiscalPeriodStartDate])))");

            migrationBuilder.Sql(
                """
                DECLARE @FiscalPeriodDefaults TABLE
                (
                    [ChangedAtUtc] datetimeoffset NOT NULL,
                    [ChangedByUserId] uniqueidentifier NOT NULL
                );

                UPDATE [finanzas].[ParametrosSistema]
                SET [FiscalPeriodStartDate] = '2026-01-01',
                    [FiscalPeriodEndDate] = '2026-12-31',
                    [UpdatedAtUtc] = TODATETIMEOFFSET(SYSUTCDATETIME(), '+00:00')
                OUTPUT inserted.[UpdatedAtUtc], inserted.[UpdatedByUserId]
                    INTO @FiscalPeriodDefaults ([ChangedAtUtc], [ChangedByUserId])
                WHERE [Id] = '00000000-0000-0000-0000-000000000001'
                    AND [FiscalPeriodStartDate] IS NULL
                    AND [FiscalPeriodEndDate] IS NULL;

                INSERT INTO [finanzas].[PeriodoFiscalHistorial]
                    ([Id], [PreviousStartDate], [PreviousEndDate], [NewStartDate], [NewEndDate],
                     [Reason], [ChangedAtUtc], [ChangedByUserId])
                SELECT NEWID(), NULL, NULL, '2026-01-01', '2026-12-31',
                    NULL, [ChangedAtUtc], [ChangedByUserId]
                FROM @FiscalPeriodDefaults;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_PeriodoFiscalHistorial_ChangedAtUtc",
                schema: "finanzas",
                table: "PeriodoFiscalHistorial",
                column: "ChangedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_PeriodoFiscalHistorial_ChangedByUserId",
                schema: "finanzas",
                table: "PeriodoFiscalHistorial",
                column: "ChangedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PeriodosContablesHistorial_ChangedByUserId",
                schema: "finanzas",
                table: "PeriodosContablesHistorial",
                column: "ChangedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PeriodosContablesHistorial_Periodo_ChangedAtUtc",
                schema: "finanzas",
                table: "PeriodosContablesHistorial",
                columns: new[] { "Year", "Month", "ChangedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_PeriodosContablesReabiertos_CreatedByUserId",
                schema: "finanzas",
                table: "PeriodosContablesReabiertos",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PeriodosContablesReabiertos_UpdatedByUserId",
                schema: "finanzas",
                table: "PeriodosContablesReabiertos",
                column: "UpdatedByUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PeriodoFiscalHistorial",
                schema: "finanzas");

            migrationBuilder.DropTable(
                name: "PeriodosContablesHistorial",
                schema: "finanzas");

            migrationBuilder.DropTable(
                name: "PeriodosContablesReabiertos",
                schema: "finanzas");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ParametrosSistema_FiscalPeriod",
                schema: "finanzas",
                table: "ParametrosSistema");

            migrationBuilder.DropColumn(
                name: "FiscalPeriodEndDate",
                schema: "finanzas",
                table: "ParametrosSistema");

            migrationBuilder.DropColumn(
                name: "FiscalPeriodStartDate",
                schema: "finanzas",
                table: "ParametrosSistema");
        }
    }
}
