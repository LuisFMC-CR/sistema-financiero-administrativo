using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SistemaFinanciero.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddBusinessCatalogs : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(
            name: "catalogos");

        migrationBuilder.EnsureSchema(
            name: "finanzas");

        migrationBuilder.CreateTable(
            name: "CategoriasFinancieras",
            schema: "catalogos",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Code = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                Kind = table.Column<int>(type: "int", nullable: false),
                ParentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
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
                table.PrimaryKey("PK_CategoriasFinancieras", x => x.Id);
                table.CheckConstraint("CK_CategoriasFinancieras_Codigo_NoVacio", "LEN(LTRIM(RTRIM([Code]))) > 0");
                table.CheckConstraint("CK_CategoriasFinancieras_Kind", "[Kind] IN (1, 2)");
                table.CheckConstraint("CK_CategoriasFinancieras_Parent", "[ParentId] IS NULL OR [ParentId] <> [Id]");
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

        migrationBuilder.CreateTable(
            name: "Clientes",
            schema: "catalogos",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Code = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                Identification = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                Email = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: true),
                Phone = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                Address = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Clientes", x => x.Id);
                table.CheckConstraint("CK_Clientes_Codigo_NoVacio", "LEN(LTRIM(RTRIM([Code]))) > 0");
                table.ForeignKey(
                    name: "FK_Clientes_Usuarios_CreatedBy",
                    column: x => x.CreatedByUserId,
                    principalSchema: "seguridad",
                    principalTable: "Usuarios",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_Clientes_Usuarios_UpdatedBy",
                    column: x => x.UpdatedByUserId,
                    principalSchema: "seguridad",
                    principalTable: "Usuarios",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "CuentasFinancieras",
            schema: "finanzas",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Code = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                Type = table.Column<int>(type: "int", nullable: false),
                Currency = table.Column<string>(type: "char(3)", unicode: false, nullable: false),
                Reference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
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

        migrationBuilder.CreateTable(
            name: "Proveedores",
            schema: "catalogos",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Code = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                Identification = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                Email = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: true),
                Phone = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                Address = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Proveedores", x => x.Id);
                table.CheckConstraint("CK_Proveedores_Codigo_NoVacio", "LEN(LTRIM(RTRIM([Code]))) > 0");
                table.ForeignKey(
                    name: "FK_Proveedores_Usuarios_CreatedBy",
                    column: x => x.CreatedByUserId,
                    principalSchema: "seguridad",
                    principalTable: "Usuarios",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_Proveedores_Usuarios_UpdatedBy",
                    column: x => x.UpdatedByUserId,
                    principalSchema: "seguridad",
                    principalTable: "Usuarios",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "TiposCambioDiarios",
            schema: "finanzas",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                EffectiveDate = table.Column<DateOnly>(type: "date", nullable: false),
                CrcPerUsd = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                Source = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                Notes = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
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

        migrationBuilder.CreateTable(
            name: "ProductosServicios",
            schema: "catalogos",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Code = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                Type = table.Column<int>(type: "int", nullable: false),
                Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                UnitOfMeasure = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                ReferencePriceCrc = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                ReferencePriceUsd = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                DefaultIncomeCategoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ProductosServicios", x => x.Id);
                table.CheckConstraint("CK_ProductosServicios_Codigo_NoVacio", "LEN(LTRIM(RTRIM([Code]))) > 0");
                table.CheckConstraint("CK_ProductosServicios_PriceCrc", "[ReferencePriceCrc] IS NULL OR [ReferencePriceCrc] > 0");
                table.CheckConstraint("CK_ProductosServicios_PriceUsd", "[ReferencePriceUsd] IS NULL OR [ReferencePriceUsd] > 0");
                table.CheckConstraint("CK_ProductosServicios_Type", "[Type] IN (1, 2)");
                table.ForeignKey(
                    name: "FK_ProductosServicios_CategoriasFinancieras",
                    column: x => x.DefaultIncomeCategoryId,
                    principalSchema: "catalogos",
                    principalTable: "CategoriasFinancieras",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_ProductosServicios_Usuarios_CreatedBy",
                    column: x => x.CreatedByUserId,
                    principalSchema: "seguridad",
                    principalTable: "Usuarios",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_ProductosServicios_Usuarios_UpdatedBy",
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

        migrationBuilder.CreateIndex(
            name: "IX_Clientes_CreatedByUserId",
            schema: "catalogos",
            table: "Clientes",
            column: "CreatedByUserId");

        migrationBuilder.CreateIndex(
            name: "IX_Clientes_IsActive_Name",
            schema: "catalogos",
            table: "Clientes",
            columns: new[] { "IsActive", "Name" });

        migrationBuilder.CreateIndex(
            name: "IX_Clientes_UpdatedByUserId",
            schema: "catalogos",
            table: "Clientes",
            column: "UpdatedByUserId");

        migrationBuilder.CreateIndex(
            name: "UX_Clientes_Code",
            schema: "catalogos",
            table: "Clientes",
            column: "Code",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "UX_Clientes_Identification",
            schema: "catalogos",
            table: "Clientes",
            column: "Identification",
            unique: true,
            filter: "[Identification] IS NOT NULL");

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

        migrationBuilder.CreateIndex(
            name: "IX_ProductosServicios_CreatedByUserId",
            schema: "catalogos",
            table: "ProductosServicios",
            column: "CreatedByUserId");

        migrationBuilder.CreateIndex(
            name: "IX_ProductosServicios_DefaultIncomeCategoryId",
            schema: "catalogos",
            table: "ProductosServicios",
            column: "DefaultIncomeCategoryId");

        migrationBuilder.CreateIndex(
            name: "IX_ProductosServicios_IsActive_Name",
            schema: "catalogos",
            table: "ProductosServicios",
            columns: new[] { "IsActive", "Name" });

        migrationBuilder.CreateIndex(
            name: "IX_ProductosServicios_UpdatedByUserId",
            schema: "catalogos",
            table: "ProductosServicios",
            column: "UpdatedByUserId");

        migrationBuilder.CreateIndex(
            name: "UX_ProductosServicios_Code",
            schema: "catalogos",
            table: "ProductosServicios",
            column: "Code",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_Proveedores_CreatedByUserId",
            schema: "catalogos",
            table: "Proveedores",
            column: "CreatedByUserId");

        migrationBuilder.CreateIndex(
            name: "IX_Proveedores_IsActive_Name",
            schema: "catalogos",
            table: "Proveedores",
            columns: new[] { "IsActive", "Name" });

        migrationBuilder.CreateIndex(
            name: "IX_Proveedores_UpdatedByUserId",
            schema: "catalogos",
            table: "Proveedores",
            column: "UpdatedByUserId");

        migrationBuilder.CreateIndex(
            name: "UX_Proveedores_Code",
            schema: "catalogos",
            table: "Proveedores",
            column: "Code",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "UX_Proveedores_Identification",
            schema: "catalogos",
            table: "Proveedores",
            column: "Identification",
            unique: true,
            filter: "[Identification] IS NOT NULL");

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

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "Clientes",
            schema: "catalogos");

        migrationBuilder.DropTable(
            name: "CuentasFinancieras",
            schema: "finanzas");

        migrationBuilder.DropTable(
            name: "ProductosServicios",
            schema: "catalogos");

        migrationBuilder.DropTable(
            name: "Proveedores",
            schema: "catalogos");

        migrationBuilder.DropTable(
            name: "TiposCambioDiarios",
            schema: "finanzas");

        migrationBuilder.DropTable(
            name: "CategoriasFinancieras",
            schema: "catalogos");
    }
}
