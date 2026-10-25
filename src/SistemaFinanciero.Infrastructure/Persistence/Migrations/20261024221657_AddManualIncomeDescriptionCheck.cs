using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SistemaFinanciero.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddManualIncomeDescriptionCheck : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddCheckConstraint(
                name: "CK_Ingresos_Description",
                schema: "finanzas",
                table: "Ingresos",
                sql: "([Origin] = 3 AND [Description] IS NOT NULL) OR ([Origin] IN (1, 2))");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Ingresos_Description",
                schema: "finanzas",
                table: "Ingresos");
        }
    }
}
