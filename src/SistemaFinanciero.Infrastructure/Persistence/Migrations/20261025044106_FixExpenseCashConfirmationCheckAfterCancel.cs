using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SistemaFinanciero.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FixExpenseCashConfirmationCheckAfterCancel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Gastos_CashConfirmation",
                schema: "finanzas",
                table: "Gastos");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Gastos_CashConfirmation",
                schema: "finanzas",
                table: "Gastos",
                sql: "(([Status] IN (2, 3) AND [PaymentTerm] = 1) AND [CashAccountId] IS NOT NULL AND [Reference] IS NOT NULL) OR (NOT ([Status] IN (2, 3) AND [PaymentTerm] = 1) AND [CashAccountId] IS NULL AND [Reference] IS NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Gastos_CashConfirmation",
                schema: "finanzas",
                table: "Gastos");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Gastos_CashConfirmation",
                schema: "finanzas",
                table: "Gastos",
                sql: "(([Status] = 2 AND [PaymentTerm] = 1) AND [CashAccountId] IS NOT NULL AND [Reference] IS NOT NULL) OR (NOT ([Status] = 2 AND [PaymentTerm] = 1) AND [CashAccountId] IS NULL AND [Reference] IS NULL)");
        }
    }
}
