using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SistemaFinanciero.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLedgerAccountMovementClassification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Facturas_CategoriasFinancieras",
                schema: "finanzas",
                table: "Facturas");

            migrationBuilder.DropForeignKey(
                name: "FK_Gastos_CategoriasFinancieras",
                schema: "finanzas",
                table: "Gastos");

            migrationBuilder.DropForeignKey(
                name: "FK_Ingresos_CategoriasFinancieras",
                schema: "finanzas",
                table: "Ingresos");

            migrationBuilder.DropForeignKey(
                name: "FK_Presupuestos_CategoriasFinancieras",
                schema: "finanzas",
                table: "Presupuestos");

            migrationBuilder.DropForeignKey(
                name: "FK_ProductosServicios_CategoriasFinancieras",
                schema: "catalogos",
                table: "ProductosServicios");

            // Las columnas históricas contienen Id de categoría. Antes de convertirlas en referencias
            // directas al catálogo contable se reemplazan por la cuenta vinculada a cada categoría.
            migrationBuilder.Sql("""
                UPDATE f SET IncomeCategoryId = c.LedgerAccountId
                FROM finanzas.Facturas AS f
                INNER JOIN catalogos.CategoriasFinancieras AS c ON c.Id = f.IncomeCategoryId;

                UPDATE g SET CategoryId = c.LedgerAccountId
                FROM finanzas.Gastos AS g
                INNER JOIN catalogos.CategoriasFinancieras AS c ON c.Id = g.CategoryId;

                UPDATE i SET CategoryId = c.LedgerAccountId
                FROM finanzas.Ingresos AS i
                INNER JOIN catalogos.CategoriasFinancieras AS c ON c.Id = i.CategoryId;

                UPDATE p SET CategoryId = c.LedgerAccountId
                FROM finanzas.Presupuestos AS p
                INNER JOIN catalogos.CategoriasFinancieras AS c ON c.Id = p.CategoryId;

                UPDATE ps SET DefaultIncomeCategoryId = c.LedgerAccountId
                FROM catalogos.ProductosServicios AS ps
                INNER JOIN catalogos.CategoriasFinancieras AS c ON c.Id = ps.DefaultIncomeCategoryId;
                """);

            migrationBuilder.DropTable(
                name: "CategoriasFinancieras",
                schema: "catalogos");

            migrationBuilder.RenameColumn(
                name: "DefaultIncomeCategoryId",
                schema: "catalogos",
                table: "ProductosServicios",
                newName: "DefaultIncomeAccountId");

            migrationBuilder.RenameIndex(
                name: "IX_ProductosServicios_DefaultIncomeCategoryId",
                schema: "catalogos",
                table: "ProductosServicios",
                newName: "IX_ProductosServicios_DefaultIncomeAccountId");

            migrationBuilder.RenameColumn(
                name: "CategoryId",
                schema: "finanzas",
                table: "Presupuestos",
                newName: "AccountId");

            migrationBuilder.RenameIndex(
                name: "UX_Presupuestos_CategoryId_Period",
                schema: "finanzas",
                table: "Presupuestos",
                newName: "UX_Presupuestos_AccountId_Period");

            migrationBuilder.RenameColumn(
                name: "CategoryId",
                schema: "finanzas",
                table: "Ingresos",
                newName: "AccountId");

            migrationBuilder.RenameIndex(
                name: "IX_Ingresos_CategoryId",
                schema: "finanzas",
                table: "Ingresos",
                newName: "IX_Ingresos_AccountId");

            migrationBuilder.RenameColumn(
                name: "CategoryId",
                schema: "finanzas",
                table: "Gastos",
                newName: "AccountId");

            migrationBuilder.RenameIndex(
                name: "IX_Gastos_CategoryId",
                schema: "finanzas",
                table: "Gastos",
                newName: "IX_Gastos_AccountId");

            migrationBuilder.RenameColumn(
                name: "IncomeCategoryId",
                schema: "finanzas",
                table: "Facturas",
                newName: "IncomeAccountId");

            migrationBuilder.RenameIndex(
                name: "IX_Facturas_IncomeCategoryId",
                schema: "finanzas",
                table: "Facturas",
                newName: "IX_Facturas_IncomeAccountId");

            migrationBuilder.AddColumn<bool>(
                name: "IsMovement",
                schema: "catalogos",
                table: "CuentasContables",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.Sql("""
                UPDATE [catalogos].[CuentasContables]
                SET [IsMovement] = CASE
                    WHEN [Code] LIKE '[1-5]' OR [Code] LIKE '[1-5].[0-9]' THEN 0
                    ELSE 1
                END;
                """);

            migrationBuilder.AddForeignKey(
                name: "FK_Facturas_CategoriasFinancieras",
                schema: "finanzas",
                table: "Facturas",
                column: "IncomeAccountId",
                principalSchema: "catalogos",
                principalTable: "CuentasContables",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Gastos_CategoriasFinancieras",
                schema: "finanzas",
                table: "Gastos",
                column: "AccountId",
                principalSchema: "catalogos",
                principalTable: "CuentasContables",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Ingresos_CategoriasFinancieras",
                schema: "finanzas",
                table: "Ingresos",
                column: "AccountId",
                principalSchema: "catalogos",
                principalTable: "CuentasContables",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Presupuestos_CategoriasFinancieras",
                schema: "finanzas",
                table: "Presupuestos",
                column: "AccountId",
                principalSchema: "catalogos",
                principalTable: "CuentasContables",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ProductosServicios_CategoriasFinancieras",
                schema: "catalogos",
                table: "ProductosServicios",
                column: "DefaultIncomeAccountId",
                principalSchema: "catalogos",
                principalTable: "CuentasContables",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Facturas_CategoriasFinancieras",
                schema: "finanzas",
                table: "Facturas");

            migrationBuilder.DropForeignKey(
                name: "FK_Gastos_CategoriasFinancieras",
                schema: "finanzas",
                table: "Gastos");

            migrationBuilder.DropForeignKey(
                name: "FK_Ingresos_CategoriasFinancieras",
                schema: "finanzas",
                table: "Ingresos");

            migrationBuilder.DropForeignKey(
                name: "FK_Presupuestos_CategoriasFinancieras",
                schema: "finanzas",
                table: "Presupuestos");

            migrationBuilder.DropForeignKey(
                name: "FK_ProductosServicios_CategoriasFinancieras",
                schema: "catalogos",
                table: "ProductosServicios");

            migrationBuilder.DropColumn(
                name: "IsMovement",
                schema: "catalogos",
                table: "CuentasContables");

            migrationBuilder.RenameColumn(
                name: "DefaultIncomeAccountId",
                schema: "catalogos",
                table: "ProductosServicios",
                newName: "DefaultIncomeCategoryId");

            migrationBuilder.RenameIndex(
                name: "IX_ProductosServicios_DefaultIncomeAccountId",
                schema: "catalogos",
                table: "ProductosServicios",
                newName: "IX_ProductosServicios_DefaultIncomeCategoryId");

            migrationBuilder.RenameColumn(
                name: "AccountId",
                schema: "finanzas",
                table: "Presupuestos",
                newName: "CategoryId");

            migrationBuilder.RenameIndex(
                name: "UX_Presupuestos_AccountId_Period",
                schema: "finanzas",
                table: "Presupuestos",
                newName: "UX_Presupuestos_CategoryId_Period");

            migrationBuilder.RenameColumn(
                name: "AccountId",
                schema: "finanzas",
                table: "Ingresos",
                newName: "CategoryId");

            migrationBuilder.RenameIndex(
                name: "IX_Ingresos_AccountId",
                schema: "finanzas",
                table: "Ingresos",
                newName: "IX_Ingresos_CategoryId");

            migrationBuilder.RenameColumn(
                name: "AccountId",
                schema: "finanzas",
                table: "Gastos",
                newName: "CategoryId");

            migrationBuilder.RenameIndex(
                name: "IX_Gastos_AccountId",
                schema: "finanzas",
                table: "Gastos",
                newName: "IX_Gastos_CategoryId");

            migrationBuilder.RenameColumn(
                name: "IncomeAccountId",
                schema: "finanzas",
                table: "Facturas",
                newName: "IncomeCategoryId");

            migrationBuilder.RenameIndex(
                name: "IX_Facturas_IncomeAccountId",
                schema: "finanzas",
                table: "Facturas",
                newName: "IX_Facturas_IncomeCategoryId");

            migrationBuilder.CreateTable(
                name: "CategoriasFinancieras",
                schema: "catalogos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    LedgerAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    ParentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CategoriasFinancieras", x => x.Id);
                    table.CheckConstraint("CK_CategoriasFinancieras_Codigo_NoVacio", "LEN(LTRIM(RTRIM([Code]))) > 0");
                    table.CheckConstraint("CK_CategoriasFinancieras_Kind", "[Kind] IN (1, 2)");
                    table.CheckConstraint("CK_CategoriasFinancieras_Parent", "[ParentId] IS NULL OR [ParentId] <> [Id]");
                    table.ForeignKey(
                        name: "FK_CategoriasFinancieras_CuentasContables",
                        column: x => x.LedgerAccountId,
                        principalSchema: "catalogos",
                        principalTable: "CuentasContables",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CategoriasFinancieras_Parent",
                        column: x => x.ParentId,
                        principalSchema: "catalogos",
                        principalTable: "CategoriasFinancieras",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CategoriasFinancieras_Usuarios_CreatedBy",
                        column: x => x.CreatedByUserId,
                        principalSchema: "seguridad",
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CategoriasFinancieras_Usuarios_UpdatedBy",
                        column: x => x.UpdatedByUserId,
                        principalSchema: "seguridad",
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CategoriasFinancieras_CreatedByUserId",
                schema: "catalogos",
                table: "CategoriasFinancieras",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CategoriasFinancieras_IsActive_Kind_Name",
                schema: "catalogos",
                table: "CategoriasFinancieras",
                columns: new[] { "IsActive", "Kind", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_CategoriasFinancieras_LedgerAccountId",
                schema: "catalogos",
                table: "CategoriasFinancieras",
                column: "LedgerAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_CategoriasFinancieras_ParentId",
                schema: "catalogos",
                table: "CategoriasFinancieras",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_CategoriasFinancieras_UpdatedByUserId",
                schema: "catalogos",
                table: "CategoriasFinancieras",
                column: "UpdatedByUserId");

            migrationBuilder.CreateIndex(
                name: "UX_CategoriasFinancieras_Code",
                schema: "catalogos",
                table: "CategoriasFinancieras",
                column: "Code",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Facturas_CategoriasFinancieras",
                schema: "finanzas",
                table: "Facturas",
                column: "IncomeCategoryId",
                principalSchema: "catalogos",
                principalTable: "CategoriasFinancieras",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Gastos_CategoriasFinancieras",
                schema: "finanzas",
                table: "Gastos",
                column: "CategoryId",
                principalSchema: "catalogos",
                principalTable: "CategoriasFinancieras",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Ingresos_CategoriasFinancieras",
                schema: "finanzas",
                table: "Ingresos",
                column: "CategoryId",
                principalSchema: "catalogos",
                principalTable: "CategoriasFinancieras",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Presupuestos_CategoriasFinancieras",
                schema: "finanzas",
                table: "Presupuestos",
                column: "CategoryId",
                principalSchema: "catalogos",
                principalTable: "CategoriasFinancieras",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ProductosServicios_CategoriasFinancieras",
                schema: "catalogos",
                table: "ProductosServicios",
                column: "DefaultIncomeCategoryId",
                principalSchema: "catalogos",
                principalTable: "CategoriasFinancieras",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
