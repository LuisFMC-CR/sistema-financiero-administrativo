using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SistemaFinanciero.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLedgerAccounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CuentasContables",
                schema: "catalogos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    ParentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CashKind = table.Column<int>(type: "int", nullable: true),
                    Currency = table.Column<string>(type: "char(3)", unicode: false, nullable: true),
                    Reference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
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
                    table.PrimaryKey("PK_CuentasContables", x => x.Id);
                    table.CheckConstraint("CK_CuentasContables_Cash", "([CashKind] IS NULL AND [Currency] IS NULL) OR ([CashKind] IS NOT NULL AND [Currency] IS NOT NULL AND [Type] = 1)");
                    table.CheckConstraint("CK_CuentasContables_CashKind", "[CashKind] IS NULL OR [CashKind] IN (1, 2)");
                    table.CheckConstraint("CK_CuentasContables_Codigo_NoVacio", "LEN(LTRIM(RTRIM([Code]))) > 0");
                    table.CheckConstraint("CK_CuentasContables_Currency", "[Currency] IS NULL OR [Currency] IN ('CRC', 'USD')");
                    table.CheckConstraint("CK_CuentasContables_Parent", "[ParentId] IS NULL OR [ParentId] <> [Id]");
                    table.CheckConstraint("CK_CuentasContables_Type", "[Type] IN (1, 2, 3, 4, 5)");
                    table.ForeignKey(
                        name: "FK_CuentasContables_Parent",
                        column: x => x.ParentId,
                        principalSchema: "catalogos",
                        principalTable: "CuentasContables",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CuentasContables_Usuarios_CreatedBy",
                        column: x => x.CreatedByUserId,
                        principalSchema: "seguridad",
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CuentasContables_Usuarios_UpdatedBy",
                        column: x => x.UpdatedByUserId,
                        principalSchema: "seguridad",
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CuentasContables_CreatedByUserId",
                schema: "catalogos",
                table: "CuentasContables",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CuentasContables_IsActive_Type_Name",
                schema: "catalogos",
                table: "CuentasContables",
                columns: new[] { "IsActive", "Type", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_CuentasContables_ParentId",
                schema: "catalogos",
                table: "CuentasContables",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_CuentasContables_UpdatedByUserId",
                schema: "catalogos",
                table: "CuentasContables",
                column: "UpdatedByUserId");

            migrationBuilder.CreateIndex(
                name: "UX_CuentasContables_Code",
                schema: "catalogos",
                table: "CuentasContables",
                column: "Code",
                unique: true);

            // Las cajas y cuentas bancarias existentes pasan a ser cuentas contables de tipo Activo (1),
            // de efectivo, con los mismos identificadores, código, nombre, moneda, referencia, estado y
            // auditoría. El tipo antiguo (1 = caja, 2 = banco) coincide con el subtipo nuevo. La tabla
            // CuentasFinancieras se conserva hasta que se retire su código.
            migrationBuilder.Sql(
                """
                INSERT INTO [catalogos].[CuentasContables]
                    ([Id], [Code], [Name], [Type], [ParentId], [CashKind], [Currency], [Reference],
                     [Description], [IsActive], [CreatedAtUtc], [CreatedByUserId], [UpdatedAtUtc], [UpdatedByUserId])
                SELECT
                    [Id], [Code], [Name], 1, NULL, [Type], [Currency], [Reference],
                    NULL, [IsActive], [CreatedAtUtc], [CreatedByUserId], [UpdatedAtUtc], [UpdatedByUserId]
                FROM [finanzas].[CuentasFinancieras];
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CuentasContables",
                schema: "catalogos");
        }
    }
}
