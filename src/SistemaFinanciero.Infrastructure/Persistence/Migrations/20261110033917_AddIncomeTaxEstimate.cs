using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SistemaFinanciero.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Estimación del impuesto sobre la renta (RF-99): escala de tramos en <c>ParametrosSistema</c> (con
    /// los valores de 2026 como valor inicial de la fila existente), su historial, los pagos a Hacienda
    /// con su secuencia de código <c>PIR</c>, y la cuenta de sistema 1.3.03 "Impuesto sobre la renta
    /// pagado por anticipado" (rol 9).
    /// </summary>
    public partial class AddIncomeTaxEstimate : Migration
    {
        private const string SystemUserId = "00000000-0000-0000-0000-000000000001";
        private const string TaxesReceivableGroupId = "10000000-0000-0000-0000-000000000007";
        private const string IncomeTaxPrepaidId = "10000000-0000-0000-0000-00000000000a";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateSequence(
                name: "Seq_PagoRentaCodigo",
                schema: "finanzas",
                startValue: 1L);

            migrationBuilder.DropCheckConstraint(
                name: "CK_CuentasContables_SystemRole",
                schema: "catalogos",
                table: "CuentasContables");

            migrationBuilder.DropCheckConstraint(
                name: "CK_BitacoraOperaciones_EntityType",
                schema: "seguridad",
                table: "BitacoraOperaciones");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Asientos_SourceType",
                schema: "finanzas",
                table: "Asientos");

            migrationBuilder.AddColumn<decimal>(
                name: "IncomeTaxBracket1Rate",
                schema: "finanzas",
                table: "ParametrosSistema",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 5m);

            migrationBuilder.AddColumn<decimal>(
                name: "IncomeTaxBracket1UpperLimit",
                schema: "finanzas",
                table: "ParametrosSistema",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 5_621_000m);

            migrationBuilder.AddColumn<decimal>(
                name: "IncomeTaxBracket2Rate",
                schema: "finanzas",
                table: "ParametrosSistema",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 10m);

            migrationBuilder.AddColumn<decimal>(
                name: "IncomeTaxBracket2UpperLimit",
                schema: "finanzas",
                table: "ParametrosSistema",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 8_433_000m);

            migrationBuilder.AddColumn<decimal>(
                name: "IncomeTaxBracket3Rate",
                schema: "finanzas",
                table: "ParametrosSistema",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 15m);

            migrationBuilder.AddColumn<decimal>(
                name: "IncomeTaxBracket3UpperLimit",
                schema: "finanzas",
                table: "ParametrosSistema",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 11_243_000m);

            migrationBuilder.AddColumn<decimal>(
                name: "IncomeTaxBracket4Rate",
                schema: "finanzas",
                table: "ParametrosSistema",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 20m);

            migrationBuilder.AddColumn<decimal>(
                name: "IncomeTaxGeneralRate",
                schema: "finanzas",
                table: "ParametrosSistema",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 30m);

            migrationBuilder.AddColumn<decimal>(
                name: "IncomeTaxGrossIncomeThreshold",
                schema: "finanzas",
                table: "ParametrosSistema",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 119_174_000m);

            migrationBuilder.CreateTable(
                name: "EscalaRentaHistorial",
                schema: "finanzas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Bracket1UpperLimit = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Bracket2UpperLimit = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Bracket3UpperLimit = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Bracket1Rate = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    Bracket2Rate = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    Bracket3Rate = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    Bracket4Rate = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    GrossIncomeThreshold = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    GeneralRate = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    ChangedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ChangedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EscalaRentaHistorial", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EscalaRentaHistorial_Usuarios_ChangedBy",
                        column: x => x.ChangedByUserId,
                        principalSchema: "seguridad",
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PagosImpuestoRenta",
                schema: "finanzas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    FiscalPeriodStartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    FiscalPeriodEndDate = table.Column<DateOnly>(type: "date", nullable: false),
                    CashAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PaymentMethod = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Reference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    PaymentDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CancellationReason = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    CancelledAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CancelledByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PagosImpuestoRenta", x => x.Id);
                    table.CheckConstraint("CK_PagosImpuestoRenta_Amount", "[Amount] > 0");
                    table.CheckConstraint("CK_PagosImpuestoRenta_FiscalPeriod", "DAY([FiscalPeriodStartDate]) = 1 AND [FiscalPeriodEndDate] = DATEADD(day, -1, DATEADD(year, 1, [FiscalPeriodStartDate]))");
                    table.CheckConstraint("CK_PagosImpuestoRenta_PaymentDate", "[PaymentDate] >= [FiscalPeriodStartDate]");
                    table.CheckConstraint("CK_PagosImpuestoRenta_PaymentMethod", "[PaymentMethod] IN (1, 2, 3, 4, 5)");
                    table.CheckConstraint("CK_PagosImpuestoRenta_Status", "[Status] IN (1, 2)");
                    table.ForeignKey(
                        name: "FK_PagosImpuestoRenta_CuentasContables_CashAccount",
                        column: x => x.CashAccountId,
                        principalSchema: "catalogos",
                        principalTable: "CuentasContables",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PagosImpuestoRenta_Usuarios_CancelledBy",
                        column: x => x.CancelledByUserId,
                        principalSchema: "seguridad",
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PagosImpuestoRenta_Usuarios_CreatedBy",
                        column: x => x.CreatedByUserId,
                        principalSchema: "seguridad",
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PagosImpuestoRenta_Usuarios_UpdatedBy",
                        column: x => x.UpdatedByUserId,
                        principalSchema: "seguridad",
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_ParametrosSistema_IncomeTaxBrackets",
                schema: "finanzas",
                table: "ParametrosSistema",
                sql: "[IncomeTaxBracket1UpperLimit] > 0 AND [IncomeTaxBracket2UpperLimit] > [IncomeTaxBracket1UpperLimit] AND [IncomeTaxBracket3UpperLimit] > [IncomeTaxBracket2UpperLimit] AND [IncomeTaxGrossIncomeThreshold] > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ParametrosSistema_IncomeTaxRates",
                schema: "finanzas",
                table: "ParametrosSistema",
                sql: "[IncomeTaxBracket1Rate] BETWEEN 0 AND 100 AND [IncomeTaxBracket2Rate] BETWEEN 0 AND 100 AND [IncomeTaxBracket3Rate] BETWEEN 0 AND 100 AND [IncomeTaxBracket4Rate] BETWEEN 0 AND 100 AND [IncomeTaxGeneralRate] BETWEEN 0 AND 100");

            migrationBuilder.AddCheckConstraint(
                name: "CK_CuentasContables_SystemRole",
                schema: "catalogos",
                table: "CuentasContables",
                sql: "[SystemRole] IS NULL OR [SystemRole] IN (1, 2, 3, 4, 5, 6, 7, 8, 9)");

            migrationBuilder.Sql($"""
                INSERT INTO [catalogos].[CuentasContables]
                    ([Id], [Code], [Name], [Type], [ParentId], [CashKind], [Reference], [Description],
                     [IsActive], [IsProtected], [SystemRole], [CreatedAtUtc], [CreatedByUserId], [UpdatedAtUtc], [UpdatedByUserId])
                VALUES
                    ('{IncomeTaxPrepaidId}', '1.3.03', N'Impuesto sobre la renta pagado por anticipado', 1,
                     '{TaxesReceivableGroupId}', NULL, NULL, NULL, 1, 0, 9,
                     CAST(SYSUTCDATETIME() AS datetimeoffset), '{SystemUserId}',
                     CAST(SYSUTCDATETIME() AS datetimeoffset), '{SystemUserId}');
                """);

            migrationBuilder.AddCheckConstraint(
                name: "CK_BitacoraOperaciones_EntityType",
                schema: "seguridad",
                table: "BitacoraOperaciones",
                sql: "[EntityType] BETWEEN 1 AND 11");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Asientos_SourceType",
                schema: "finanzas",
                table: "Asientos",
                sql: "[SourceType] BETWEEN 1 AND 10");

            migrationBuilder.CreateIndex(
                name: "IX_EscalaRentaHistorial_ChangedAtUtc",
                schema: "finanzas",
                table: "EscalaRentaHistorial",
                column: "ChangedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_EscalaRentaHistorial_ChangedByUserId",
                schema: "finanzas",
                table: "EscalaRentaHistorial",
                column: "ChangedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PagosImpuestoRenta_CancelledByUserId",
                schema: "finanzas",
                table: "PagosImpuestoRenta",
                column: "CancelledByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PagosImpuestoRenta_CashAccountId_Reference",
                schema: "finanzas",
                table: "PagosImpuestoRenta",
                columns: new[] { "CashAccountId", "Reference" });

            migrationBuilder.CreateIndex(
                name: "IX_PagosImpuestoRenta_CreatedByUserId",
                schema: "finanzas",
                table: "PagosImpuestoRenta",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PagosImpuestoRenta_FiscalPeriodStartDate_Status",
                schema: "finanzas",
                table: "PagosImpuestoRenta",
                columns: new[] { "FiscalPeriodStartDate", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_PagosImpuestoRenta_UpdatedByUserId",
                schema: "finanzas",
                table: "PagosImpuestoRenta",
                column: "UpdatedByUserId");

            migrationBuilder.CreateIndex(
                name: "UX_PagosImpuestoRenta_Code",
                schema: "finanzas",
                table: "PagosImpuestoRenta",
                column: "Code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // La cuenta solo se puede quitar si no tiene movimientos; si los tiene, la FK de AsientoLineas
            // detiene la reversión, igual que debe ser: no se borra historia contable.
            migrationBuilder.Sql($"DELETE FROM [catalogos].[CuentasContables] WHERE [Id] = '{IncomeTaxPrepaidId}';");

            migrationBuilder.DropSequence(
                name: "Seq_PagoRentaCodigo",
                schema: "finanzas");

            migrationBuilder.DropTable(
                name: "EscalaRentaHistorial",
                schema: "finanzas");

            migrationBuilder.DropTable(
                name: "PagosImpuestoRenta",
                schema: "finanzas");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ParametrosSistema_IncomeTaxBrackets",
                schema: "finanzas",
                table: "ParametrosSistema");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ParametrosSistema_IncomeTaxRates",
                schema: "finanzas",
                table: "ParametrosSistema");

            migrationBuilder.DropCheckConstraint(
                name: "CK_CuentasContables_SystemRole",
                schema: "catalogos",
                table: "CuentasContables");

            migrationBuilder.DropCheckConstraint(
                name: "CK_BitacoraOperaciones_EntityType",
                schema: "seguridad",
                table: "BitacoraOperaciones");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Asientos_SourceType",
                schema: "finanzas",
                table: "Asientos");

            migrationBuilder.DropColumn(
                name: "IncomeTaxBracket1Rate",
                schema: "finanzas",
                table: "ParametrosSistema");

            migrationBuilder.DropColumn(
                name: "IncomeTaxBracket1UpperLimit",
                schema: "finanzas",
                table: "ParametrosSistema");

            migrationBuilder.DropColumn(
                name: "IncomeTaxBracket2Rate",
                schema: "finanzas",
                table: "ParametrosSistema");

            migrationBuilder.DropColumn(
                name: "IncomeTaxBracket2UpperLimit",
                schema: "finanzas",
                table: "ParametrosSistema");

            migrationBuilder.DropColumn(
                name: "IncomeTaxBracket3Rate",
                schema: "finanzas",
                table: "ParametrosSistema");

            migrationBuilder.DropColumn(
                name: "IncomeTaxBracket3UpperLimit",
                schema: "finanzas",
                table: "ParametrosSistema");

            migrationBuilder.DropColumn(
                name: "IncomeTaxBracket4Rate",
                schema: "finanzas",
                table: "ParametrosSistema");

            migrationBuilder.DropColumn(
                name: "IncomeTaxGeneralRate",
                schema: "finanzas",
                table: "ParametrosSistema");

            migrationBuilder.DropColumn(
                name: "IncomeTaxGrossIncomeThreshold",
                schema: "finanzas",
                table: "ParametrosSistema");

            migrationBuilder.AddCheckConstraint(
                name: "CK_CuentasContables_SystemRole",
                schema: "catalogos",
                table: "CuentasContables",
                sql: "[SystemRole] IS NULL OR [SystemRole] IN (1, 2, 3, 4, 5, 6, 7, 8)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_BitacoraOperaciones_EntityType",
                schema: "seguridad",
                table: "BitacoraOperaciones",
                sql: "[EntityType] BETWEEN 1 AND 10");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Asientos_SourceType",
                schema: "finanzas",
                table: "Asientos",
                sql: "[SourceType] BETWEEN 1 AND 9");
        }
    }
}
