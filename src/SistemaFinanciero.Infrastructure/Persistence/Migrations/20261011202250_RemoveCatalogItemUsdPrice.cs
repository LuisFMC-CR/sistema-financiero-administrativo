using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SistemaFinanciero.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemoveCatalogItemUsdPrice : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_ProductosServicios_PriceUsd",
                schema: "catalogos",
                table: "ProductosServicios");

            migrationBuilder.DropColumn(
                name: "ReferencePriceUsd",
                schema: "catalogos",
                table: "ProductosServicios");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "ReferencePriceUsd",
                schema: "catalogos",
                table: "ProductosServicios",
                type: "decimal(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_ProductosServicios_PriceUsd",
                schema: "catalogos",
                table: "ProductosServicios",
                sql: "[ReferencePriceUsd] IS NULL OR [ReferencePriceUsd] > 0");
        }
    }
}
