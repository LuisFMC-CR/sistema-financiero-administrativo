using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SistemaFinanciero.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCompanyTerritorialData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CompanyCantonCode",
                schema: "finanzas",
                table: "ParametrosSistema",
                type: "nvarchar(2)",
                maxLength: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CompanyDistrictCode",
                schema: "finanzas",
                table: "ParametrosSistema",
                type: "nvarchar(2)",
                maxLength: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CompanyProvinceCode",
                schema: "finanzas",
                table: "ParametrosSistema",
                type: "nvarchar(1)",
                maxLength: 1,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CompanyTradeName",
                schema: "finanzas",
                table: "ParametrosSistema",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CompanyCantonCode",
                schema: "finanzas",
                table: "ParametrosEmpresaHistorial",
                type: "nvarchar(2)",
                maxLength: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CompanyDistrictCode",
                schema: "finanzas",
                table: "ParametrosEmpresaHistorial",
                type: "nvarchar(2)",
                maxLength: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CompanyProvinceCode",
                schema: "finanzas",
                table: "ParametrosEmpresaHistorial",
                type: "nvarchar(1)",
                maxLength: 1,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CompanyTradeName",
                schema: "finanzas",
                table: "ParametrosEmpresaHistorial",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_ParametrosSistema_CompanyCantonCode",
                schema: "finanzas",
                table: "ParametrosSistema",
                sql: "[CompanyCantonCode] IS NULL OR LEN([CompanyCantonCode]) = 2");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ParametrosSistema_CompanyDistrictCode",
                schema: "finanzas",
                table: "ParametrosSistema",
                sql: "[CompanyDistrictCode] IS NULL OR LEN([CompanyDistrictCode]) = 2");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ParametrosSistema_CompanyProvinceCode",
                schema: "finanzas",
                table: "ParametrosSistema",
                sql: "[CompanyProvinceCode] IS NULL OR LEN([CompanyProvinceCode]) = 1");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ParametrosEmpresaHistorial_CompanyCantonCode",
                schema: "finanzas",
                table: "ParametrosEmpresaHistorial",
                sql: "[CompanyCantonCode] IS NULL OR LEN([CompanyCantonCode]) = 2");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ParametrosEmpresaHistorial_CompanyDistrictCode",
                schema: "finanzas",
                table: "ParametrosEmpresaHistorial",
                sql: "[CompanyDistrictCode] IS NULL OR LEN([CompanyDistrictCode]) = 2");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ParametrosEmpresaHistorial_CompanyProvinceCode",
                schema: "finanzas",
                table: "ParametrosEmpresaHistorial",
                sql: "[CompanyProvinceCode] IS NULL OR LEN([CompanyProvinceCode]) = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_ParametrosSistema_CompanyCantonCode",
                schema: "finanzas",
                table: "ParametrosSistema");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ParametrosSistema_CompanyDistrictCode",
                schema: "finanzas",
                table: "ParametrosSistema");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ParametrosSistema_CompanyProvinceCode",
                schema: "finanzas",
                table: "ParametrosSistema");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ParametrosEmpresaHistorial_CompanyCantonCode",
                schema: "finanzas",
                table: "ParametrosEmpresaHistorial");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ParametrosEmpresaHistorial_CompanyDistrictCode",
                schema: "finanzas",
                table: "ParametrosEmpresaHistorial");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ParametrosEmpresaHistorial_CompanyProvinceCode",
                schema: "finanzas",
                table: "ParametrosEmpresaHistorial");

            migrationBuilder.DropColumn(
                name: "CompanyCantonCode",
                schema: "finanzas",
                table: "ParametrosSistema");

            migrationBuilder.DropColumn(
                name: "CompanyDistrictCode",
                schema: "finanzas",
                table: "ParametrosSistema");

            migrationBuilder.DropColumn(
                name: "CompanyProvinceCode",
                schema: "finanzas",
                table: "ParametrosSistema");

            migrationBuilder.DropColumn(
                name: "CompanyTradeName",
                schema: "finanzas",
                table: "ParametrosSistema");

            migrationBuilder.DropColumn(
                name: "CompanyCantonCode",
                schema: "finanzas",
                table: "ParametrosEmpresaHistorial");

            migrationBuilder.DropColumn(
                name: "CompanyDistrictCode",
                schema: "finanzas",
                table: "ParametrosEmpresaHistorial");

            migrationBuilder.DropColumn(
                name: "CompanyProvinceCode",
                schema: "finanzas",
                table: "ParametrosEmpresaHistorial");

            migrationBuilder.DropColumn(
                name: "CompanyTradeName",
                schema: "finanzas",
                table: "ParametrosEmpresaHistorial");
        }
    }
}
