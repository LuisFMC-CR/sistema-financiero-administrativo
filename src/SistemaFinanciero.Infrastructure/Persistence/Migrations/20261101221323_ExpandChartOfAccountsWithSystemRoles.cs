using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SistemaFinanciero.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Catálogo contable ampliado (bloque 4 del paquete de mejoras finales): agrega Pasivo y
    /// Patrimonio, renumera el catálogo base a la convención 1 Activo, 2 Pasivo, 3 Patrimonio,
    /// 4 Ingresos, 5 Gastos (los Id no cambian, así que las categorías financieras siguen apuntando a
    /// sus cuentas), y siembra las cuentas de sistema (<c>SystemRole</c>) y los grupos nuevos.
    /// </summary>
    /// <inheritdoc />
    public partial class ExpandChartOfAccountsWithSystemRoles : Migration
    {
        private const string SystemUserId = "00000000-0000-0000-0000-000000000001";

        // Cuentas existentes (siembra original): se renombran de código, no de Id.
        private const string IncomeRootId = "20000000-0000-0000-0000-000000000001";
        private const string OtherIncomeGroupId = "20000000-0000-0000-0000-000000000008";
        private const string ExpenseRootId = "30000000-0000-0000-0000-000000000001";
        private const string AdminExpenseGroupId = "30000000-0000-0000-0000-000000000009";

        // Cuentas nuevas de este bloque.
        private const string ReceivablesGroupId = "10000000-0000-0000-0000-000000000005";
        private const string ReceivablesFromCustomersId = "10000000-0000-0000-0000-000000000006";
        private const string TaxesReceivableGroupId = "10000000-0000-0000-0000-000000000007";
        private const string VatCreditableId = "10000000-0000-0000-0000-000000000008";
        private const string WithholdingsReceivableId = "10000000-0000-0000-0000-000000000009";

        private const string LiabilityRootId = "40000000-0000-0000-0000-000000000001";
        private const string PayablesGroupId = "40000000-0000-0000-0000-000000000002";
        private const string PayablesToSuppliersId = "40000000-0000-0000-0000-000000000003";
        private const string TaxesPayableGroupId = "40000000-0000-0000-0000-000000000004";
        private const string VatPayableId = "40000000-0000-0000-0000-000000000005";

        private const string EquityRootId = "50000000-0000-0000-0000-000000000001";

        private const string PayableAdjustmentsInFavorId = "20000000-0000-0000-0000-00000000000A";
        private const string SalesReturnsGroupId = "20000000-0000-0000-0000-00000000000B";
        private const string SalesReturnsAndAllowancesId = "20000000-0000-0000-0000-00000000000C";

        private const string ReceivableAdjustmentsId = "30000000-0000-0000-0000-00000000000E";
        private const string CostOfSalesGroupId = "30000000-0000-0000-0000-00000000000F";
        private const string CostOfSalesDetailId = "30000000-0000-0000-0000-000000000010";
        private const string CostOfSalesCategoryId = "30000000-0000-0000-0001-00000000000B";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_CuentasContables_Type",
                schema: "catalogos",
                table: "CuentasContables");

            migrationBuilder.AddColumn<int>(
                name: "SystemRole",
                schema: "catalogos",
                table: "CuentasContables",
                type: "int",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_CuentasContables_Type",
                schema: "catalogos",
                table: "CuentasContables",
                sql: "[Type] IN (1, 2, 3, 4, 5)");

            // Renumera Ingresos (2.x -> 4.x) y Gastos (3.x -> 5.x); los Id no cambian, así que las
            // categorías financieras siguen apuntando a sus cuentas sin tocarlas.
            migrationBuilder.Sql("""
                UPDATE catalogos.CuentasContables
                    SET Code = '4' + SUBSTRING(Code, 2, LEN(Code))
                WHERE Code = '2' OR Code LIKE '2.%';
                """);
            migrationBuilder.Sql("""
                UPDATE catalogos.CuentasContables
                    SET Code = '5' + SUBSTRING(Code, 2, LEN(Code))
                WHERE Code = '3' OR Code LIKE '3.%';
                """);

            // Grupos y cuentas de detalle nuevos del catálogo contable ampliado.
            migrationBuilder.Sql(BuildLedgerAccountInsert(
                (ReceivablesGroupId, "1.2", "Cuentas por cobrar", 1, "10000000-0000-0000-0000-000000000001", true),
                (TaxesReceivableGroupId, "1.3", "Impuestos a favor", 1, "10000000-0000-0000-0000-000000000001", true),
                (LiabilityRootId, "2", "Pasivo", 2, null, true),
                (PayablesGroupId, "2.1", "Cuentas por pagar", 2, LiabilityRootId, true),
                (TaxesPayableGroupId, "2.2", "Impuestos por pagar", 2, LiabilityRootId, true),
                (EquityRootId, "3", "Patrimonio", 3, null, true),
                (SalesReturnsGroupId, "4.4", "Devoluciones y descuentos sobre ventas", 4, IncomeRootId, true),
                (CostOfSalesGroupId, "5.3", "Costo de ventas", 5, ExpenseRootId, true)));

            migrationBuilder.Sql(BuildLedgerAccountInsert(
                (ReceivablesFromCustomersId, "1.2.01", "Cuentas por cobrar a clientes", 1, ReceivablesGroupId, false),
                (VatCreditableId, "1.3.01", "IVA acreditable", 1, TaxesReceivableGroupId, false),
                (WithholdingsReceivableId, "1.3.02", "Retenciones de renta a favor", 1, TaxesReceivableGroupId, false),
                (PayablesToSuppliersId, "2.1.01", "Cuentas por pagar a proveedores", 2, PayablesGroupId, false),
                (VatPayableId, "2.2.01", "IVA por pagar", 2, TaxesPayableGroupId, false),
                (PayableAdjustmentsInFavorId, "4.3.02", "Ajustes a favor en cuentas por pagar", 4, OtherIncomeGroupId, false),
                (SalesReturnsAndAllowancesId, "4.4.01", "Devoluciones y descuentos sobre ventas", 4, SalesReturnsGroupId, false),
                (ReceivableAdjustmentsId, "5.2.05", "Ajustes a cuentas por cobrar", 5, AdminExpenseGroupId, false),
                (CostOfSalesDetailId, "5.3.01", "Compras de equipos para la venta", 5, CostOfSalesGroupId, false)));

            // 5.3.01 no es cuenta de sistema: lleva su propia categoría de gasto, con el siguiente
            // código automático de la secuencia que ya usa el alta en vivo (regla 12).
            migrationBuilder.Sql($"""
                INSERT INTO [catalogos].[CategoriasFinancieras]
                    ([Id], [Code], [Name], [Kind], [ParentId], [LedgerAccountId], [Description],
                     [IsActive], [CreatedAtUtc], [CreatedByUserId], [UpdatedAtUtc], [UpdatedByUserId])
                VALUES
                    ('{CostOfSalesCategoryId}',
                     'GAS-' + RIGHT('000' + CAST(NEXT VALUE FOR catalogos.Seq_CategoriaGastoCodigo AS nvarchar(10)), 3),
                     N'Compras de equipos para la venta', 2, NULL, '{CostOfSalesDetailId}', NULL,
                     1, CAST(SYSUTCDATETIME() AS datetimeoffset), '{SystemUserId}',
                     CAST(SYSUTCDATETIME() AS datetimeoffset), '{SystemUserId}');
                """);

            // Rol fijo de cada cuenta de sistema (regla del bloque 4): las reglas de contabilización
            // las buscan por este rol, nunca por código ni nombre.
            migrationBuilder.Sql($"""
                UPDATE catalogos.CuentasContables SET SystemRole = 1 WHERE Id = '{ReceivablesFromCustomersId}';
                UPDATE catalogos.CuentasContables SET SystemRole = 2 WHERE Id = '{VatCreditableId}';
                UPDATE catalogos.CuentasContables SET SystemRole = 3 WHERE Id = '{WithholdingsReceivableId}';
                UPDATE catalogos.CuentasContables SET SystemRole = 4 WHERE Id = '{PayablesToSuppliersId}';
                UPDATE catalogos.CuentasContables SET SystemRole = 5 WHERE Id = '{VatPayableId}';
                UPDATE catalogos.CuentasContables SET SystemRole = 6 WHERE Id = '{ReceivableAdjustmentsId}';
                UPDATE catalogos.CuentasContables SET SystemRole = 7 WHERE Id = '{PayableAdjustmentsInFavorId}';
                UPDATE catalogos.CuentasContables SET SystemRole = 8 WHERE Id = '{SalesReturnsAndAllowancesId}';
                """);

            migrationBuilder.CreateIndex(
                name: "UX_CuentasContables_SystemRole",
                schema: "catalogos",
                table: "CuentasContables",
                column: "SystemRole",
                unique: true,
                filter: "[SystemRole] IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_CuentasContables_SystemRole",
                schema: "catalogos",
                table: "CuentasContables",
                sql: "[SystemRole] IS NULL OR [SystemRole] IN (1, 2, 3, 4, 5, 6, 7, 8)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql($"DELETE FROM [catalogos].[CategoriasFinancieras] WHERE [Id] = '{CostOfSalesCategoryId}';");
            migrationBuilder.Sql($"""
                DELETE FROM [catalogos].[CuentasContables] WHERE [Id] IN (
                    '{ReceivablesGroupId}', '{TaxesReceivableGroupId}', '{LiabilityRootId}', '{PayablesGroupId}',
                    '{TaxesPayableGroupId}', '{EquityRootId}', '{SalesReturnsGroupId}', '{CostOfSalesGroupId}',
                    '{ReceivablesFromCustomersId}', '{VatCreditableId}', '{WithholdingsReceivableId}',
                    '{PayablesToSuppliersId}', '{VatPayableId}', '{PayableAdjustmentsInFavorId}',
                    '{SalesReturnsAndAllowancesId}', '{ReceivableAdjustmentsId}', '{CostOfSalesDetailId}');
                """);

            migrationBuilder.Sql("""
                UPDATE catalogos.CuentasContables
                    SET Code = '2' + SUBSTRING(Code, 2, LEN(Code))
                WHERE Code = '4' OR Code LIKE '4.%';
                """);
            migrationBuilder.Sql("""
                UPDATE catalogos.CuentasContables
                    SET Code = '3' + SUBSTRING(Code, 2, LEN(Code))
                WHERE Code = '5' OR Code LIKE '5.%';
                """);

            migrationBuilder.DropIndex(
                name: "UX_CuentasContables_SystemRole",
                schema: "catalogos",
                table: "CuentasContables");

            migrationBuilder.DropCheckConstraint(
                name: "CK_CuentasContables_SystemRole",
                schema: "catalogos",
                table: "CuentasContables");

            migrationBuilder.DropCheckConstraint(
                name: "CK_CuentasContables_Type",
                schema: "catalogos",
                table: "CuentasContables");

            migrationBuilder.DropColumn(
                name: "SystemRole",
                schema: "catalogos",
                table: "CuentasContables");

            migrationBuilder.AddCheckConstraint(
                name: "CK_CuentasContables_Type",
                schema: "catalogos",
                table: "CuentasContables",
                sql: "[Type] IN (1, 4, 5)");
        }

        private static string BuildLedgerAccountInsert(
            params (string Id, string Code, string Name, int Type, string ParentId, bool IsProtected)[] rows)
        {
            StringBuilder sql = new();
            sql.AppendLine("""
                INSERT INTO [catalogos].[CuentasContables]
                    ([Id], [Code], [Name], [Type], [ParentId], [CashKind], [Reference], [Description],
                     [IsActive], [IsProtected], [CreatedAtUtc], [CreatedByUserId], [UpdatedAtUtc], [UpdatedByUserId])
                VALUES
                """);

            for (int index = 0; index < rows.Length; index++)
            {
                (string id, string code, string name, int type, string parentId, bool isProtected) = rows[index];
                string parentSql = parentId is null ? "NULL" : $"'{parentId}'";
                string isProtectedSql = isProtected ? "1" : "0";
                string separator = index == rows.Length - 1 ? ";" : ",";

                sql.AppendLine(CultureInfo.InvariantCulture,
                    $"    ('{id}', '{code}', N'{name}', {type}, {parentSql}, NULL, NULL, NULL, " +
                    $"1, {isProtectedSql}, CAST(SYSUTCDATETIME() AS datetimeoffset), '{SystemUserId}', " +
                    $"CAST(SYSUTCDATETIME() AS datetimeoffset), '{SystemUserId}'){separator}");
            }

            return sql.ToString();
        }
    }
}
