using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SistemaFinanciero.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FixJournalEntrySourceTypeCheckConstraint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Asientos_SourceType",
                schema: "finanzas",
                table: "Asientos");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Asientos_SourceType",
                schema: "finanzas",
                table: "Asientos",
                sql: "[SourceType] BETWEEN 1 AND 7");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Asientos_SourceType",
                schema: "finanzas",
                table: "Asientos");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Asientos_SourceType",
                schema: "finanzas",
                table: "Asientos",
                sql: "[SourceType] BETWEEN 1 AND 6");
        }
    }
}
