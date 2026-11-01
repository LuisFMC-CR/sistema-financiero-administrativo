using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SistemaFinanciero.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Asientos contables automáticos de partida doble (RF-78/RF-79, bloque 5 del paquete de mejoras
    /// finales): <c>finanzas.Asientos</c>/<c>finanzas.AsientoLineas</c>, inmutables, y su secuencia de
    /// numeración <c>finanzas.Seq_AsientoNumero</c>.
    /// </summary>
    /// <inheritdoc />
    public partial class AddJournalEntries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateSequence(
                name: "Seq_AsientoNumero",
                schema: "finanzas",
                startValue: 1L);

            migrationBuilder.CreateTable(
                name: "Asientos",
                schema: "finanzas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Number = table.Column<long>(type: "bigint", nullable: false),
                    EntryDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    SourceType = table.Column<int>(type: "int", nullable: false),
                    SourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReversesEntryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Asientos", x => x.Id);
                    table.CheckConstraint("CK_Asientos_Number", "[Number] > 0");
                    table.CheckConstraint("CK_Asientos_SourceType", "[SourceType] BETWEEN 1 AND 6");
                    table.ForeignKey(
                        name: "FK_Asientos_Asientos_ReversesEntryId",
                        column: x => x.ReversesEntryId,
                        principalSchema: "finanzas",
                        principalTable: "Asientos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Asientos_Usuarios_CreatedBy",
                        column: x => x.CreatedByUserId,
                        principalSchema: "seguridad",
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AsientoLineas",
                schema: "finanzas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LineNumber = table.Column<int>(type: "int", nullable: false),
                    AccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Debit = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Credit = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    JournalEntryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AsientoLineas", x => x.Id);
                    table.CheckConstraint("CK_AsientoLineas_DebitCredit", "[Debit] >= 0 AND [Credit] >= 0 AND (([Debit] > 0 AND [Credit] = 0) OR ([Debit] = 0 AND [Credit] > 0))");
                    table.CheckConstraint("CK_AsientoLineas_Position", "[LineNumber] > 0");
                    table.ForeignKey(
                        name: "FK_AsientoLineas_Asientos",
                        column: x => x.JournalEntryId,
                        principalSchema: "finanzas",
                        principalTable: "Asientos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AsientoLineas_CuentasContables",
                        column: x => x.AccountId,
                        principalSchema: "catalogos",
                        principalTable: "CuentasContables",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AsientoLineas_AccountId",
                schema: "finanzas",
                table: "AsientoLineas",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_AsientoLineas_JournalEntryId",
                schema: "finanzas",
                table: "AsientoLineas",
                column: "JournalEntryId");

            migrationBuilder.CreateIndex(
                name: "UX_AsientoLineas_JournalEntryId_LineNumber",
                schema: "finanzas",
                table: "AsientoLineas",
                columns: new[] { "JournalEntryId", "LineNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Asientos_CreatedByUserId",
                schema: "finanzas",
                table: "Asientos",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Asientos_EntryDate",
                schema: "finanzas",
                table: "Asientos",
                column: "EntryDate");

            migrationBuilder.CreateIndex(
                name: "IX_Asientos_SourceType_SourceId",
                schema: "finanzas",
                table: "Asientos",
                columns: new[] { "SourceType", "SourceId" });

            migrationBuilder.CreateIndex(
                name: "UX_Asientos_Number",
                schema: "finanzas",
                table: "Asientos",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_Asientos_ReversesEntryId",
                schema: "finanzas",
                table: "Asientos",
                column: "ReversesEntryId",
                unique: true,
                filter: "[ReversesEntryId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AsientoLineas",
                schema: "finanzas");

            migrationBuilder.DropTable(
                name: "Asientos",
                schema: "finanzas");

            migrationBuilder.DropSequence(
                name: "Seq_AsientoNumero",
                schema: "finanzas");
        }
    }
}
