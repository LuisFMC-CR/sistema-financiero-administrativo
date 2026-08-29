using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SistemaFinanciero.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class InitialIdentity : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(
            name: "seguridad");

        migrationBuilder.CreateTable(
            name: "Roles",
            schema: "seguridad",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                NormalizedName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                ConcurrencyStamp = table.Column<string>(type: "nvarchar(max)", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Roles", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "Usuarios",
            schema: "seguridad",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                FullName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                UserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                NormalizedUserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                NormalizedEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                EmailConfirmed = table.Column<bool>(type: "bit", nullable: false),
                PasswordHash = table.Column<string>(type: "nvarchar(max)", nullable: true),
                SecurityStamp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                ConcurrencyStamp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                PhoneNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                PhoneNumberConfirmed = table.Column<bool>(type: "bit", nullable: false),
                TwoFactorEnabled = table.Column<bool>(type: "bit", nullable: false),
                LockoutEnd = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                LockoutEnabled = table.Column<bool>(type: "bit", nullable: false),
                AccessFailedCount = table.Column<int>(type: "int", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Usuarios", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "RolesClaims",
            schema: "seguridad",
            columns: table => new
            {
                Id = table.Column<int>(type: "int", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                RoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ClaimType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                ClaimValue = table.Column<string>(type: "nvarchar(max)", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_RolesClaims", x => x.Id);
                table.ForeignKey(
                    name: "FK_RolesClaims_Roles_RoleId",
                    column: x => x.RoleId,
                    principalSchema: "seguridad",
                    principalTable: "Roles",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "UsuariosClaims",
            schema: "seguridad",
            columns: table => new
            {
                Id = table.Column<int>(type: "int", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ClaimType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                ClaimValue = table.Column<string>(type: "nvarchar(max)", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_UsuariosClaims", x => x.Id);
                table.ForeignKey(
                    name: "FK_UsuariosClaims_Usuarios_UserId",
                    column: x => x.UserId,
                    principalSchema: "seguridad",
                    principalTable: "Usuarios",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "UsuariosLogins",
            schema: "seguridad",
            columns: table => new
            {
                LoginProvider = table.Column<string>(type: "nvarchar(450)", nullable: false),
                ProviderKey = table.Column<string>(type: "nvarchar(450)", nullable: false),
                ProviderDisplayName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_UsuariosLogins", x => new { x.LoginProvider, x.ProviderKey });
                table.ForeignKey(
                    name: "FK_UsuariosLogins_Usuarios_UserId",
                    column: x => x.UserId,
                    principalSchema: "seguridad",
                    principalTable: "Usuarios",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "UsuariosRoles",
            schema: "seguridad",
            columns: table => new
            {
                UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                RoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_UsuariosRoles", x => new { x.UserId, x.RoleId });
                table.ForeignKey(
                    name: "FK_UsuariosRoles_Roles_RoleId",
                    column: x => x.RoleId,
                    principalSchema: "seguridad",
                    principalTable: "Roles",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_UsuariosRoles_Usuarios_UserId",
                    column: x => x.UserId,
                    principalSchema: "seguridad",
                    principalTable: "Usuarios",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "UsuariosTokens",
            schema: "seguridad",
            columns: table => new
            {
                UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                LoginProvider = table.Column<string>(type: "nvarchar(450)", nullable: false),
                Name = table.Column<string>(type: "nvarchar(450)", nullable: false),
                Value = table.Column<string>(type: "nvarchar(max)", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_UsuariosTokens", x => new { x.UserId, x.LoginProvider, x.Name });
                table.ForeignKey(
                    name: "FK_UsuariosTokens_Usuarios_UserId",
                    column: x => x.UserId,
                    principalSchema: "seguridad",
                    principalTable: "Usuarios",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "RoleNameIndex",
            schema: "seguridad",
            table: "Roles",
            column: "NormalizedName",
            unique: true,
            filter: "[NormalizedName] IS NOT NULL");

        migrationBuilder.CreateIndex(
            name: "IX_RolesClaims_RoleId",
            schema: "seguridad",
            table: "RolesClaims",
            column: "RoleId");

        migrationBuilder.CreateIndex(
            name: "EmailIndex",
            schema: "seguridad",
            table: "Usuarios",
            column: "NormalizedEmail",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "UserNameIndex",
            schema: "seguridad",
            table: "Usuarios",
            column: "NormalizedUserName",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_UsuariosClaims_UserId",
            schema: "seguridad",
            table: "UsuariosClaims",
            column: "UserId");

        migrationBuilder.CreateIndex(
            name: "IX_UsuariosLogins_UserId",
            schema: "seguridad",
            table: "UsuariosLogins",
            column: "UserId");

        migrationBuilder.CreateIndex(
            name: "IX_UsuariosRoles_RoleId",
            schema: "seguridad",
            table: "UsuariosRoles",
            column: "RoleId");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "RolesClaims",
            schema: "seguridad");

        migrationBuilder.DropTable(
            name: "UsuariosClaims",
            schema: "seguridad");

        migrationBuilder.DropTable(
            name: "UsuariosLogins",
            schema: "seguridad");

        migrationBuilder.DropTable(
            name: "UsuariosRoles",
            schema: "seguridad");

        migrationBuilder.DropTable(
            name: "UsuariosTokens",
            schema: "seguridad");

        migrationBuilder.DropTable(
            name: "Roles",
            schema: "seguridad");

        migrationBuilder.DropTable(
            name: "Usuarios",
            schema: "seguridad");
    }
}
