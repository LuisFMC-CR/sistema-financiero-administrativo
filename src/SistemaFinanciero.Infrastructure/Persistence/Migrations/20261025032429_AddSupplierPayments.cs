using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SistemaFinanciero.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSupplierPayments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_BitacoraOperaciones_EntityType",
                schema: "seguridad",
                table: "BitacoraOperaciones");

            migrationBuilder.CreateTable(
                name: "Pagos",
                schema: "finanzas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PayableId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
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
                    table.PrimaryKey("PK_Pagos", x => x.Id);
                    table.CheckConstraint("CK_Pagos_Amount", "[Amount] >= 0");
                    table.CheckConstraint("CK_Pagos_Status", "[Status] IN (1, 2)");
                    table.ForeignKey(
                        name: "FK_Pagos_CuentasContables",
                        column: x => x.CashAccountId,
                        principalSchema: "catalogos",
                        principalTable: "CuentasContables",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Pagos_CuentasPorPagar",
                        column: x => x.PayableId,
                        principalSchema: "finanzas",
                        principalTable: "CuentasPorPagar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Pagos_Usuarios_CancelledBy",
                        column: x => x.CancelledByUserId,
                        principalSchema: "seguridad",
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Pagos_Usuarios_CreatedBy",
                        column: x => x.CreatedByUserId,
                        principalSchema: "seguridad",
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Pagos_Usuarios_UpdatedBy",
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
                sql: "[EntityType] BETWEEN 1 AND 7");

            migrationBuilder.CreateIndex(
                name: "IX_Pagos_CancelledByUserId",
                schema: "finanzas",
                table: "Pagos",
                column: "CancelledByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Pagos_CashAccountId_Reference",
                schema: "finanzas",
                table: "Pagos",
                columns: new[] { "CashAccountId", "Reference" });

            migrationBuilder.CreateIndex(
                name: "IX_Pagos_CreatedByUserId",
                schema: "finanzas",
                table: "Pagos",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Pagos_GroupId",
                schema: "finanzas",
                table: "Pagos",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_Pagos_PayableId_Status",
                schema: "finanzas",
                table: "Pagos",
                columns: new[] { "PayableId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Pagos_UpdatedByUserId",
                schema: "finanzas",
                table: "Pagos",
                column: "UpdatedByUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Pagos",
                schema: "finanzas");

            migrationBuilder.DropCheckConstraint(
                name: "CK_BitacoraOperaciones_EntityType",
                schema: "seguridad",
                table: "BitacoraOperaciones");

            migrationBuilder.AddCheckConstraint(
                name: "CK_BitacoraOperaciones_EntityType",
                schema: "seguridad",
                table: "BitacoraOperaciones",
                sql: "[EntityType] BETWEEN 1 AND 6");
        }
    }
}
