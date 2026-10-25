using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SistemaFinanciero.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddExpensesPayablesAndIncomeCode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Secuencias para los códigos automáticos (regla 12): GAS-001 para gastos, y la que
            // faltaba para ING-001 en ingresos (brecha frente a CLAUDE.md detectada al construir este
            // incremento). No se declaran con HasSequence en el modelo porque CatalogPersistence las
            // lee con SQL crudo (NEXT VALUE FOR no se puede componer dentro de una subconsulta de EF).
            migrationBuilder.CreateSequence(
                name: "Seq_GastoCodigo",
                schema: "finanzas",
                startValue: 1L);

            migrationBuilder.CreateSequence(
                name: "Seq_IngresoCodigo",
                schema: "finanzas",
                startValue: 1L);

            migrationBuilder.AddColumn<string>(
                name: "Code",
                schema: "finanzas",
                table: "Ingresos",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "");

            // Hay ingresos previos a esta migración, todos con Code = "" por el defaultValue de arriba,
            // lo que violaría el índice único que se crea más abajo. Se les asigna ING-001, ING-002...
            // en el orden en que se crearon, y la secuencia arranca después del último asignado, para
            // que el siguiente ingreso real no choque con ellos. En una base sin ingresos previos esto
            // no actualiza ninguna fila y la secuencia arranca en 1.
            migrationBuilder.Sql(
                """
                UPDATE i
                SET i.Code = 'ING-' + RIGHT('000' + CAST(seq.RowNum AS VARCHAR(10)), 3)
                FROM finanzas.Ingresos i
                INNER JOIN (
                    SELECT Id, ROW_NUMBER() OVER (ORDER BY CreatedAtUtc) AS RowNum
                    FROM finanzas.Ingresos
                ) seq ON seq.Id = i.Id;

                DECLARE @NextIngresoSeq BIGINT = (SELECT COUNT(*) FROM finanzas.Ingresos) + 1;
                DECLARE @RestartSql NVARCHAR(200) =
                    N'ALTER SEQUENCE finanzas.Seq_IngresoCodigo RESTART WITH ' + CAST(@NextIngresoSeq AS NVARCHAR(10));
                EXEC sp_executesql @RestartSql;
                """);

            migrationBuilder.CreateTable(
                name: "Gastos",
                schema: "finanzas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    SupplierId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CategoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    IssueDate = table.Column<DateOnly>(type: "date", nullable: false),
                    PaymentTerm = table.Column<int>(type: "int", nullable: false),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: true),
                    NetAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TaxTypeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TaxCode = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    TaxName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    TaxRate = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    TaxAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PaymentMethod = table.Column<int>(type: "int", nullable: false),
                    CashAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Reference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_Gastos", x => x.Id);
                    table.CheckConstraint("CK_Gastos_Amounts", "[NetAmount] >= 0 AND [TaxRate] >= 0 AND [TaxAmount] >= 0 AND [TotalAmount] >= 0");
                    table.CheckConstraint("CK_Gastos_CashConfirmation", "(([Status] = 2 AND [PaymentTerm] = 1) AND [CashAccountId] IS NOT NULL AND [Reference] IS NOT NULL) OR (NOT ([Status] = 2 AND [PaymentTerm] = 1) AND [CashAccountId] IS NULL AND [Reference] IS NULL)");
                    table.CheckConstraint("CK_Gastos_DueDate", "([PaymentTerm] = 1 AND [DueDate] IS NULL) OR ([PaymentTerm] = 2 AND [DueDate] = DATEADD(DAY, 30, [IssueDate])) OR ([PaymentTerm] = 3 AND [DueDate] = DATEADD(DAY, 60, [IssueDate])) OR ([PaymentTerm] = 4 AND [DueDate] = DATEADD(DAY, 90, [IssueDate]))");
                    table.CheckConstraint("CK_Gastos_PaymentMethod", "[PaymentMethod] IN (1, 2, 3, 4, 5)");
                    table.CheckConstraint("CK_Gastos_PaymentTerm", "[PaymentTerm] IN (1, 2, 3, 4)");
                    table.CheckConstraint("CK_Gastos_Status", "[Status] IN (1, 2, 3)");
                    table.ForeignKey(
                        name: "FK_Gastos_CategoriasFinancieras",
                        column: x => x.CategoryId,
                        principalSchema: "catalogos",
                        principalTable: "CategoriasFinancieras",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Gastos_CuentasContables",
                        column: x => x.CashAccountId,
                        principalSchema: "catalogos",
                        principalTable: "CuentasContables",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Gastos_Proveedores",
                        column: x => x.SupplierId,
                        principalSchema: "catalogos",
                        principalTable: "Proveedores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Gastos_TiposImpuesto",
                        column: x => x.TaxTypeId,
                        principalSchema: "catalogos",
                        principalTable: "TiposImpuesto",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Gastos_Usuarios_CancelledBy",
                        column: x => x.CancelledByUserId,
                        principalSchema: "seguridad",
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Gastos_Usuarios_ConfirmedBy",
                        column: x => x.ConfirmedByUserId,
                        principalSchema: "seguridad",
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Gastos_Usuarios_CreatedBy",
                        column: x => x.CreatedByUserId,
                        principalSchema: "seguridad",
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Gastos_Usuarios_UpdatedBy",
                        column: x => x.UpdatedByUserId,
                        principalSchema: "seguridad",
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CuentasPorPagar",
                schema: "finanzas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExpenseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SupplierId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OriginalAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Balance = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_CuentasPorPagar", x => x.Id);
                    table.CheckConstraint("CK_CuentasPorPagar_Balance", "[Balance] >= 0 AND [Balance] <= [OriginalAmount]");
                    table.CheckConstraint("CK_CuentasPorPagar_Status", "[Status] IN (1, 2, 3, 4)");
                    table.ForeignKey(
                        name: "FK_CuentasPorPagar_Gastos",
                        column: x => x.ExpenseId,
                        principalSchema: "finanzas",
                        principalTable: "Gastos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CuentasPorPagar_Proveedores",
                        column: x => x.SupplierId,
                        principalSchema: "catalogos",
                        principalTable: "Proveedores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CuentasPorPagar_Usuarios_CancelledBy",
                        column: x => x.CancelledByUserId,
                        principalSchema: "seguridad",
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CuentasPorPagar_Usuarios_CreatedBy",
                        column: x => x.CreatedByUserId,
                        principalSchema: "seguridad",
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CuentasPorPagar_Usuarios_UpdatedBy",
                        column: x => x.UpdatedByUserId,
                        principalSchema: "seguridad",
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "UX_Ingresos_Code",
                schema: "finanzas",
                table: "Ingresos",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CuentasPorPagar_CancelledByUserId",
                schema: "finanzas",
                table: "CuentasPorPagar",
                column: "CancelledByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CuentasPorPagar_CreatedByUserId",
                schema: "finanzas",
                table: "CuentasPorPagar",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CuentasPorPagar_Status_DueDate",
                schema: "finanzas",
                table: "CuentasPorPagar",
                columns: new[] { "Status", "DueDate" });

            migrationBuilder.CreateIndex(
                name: "IX_CuentasPorPagar_SupplierId_Status",
                schema: "finanzas",
                table: "CuentasPorPagar",
                columns: new[] { "SupplierId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_CuentasPorPagar_UpdatedByUserId",
                schema: "finanzas",
                table: "CuentasPorPagar",
                column: "UpdatedByUserId");

            migrationBuilder.CreateIndex(
                name: "UX_CuentasPorPagar_ExpenseId",
                schema: "finanzas",
                table: "CuentasPorPagar",
                column: "ExpenseId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Gastos_CancelledByUserId",
                schema: "finanzas",
                table: "Gastos",
                column: "CancelledByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Gastos_CashAccountId",
                schema: "finanzas",
                table: "Gastos",
                column: "CashAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_Gastos_CategoryId",
                schema: "finanzas",
                table: "Gastos",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Gastos_ConfirmedByUserId",
                schema: "finanzas",
                table: "Gastos",
                column: "ConfirmedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Gastos_CreatedByUserId",
                schema: "finanzas",
                table: "Gastos",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Gastos_Status_IssueDate",
                schema: "finanzas",
                table: "Gastos",
                columns: new[] { "Status", "IssueDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Gastos_SupplierId_Status",
                schema: "finanzas",
                table: "Gastos",
                columns: new[] { "SupplierId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Gastos_TaxTypeId",
                schema: "finanzas",
                table: "Gastos",
                column: "TaxTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_Gastos_UpdatedByUserId",
                schema: "finanzas",
                table: "Gastos",
                column: "UpdatedByUserId");

            migrationBuilder.CreateIndex(
                name: "UX_Gastos_Code",
                schema: "finanzas",
                table: "Gastos",
                column: "Code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CuentasPorPagar",
                schema: "finanzas");

            migrationBuilder.DropTable(
                name: "Gastos",
                schema: "finanzas");

            migrationBuilder.DropIndex(
                name: "UX_Ingresos_Code",
                schema: "finanzas",
                table: "Ingresos");

            migrationBuilder.DropColumn(
                name: "Code",
                schema: "finanzas",
                table: "Ingresos");

            migrationBuilder.DropSequence(
                name: "Seq_IngresoCodigo",
                schema: "finanzas");

            migrationBuilder.DropSequence(
                name: "Seq_GastoCodigo",
                schema: "finanzas");
        }
    }
}
