using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SistemaFinanciero.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddInvoicePaymentTerm : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "DueDate",
                schema: "finanzas",
                table: "Facturas",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PaymentTerm",
                schema: "finanzas",
                table: "Facturas",
                type: "int",
                nullable: false,
                defaultValue: 1);

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

            migrationBuilder.DropColumn(
                name: "DueDate",
                schema: "finanzas",
                table: "Facturas");

            migrationBuilder.DropColumn(
                name: "PaymentTerm",
                schema: "finanzas",
                table: "Facturas");
        }
    }
}
