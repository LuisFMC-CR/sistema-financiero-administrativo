using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SistemaFinanciero.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddReceivablesAndIncomes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CuentasPorCobrar",
                schema: "finanzas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InvoiceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OriginalAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Balance = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_CuentasPorCobrar", x => x.Id);
                    table.CheckConstraint("CK_CuentasPorCobrar_Balance", "[Balance] >= 0 AND [Balance] <= [OriginalAmount]");
                    table.CheckConstraint("CK_CuentasPorCobrar_Status", "[Status] IN (1, 2, 3, 4)");
                    table.ForeignKey(
                        name: "FK_CuentasPorCobrar_Clientes",
                        column: x => x.CustomerId,
                        principalSchema: "catalogos",
                        principalTable: "Clientes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CuentasPorCobrar_Facturas",
                        column: x => x.InvoiceId,
                        principalSchema: "finanzas",
                        principalTable: "Facturas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CuentasPorCobrar_Usuarios_CancelledBy",
                        column: x => x.CancelledByUserId,
                        principalSchema: "seguridad",
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CuentasPorCobrar_Usuarios_CreatedBy",
                        column: x => x.CreatedByUserId,
                        principalSchema: "seguridad",
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CuentasPorCobrar_Usuarios_UpdatedBy",
                        column: x => x.UpdatedByUserId,
                        principalSchema: "seguridad",
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Ingresos",
                schema: "finanzas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Origin = table.Column<int>(type: "int", nullable: false),
                    InvoiceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReceivablePaymentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CategoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CashAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    IncomeDate = table.Column<DateOnly>(type: "date", nullable: false),
                    PaymentMethod = table.Column<int>(type: "int", nullable: true),
                    Reference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
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
                    table.PrimaryKey("PK_Ingresos", x => x.Id);
                    table.CheckConstraint("CK_Ingresos_Amount", "[Amount] >= 0");
                    table.CheckConstraint("CK_Ingresos_Origin", "[Origin] IN (1, 2, 3)");
                    table.CheckConstraint("CK_Ingresos_OriginReferences", "([Origin] = 1 AND [InvoiceId] IS NOT NULL AND [ReceivablePaymentId] IS NULL) OR ([Origin] = 2 AND [ReceivablePaymentId] IS NOT NULL AND [InvoiceId] IS NULL) OR ([Origin] = 3 AND [InvoiceId] IS NULL AND [ReceivablePaymentId] IS NULL)");
                    table.CheckConstraint("CK_Ingresos_PaymentMethod", "([Origin] IN (1, 3) AND [PaymentMethod] IS NOT NULL) OR ([Origin] = 2 AND [PaymentMethod] IS NULL)");
                    table.CheckConstraint("CK_Ingresos_Status", "[Status] IN (1, 2)");
                    table.ForeignKey(
                        name: "FK_Ingresos_CategoriasFinancieras",
                        column: x => x.CategoryId,
                        principalSchema: "catalogos",
                        principalTable: "CategoriasFinancieras",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Ingresos_CuentasContables",
                        column: x => x.CashAccountId,
                        principalSchema: "catalogos",
                        principalTable: "CuentasContables",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Ingresos_Facturas",
                        column: x => x.InvoiceId,
                        principalSchema: "finanzas",
                        principalTable: "Facturas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Ingresos_Usuarios_CancelledBy",
                        column: x => x.CancelledByUserId,
                        principalSchema: "seguridad",
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Ingresos_Usuarios_CreatedBy",
                        column: x => x.CreatedByUserId,
                        principalSchema: "seguridad",
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Ingresos_Usuarios_UpdatedBy",
                        column: x => x.UpdatedByUserId,
                        principalSchema: "seguridad",
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CuentasPorCobrar_CancelledByUserId",
                schema: "finanzas",
                table: "CuentasPorCobrar",
                column: "CancelledByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CuentasPorCobrar_CreatedByUserId",
                schema: "finanzas",
                table: "CuentasPorCobrar",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CuentasPorCobrar_CustomerId_Status",
                schema: "finanzas",
                table: "CuentasPorCobrar",
                columns: new[] { "CustomerId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_CuentasPorCobrar_Status_DueDate",
                schema: "finanzas",
                table: "CuentasPorCobrar",
                columns: new[] { "Status", "DueDate" });

            migrationBuilder.CreateIndex(
                name: "IX_CuentasPorCobrar_UpdatedByUserId",
                schema: "finanzas",
                table: "CuentasPorCobrar",
                column: "UpdatedByUserId");

            migrationBuilder.CreateIndex(
                name: "UX_CuentasPorCobrar_InvoiceId",
                schema: "finanzas",
                table: "CuentasPorCobrar",
                column: "InvoiceId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Ingresos_CancelledByUserId",
                schema: "finanzas",
                table: "Ingresos",
                column: "CancelledByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Ingresos_CashAccountId",
                schema: "finanzas",
                table: "Ingresos",
                column: "CashAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_Ingresos_CategoryId",
                schema: "finanzas",
                table: "Ingresos",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Ingresos_CreatedByUserId",
                schema: "finanzas",
                table: "Ingresos",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Ingresos_Status_IncomeDate",
                schema: "finanzas",
                table: "Ingresos",
                columns: new[] { "Status", "IncomeDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Ingresos_UpdatedByUserId",
                schema: "finanzas",
                table: "Ingresos",
                column: "UpdatedByUserId");

            migrationBuilder.CreateIndex(
                name: "UX_Ingresos_InvoiceId",
                schema: "finanzas",
                table: "Ingresos",
                column: "InvoiceId",
                unique: true,
                filter: "[InvoiceId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_Ingresos_ReceivablePaymentId",
                schema: "finanzas",
                table: "Ingresos",
                column: "ReceivablePaymentId",
                unique: true,
                filter: "[ReceivablePaymentId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CuentasPorCobrar",
                schema: "finanzas");

            migrationBuilder.DropTable(
                name: "Ingresos",
                schema: "finanzas");
        }
    }
}
