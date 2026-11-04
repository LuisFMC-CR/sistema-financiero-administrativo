using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SistemaFinanciero.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomerVatConditionAndStateWithholding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AppliesStateWithholding",
                schema: "catalogos",
                table: "Clientes",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "VatCondition",
                schema: "catalogos",
                table: "Clientes",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Clientes_VatCondition",
                schema: "catalogos",
                table: "Clientes",
                sql: "[VatCondition] IN (1, 2, 3)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Clientes_VatCondition",
                schema: "catalogos",
                table: "Clientes");

            migrationBuilder.DropColumn(
                name: "AppliesStateWithholding",
                schema: "catalogos",
                table: "Clientes");

            migrationBuilder.DropColumn(
                name: "VatCondition",
                schema: "catalogos",
                table: "Clientes");
        }
    }
}
