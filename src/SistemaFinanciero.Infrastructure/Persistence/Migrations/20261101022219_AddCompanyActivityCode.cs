using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SistemaFinanciero.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCompanyActivityCode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CompanyActivityCode",
                schema: "finanzas",
                table: "ParametrosSistema",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CompanyActivityCode",
                schema: "finanzas",
                table: "ParametrosEmpresaHistorial",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CompanyActivityCode",
                schema: "finanzas",
                table: "ParametrosSistema");

            migrationBuilder.DropColumn(
                name: "CompanyActivityCode",
                schema: "finanzas",
                table: "ParametrosEmpresaHistorial");
        }
    }
}
