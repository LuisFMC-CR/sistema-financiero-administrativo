using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SistemaFinanciero.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddVatSettlements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateSequence(
                name: "Seq_LiquidacionIvaCodigo",
                schema: "finanzas",
                startValue: 1L);

            migrationBuilder.DropCheckConstraint(
                name: "CK_BitacoraOperaciones_EntityType",
                schema: "seguridad",
                table: "BitacoraOperaciones");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Asientos_SourceType",
                schema: "finanzas",
                table: "Asientos");

            migrationBuilder.CreateTable(
                name: "LiquidacionesIva",
                schema: "finanzas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    PeriodYear = table.Column<int>(type: "int", nullable: false),
                    PeriodMonth = table.Column<int>(type: "int", nullable: false),
                    SettlementDate = table.Column<DateOnly>(type: "date", nullable: false),
                    VatPayableAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    VatCreditApplied = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PaymentAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CreditCarriedForward = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CashAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Reference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
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
                    table.PrimaryKey("PK_LiquidacionesIva", x => x.Id);
                    table.CheckConstraint("CK_LiquidacionesIva_NonNegativeAmounts", "[VatPayableAmount] >= 0 AND [VatCreditApplied] >= 0 AND [PaymentAmount] >= 0 AND [CreditCarriedForward] >= 0");
                    table.CheckConstraint("CK_LiquidacionesIva_Payment", "([PaymentAmount] = 0 AND [CashAccountId] IS NULL AND [Reference] IS NULL) OR ([PaymentAmount] > 0 AND [CashAccountId] IS NOT NULL AND [Reference] IS NOT NULL)");
                    table.CheckConstraint("CK_LiquidacionesIva_PeriodMonth", "[PeriodMonth] BETWEEN 1 AND 12");
                    table.CheckConstraint("CK_LiquidacionesIva_Status", "[Status] IN (1, 2)");
                    table.CheckConstraint("CK_LiquidacionesIva_Totals", "[VatPayableAmount] = [VatCreditApplied] + [PaymentAmount]");
                    table.ForeignKey(
                        name: "FK_LiquidacionesIva_CuentasContables",
                        column: x => x.CashAccountId,
                        principalSchema: "catalogos",
                        principalTable: "CuentasContables",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LiquidacionesIva_Usuarios_CancelledBy",
                        column: x => x.CancelledByUserId,
                        principalSchema: "seguridad",
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LiquidacionesIva_Usuarios_CreatedBy",
                        column: x => x.CreatedByUserId,
                        principalSchema: "seguridad",
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LiquidacionesIva_Usuarios_UpdatedBy",
                        column: x => x.UpdatedByUserId,
                        principalSchema: "seguridad",
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

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

            migrationBuilder.CreateIndex(
                name: "IX_LiquidacionesIva_CancelledByUserId",
                schema: "finanzas",
                table: "LiquidacionesIva",
                column: "CancelledByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_LiquidacionesIva_CashAccountId",
                schema: "finanzas",
                table: "LiquidacionesIva",
                column: "CashAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_LiquidacionesIva_CreatedByUserId",
                schema: "finanzas",
                table: "LiquidacionesIva",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_LiquidacionesIva_Status",
                schema: "finanzas",
                table: "LiquidacionesIva",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_LiquidacionesIva_UpdatedByUserId",
                schema: "finanzas",
                table: "LiquidacionesIva",
                column: "UpdatedByUserId");

            migrationBuilder.CreateIndex(
                name: "UX_LiquidacionesIva_Code",
                schema: "finanzas",
                table: "LiquidacionesIva",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_LiquidacionesIva_Period_Vigente",
                schema: "finanzas",
                table: "LiquidacionesIva",
                columns: new[] { "PeriodYear", "PeriodMonth" },
                unique: true,
                filter: "[Status] = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LiquidacionesIva",
                schema: "finanzas");

            migrationBuilder.DropSequence(
                name: "Seq_LiquidacionIvaCodigo",
                schema: "finanzas");

            migrationBuilder.DropCheckConstraint(
                name: "CK_BitacoraOperaciones_EntityType",
                schema: "seguridad",
                table: "BitacoraOperaciones");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Asientos_SourceType",
                schema: "finanzas",
                table: "Asientos");

            migrationBuilder.AddCheckConstraint(
                name: "CK_BitacoraOperaciones_EntityType",
                schema: "seguridad",
                table: "BitacoraOperaciones",
                sql: "[EntityType] BETWEEN 1 AND 9");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Asientos_SourceType",
                schema: "finanzas",
                table: "Asientos",
                sql: "[SourceType] BETWEEN 1 AND 8");
        }
    }
}
