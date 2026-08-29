using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SistemaFinanciero.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddUserAdministration : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "MustChangePassword",
            schema: "seguridad",
            table: "Usuarios",
            type: "bit",
            nullable: false,
            defaultValue: false);

        migrationBuilder.CreateTable(
            name: "EventosSeguridad",
            schema: "seguridad",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Action = table.Column<int>(type: "int", nullable: false),
                ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                TargetUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                OccurredAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                Role = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_EventosSeguridad", x => x.Id);
                table.CheckConstraint("CK_EventosSeguridad_Action", "[Action] BETWEEN 1 AND 9");
                table.CheckConstraint("CK_EventosSeguridad_Role", "[Role] IS NULL OR [Role] IN (N'Administrador', N'Gerencia', N'Finanzas', N'Asistente')");
                table.ForeignKey(
                    name: "FK_EventosSeguridad_Usuarios_Actor",
                    column: x => x.ActorUserId,
                    principalSchema: "seguridad",
                    principalTable: "Usuarios",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_EventosSeguridad_Usuarios_Target",
                    column: x => x.TargetUserId,
                    principalSchema: "seguridad",
                    principalTable: "Usuarios",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "UX_UsuariosRoles_UserId",
            schema: "seguridad",
            table: "UsuariosRoles",
            column: "UserId",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_Usuarios_IsActive_FullName",
            schema: "seguridad",
            table: "Usuarios",
            columns: new[] { "IsActive", "FullName" });

        migrationBuilder.AddCheckConstraint(
            name: "CK_Usuarios_Email_UserName",
            schema: "seguridad",
            table: "Usuarios",
            sql: "[NormalizedEmail] = [NormalizedUserName]");

        migrationBuilder.AddCheckConstraint(
            name: "CK_Usuarios_FullName_NoVacio",
            schema: "seguridad",
            table: "Usuarios",
            sql: "LEN(LTRIM(RTRIM([FullName]))) > 0");

        migrationBuilder.CreateIndex(
            name: "IX_EventosSeguridad_ActorUserId_OccurredAtUtc",
            schema: "seguridad",
            table: "EventosSeguridad",
            columns: new[] { "ActorUserId", "OccurredAtUtc" });

        migrationBuilder.CreateIndex(
            name: "IX_EventosSeguridad_OccurredAtUtc",
            schema: "seguridad",
            table: "EventosSeguridad",
            column: "OccurredAtUtc");

        migrationBuilder.CreateIndex(
            name: "IX_EventosSeguridad_TargetUserId_OccurredAtUtc",
            schema: "seguridad",
            table: "EventosSeguridad",
            columns: new[] { "TargetUserId", "OccurredAtUtc" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "EventosSeguridad",
            schema: "seguridad");

        migrationBuilder.DropIndex(
            name: "UX_UsuariosRoles_UserId",
            schema: "seguridad",
            table: "UsuariosRoles");

        migrationBuilder.DropIndex(
            name: "IX_Usuarios_IsActive_FullName",
            schema: "seguridad",
            table: "Usuarios");

        migrationBuilder.DropCheckConstraint(
            name: "CK_Usuarios_Email_UserName",
            schema: "seguridad",
            table: "Usuarios");

        migrationBuilder.DropCheckConstraint(
            name: "CK_Usuarios_FullName_NoVacio",
            schema: "seguridad",
            table: "Usuarios");

        migrationBuilder.DropColumn(
            name: "MustChangePassword",
            schema: "seguridad",
            table: "Usuarios");
    }
}
