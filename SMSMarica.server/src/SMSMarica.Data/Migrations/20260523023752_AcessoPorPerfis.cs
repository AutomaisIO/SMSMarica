using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMarica.Data.Migrations
{
    /// <inheritdoc />
    public partial class AcessoPorPerfis : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "perfil",
                schema: "smsmarica",
                table: "usuario");

            migrationBuilder.CreateTable(
                name: "perfil",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nome = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    descricao = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    ativo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_perfil", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "permissao_usuario",
                schema: "smsmarica",
                columns: table => new
                {
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    modulo = table.Column<int>(type: "integer", nullable: false),
                    acoes = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_permissao_usuario", x => new { x.usuario_id, x.modulo });
                    table.ForeignKey(
                        name: "FK_permissao_usuario_usuario_usuario_id",
                        column: x => x.usuario_id,
                        principalSchema: "smsmarica",
                        principalTable: "usuario",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "permissao_perfil",
                schema: "smsmarica",
                columns: table => new
                {
                    perfil_id = table.Column<Guid>(type: "uuid", nullable: false),
                    modulo = table.Column<int>(type: "integer", nullable: false),
                    acoes = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_permissao_perfil", x => new { x.perfil_id, x.modulo });
                    table.ForeignKey(
                        name: "FK_permissao_perfil_perfil_perfil_id",
                        column: x => x.perfil_id,
                        principalSchema: "smsmarica",
                        principalTable: "perfil",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "usuario_perfil",
                schema: "smsmarica",
                columns: table => new
                {
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    perfil_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_usuario_perfil", x => new { x.usuario_id, x.perfil_id });
                    table.ForeignKey(
                        name: "FK_usuario_perfil_perfil_perfil_id",
                        column: x => x.perfil_id,
                        principalSchema: "smsmarica",
                        principalTable: "perfil",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_usuario_perfil_usuario_usuario_id",
                        column: x => x.usuario_id,
                        principalSchema: "smsmarica",
                        principalTable: "usuario",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_perfil_nome",
                schema: "smsmarica",
                table: "perfil",
                column: "nome",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_usuario_perfil_perfil_id",
                schema: "smsmarica",
                table: "usuario_perfil",
                column: "perfil_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "permissao_perfil",
                schema: "smsmarica");

            migrationBuilder.DropTable(
                name: "permissao_usuario",
                schema: "smsmarica");

            migrationBuilder.DropTable(
                name: "usuario_perfil",
                schema: "smsmarica");

            migrationBuilder.DropTable(
                name: "perfil",
                schema: "smsmarica");

            migrationBuilder.AddColumn<int>(
                name: "perfil",
                schema: "smsmarica",
                table: "usuario",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }
    }
}
