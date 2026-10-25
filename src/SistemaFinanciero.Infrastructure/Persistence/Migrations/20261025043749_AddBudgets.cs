using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SistemaFinanciero.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBudgets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Presupuestos",
                schema: "finanzas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CategoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PeriodYear = table.Column<int>(type: "int", nullable: false),
                    PeriodMonth = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Presupuestos", x => x.Id);
                    table.CheckConstraint("CK_Presupuestos_Amount", "[Amount] >= 0");
                    table.CheckConstraint("CK_Presupuestos_PeriodMonth", "[PeriodMonth] BETWEEN 1 AND 12");
                    table.ForeignKey(
                        name: "FK_Presupuestos_CategoriasFinancieras",
                        column: x => x.CategoryId,
                        principalSchema: "catalogos",
                        principalTable: "CategoriasFinancieras",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Presupuestos_Usuarios_CreatedBy",
                        column: x => x.CreatedByUserId,
                        principalSchema: "seguridad",
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Presupuestos_Usuarios_UpdatedBy",
                        column: x => x.UpdatedByUserId,
                        principalSchema: "seguridad",
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PresupuestoCambios",
                schema: "finanzas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BudgetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PreviousAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    NewAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ChangedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ChangedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PresupuestoCambios", x => x.Id);
                    table.CheckConstraint("CK_PresupuestoCambios_NewAmount", "[NewAmount] >= 0");
                    table.CheckConstraint("CK_PresupuestoCambios_PreviousAmount", "[PreviousAmount] IS NULL OR [PreviousAmount] >= 0");
                    table.ForeignKey(
                        name: "FK_PresupuestoCambios_Presupuestos",
                        column: x => x.BudgetId,
                        principalSchema: "finanzas",
                        principalTable: "Presupuestos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PresupuestoCambios_Usuarios_ChangedBy",
                        column: x => x.ChangedByUserId,
                        principalSchema: "seguridad",
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PresupuestoCambios_BudgetId",
                schema: "finanzas",
                table: "PresupuestoCambios",
                column: "BudgetId");

            migrationBuilder.CreateIndex(
                name: "IX_PresupuestoCambios_ChangedAtUtc",
                schema: "finanzas",
                table: "PresupuestoCambios",
                column: "ChangedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_PresupuestoCambios_ChangedByUserId",
                schema: "finanzas",
                table: "PresupuestoCambios",
                column: "ChangedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Presupuestos_CreatedByUserId",
                schema: "finanzas",
                table: "Presupuestos",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Presupuestos_UpdatedByUserId",
                schema: "finanzas",
                table: "Presupuestos",
                column: "UpdatedByUserId");

            migrationBuilder.CreateIndex(
                name: "UX_Presupuestos_CategoryId_Period",
                schema: "finanzas",
                table: "Presupuestos",
                columns: new[] { "CategoryId", "PeriodYear", "PeriodMonth" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PresupuestoCambios",
                schema: "finanzas");

            migrationBuilder.DropTable(
                name: "Presupuestos",
                schema: "finanzas");
        }
    }
}
