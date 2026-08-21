using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Automais.Zap.Data.Migrations
{
    /// <inheritdoc />
    public partial class Inicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "zap");

            migrationBuilder.CreateTable(
                name: "destino",
                schema: "zap",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nome = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    url_webhook = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ativo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    observacao = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    criado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    atualizado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_destino", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "entrega_log",
                schema: "zap",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    phone_number_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    destino_id = table.Column<Guid>(type: "uuid", nullable: true),
                    tipo = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    sucesso = table.Column<bool>(type: "boolean", nullable: false),
                    status_http = table.Column<int>(type: "integer", nullable: true),
                    duracao_ms = table.Column<int>(type: "integer", nullable: false),
                    erro = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    recebido_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_entrega_log", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "usuario_admin",
                schema: "zap",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    senha_hash = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    nome = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    ativo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    criado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ultimo_acesso_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_usuario_admin", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "numero",
                schema: "zap",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    phone_number_id = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    destino_id = table.Column<Guid>(type: "uuid", nullable: false),
                    waba_id = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    display_phone_number = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    rotulo = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    ativo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    criado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    atualizado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_numero", x => x.id);
                    table.ForeignKey(
                        name: "FK_numero_destino_destino_id",
                        column: x => x.destino_id,
                        principalSchema: "zap",
                        principalTable: "destino",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ux_destino_nome",
                schema: "zap",
                table: "destino",
                column: "nome",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_entrega_log_destino_recebido",
                schema: "zap",
                table: "entrega_log",
                columns: new[] { "destino_id", "recebido_em" });

            migrationBuilder.CreateIndex(
                name: "ix_entrega_log_recebido_em",
                schema: "zap",
                table: "entrega_log",
                column: "recebido_em");

            migrationBuilder.CreateIndex(
                name: "IX_numero_destino_id",
                schema: "zap",
                table: "numero",
                column: "destino_id");

            migrationBuilder.CreateIndex(
                name: "ux_numero_phone_number_id",
                schema: "zap",
                table: "numero",
                column: "phone_number_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_usuario_admin_email",
                schema: "zap",
                table: "usuario_admin",
                column: "email",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "entrega_log",
                schema: "zap");

            migrationBuilder.DropTable(
                name: "numero",
                schema: "zap");

            migrationBuilder.DropTable(
                name: "usuario_admin",
                schema: "zap");

            migrationBuilder.DropTable(
                name: "destino",
                schema: "zap");
        }
    }
}
