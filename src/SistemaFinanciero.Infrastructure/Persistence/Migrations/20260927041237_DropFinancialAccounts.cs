using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SistemaFinanciero.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DropFinancialAccounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Verificación previa: cada cuenta financiera debe tener su equivalente en cuentas contables
            // (mismo Id, tipo Activo, mismo subtipo y misma moneda). Código, nombre, referencia y estado
            // pueden haberse editado después en el catálogo nuevo. Si falta alguna, la migración se
            // interrumpe y no se elimina nada.
            migrationBuilder.Sql(
                """
                IF EXISTS (
                    SELECT 1
                    FROM [finanzas].[CuentasFinancieras] AS financial
                    WHERE NOT EXISTS (
                        SELECT 1
                        FROM [catalogos].[CuentasContables] AS ledger
                        WHERE ledger.[Id] = financial.[Id]
                          AND ledger.[Type] = 1
                          AND ledger.[CashKind] = financial.[Type]
                          AND ledger.[Currency] = financial.[Currency]))
                    THROW 50001, N'Hay cuentas financieras sin su cuenta contable de efectivo equivalente; no se elimina la tabla CuentasFinancieras.', 1;
                """);

            migrationBuilder.DropTable(
                name: "CuentasFinancieras",
                schema: "finanzas");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CuentasFinancieras",
                schema: "finanzas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Currency = table.Column<string>(type: "char(3)", unicode: false, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Reference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CuentasFinancieras", x => x.Id);
                    table.CheckConstraint("CK_CuentasFinancieras_Codigo_NoVacio", "LEN(LTRIM(RTRIM([Code]))) > 0");
                    table.CheckConstraint("CK_CuentasFinancieras_Currency", "[Currency] IN ('CRC', 'USD')");
                    table.CheckConstraint("CK_CuentasFinancieras_Type", "[Type] IN (1, 2)");
                    table.ForeignKey(
                        name: "FK_CuentasFinancieras_Usuarios_CreatedBy",
                        column: x => x.CreatedByUserId,
                        principalSchema: "seguridad",
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CuentasFinancieras_Usuarios_UpdatedBy",
                        column: x => x.UpdatedByUserId,
                        principalSchema: "seguridad",
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CuentasFinancieras_CreatedByUserId",
                schema: "finanzas",
                table: "CuentasFinancieras",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CuentasFinancieras_IsActive_Currency_Name",
                schema: "finanzas",
                table: "CuentasFinancieras",
                columns: new[] { "IsActive", "Currency", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_CuentasFinancieras_UpdatedByUserId",
                schema: "finanzas",
                table: "CuentasFinancieras",
                column: "UpdatedByUserId");

            migrationBuilder.CreateIndex(
                name: "UX_CuentasFinancieras_Code",
                schema: "finanzas",
                table: "CuentasFinancieras",
                column: "Code",
                unique: true);

            // Al revertir, las cuentas contables de efectivo vuelven a la tabla antigua con su subtipo como
            // tipo, de modo que no se pierdan cajas ni bancos aunque se hayan creado después de esta migración.
            migrationBuilder.Sql(
                """
                INSERT INTO [finanzas].[CuentasFinancieras]
                    ([Id], [Code], [Name], [Type], [Currency], [Reference], [IsActive],
                     [CreatedAtUtc], [CreatedByUserId], [UpdatedAtUtc], [UpdatedByUserId])
                SELECT
                    [Id], [Code], [Name], [CashKind], [Currency], [Reference], [IsActive],
                    [CreatedAtUtc], [CreatedByUserId], [UpdatedAtUtc], [UpdatedByUserId]
                FROM [catalogos].[CuentasContables]
                WHERE [CashKind] IS NOT NULL;
                """);
        }
    }
}
