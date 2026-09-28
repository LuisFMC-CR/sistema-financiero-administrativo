using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SistemaFinanciero.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSystemParameters : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ParametrosSistema",
                schema: "finanzas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AuthorizationLimitCrc = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    OverdueAlertDays = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParametrosSistema", x => x.Id);
                    table.CheckConstraint("CK_ParametrosSistema_AuthorizationLimit", "[AuthorizationLimitCrc] >= 0");
                    table.CheckConstraint("CK_ParametrosSistema_OverdueAlertDays", "[OverdueAlertDays] > 0");
                    table.ForeignKey(
                        name: "FK_ParametrosSistema_Usuarios_CreatedBy",
                        column: x => x.CreatedByUserId,
                        principalSchema: "seguridad",
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ParametrosSistema_Usuarios_UpdatedBy",
                        column: x => x.UpdatedByUserId,
                        principalSchema: "seguridad",
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ParametrosSistemaHistorial",
                schema: "finanzas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PreviousAuthorizationLimitCrc = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    NewAuthorizationLimitCrc = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PreviousOverdueAlertDays = table.Column<int>(type: "int", nullable: true),
                    NewOverdueAlertDays = table.Column<int>(type: "int", nullable: false),
                    ChangedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ChangedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParametrosSistemaHistorial", x => x.Id);
                    table.CheckConstraint("CK_ParametrosSistemaHistorial_NewAuthorizationLimit", "[NewAuthorizationLimitCrc] >= 0");
                    table.CheckConstraint("CK_ParametrosSistemaHistorial_NewOverdueAlertDays", "[NewOverdueAlertDays] > 0");
                    table.CheckConstraint("CK_ParametrosSistemaHistorial_PreviousAuthorizationLimit", "[PreviousAuthorizationLimitCrc] IS NULL OR [PreviousAuthorizationLimitCrc] >= 0");
                    table.CheckConstraint("CK_ParametrosSistemaHistorial_PreviousOverdueAlertDays", "[PreviousOverdueAlertDays] IS NULL OR [PreviousOverdueAlertDays] > 0");
                    table.ForeignKey(
                        name: "FK_ParametrosSistemaHistorial_Usuarios_ChangedBy",
                        column: x => x.ChangedByUserId,
                        principalSchema: "seguridad",
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ParametrosSistema_CreatedByUserId",
                schema: "finanzas",
                table: "ParametrosSistema",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ParametrosSistema_UpdatedByUserId",
                schema: "finanzas",
                table: "ParametrosSistema",
                column: "UpdatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ParametrosSistemaHistorial_ChangedAtUtc",
                schema: "finanzas",
                table: "ParametrosSistemaHistorial",
                column: "ChangedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_ParametrosSistemaHistorial_ChangedByUserId",
                schema: "finanzas",
                table: "ParametrosSistemaHistorial",
                column: "ChangedByUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ParametrosSistema",
                schema: "finanzas");

            migrationBuilder.DropTable(
                name: "ParametrosSistemaHistorial",
                schema: "finanzas");
        }
    }
}
