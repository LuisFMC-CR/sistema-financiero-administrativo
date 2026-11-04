using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SistemaFinanciero.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomerCreditLimit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "CreditLimit",
                schema: "catalogos",
                table: "Clientes",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Clientes_CreditLimit",
                schema: "catalogos",
                table: "Clientes",
                sql: "[CreditLimit] IS NULL OR [CreditLimit] >= 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Clientes_CreditLimit",
                schema: "catalogos",
                table: "Clientes");

            migrationBuilder.DropColumn(
                name: "CreditLimit",
                schema: "catalogos",
                table: "Clientes");
        }
    }
}
