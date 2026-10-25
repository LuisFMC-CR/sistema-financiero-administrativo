using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SistemaFinanciero.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBalanceAdjustments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_BitacoraOperaciones_Action",
                schema: "seguridad",
                table: "BitacoraOperaciones");

            migrationBuilder.CreateTable(
                name: "AjustesSaldo",
                schema: "finanzas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReceivableId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PayableId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Direction = table.Column<int>(type: "int", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AjustesSaldo", x => x.Id);
                    table.CheckConstraint("CK_AjustesSaldo_Amount", "[Amount] > 0");
                    table.CheckConstraint("CK_AjustesSaldo_Direction", "[Direction] IN (1, 2)");
                    table.CheckConstraint("CK_AjustesSaldo_UnaSolaCuenta", "([ReceivableId] IS NOT NULL AND [PayableId] IS NULL) OR ([ReceivableId] IS NULL AND [PayableId] IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_AjustesSaldo_CuentasPorCobrar",
                        column: x => x.ReceivableId,
                        principalSchema: "finanzas",
                        principalTable: "CuentasPorCobrar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AjustesSaldo_CuentasPorPagar",
                        column: x => x.PayableId,
                        principalSchema: "finanzas",
                        principalTable: "CuentasPorPagar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AjustesSaldo_Usuarios_CreatedBy",
                        column: x => x.CreatedByUserId,
                        principalSchema: "seguridad",
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_BitacoraOperaciones_Action",
                schema: "seguridad",
                table: "BitacoraOperaciones",
                sql: "[Action] BETWEEN 1 AND 3");

            migrationBuilder.CreateIndex(
                name: "IX_AjustesSaldo_CreatedByUserId",
                schema: "finanzas",
                table: "AjustesSaldo",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AjustesSaldo_PayableId",
                schema: "finanzas",
                table: "AjustesSaldo",
                column: "PayableId");

            migrationBuilder.CreateIndex(
                name: "IX_AjustesSaldo_ReceivableId",
                schema: "finanzas",
                table: "AjustesSaldo",
                column: "ReceivableId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AjustesSaldo",
                schema: "finanzas");

            migrationBuilder.DropCheckConstraint(
                name: "CK_BitacoraOperaciones_Action",
                schema: "seguridad",
                table: "BitacoraOperaciones");

            migrationBuilder.AddCheckConstraint(
                name: "CK_BitacoraOperaciones_Action",
                schema: "seguridad",
                table: "BitacoraOperaciones",
                sql: "[Action] BETWEEN 1 AND 2");
        }
    }
}
