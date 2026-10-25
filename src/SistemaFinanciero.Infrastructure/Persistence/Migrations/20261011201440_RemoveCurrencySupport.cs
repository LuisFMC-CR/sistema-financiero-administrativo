using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SistemaFinanciero.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemoveCurrencySupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TiposCambioDiarios",
                schema: "finanzas");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Facturas_Currency",
                schema: "finanzas",
                table: "Facturas");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Facturas_UsdRate",
                schema: "finanzas",
                table: "Facturas");

            migrationBuilder.DropCheckConstraint(
                name: "CK_CuentasContables_Cash",
                schema: "catalogos",
                table: "CuentasContables");

            migrationBuilder.DropCheckConstraint(
                name: "CK_CuentasContables_Currency",
                schema: "catalogos",
                table: "CuentasContables");

            migrationBuilder.DropColumn(
                name: "ConfirmedCrcPerUsd",
                schema: "finanzas",
                table: "Facturas");

            migrationBuilder.DropColumn(
                name: "Currency",
                schema: "finanzas",
                table: "Facturas");

            migrationBuilder.DropColumn(
                name: "Currency",
                schema: "catalogos",
                table: "CuentasContables");

            migrationBuilder.AddCheckConstraint(
                name: "CK_CuentasContables_Cash",
                schema: "catalogos",
                table: "CuentasContables",
                sql: "[CashKind] IS NULL OR [Type] = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_CuentasContables_Cash",
                schema: "catalogos",
                table: "CuentasContables");

            migrationBuilder.AddColumn<decimal>(
                name: "ConfirmedCrcPerUsd",
                schema: "finanzas",
                table: "Facturas",
                type: "decimal(18,6)",
                precision: 18,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Currency",
                schema: "finanzas",
                table: "Facturas",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Currency",
                schema: "catalogos",
                table: "CuentasContables",
                type: "char(3)",
                unicode: false,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "TiposCambioDiarios",
                schema: "finanzas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EffectiveDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    CrcPerUsd = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    Source = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TiposCambioDiarios", x => x.Id);
                    table.CheckConstraint("CK_TiposCambioDiarios_CrcPerUsd", "[CrcPerUsd] > 0");
                    table.ForeignKey(
                        name: "FK_TiposCambioDiarios_Usuarios_CreatedBy",
                        column: x => x.CreatedByUserId,
                        principalSchema: "seguridad",
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TiposCambioDiarios_Usuarios_UpdatedBy",
                        column: x => x.UpdatedByUserId,
                        principalSchema: "seguridad",
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Facturas_Currency",
                schema: "finanzas",
                table: "Facturas",
                sql: "[Currency] IN (1, 2)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Facturas_UsdRate",
                schema: "finanzas",
                table: "Facturas",
                sql: "[Currency] <> 2 OR [Status] = 1 OR [ConfirmedCrcPerUsd] > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_CuentasContables_Cash",
                schema: "catalogos",
                table: "CuentasContables",
                sql: "([CashKind] IS NULL AND [Currency] IS NULL) OR ([CashKind] IS NOT NULL AND [Currency] IS NOT NULL AND [Type] = 1)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_CuentasContables_Currency",
                schema: "catalogos",
                table: "CuentasContables",
                sql: "[Currency] IS NULL OR [Currency] IN ('CRC', 'USD')");

            migrationBuilder.CreateIndex(
                name: "IX_TiposCambioDiarios_CreatedByUserId",
                schema: "finanzas",
                table: "TiposCambioDiarios",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TiposCambioDiarios_UpdatedByUserId",
                schema: "finanzas",
                table: "TiposCambioDiarios",
                column: "UpdatedByUserId");

            migrationBuilder.CreateIndex(
                name: "UX_TiposCambioDiarios_EffectiveDate",
                schema: "finanzas",
                table: "TiposCambioDiarios",
                column: "EffectiveDate",
                unique: true);
        }
    }
}
