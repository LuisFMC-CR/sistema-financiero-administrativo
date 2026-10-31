using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SistemaFinanciero.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class WidenSecurityAuditActionRange : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_EventosSeguridad_Action",
                schema: "seguridad",
                table: "EventosSeguridad");

            migrationBuilder.AddCheckConstraint(
                name: "CK_EventosSeguridad_Action",
                schema: "seguridad",
                table: "EventosSeguridad",
                sql: "[Action] BETWEEN 1 AND 12");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_EventosSeguridad_Action",
                schema: "seguridad",
                table: "EventosSeguridad");

            migrationBuilder.AddCheckConstraint(
                name: "CK_EventosSeguridad_Action",
                schema: "seguridad",
                table: "EventosSeguridad",
                sql: "[Action] BETWEEN 1 AND 9");
        }
    }
}
