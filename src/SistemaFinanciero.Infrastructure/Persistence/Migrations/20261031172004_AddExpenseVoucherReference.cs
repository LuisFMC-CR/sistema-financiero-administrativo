using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SistemaFinanciero.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Agrega <c>Gastos.VoucherReference</c> (RF-25, CU-14): referencia del comprobante electrónico
    /// del proveedor. Los gastos ya existentes no tienen ese dato capturado, así que se marcan con un
    /// texto visible en vez de dejarlos vacíos (la columna es obligatoria).
    /// </summary>
    /// <inheritdoc />
    public partial class AddExpenseVoucherReference : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "VoucherReference",
                schema: "finanzas",
                table: "Gastos",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql(
                "UPDATE [finanzas].[Gastos] SET [VoucherReference] = N'Sin referencia registrada' " +
                "WHERE [VoucherReference] = N'';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "VoucherReference",
                schema: "finanzas",
                table: "Gastos");
        }
    }
}
