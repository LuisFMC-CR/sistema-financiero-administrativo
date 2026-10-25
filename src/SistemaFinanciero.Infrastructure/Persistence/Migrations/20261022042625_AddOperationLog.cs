using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SistemaFinanciero.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOperationLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BitacoraOperaciones",
                schema: "seguridad",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EntityType = table.Column<int>(type: "int", nullable: false),
                    EntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Action = table.Column<int>(type: "int", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    PerformedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    PerformedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BitacoraOperaciones", x => x.Id);
                    table.CheckConstraint("CK_BitacoraOperaciones_Action", "[Action] BETWEEN 1 AND 2");
                    table.CheckConstraint("CK_BitacoraOperaciones_EntityType", "[EntityType] BETWEEN 1 AND 4");
                    table.ForeignKey(
                        name: "FK_BitacoraOperaciones_Usuarios_PerformedBy",
                        column: x => x.PerformedByUserId,
                        principalSchema: "seguridad",
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BitacoraOperaciones_EntityType_EntityId",
                schema: "seguridad",
                table: "BitacoraOperaciones",
                columns: new[] { "EntityType", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_BitacoraOperaciones_PerformedAtUtc",
                schema: "seguridad",
                table: "BitacoraOperaciones",
                column: "PerformedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_BitacoraOperaciones_PerformedByUserId",
                schema: "seguridad",
                table: "BitacoraOperaciones",
                column: "PerformedByUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BitacoraOperaciones",
                schema: "seguridad");
        }
    }
}
