using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SistemaFinanciero.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCashTransfers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateSequence(
                name: "Seq_TraspasoCodigo",
                schema: "finanzas",
                startValue: 1L);

            migrationBuilder.DropCheckConstraint(
                name: "CK_BitacoraOperaciones_EntityType",
                schema: "seguridad",
                table: "BitacoraOperaciones");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Asientos_SourceType",
                schema: "finanzas",
                table: "Asientos");

            migrationBuilder.CreateTable(
                name: "Traspasos",
                schema: "finanzas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    SourceCashAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DestinationCashAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Reference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    TransferDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CancellationReason = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    CancelledAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CancelledByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Traspasos", x => x.Id);
                    table.CheckConstraint("CK_Traspasos_Amount", "[Amount] > 0");
                    table.CheckConstraint("CK_Traspasos_DifferentAccounts", "[SourceCashAccountId] <> [DestinationCashAccountId]");
                    table.CheckConstraint("CK_Traspasos_Status", "[Status] IN (1, 2)");
                    table.ForeignKey(
                        name: "FK_Traspasos_CuentasContables_Destination",
                        column: x => x.DestinationCashAccountId,
                        principalSchema: "catalogos",
                        principalTable: "CuentasContables",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Traspasos_CuentasContables_Source",
                        column: x => x.SourceCashAccountId,
                        principalSchema: "catalogos",
                        principalTable: "CuentasContables",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Traspasos_Usuarios_CancelledBy",
                        column: x => x.CancelledByUserId,
                        principalSchema: "seguridad",
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Traspasos_Usuarios_CreatedBy",
                        column: x => x.CreatedByUserId,
                        principalSchema: "seguridad",
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Traspasos_Usuarios_UpdatedBy",
                        column: x => x.UpdatedByUserId,
                        principalSchema: "seguridad",
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_BitacoraOperaciones_EntityType",
                schema: "seguridad",
                table: "BitacoraOperaciones",
                sql: "[EntityType] BETWEEN 1 AND 9");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Asientos_SourceType",
                schema: "finanzas",
                table: "Asientos",
                sql: "[SourceType] BETWEEN 1 AND 8");

            migrationBuilder.CreateIndex(
                name: "IX_Traspasos_CancelledByUserId",
                schema: "finanzas",
                table: "Traspasos",
                column: "CancelledByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Traspasos_CreatedByUserId",
                schema: "finanzas",
                table: "Traspasos",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Traspasos_DestinationCashAccountId_Reference",
                schema: "finanzas",
                table: "Traspasos",
                columns: new[] { "DestinationCashAccountId", "Reference" });

            migrationBuilder.CreateIndex(
                name: "IX_Traspasos_SourceCashAccountId_Reference",
                schema: "finanzas",
                table: "Traspasos",
                columns: new[] { "SourceCashAccountId", "Reference" });

            migrationBuilder.CreateIndex(
                name: "IX_Traspasos_Status_TransferDate",
                schema: "finanzas",
                table: "Traspasos",
                columns: new[] { "Status", "TransferDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Traspasos_UpdatedByUserId",
                schema: "finanzas",
                table: "Traspasos",
                column: "UpdatedByUserId");

            migrationBuilder.CreateIndex(
                name: "UX_Traspasos_Code",
                schema: "finanzas",
                table: "Traspasos",
                column: "Code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Traspasos",
                schema: "finanzas");

            migrationBuilder.DropSequence(
                name: "Seq_TraspasoCodigo",
                schema: "finanzas");

            migrationBuilder.DropCheckConstraint(
                name: "CK_BitacoraOperaciones_EntityType",
                schema: "seguridad",
                table: "BitacoraOperaciones");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Asientos_SourceType",
                schema: "finanzas",
                table: "Asientos");

            migrationBuilder.AddCheckConstraint(
                name: "CK_BitacoraOperaciones_EntityType",
                schema: "seguridad",
                table: "BitacoraOperaciones",
                sql: "[EntityType] BETWEEN 1 AND 8");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Asientos_SourceType",
                schema: "finanzas",
                table: "Asientos",
                sql: "[SourceType] BETWEEN 1 AND 7");
        }
    }
}
