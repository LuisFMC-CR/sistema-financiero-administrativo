using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SistemaFinanciero.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddReceivablePaymentsAndWithholdings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Abonos",
                schema: "finanzas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReceivableId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GroupId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CashAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Reference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
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
                    table.PrimaryKey("PK_Abonos", x => x.Id);
                    table.CheckConstraint("CK_Abonos_Amount", "[Amount] >= 0");
                    table.CheckConstraint("CK_Abonos_Status", "[Status] IN (1, 2)");
                    table.ForeignKey(
                        name: "FK_Abonos_CuentasContables",
                        column: x => x.CashAccountId,
                        principalSchema: "catalogos",
                        principalTable: "CuentasContables",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Abonos_CuentasPorCobrar",
                        column: x => x.ReceivableId,
                        principalSchema: "finanzas",
                        principalTable: "CuentasPorCobrar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Abonos_Usuarios_CancelledBy",
                        column: x => x.CancelledByUserId,
                        principalSchema: "seguridad",
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Abonos_Usuarios_CreatedBy",
                        column: x => x.CreatedByUserId,
                        principalSchema: "seguridad",
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Abonos_Usuarios_UpdatedBy",
                        column: x => x.UpdatedByUserId,
                        principalSchema: "seguridad",
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Retenciones",
                schema: "finanzas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReceivablePaymentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WithholdingTypeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Rate = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    Base = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Retenciones", x => x.Id);
                    table.CheckConstraint("CK_Retenciones_Amounts", "[Base] >= 0 AND [Amount] >= 0");
                    table.CheckConstraint("CK_Retenciones_Rate", "[Rate] >= 0 AND [Rate] <= 100");
                    table.ForeignKey(
                        name: "FK_Retenciones_Abonos",
                        column: x => x.ReceivablePaymentId,
                        principalSchema: "finanzas",
                        principalTable: "Abonos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Retenciones_TiposRetencion",
                        column: x => x.WithholdingTypeId,
                        principalSchema: "catalogos",
                        principalTable: "TiposRetencion",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Retenciones_Usuarios_CreatedBy",
                        column: x => x.CreatedByUserId,
                        principalSchema: "seguridad",
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Abonos_CancelledByUserId",
                schema: "finanzas",
                table: "Abonos",
                column: "CancelledByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Abonos_CashAccountId_Reference",
                schema: "finanzas",
                table: "Abonos",
                columns: new[] { "CashAccountId", "Reference" });

            migrationBuilder.CreateIndex(
                name: "IX_Abonos_CreatedByUserId",
                schema: "finanzas",
                table: "Abonos",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Abonos_GroupId",
                schema: "finanzas",
                table: "Abonos",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_Abonos_ReceivableId_Status",
                schema: "finanzas",
                table: "Abonos",
                columns: new[] { "ReceivableId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Abonos_UpdatedByUserId",
                schema: "finanzas",
                table: "Abonos",
                column: "UpdatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Retenciones_CreatedByUserId",
                schema: "finanzas",
                table: "Retenciones",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Retenciones_WithholdingTypeId",
                schema: "finanzas",
                table: "Retenciones",
                column: "WithholdingTypeId");

            migrationBuilder.CreateIndex(
                name: "UX_Retenciones_ReceivablePaymentId",
                schema: "finanzas",
                table: "Retenciones",
                column: "ReceivablePaymentId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Ingresos_Abonos",
                schema: "finanzas",
                table: "Ingresos",
                column: "ReceivablePaymentId",
                principalSchema: "finanzas",
                principalTable: "Abonos",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Ingresos_Abonos",
                schema: "finanzas",
                table: "Ingresos");

            migrationBuilder.DropTable(
                name: "Retenciones",
                schema: "finanzas");

            migrationBuilder.DropTable(
                name: "Abonos",
                schema: "finanzas");
        }
    }
}
