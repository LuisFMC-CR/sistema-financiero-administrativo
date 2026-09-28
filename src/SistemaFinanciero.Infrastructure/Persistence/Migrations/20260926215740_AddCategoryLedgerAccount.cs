using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SistemaFinanciero.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCategoryLedgerAccount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1) La columna nace opcional para poder rellenar las categorías que ya existen.
            migrationBuilder.AddColumn<Guid>(
                name: "LedgerAccountId",
                schema: "catalogos",
                table: "CategoriasFinancieras",
                type: "uniqueidentifier",
                nullable: true);

            // 2) Cada naturaleza en uso recibe una cuenta contable raíz definitiva, "Ingresos" (tipo 4) o
            //    "Gastos" (tipo 5), a nombre del creador de su primera categoría, y las categorías existentes
            //    se asignan a ella. Son cuentas de nivel superior de las que pueden colgar otras; Finanzas
            //    puede reasignar las categorías. Si una naturaleza no tiene categorías no se crea nada.
            //    Si ya existiera una cuenta con ese código de otro tipo, la asignación queda vacía y el paso
            //    3 falla de forma explícita en lugar de asignar una cuenta incorrecta.
            migrationBuilder.Sql(
                """
                IF EXISTS (SELECT 1 FROM [catalogos].[CategoriasFinancieras] WHERE [Kind] = 1)
                   AND NOT EXISTS (SELECT 1 FROM [catalogos].[CuentasContables] WHERE [Code] = N'INGRESOS')
                BEGIN
                    DECLARE @creator uniqueidentifier = (
                        SELECT TOP (1) [CreatedByUserId] FROM [catalogos].[CategoriasFinancieras]
                        WHERE [Kind] = 1 ORDER BY [CreatedAtUtc], [Id]);
                    DECLARE @now datetimeoffset = TODATETIMEOFFSET(SYSUTCDATETIME(), 0);

                    INSERT INTO [catalogos].[CuentasContables]
                        ([Id], [Code], [Name], [Type], [ParentId], [CashKind], [Currency], [Reference],
                         [Description], [IsActive], [CreatedAtUtc], [CreatedByUserId], [UpdatedAtUtc], [UpdatedByUserId])
                    VALUES
                        (NEWID(), N'INGRESOS', N'Ingresos', 4, NULL, NULL, NULL, NULL,
                         NULL, 1, @now, @creator, @now, @creator);
                END;

                UPDATE [catalogos].[CategoriasFinancieras]
                SET [LedgerAccountId] = (
                    SELECT TOP (1) [Id] FROM [catalogos].[CuentasContables]
                    WHERE [Code] = N'INGRESOS' AND [Type] = 4)
                WHERE [Kind] = 1 AND [LedgerAccountId] IS NULL;
                """);

            migrationBuilder.Sql(
                """
                IF EXISTS (SELECT 1 FROM [catalogos].[CategoriasFinancieras] WHERE [Kind] = 2)
                   AND NOT EXISTS (SELECT 1 FROM [catalogos].[CuentasContables] WHERE [Code] = N'GASTOS')
                BEGIN
                    DECLARE @creator uniqueidentifier = (
                        SELECT TOP (1) [CreatedByUserId] FROM [catalogos].[CategoriasFinancieras]
                        WHERE [Kind] = 2 ORDER BY [CreatedAtUtc], [Id]);
                    DECLARE @now datetimeoffset = TODATETIMEOFFSET(SYSUTCDATETIME(), 0);

                    INSERT INTO [catalogos].[CuentasContables]
                        ([Id], [Code], [Name], [Type], [ParentId], [CashKind], [Currency], [Reference],
                         [Description], [IsActive], [CreatedAtUtc], [CreatedByUserId], [UpdatedAtUtc], [UpdatedByUserId])
                    VALUES
                        (NEWID(), N'GASTOS', N'Gastos', 5, NULL, NULL, NULL, NULL,
                         NULL, 1, @now, @creator, @now, @creator);
                END;

                UPDATE [catalogos].[CategoriasFinancieras]
                SET [LedgerAccountId] = (
                    SELECT TOP (1) [Id] FROM [catalogos].[CuentasContables]
                    WHERE [Code] = N'GASTOS' AND [Type] = 5)
                WHERE [Kind] = 2 AND [LedgerAccountId] IS NULL;
                """);

            // 3) Con todas las filas asignadas, la cuenta pasa a ser obligatoria.
            migrationBuilder.AlterColumn<Guid>(
                name: "LedgerAccountId",
                schema: "catalogos",
                table: "CategoriasFinancieras",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_CategoriasFinancieras_LedgerAccountId",
                schema: "catalogos",
                table: "CategoriasFinancieras",
                column: "LedgerAccountId");

            migrationBuilder.AddForeignKey(
                name: "FK_CategoriasFinancieras_CuentasContables",
                schema: "catalogos",
                table: "CategoriasFinancieras",
                column: "LedgerAccountId",
                principalSchema: "catalogos",
                principalTable: "CuentasContables",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CategoriasFinancieras_CuentasContables",
                schema: "catalogos",
                table: "CategoriasFinancieras");

            migrationBuilder.DropIndex(
                name: "IX_CategoriasFinancieras_LedgerAccountId",
                schema: "catalogos",
                table: "CategoriasFinancieras");

            migrationBuilder.DropColumn(
                name: "LedgerAccountId",
                schema: "catalogos",
                table: "CategoriasFinancieras");
        }
    }
}
