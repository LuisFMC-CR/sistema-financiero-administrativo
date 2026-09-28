using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SistemaFinanciero.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTaxTypes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TiposImpuesto",
                schema: "catalogos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    CalculationType = table.Column<int>(type: "int", nullable: false),
                    Rate = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TiposImpuesto", x => x.Id);
                    table.CheckConstraint("CK_TiposImpuesto_CalculationType", "[CalculationType] IN (1, 2)");
                    table.CheckConstraint("CK_TiposImpuesto_Codigo_NoVacio", "LEN(LTRIM(RTRIM([Code]))) > 0");
                    table.CheckConstraint("CK_TiposImpuesto_Rate", "[Rate] >= 0 AND ([CalculationType] <> 1 OR [Rate] <= 100)");
                    table.ForeignKey(
                        name: "FK_TiposImpuesto_Usuarios_CreatedBy",
                        column: x => x.CreatedByUserId,
                        principalSchema: "seguridad",
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TiposImpuesto_Usuarios_UpdatedBy",
                        column: x => x.UpdatedByUserId,
                        principalSchema: "seguridad",
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TiposImpuesto_CreatedByUserId",
                schema: "catalogos",
                table: "TiposImpuesto",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TiposImpuesto_IsActive_Name",
                schema: "catalogos",
                table: "TiposImpuesto",
                columns: new[] { "IsActive", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_TiposImpuesto_UpdatedByUserId",
                schema: "catalogos",
                table: "TiposImpuesto",
                column: "UpdatedByUserId");

            migrationBuilder.CreateIndex(
                name: "UX_TiposImpuesto_Code",
                schema: "catalogos",
                table: "TiposImpuesto",
                column: "Code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TiposImpuesto",
                schema: "catalogos");
        }
    }
}
