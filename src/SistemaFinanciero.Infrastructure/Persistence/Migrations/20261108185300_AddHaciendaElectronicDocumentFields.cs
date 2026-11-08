using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SistemaFinanciero.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddHaciendaElectronicDocumentFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Secuencias del secuencial de 10 dígitos del consecutivo de Hacienda (bloque 3): una por
            // tipo de documento, independientes de Seq_FacturaNumero (número administrativo interno)
            // y de Seq_NotaCreditoCodigo (código interno "NCR-000001").
            migrationBuilder.CreateSequence(
                name: "Seq_FacturaClaveConsecutivo",
                schema: "finanzas",
                startValue: 1L,
                maxValue: 9999999999L);

            migrationBuilder.CreateSequence(
                name: "Seq_NotaCreditoClaveConsecutivo",
                schema: "finanzas",
                startValue: 1L,
                maxValue: 9999999999L);

            migrationBuilder.AddColumn<string>(
                name: "CompanyBranchCode",
                schema: "finanzas",
                table: "ParametrosSistema",
                type: "nvarchar(3)",
                maxLength: 3,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CompanyTerminalCode",
                schema: "finanzas",
                table: "ParametrosSistema",
                type: "nvarchar(5)",
                maxLength: 5,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CompanyBranchCode",
                schema: "finanzas",
                table: "ParametrosEmpresaHistorial",
                type: "nvarchar(3)",
                maxLength: 3,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CompanyTerminalCode",
                schema: "finanzas",
                table: "ParametrosEmpresaHistorial",
                type: "nvarchar(5)",
                maxLength: 5,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ElectronicCreditNoteNumber",
                schema: "finanzas",
                table: "NotasCredito",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_ParametrosSistema_CompanyBranchCode",
                schema: "finanzas",
                table: "ParametrosSistema",
                sql: "[CompanyBranchCode] IS NULL OR LEN([CompanyBranchCode]) = 3");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ParametrosSistema_CompanyTerminalCode",
                schema: "finanzas",
                table: "ParametrosSistema",
                sql: "[CompanyTerminalCode] IS NULL OR LEN([CompanyTerminalCode]) = 5");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ParametrosEmpresaHistorial_CompanyBranchCode",
                schema: "finanzas",
                table: "ParametrosEmpresaHistorial",
                sql: "[CompanyBranchCode] IS NULL OR LEN([CompanyBranchCode]) = 3");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ParametrosEmpresaHistorial_CompanyTerminalCode",
                schema: "finanzas",
                table: "ParametrosEmpresaHistorial",
                sql: "[CompanyTerminalCode] IS NULL OR LEN([CompanyTerminalCode]) = 5");

            migrationBuilder.CreateIndex(
                name: "UX_NotasCredito_ElectronicCreditNoteKey",
                schema: "finanzas",
                table: "NotasCredito",
                column: "ElectronicCreditNoteKey",
                unique: true,
                filter: "[ElectronicCreditNoteKey] IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_NotasCredito_ElectronicCreditNoteKey",
                schema: "finanzas",
                table: "NotasCredito",
                sql: "[ElectronicCreditNoteKey] IS NULL OR LEN([ElectronicCreditNoteKey]) = 50");

            migrationBuilder.AddCheckConstraint(
                name: "CK_NotasCredito_ElectronicCreditNoteNumber",
                schema: "finanzas",
                table: "NotasCredito",
                sql: "[ElectronicCreditNoteNumber] IS NULL OR LEN([ElectronicCreditNoteNumber]) = 20");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_ParametrosSistema_CompanyBranchCode",
                schema: "finanzas",
                table: "ParametrosSistema");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ParametrosSistema_CompanyTerminalCode",
                schema: "finanzas",
                table: "ParametrosSistema");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ParametrosEmpresaHistorial_CompanyBranchCode",
                schema: "finanzas",
                table: "ParametrosEmpresaHistorial");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ParametrosEmpresaHistorial_CompanyTerminalCode",
                schema: "finanzas",
                table: "ParametrosEmpresaHistorial");

            migrationBuilder.DropIndex(
                name: "UX_NotasCredito_ElectronicCreditNoteKey",
                schema: "finanzas",
                table: "NotasCredito");

            migrationBuilder.DropCheckConstraint(
                name: "CK_NotasCredito_ElectronicCreditNoteKey",
                schema: "finanzas",
                table: "NotasCredito");

            migrationBuilder.DropCheckConstraint(
                name: "CK_NotasCredito_ElectronicCreditNoteNumber",
                schema: "finanzas",
                table: "NotasCredito");

            migrationBuilder.DropColumn(
                name: "CompanyBranchCode",
                schema: "finanzas",
                table: "ParametrosSistema");

            migrationBuilder.DropColumn(
                name: "CompanyTerminalCode",
                schema: "finanzas",
                table: "ParametrosSistema");

            migrationBuilder.DropColumn(
                name: "CompanyBranchCode",
                schema: "finanzas",
                table: "ParametrosEmpresaHistorial");

            migrationBuilder.DropColumn(
                name: "CompanyTerminalCode",
                schema: "finanzas",
                table: "ParametrosEmpresaHistorial");

            migrationBuilder.DropColumn(
                name: "ElectronicCreditNoteNumber",
                schema: "finanzas",
                table: "NotasCredito");

            migrationBuilder.DropSequence(
                name: "Seq_FacturaClaveConsecutivo",
                schema: "finanzas");

            migrationBuilder.DropSequence(
                name: "Seq_NotaCreditoClaveConsecutivo",
                schema: "finanzas");
        }
    }
}
