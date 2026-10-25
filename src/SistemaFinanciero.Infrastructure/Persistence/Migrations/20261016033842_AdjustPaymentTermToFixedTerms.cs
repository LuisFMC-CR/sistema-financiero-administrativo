using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SistemaFinanciero.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AdjustPaymentTermToFixedTerms : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Facturas_DueDate",
                schema: "finanzas",
                table: "Facturas");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Facturas_PaymentTerm",
                schema: "finanzas",
                table: "Facturas");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Facturas_DueDate",
                schema: "finanzas",
                table: "Facturas",
                sql: "([PaymentTerm] = 1 AND [DueDate] IS NULL) OR ([PaymentTerm] = 2 AND [DueDate] = DATEADD(DAY, 30, [IssueDate])) OR ([PaymentTerm] = 3 AND [DueDate] = DATEADD(DAY, 60, [IssueDate])) OR ([PaymentTerm] = 4 AND [DueDate] = DATEADD(DAY, 90, [IssueDate]))");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Facturas_PaymentTerm",
                schema: "finanzas",
                table: "Facturas",
                sql: "[PaymentTerm] IN (1, 2, 3, 4)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Facturas_DueDate",
                schema: "finanzas",
                table: "Facturas");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Facturas_PaymentTerm",
                schema: "finanzas",
                table: "Facturas");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Facturas_DueDate",
                schema: "finanzas",
                table: "Facturas",
                sql: "([PaymentTerm] = 1 AND [DueDate] IS NULL) OR ([PaymentTerm] = 2 AND [DueDate] IS NOT NULL AND [DueDate] >= [IssueDate])");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Facturas_PaymentTerm",
                schema: "finanzas",
                table: "Facturas",
                sql: "[PaymentTerm] IN (1, 2)");
        }
    }
}
