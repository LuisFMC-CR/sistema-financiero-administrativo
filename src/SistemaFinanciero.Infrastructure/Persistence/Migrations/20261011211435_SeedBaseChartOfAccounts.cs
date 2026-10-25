using System.Globalization;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SistemaFinanciero.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Reduce <c>LedgerAccountType</c> a Activo/Ingreso/Gasto, agrega <c>IsProtected</c> y siembra el
    /// catálogo de cuentas base y sus categorías iniciales (regla 13 del CLAUDE.md).
    /// </summary>
    /// <remarks>
    /// Las filas sembradas necesitan un usuario creador válido (FK obligatoria), pero esta migración
    /// corre antes de que exista cualquier usuario real: el primer administrador se crea después, con
    /// <c>--bootstrap-admin</c>. Por eso esta migración primero inserta un usuario técnico "Sistema"
    /// (inactivo, sin contraseña, sin rol) y lo usa como autor de las cuentas y categorías base.
    /// </remarks>
    /// <inheritdoc />
    public partial class SeedBaseChartOfAccounts : Migration
    {
        private const string SystemUserId = "00000000-0000-0000-0000-000000000001";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_CuentasContables_Type",
                schema: "catalogos",
                table: "CuentasContables");

            migrationBuilder.AddColumn<bool>(
                name: "IsProtected",
                schema: "catalogos",
                table: "CuentasContables",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddCheckConstraint(
                name: "CK_CuentasContables_Type",
                schema: "catalogos",
                table: "CuentasContables",
                sql: "[Type] IN (1, 4, 5)");

            migrationBuilder.Sql($"""
                INSERT INTO [seguridad].[Usuarios]
                    ([Id], [UserName], [NormalizedUserName], [Email], [NormalizedEmail], [EmailConfirmed],
                     [PasswordHash], [SecurityStamp], [ConcurrencyStamp], [PhoneNumber], [PhoneNumberConfirmed],
                     [TwoFactorEnabled], [LockoutEnd], [LockoutEnabled], [AccessFailedCount],
                     [FullName], [IsActive], [MustChangePassword])
                VALUES
                    ('{SystemUserId}', 'sistema@interno.local', 'SISTEMA@INTERNO.LOCAL',
                     'sistema@interno.local', 'SISTEMA@INTERNO.LOCAL', 1,
                     NULL, NULL, CONVERT(nvarchar(36), NEWID()), NULL, 0,
                     0, NULL, 0, 0,
                     'Sistema (siembra de catálogo base)', 0, 0);
                """);

            migrationBuilder.Sql(BuildLedgerAccountInsert(
                ("10000000-0000-0000-0000-000000000001", "1", "Activo", 1, null, null, true),
                ("10000000-0000-0000-0000-000000000002", "1.1", "Efectivo", 1, "10000000-0000-0000-0000-000000000001", null, true),
                ("20000000-0000-0000-0000-000000000001", "2", "Ingresos", 4, null, null, true),
                ("20000000-0000-0000-0000-000000000002", "2.1", "Ingresos por servicios", 4, "20000000-0000-0000-0000-000000000001", null, true),
                ("20000000-0000-0000-0000-000000000006", "2.2", "Ingresos por ventas", 4, "20000000-0000-0000-0000-000000000001", null, true),
                ("20000000-0000-0000-0000-000000000008", "2.3", "Otros ingresos", 4, "20000000-0000-0000-0000-000000000001", null, true),
                ("30000000-0000-0000-0000-000000000001", "3", "Gastos", 5, null, null, true),
                ("30000000-0000-0000-0000-000000000002", "3.1", "Gastos de operación", 5, "30000000-0000-0000-0000-000000000001", null, true),
                ("30000000-0000-0000-0000-000000000009", "3.2", "Gastos administrativos", 5, "30000000-0000-0000-0000-000000000001", null, true)));

            migrationBuilder.Sql(BuildLedgerAccountInsert(
                ("10000000-0000-0000-0000-000000000003", "1.1.01", "Caja chica", 1, "10000000-0000-0000-0000-000000000002", 1, false),
                ("10000000-0000-0000-0000-000000000004", "1.1.02", "Cuenta bancaria principal", 1, "10000000-0000-0000-0000-000000000002", 2, false),
                ("20000000-0000-0000-0000-000000000003", "2.1.01", "Soporte técnico", 4, "20000000-0000-0000-0000-000000000002", null, false),
                ("20000000-0000-0000-0000-000000000004", "2.1.02", "Mantenimiento y reparación de equipos", 4, "20000000-0000-0000-0000-000000000002", null, false),
                ("20000000-0000-0000-0000-000000000005", "2.1.03", "Instalación de infraestructura informática", 4, "20000000-0000-0000-0000-000000000002", null, false),
                ("20000000-0000-0000-0000-000000000007", "2.2.01", "Venta de equipos y repuestos", 4, "20000000-0000-0000-0000-000000000006", null, false),
                ("20000000-0000-0000-0000-000000000009", "2.3.01", "Otros ingresos de la operación", 4, "20000000-0000-0000-0000-000000000008", null, false),
                ("30000000-0000-0000-0000-000000000003", "3.1.01", "Alquiler", 5, "30000000-0000-0000-0000-000000000002", null, false),
                ("30000000-0000-0000-0000-000000000004", "3.1.02", "Electricidad y agua", 5, "30000000-0000-0000-0000-000000000002", null, false),
                ("30000000-0000-0000-0000-000000000005", "3.1.03", "Internet y telefonía", 5, "30000000-0000-0000-0000-000000000002", null, false),
                ("30000000-0000-0000-0000-000000000006", "3.1.04", "Materiales y repuestos", 5, "30000000-0000-0000-0000-000000000002", null, false),
                ("30000000-0000-0000-0000-000000000007", "3.1.05", "Software y licencias", 5, "30000000-0000-0000-0000-000000000002", null, false),
                ("30000000-0000-0000-0000-000000000008", "3.1.06", "Transporte y combustible", 5, "30000000-0000-0000-0000-000000000002", null, false),
                ("30000000-0000-0000-0000-00000000000A", "3.2.01", "Papelería y suministros de oficina", 5, "30000000-0000-0000-0000-000000000009", null, false),
                ("30000000-0000-0000-0000-00000000000B", "3.2.02", "Servicios profesionales", 5, "30000000-0000-0000-0000-000000000009", null, false),
                ("30000000-0000-0000-0000-00000000000C", "3.2.03", "Comisiones bancarias", 5, "30000000-0000-0000-0000-000000000009", null, false),
                ("30000000-0000-0000-0000-00000000000D", "3.2.04", "Otros gastos administrativos", 5, "30000000-0000-0000-0000-000000000009", null, false)));

            migrationBuilder.Sql(BuildFinancialCategoryInsert(
                ("20000000-0000-0000-0001-000000000001", "ING-001", "Soporte técnico", 1, "20000000-0000-0000-0000-000000000003"),
                ("20000000-0000-0000-0001-000000000002", "ING-002", "Mantenimiento y reparación de equipos", 1, "20000000-0000-0000-0000-000000000004"),
                ("20000000-0000-0000-0001-000000000003", "ING-003", "Instalación de infraestructura informática", 1, "20000000-0000-0000-0000-000000000005"),
                ("20000000-0000-0000-0001-000000000004", "ING-004", "Venta de equipos y repuestos", 1, "20000000-0000-0000-0000-000000000007"),
                ("20000000-0000-0000-0001-000000000005", "ING-005", "Otros ingresos de la operación", 1, "20000000-0000-0000-0000-000000000009"),
                ("30000000-0000-0000-0001-000000000001", "GAS-001", "Alquiler", 2, "30000000-0000-0000-0000-000000000003"),
                ("30000000-0000-0000-0001-000000000002", "GAS-002", "Electricidad y agua", 2, "30000000-0000-0000-0000-000000000004"),
                ("30000000-0000-0000-0001-000000000003", "GAS-003", "Internet y telefonía", 2, "30000000-0000-0000-0000-000000000005"),
                ("30000000-0000-0000-0001-000000000004", "GAS-004", "Materiales y repuestos", 2, "30000000-0000-0000-0000-000000000006"),
                ("30000000-0000-0000-0001-000000000005", "GAS-005", "Software y licencias", 2, "30000000-0000-0000-0000-000000000007"),
                ("30000000-0000-0000-0001-000000000006", "GAS-006", "Transporte y combustible", 2, "30000000-0000-0000-0000-000000000008"),
                ("30000000-0000-0000-0001-000000000007", "GAS-007", "Papelería y suministros de oficina", 2, "30000000-0000-0000-0000-00000000000A"),
                ("30000000-0000-0000-0001-000000000008", "GAS-008", "Servicios profesionales", 2, "30000000-0000-0000-0000-00000000000B"),
                ("30000000-0000-0000-0001-000000000009", "GAS-009", "Comisiones bancarias", 2, "30000000-0000-0000-0000-00000000000C"),
                ("30000000-0000-0000-0001-00000000000A", "GAS-010", "Otros gastos administrativos", 2, "30000000-0000-0000-0000-00000000000D")));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM [catalogos].[CategoriasFinancieras] WHERE [CreatedByUserId] = '" + SystemUserId + "';");
            migrationBuilder.Sql("DELETE FROM [catalogos].[CuentasContables] WHERE [CreatedByUserId] = '" + SystemUserId + "';");
            migrationBuilder.Sql("DELETE FROM [seguridad].[Usuarios] WHERE [Id] = '" + SystemUserId + "';");

            migrationBuilder.DropCheckConstraint(
                name: "CK_CuentasContables_Type",
                schema: "catalogos",
                table: "CuentasContables");

            migrationBuilder.DropColumn(
                name: "IsProtected",
                schema: "catalogos",
                table: "CuentasContables");

            migrationBuilder.AddCheckConstraint(
                name: "CK_CuentasContables_Type",
                schema: "catalogos",
                table: "CuentasContables",
                sql: "[Type] IN (1, 2, 3, 4, 5)");
        }

        private static string BuildLedgerAccountInsert(
            params (string Id, string Code, string Name, int Type, string ParentId, int? CashKind, bool IsProtected)[] rows)
        {
            System.Text.StringBuilder sql = new();
            sql.AppendLine("""
                INSERT INTO [catalogos].[CuentasContables]
                    ([Id], [Code], [Name], [Type], [ParentId], [CashKind], [Reference], [Description],
                     [IsActive], [IsProtected], [CreatedAtUtc], [CreatedByUserId], [UpdatedAtUtc], [UpdatedByUserId])
                VALUES
                """);

            for (int index = 0; index < rows.Length; index++)
            {
                (string id, string code, string name, int type, string parentId, int? cashKind, bool isProtected) = rows[index];
                string parentSql = parentId is null ? "NULL" : $"'{parentId}'";
                string cashKindSql = cashKind is null ? "NULL" : cashKind.Value.ToString(CultureInfo.InvariantCulture);
                string isProtectedSql = isProtected ? "1" : "0";
                string separator = index == rows.Length - 1 ? ";" : ",";

                sql.AppendLine(CultureInfo.InvariantCulture,
                    $"    ('{id}', '{code}', N'{name}', {type}, {parentSql}, {cashKindSql}, NULL, NULL, " +
                    $"1, {isProtectedSql}, CAST(SYSUTCDATETIME() AS datetimeoffset), '{SystemUserId}', " +
                    $"CAST(SYSUTCDATETIME() AS datetimeoffset), '{SystemUserId}'){separator}");
            }

            return sql.ToString();
        }

        private static string BuildFinancialCategoryInsert(
            params (string Id, string Code, string Name, int Kind, string LedgerAccountId)[] rows)
        {
            System.Text.StringBuilder sql = new();
            sql.AppendLine("""
                INSERT INTO [catalogos].[CategoriasFinancieras]
                    ([Id], [Code], [Name], [Kind], [ParentId], [LedgerAccountId], [Description],
                     [IsActive], [CreatedAtUtc], [CreatedByUserId], [UpdatedAtUtc], [UpdatedByUserId])
                VALUES
                """);

            for (int index = 0; index < rows.Length; index++)
            {
                (string id, string code, string name, int kind, string ledgerAccountId) = rows[index];
                string separator = index == rows.Length - 1 ? ";" : ",";

                sql.AppendLine(CultureInfo.InvariantCulture,
                    $"    ('{id}', '{code}', N'{name}', {kind}, NULL, '{ledgerAccountId}', NULL, " +
                    $"1, CAST(SYSUTCDATETIME() AS datetimeoffset), '{SystemUserId}', " +
                    $"CAST(SYSUTCDATETIME() AS datetimeoffset), '{SystemUserId}'){separator}");
            }

            return sql.ToString();
        }
    }
}
