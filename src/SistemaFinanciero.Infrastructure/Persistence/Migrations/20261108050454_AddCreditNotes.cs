using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SistemaFinanciero.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCreditNotes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateSequence(
                name: "Seq_NotaCreditoCodigo",
                schema: "finanzas",
                startValue: 1L);

            migrationBuilder.DropCheckConstraint(
                name: "CK_BitacoraOperaciones_EntityType",
                schema: "seguridad",
                table: "BitacoraOperaciones");

            migrationBuilder.CreateTable(
                name: "NotasCredito",
                schema: "finanzas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    InvoiceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IssueDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    NetAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TaxAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    AppliedToBalance = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    RefundedAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    WithholdingReversedAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CashAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RefundReference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ElectronicCreditNoteKey = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotasCredito", x => x.Id);
                    table.CheckConstraint("CK_NotasCredito_NonNegativeAmounts", "[NetAmount] >= 0 AND [TaxAmount] >= 0 AND [TotalAmount] > 0 AND [AppliedToBalance] >= 0 AND [RefundedAmount] >= 0 AND [WithholdingReversedAmount] >= 0");
                    table.CheckConstraint("CK_NotasCredito_Refund", "([RefundedAmount] = 0 AND [CashAccountId] IS NULL AND [RefundReference] IS NULL) OR ([RefundedAmount] > 0 AND [CashAccountId] IS NOT NULL AND [RefundReference] IS NOT NULL)");
                    table.CheckConstraint("CK_NotasCredito_Status", "[Status] = 1");
                    table.CheckConstraint("CK_NotasCredito_Totals", "[TotalAmount] = [AppliedToBalance] + [RefundedAmount] + [WithholdingReversedAmount]");
                    table.ForeignKey(
                        name: "FK_NotasCredito_CuentasContables_CashAccount",
                        column: x => x.CashAccountId,
                        principalSchema: "catalogos",
                        principalTable: "CuentasContables",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_NotasCredito_Facturas",
                        column: x => x.InvoiceId,
                        principalSchema: "finanzas",
                        principalTable: "Facturas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_NotasCredito_Usuarios_CreatedBy",
                        column: x => x.CreatedByUserId,
                        principalSchema: "seguridad",
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_NotasCredito_Usuarios_UpdatedBy",
                        column: x => x.UpdatedByUserId,
                        principalSchema: "seguridad",
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "NotaCreditoLineas",
                schema: "finanzas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreditNoteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InvoiceLineId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NetAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TaxRate = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    TaxAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotaCreditoLineas", x => x.Id);
                    table.CheckConstraint("CK_NotaCreditoLineas_Amounts", "[NetAmount] >= 0 AND [TaxRate] >= 0 AND [TaxAmount] >= 0 AND [TotalAmount] = [NetAmount] + [TaxAmount]");
                    table.ForeignKey(
                        name: "FK_NotaCreditoLineas_FacturaLineas",
                        column: x => x.InvoiceLineId,
                        principalSchema: "finanzas",
                        principalTable: "FacturaLineas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_NotaCreditoLineas_NotasCredito",
                        column: x => x.CreditNoteId,
                        principalSchema: "finanzas",
                        principalTable: "NotasCredito",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_BitacoraOperaciones_EntityType",
                schema: "seguridad",
                table: "BitacoraOperaciones",
                sql: "[EntityType] BETWEEN 1 AND 8");

            migrationBuilder.CreateIndex(
                name: "IX_NotaCreditoLineas_InvoiceLineId",
                schema: "finanzas",
                table: "NotaCreditoLineas",
                column: "InvoiceLineId");

            migrationBuilder.CreateIndex(
                name: "UX_NotaCreditoLineas_CreditNoteId_InvoiceLineId",
                schema: "finanzas",
                table: "NotaCreditoLineas",
                columns: new[] { "CreditNoteId", "InvoiceLineId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NotasCredito_CashAccountId",
                schema: "finanzas",
                table: "NotasCredito",
                column: "CashAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_NotasCredito_CreatedByUserId",
                schema: "finanzas",
                table: "NotasCredito",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_NotasCredito_Status_IssueDate",
                schema: "finanzas",
                table: "NotasCredito",
                columns: new[] { "Status", "IssueDate" });

            migrationBuilder.CreateIndex(
                name: "IX_NotasCredito_UpdatedByUserId",
                schema: "finanzas",
                table: "NotasCredito",
                column: "UpdatedByUserId");

            migrationBuilder.CreateIndex(
                name: "UX_NotasCredito_Code",
                schema: "finanzas",
                table: "NotasCredito",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_NotasCredito_InvoiceId",
                schema: "finanzas",
                table: "NotasCredito",
                column: "InvoiceId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NotaCreditoLineas",
                schema: "finanzas");

            migrationBuilder.DropTable(
                name: "NotasCredito",
                schema: "finanzas");

            migrationBuilder.DropSequence(
                name: "Seq_NotaCreditoCodigo",
                schema: "finanzas");

            migrationBuilder.DropCheckConstraint(
                name: "CK_BitacoraOperaciones_EntityType",
                schema: "seguridad",
                table: "BitacoraOperaciones");

            migrationBuilder.AddCheckConstraint(
                name: "CK_BitacoraOperaciones_EntityType",
                schema: "seguridad",
                table: "BitacoraOperaciones",
                sql: "[EntityType] BETWEEN 1 AND 7");
        }
    }
}
