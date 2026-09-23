using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SistemaFinanciero.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddInvoices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Facturas",
                schema: "finanzas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Number = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CustomerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IssueDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Currency = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    GrossAmount = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    DiscountAmount = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    NetAmount = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    TaxAmount = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    ConfirmedCrcPerUsd = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: true),
                    ConfirmedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ConfirmedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
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
                    table.PrimaryKey("PK_Facturas", x => x.Id);
                    table.CheckConstraint("CK_Facturas_Currency", "[Currency] IN (1, 2)");
                    table.CheckConstraint("CK_Facturas_Status", "[Status] IN (1, 2, 3)");
                    table.CheckConstraint("CK_Facturas_Totals", "[GrossAmount] >= 0 AND [DiscountAmount] >= 0 AND [NetAmount] >= 0 AND [TaxAmount] >= 0 AND [TotalAmount] >= 0");
                    table.CheckConstraint("CK_Facturas_UsdRate", "[Currency] <> 2 OR [Status] = 1 OR [ConfirmedCrcPerUsd] > 0");
                    table.ForeignKey(
                        name: "FK_Facturas_Clientes",
                        column: x => x.CustomerId,
                        principalSchema: "catalogos",
                        principalTable: "Clientes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Facturas_Usuarios_CancelledBy",
                        column: x => x.CancelledByUserId,
                        principalSchema: "seguridad",
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Facturas_Usuarios_ConfirmedBy",
                        column: x => x.ConfirmedByUserId,
                        principalSchema: "seguridad",
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Facturas_Usuarios_CreatedBy",
                        column: x => x.CreatedByUserId,
                        principalSchema: "seguridad",
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Facturas_Usuarios_UpdatedBy",
                        column: x => x.UpdatedByUserId,
                        principalSchema: "seguridad",
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FacturaLineas",
                schema: "finanzas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CatalogItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    UnitOfMeasure = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    GrossAmount = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    DiscountAmount = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    NetAmount = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    TaxAmount = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    InvoiceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FacturaLineas", x => x.Id);
                    table.CheckConstraint("CK_FacturaLineas_Amounts", "[Quantity] > 0 AND [UnitPrice] >= 0 AND [GrossAmount] >= 0 AND [DiscountAmount] >= 0 AND [DiscountAmount] <= [GrossAmount] AND [NetAmount] >= 0 AND [TaxAmount] >= 0 AND [TotalAmount] >= 0");
                    table.ForeignKey(
                        name: "FK_FacturaLineas_Facturas",
                        column: x => x.InvoiceId,
                        principalSchema: "finanzas",
                        principalTable: "Facturas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FacturaLineas_ProductosServicios",
                        column: x => x.CatalogItemId,
                        principalSchema: "catalogos",
                        principalTable: "ProductosServicios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FacturaLineaImpuestos",
                schema: "finanzas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    CalculationType = table.Column<int>(type: "int", nullable: false),
                    Rate = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    InvoiceLineId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FacturaLineaImpuestos", x => x.Id);
                    table.CheckConstraint("CK_FacturaLineaImpuestos_RateAmount", "[Rate] >= 0 AND [Amount] >= 0");
                    table.CheckConstraint("CK_FacturaLineaImpuestos_Type", "[CalculationType] IN (1, 2)");
                    table.ForeignKey(
                        name: "FK_FacturaLineaImpuestos_FacturaLineas",
                        column: x => x.InvoiceLineId,
                        principalSchema: "finanzas",
                        principalTable: "FacturaLineas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "UX_FacturaLineaImpuestos_Line_Code",
                schema: "finanzas",
                table: "FacturaLineaImpuestos",
                columns: new[] { "InvoiceLineId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FacturaLineas_CatalogItemId",
                schema: "finanzas",
                table: "FacturaLineas",
                column: "CatalogItemId");

            migrationBuilder.CreateIndex(
                name: "IX_FacturaLineas_InvoiceId",
                schema: "finanzas",
                table: "FacturaLineas",
                column: "InvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_Facturas_CancelledByUserId",
                schema: "finanzas",
                table: "Facturas",
                column: "CancelledByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Facturas_ConfirmedByUserId",
                schema: "finanzas",
                table: "Facturas",
                column: "ConfirmedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Facturas_CreatedByUserId",
                schema: "finanzas",
                table: "Facturas",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Facturas_CustomerId_Status",
                schema: "finanzas",
                table: "Facturas",
                columns: new[] { "CustomerId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Facturas_Status_IssueDate",
                schema: "finanzas",
                table: "Facturas",
                columns: new[] { "Status", "IssueDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Facturas_UpdatedByUserId",
                schema: "finanzas",
                table: "Facturas",
                column: "UpdatedByUserId");

            migrationBuilder.CreateIndex(
                name: "UX_Facturas_Number",
                schema: "finanzas",
                table: "Facturas",
                column: "Number",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FacturaLineaImpuestos",
                schema: "finanzas");

            migrationBuilder.DropTable(
                name: "FacturaLineas",
                schema: "finanzas");

            migrationBuilder.DropTable(
                name: "Facturas",
                schema: "finanzas");
        }
    }
}
