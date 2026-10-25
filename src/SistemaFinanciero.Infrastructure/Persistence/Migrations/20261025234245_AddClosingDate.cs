using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SistemaFinanciero.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddClosingDate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "ClosingDate",
                schema: "finanzas",
                table: "ParametrosSistema",
                type: "date",
                nullable: false,
                defaultValue: new DateOnly(1, 1, 1));

            migrationBuilder.CreateTable(
                name: "ParametrosFechaCierreHistorial",
                schema: "finanzas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PreviousClosingDate = table.Column<DateOnly>(type: "date", nullable: false),
                    NewClosingDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    ChangedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ChangedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParametrosFechaCierreHistorial", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ParametrosFechaCierreHistorial_Usuarios_ChangedBy",
                        column: x => x.ChangedByUserId,
                        principalSchema: "seguridad",
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ParametrosFechaCierreHistorial_ChangedAtUtc",
                schema: "finanzas",
                table: "ParametrosFechaCierreHistorial",
                column: "ChangedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_ParametrosFechaCierreHistorial_ChangedByUserId",
                schema: "finanzas",
                table: "ParametrosFechaCierreHistorial",
                column: "ChangedByUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ParametrosFechaCierreHistorial",
                schema: "finanzas");

            migrationBuilder.DropColumn(
                name: "ClosingDate",
                schema: "finanzas",
                table: "ParametrosSistema");
        }
    }
}
