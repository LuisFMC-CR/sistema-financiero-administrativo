using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SistemaFinanciero.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCompanyData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CompanyAddress",
                schema: "finanzas",
                table: "ParametrosSistema",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CompanyEmail",
                schema: "finanzas",
                table: "ParametrosSistema",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CompanyLegalId",
                schema: "finanzas",
                table: "ParametrosSistema",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CompanyName",
                schema: "finanzas",
                table: "ParametrosSistema",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CompanyPhone",
                schema: "finanzas",
                table: "ParametrosSistema",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ParametrosEmpresaHistorial",
                schema: "finanzas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    CompanyLegalId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CompanyAddress = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    CompanyPhone = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    CompanyEmail = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    ChangedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ChangedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParametrosEmpresaHistorial", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ParametrosEmpresaHistorial_Usuarios_ChangedBy",
                        column: x => x.ChangedByUserId,
                        principalSchema: "seguridad",
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ParametrosEmpresaHistorial_ChangedAtUtc",
                schema: "finanzas",
                table: "ParametrosEmpresaHistorial",
                column: "ChangedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_ParametrosEmpresaHistorial_ChangedByUserId",
                schema: "finanzas",
                table: "ParametrosEmpresaHistorial",
                column: "ChangedByUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ParametrosEmpresaHistorial",
                schema: "finanzas");

            migrationBuilder.DropColumn(
                name: "CompanyAddress",
                schema: "finanzas",
                table: "ParametrosSistema");

            migrationBuilder.DropColumn(
                name: "CompanyEmail",
                schema: "finanzas",
                table: "ParametrosSistema");

            migrationBuilder.DropColumn(
                name: "CompanyLegalId",
                schema: "finanzas",
                table: "ParametrosSistema");

            migrationBuilder.DropColumn(
                name: "CompanyName",
                schema: "finanzas",
                table: "ParametrosSistema");

            migrationBuilder.DropColumn(
                name: "CompanyPhone",
                schema: "finanzas",
                table: "ParametrosSistema");
        }
    }
}
